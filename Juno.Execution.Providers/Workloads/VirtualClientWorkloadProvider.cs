namespace Juno.Execution.Providers.Workloads
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Security;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime;
    using Juno.Execution.ArmIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.CodeAnalysis;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Polly;
    using Common = Microsoft.Azure.CRC;

    /// <summary>
    /// Provider starts the VirtualClient.exe process and monitors its lifetime.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteOnVirtualMachine)]
    [SupportedParameter(Name = StepParameters.Platform, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = StepParameters.PackageVersion, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = StepParameters.Command, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = StepParameters.CommandArguments, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = StepParameters.Duration, Type = typeof(TimeSpan), Required = true)]
    [SupportedParameter(Name = StepParameters.IncludeSpecifications, Type = typeof(bool), Required = false)]
    [SupportedParameter(Name = StepParameters.TimeoutMinStepsSucceeded, Type = typeof(int), Required = false)]
    [SupportedParameter(Name = StepParameters.EventHubConnectionString, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = StepParameters.ApplicationInsightsInstrumentationKey, Type = typeof(string), Required = false)]
    [ProviderInfo(Name = "Run performance and IO Workload", Description = "Run performance and IO workload on the VMs in the specified experiment group", FullDescription = "Step to run virtual client workload on virtual machine as part of Juno experiment. The workloads will be running on the VMs when the Intel microcode update is applied (e.g. the payload).")]
    public class VirtualClientWorkloadProvider : ExperimentProvider
    {
        private const int MaximumVcCrashes = 5;

        /// <summary>
        /// Retry logic if unable to kill process or delete directory
        /// https://docs.microsoft.com/en-us/dotnet/api/system.io.directory.delete?view=netframework-4.8
        /// https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=netcore-2.2#System_Diagnostics_Process_Kill
        /// </summary>
        private static readonly IAsyncPolicy CleanupRetryPolicy = Policy
            .Handle<Win32Exception>()
            .Or<IOException>()
            .Or<UnauthorizedAccessException>()
            .WaitAndRetryAsync(1, (retries) => TimeSpan.FromMilliseconds(retries * 100));

        /// <summary>
        /// Retry logic to try and successfull write the specifications file. We want to 
        /// survive transient file system/IO errors when possible.
        /// </summary>
        private static readonly ISyncPolicy SpecificationFileWriteRetryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetry(10, (retries) => TimeSpan.FromSeconds(retries));

        private static IProcessProxy currentProcess;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualClientWorkloadProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public VirtualClientWorkloadProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc />
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            if (!this.Services.TryGetService<IFileSystem>(out IFileSystem fileSystem))
            {
                this.Services.AddSingleton<IFileSystem>(new FileSystem());
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgressContinue);

            if (!cancellationToken.IsCancellationRequested)
            {
                State state = await this.GetStateAsync<State>(context, cancellationToken, sharedState: false).ConfigureDefaults()
                    ?? new State();

                AgentIdentification agentId = this.Services.GetService<AgentIdentification>();

                if (!state.DependenciesInstalled)
                {
                    await this.InstallDependenciesAsync(context, component, telemetryContext, state, cancellationToken)
                        .ConfigureDefaults();

                    await this.SaveStateAsync(context, state, cancellationToken, sharedState: false)
                        .ConfigureDefaults();
                }
                else if (!state.ProcessRunning)
                {
                    // Fresh run
                    await this.StartVirtualClientAsync(context, component, telemetryContext, agentId, state, cancellationToken)
                        .ConfigureDefaults();

                    await this.SaveStateAsync(context, state, cancellationToken, sharedState: false)
                        .ConfigureDefaults();
                }
                else
                {
                    IProcessProxy expectedProcess = VirtualClientWorkloadProvider.currentProcess;
                    IProcessProxy actualProcess;
                    ProcessProxy.TryGetProxy(state.ProcessId, out actualProcess);

                    if (expectedProcess != null)
                    {
                        telemetryContext.AddContext("processExited", expectedProcess.HasExited);

                        // VC run duration is completed as expected, stop the process
                        if (state.IsProcessDurationExpired)
                        {
                            telemetryContext.AddContext("processEndTime", state.ProcessEndTime);
                            telemetryContext.AddContext("processDurationExpired", true);
                            result = new ExecutionResult(ExecutionStatus.Succeeded);
                            await this.StopProcessAsync(expectedProcess, telemetryContext, cancellationToken)
                                .ConfigureDefaults();
                        }
                        else if (expectedProcess.HasExited)
                        {
                            // VC must have crashed or exited. Start new instance
                            telemetryContext.AddContext("processExitCode", expectedProcess.ExitCode);
                            telemetryContext.AddContext("processRestarted", true);
                            await this.RestartVirtualClientAsync(context, component, telemetryContext, state, agentId, cancellationToken).ConfigureDefaults();
                        }
                    }
                    else if (actualProcess != null && actualProcess.Name == state.ProcessName)
                    {
                        // GA crash scenario
                        VirtualClientWorkloadProvider.currentProcess = actualProcess;
                        telemetryContext.AddContext("processRestarted", false);
                        telemetryContext.AddContext("processScenario", "agentRestart");
                    }
                    else if (actualProcess == null || actualProcess.Name != state.ProcessName)
                    {
                        // Reboot scenario
                        telemetryContext.AddContext("processRestarted", true);
                        telemetryContext.AddContext("processScenario", "systemReboot");
                        await this.RestartVirtualClientAsync(context, component, telemetryContext, state, agentId, cancellationToken).ConfigureDefaults();
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Creates the metadata properties to supply to the VirtualClient.exe.
        /// </summary>
        /// <param name="context">The experiment context information.</param>
        /// <param name="component">The experiment component containing the step definition and parameters.</param>
        /// <param name="agentId">The ID of the Juno Guest agent in which the provider is running.</param>
        /// <param name="telemetryContext">Provides context properties to associate with the metadata.</param>
        /// <returns>
        /// A set of one or more metadata properties to pass to the VirtualClient.exe.
        /// </returns>
        protected static IDictionary<string, string> CreateMetadata(ExperimentContext context, ExperimentComponent component, AgentIdentification agentId, EventContext telemetryContext)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            agentId.ThrowIfNull(nameof(agentId));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            string containerId = string.Empty;
            if (telemetryContext.Properties.ContainsKey(Metadata.ContainerId))
            {
                containerId = telemetryContext.Properties[Metadata.ContainerId]?.ToString();
            }

            return new Dictionary<string, string>
            {
                { Metadata.AgentId, agentId.ToString() },
                { Metadata.ContainerId, containerId },
                { Metadata.TipSessionId, agentId.Context },
                { Metadata.NodeId, agentId.NodeName },
                { Metadata.NodeName, agentId.NodeName },
                { Metadata.ExperimentId, context.Experiment.Id },
                { Metadata.ExperimentStepId, context.ExperimentStep.Id },
                { Metadata.ExperimentGroup, context.ExperimentStep.ExperimentGroup },
                { Metadata.GroupId, context.ExperimentStep.ExperimentGroup },
                { Metadata.VirtualMachineName, agentId.VirtualMachineName },
                { Metadata.ClusterName, agentId.ClusterName }
            };
        }

        /// <summary>
        /// Creates the process that will host the VirtualClient.exe workload.
        /// </summary>
        /// <param name="context">The experiment context information.</param>
        /// <param name="component">The experiment component containing the step definition and parameters.</param>
        /// <param name="metadata">A set of one or more metadata properties to pass to the VirtualClient.exe on the command-line.</param>
        /// <param name="specificationFilePath">The path to the specification file (if it exists).</param>
        /// <param name="cancellationToken">Cancellation token to talk to AzureKeyvault</param>
        /// <returns></returns>
        [SuppressMessage("Readability", "AZCA1006:PrefixStaticCallsWithClassName Rule", Justification = "Code analysis is mistaken here.")]
        protected async virtual Task<IProcessProxy> CreateProcessAsync(
            ExperimentContext context, ExperimentComponent component, IDictionary<string, string> metadata, string specificationFilePath, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            metadata.ThrowIfNull(nameof(metadata));

            string commandFullPath = VirtualClientWorkloadProvider.GetVirtualClientExePath(component);
            string commandArguments = component.Parameters.GetValue<string>(StepParameters.CommandArguments);
            string metadataArguments = string.Join(",,,", metadata.Select(entry => $"{entry.Key}={entry.Value}"));
            int durationMins = (int)component.Parameters.GetTimeSpanValue(StepParameters.Duration).TotalMinutes;

            // The Virtual Client command-line contract includes the following parameters
            // 1) timeout  - the timeout (in-minutes) the client process will run before exiting.
            // 2) metadata - key/value pairs of metadata to pass to the client (for telemetry) delimited by ',,,'
            commandArguments = $"{commandArguments.Trim()} --timeout={durationMins} --metadata=\"{metadataArguments}\"";

            if (!string.IsNullOrWhiteSpace(specificationFilePath))
            {
                commandArguments += $" --specificationPath=\"{specificationFilePath}\"";
            }

            // The gives virtual client the same seed for the same experiment.
            int seed = VirtualClientWorkloadProvider.GenerateSeedFromExperimentId(context.Experiment.Id);
            commandArguments += $" --seed={seed}";

            string appInsightsKey = component.Parameters.GetValue<string>(StepParameters.ApplicationInsightsInstrumentationKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(appInsightsKey))
            {
                const string parameter = "--applicationInsightsInstrumentationKey";
                IAzureKeyVault keyVaultClient = this.Services.GetService<IAzureKeyVault>();
                if (keyVaultClient.IsSecretReference(appInsightsKey))
                {
                    using (SecureString secureString = await keyVaultClient.ResolveSecretAsync(appInsightsKey, cancellationToken).ConfigureDefaults())
                    {
                        appInsightsKey = secureString.ToOriginalString();
                    }
                }

                commandArguments += $" {parameter}={appInsightsKey}";
                VirtualClientWorkloadProvider.ThrowOnDuplicateParameters(commandArguments, parameter);
            }

            string eventHubConnectionString = component.Parameters.GetValue<string>(StepParameters.EventHubConnectionString, string.Empty);
            if (!string.IsNullOrWhiteSpace(eventHubConnectionString))
            {
                const string parameter = "--eventHubConnectionString";
                IAzureKeyVault keyVaultClient = this.Services.GetService<IAzureKeyVault>();
                if (keyVaultClient.IsSecretReference(eventHubConnectionString))
                {
                    using (SecureString secureString = await keyVaultClient.ResolveSecretAsync(eventHubConnectionString, cancellationToken).ConfigureDefaults())
                    {
                        eventHubConnectionString = secureString.ToOriginalString();
                    }
                }

                commandArguments += $" {parameter}={eventHubConnectionString}";
                VirtualClientWorkloadProvider.ThrowOnDuplicateParameters(commandArguments, parameter);
            }

            this.Logger.LogTelemetry($"{nameof(VirtualClientWorkloadProvider)}.CommandPath", LogLevel.Information, EventContext.Persisted()
                .AddContext("experimentId", context.ExperimentId)
                .AddContext("commandFullPath", commandFullPath)
                .AddContext("commandArguments", Common.SensitiveData.ObscureSecrets(commandArguments))); // Avoid writing secrets to our telemetry stores.

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = commandFullPath,
                    Arguments = commandArguments,
                    WorkingDirectory = Path.GetDirectoryName(commandFullPath),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    EventContext telemetryContext = EventContext.Persisted()
                        .AddContext("experimentId", context.ExperimentId)
                        .AddContext("standardError", args.Data);

                    this.Logger.LogTelemetry($"{nameof(VirtualClientWorkloadProvider)}.StandardError", LogLevel.Warning, telemetryContext);
                }
            };

            return new ProcessProxy(process);
        }

        private static string GetVirtualClientExePath(ExperimentComponent component)
        {
            string command = component.Parameters.GetValue<string>(StepParameters.Command, string.Empty);
            // Construct command based on platform and version if command is not explicitly provided.
            // TODO [8601409]: Remove command parameter and make platform and version parameter required
            if (string.IsNullOrEmpty(command))
            {
                string platform = component.Parameters.GetValue<string>(StepParameters.Platform, string.Empty);
                string packageVersion = component.Parameters.GetValue<string>(StepParameters.PackageVersion, string.Empty);
                if (string.IsNullOrEmpty(platform))
                {
                    throw new ProviderException($"Required parameter {nameof(StepParameters.Platform)} is missing.", ErrorReason.ProviderStateInvalid);
                }

                if (string.IsNullOrEmpty(packageVersion))
                {
                    throw new ProviderException($"Required parameter {nameof(StepParameters.PackageVersion)} is missing.", ErrorReason.ProviderStateInvalid);
                }

                switch (platform)
                {
                    case VmPlatform.LinuxArm64:
                        command = $"{{NuGetPackagePath}}/virtualclient.arm64/{packageVersion}/content/{platform}/VirtualClient";
                        break;
                    case VmPlatform.LinuxX64:
                        command = $"{{NuGetPackagePath}}/virtualclient/{packageVersion}/content/{platform}/VirtualClient";
                        break;
                    case VmPlatform.WinArm64:
                        command = $"{{NuGetPackagePath}}\\virtualclient.arm64\\{packageVersion}\\content\\{platform}\\VirtualClient.exe";
                        break;
                    case VmPlatform.WinX64:
                        command = $"{{NuGetPackagePath}}\\virtualclient\\{packageVersion}\\content\\{platform}\\VirtualClient.exe";
                        break;
                    default:
                        throw new ProviderException($"Platform: '{platform}' is not supported.", ErrorReason.ProviderNotSupported);
                }
            }

            string commandFullPath = Path.Combine(DependencyPaths.ReplacePathReferences(command));

            if (!Path.IsPathRooted(commandFullPath))
            {
                commandFullPath = Path.Combine(DependencyPaths.RootPath, commandFullPath);
            }

            return commandFullPath;
        }

        private static int GenerateSeedFromExperimentId(string experimentId)
        {
            return Guid.Parse(experimentId).GetHashCode();
        }

        private static void ThrowOnDuplicateParameters(string commandLineArguments, string parameter)
        {
            MatchCollection matches = Regex.Matches(commandLineArguments, parameter, RegexOptions.IgnoreCase);
            if (matches != null && matches.Count > 1)
            {
                throw new ProviderException(
                    $"Invalid command line usage.  Duplicate command line parameters found: '{parameter}'.",
                    ErrorReason.InvalidUsage);
            }
        }

        private async Task InstallDependenciesAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, State state, CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);

            await this.Logger.LogTelemetryAsync($"{nameof(VirtualClientWorkloadProvider)}.InstallDependencies", relatedContext, async () =>
            {
                ExecutionResult installationResult = await this.InstallDependenciesAsync(context, component, cancellationToken)
                    .ConfigureDefaults();

                if (installationResult.Status == ExecutionStatus.Failed)
                {
                    throw new ProviderException("Provider dependency installation failed.", ErrorReason.DependencyFailure, installationResult.Error);
                }

                state.DependenciesInstalled = true;
            }).ConfigureDefaults();
        }

        private async Task RestartVirtualClientAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, State state, AgentIdentification agentId, CancellationToken cancellationToken)
        {
            state.VirtualClientCrashes += 1;
            if (state.VirtualClientCrashes <= VirtualClientWorkloadProvider.MaximumVcCrashes)
            {
                await this.StartVirtualClientAsync(context, component, telemetryContext, agentId, state, cancellationToken)
                    .ConfigureDefaults();

                await this.SaveStateAsync(context, state, cancellationToken, sharedState: false)
                    .ConfigureDefaults();

            }
            else
            {
                // VC reached maximum crashes. Failing the provider instance.
                throw new ProviderException(
                    $"Maximum VC crashes reached at {VirtualClientWorkloadProvider.MaximumVcCrashes} times, failing the instance.",
                    ErrorReason.MaximumFailureReached);
            }
        }

        [SuppressMessage("Readability", "AZCA1006:PrefixStaticCallsWithClassName Rule", Justification = "Code analysis is mistaken here.")]
        private bool StartProcess(IProcessProxy process, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            bool isStarted = false;
            if (!cancellationToken.IsCancellationRequested)
            {
                process.ThrowIfNull(nameof(process));
                telemetryContext.ThrowIfNull(nameof(telemetryContext));

                EventContext relatedContext = telemetryContext.Clone(withProperties: true)
                    .AddContext(nameof(process), new
                    {
                        id = process.Id,
                        command = process.StartInfo.FileName,
                        commandArguments = Common.SensitiveData.ObscureSecrets(process.StartInfo.Arguments),
                        workingDir = process.StartInfo.WorkingDirectory
                    });

                this.Logger.LogTelemetry($"{nameof(VirtualClientWorkloadProvider)}.StartProcess", relatedContext, () =>
                {
                    if (process.Start())
                    {
                        // Start asynchronous reading of output, as opposed to waiting until process closes.
                        process.BeginReadingOutput(standardOutput: false, standardError: true);
                        isStarted = true;
                    }
                });
            }

            return isStarted;
        }

        private Task StopProcessAsync(IProcessProxy process, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            process.ThrowIfNull(nameof(process));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            return VirtualClientWorkloadProvider.CleanupRetryPolicy.ExecuteAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    EventContext relatedContext = telemetryContext.Clone(withProperties: true)
                        .AddContext(nameof(process), process.StartInfo);

                    this.Logger.LogTelemetry($"{nameof(VirtualClientWorkloadProvider)}.StopProcess", relatedContext, () =>
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill();
                            }

                            process.Dispose();
                        }
                        catch
                        {
                            // Try to clean in the background task and do nothing for exception. We always create new proces & download folder when provider start
                        }
                    });
                }

                return Task.CompletedTask;
            });
        }

        private async Task StartVirtualClientAsync(
            ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, AgentIdentification agentId, State state, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            agentId.ThrowIfNull(nameof(agentId));
            state.ThrowIfNull(nameof(state));

            EventContext relatedContext = telemetryContext.Clone(withProperties: true);
            await this.Logger.LogTelemetryAsync($"{nameof(VirtualClientWorkloadProvider)}.StartVirtualClient", relatedContext, async () =>
            {
                string specificationFilePath = null;
                if (!component.Parameters.TryGetValue(StepParameters.IncludeSpecifications, out IConvertible includeSpecs) || bool.Parse(includeSpecs.ToString()) == true)
                {
                    specificationFilePath = Path.Combine(Path.GetDirectoryName(VirtualClientWorkloadProvider.GetVirtualClientExePath(component)), "Specifications.json");
                    this.WriteSpecificationsFileAsync(context, specificationFilePath, telemetryContext, cancellationToken)
                        .GetAwaiter().GetResult();
                }

                DateTime processEndTime = DateTime.UtcNow.Add(component.Parameters.GetTimeSpanValue(StepParameters.Duration));
                IDictionary<string, string> metadata = VirtualClientWorkloadProvider.CreateMetadata(context, component, agentId, telemetryContext);
                IProcessProxy process = await this.CreateProcessAsync(context, component, metadata, specificationFilePath, cancellationToken)
                    .ConfigureDefaults();

                if (!this.StartProcess(process, relatedContext, cancellationToken))
                {
                    throw new ProviderException("Unable to start virtual client process.", ErrorReason.DependencyFailure);
                }

                VirtualClientWorkloadProvider.currentProcess = process;

                try
                {
                    if (process.HasExited)
                    {
                        telemetryContext.AddContext("processExitOnStart", true);
                        telemetryContext.AddContext("processExitCode", process.ExitCode);
                    }
                    else
                    {
                        relatedContext.AddContext("processId", process.Id);
                        relatedContext.AddContext("processEndTime", processEndTime);

                        state.ProcessRunning = true;
                        state.ProcessId = process.Id;
                        state.ProcessName = process.Name;

                        // Keep the original timeout if we are not running VC for the first time
                        if (state.ProcessEndTime == null)
                        {
                            state.ProcessEndTime = processEndTime;
                        }
                    }
                }
                catch (Exception exc)
                {
                    EventContext startupErrorContext = relatedContext.Clone()
                        .AddError(exc);

                    // Occasionally, the VC starts up and exits right away.
                    this.Logger.LogTelemetry($"{nameof(VirtualClientWorkloadProvider)}.UnexpectedStartupExit", LogLevel.Warning, startupErrorContext);
                }
            }).ConfigureDefaults();
        }

        private async Task<bool> WriteSpecificationsFileAsync(ExperimentContext context, string filePath, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            bool fileWritten = false;

            try
            {
                IFileSystem fileSystem = this.Services.GetService<IFileSystem>();
                if (!fileSystem.File.Exists(filePath))
                {
                    EventContext relatedContext = telemetryContext.Clone()
                        .AddContext("filePath", filePath);

                    await this.Logger.LogTelemetryAsync($"{nameof(VirtualClientWorkloadProvider)}.WriteSpecificationsFile", relatedContext, async () =>
                    {
                        EnvironmentEntity vmEntity = (await this.GetEntitiesProvisionedAsync(context, cancellationToken).ConfigureDefaults())?.FirstOrDefault(
                            entity => entity.EntityType == EntityType.VirtualMachine
                            && string.Equals(entity.AgentId(), context.ExperimentStep.AgentId, StringComparison.OrdinalIgnoreCase));

                        relatedContext.AddContext(vmEntity);

                        if (vmEntity != null)
                        {
                            string osDiskSku = vmEntity.OsDiskSku();
                            string dataDiskInfo = vmEntity.DataDisks();

                            List<object> diskMappings = new List<object>();

                            if (!string.IsNullOrWhiteSpace(osDiskSku))
                            {
                                diskMappings.Add(new
                                {
                                    id = -1,
                                    name = "osDiskSku",
                                    type = osDiskSku
                                });
                            }

                            if (!string.IsNullOrWhiteSpace(dataDiskInfo))
                            {
                                IEnumerable<VmDisk> dataDisks = VmDisk.ParseDisks(dataDiskInfo);
                                foreach (VmDisk disk in dataDisks)
                                {
                                    diskMappings.Add(new
                                    {
                                        id = disk.Lun,
                                        name = "dataDiskSku",
                                        type = disk.Sku
                                    });
                                }
                            }

                            relatedContext.AddContext("diskMapping", diskMappings);
                            if (diskMappings.Any())
                            {
                                await VirtualClientWorkloadProvider.SpecificationFileWriteRetryPolicy.Execute(async () =>
                                {
                                    await fileSystem.File.WriteAllTextAsync(filePath, new { diskMapping = diskMappings }.ToJson()).ConfigureDefaults();
                                    fileWritten = true;

                                }).ConfigureDefaults();
                            }
                        }
                    }).ConfigureDefaults();
                }
            }
            catch
            {
                // Best effort until the code path is hardened.
            }

            return fileWritten;
        }

        internal class State
        {
            public int VirtualClientCrashes { get; set; } = 0;

            public bool DependenciesInstalled { get; set; }

            public bool ProcessRunning { get; set; }

            public int ProcessId { get; set; }

            public string ProcessName { get; set; }

            public DateTime? ProcessEndTime { get; set; }

            [JsonIgnore]
            public bool IsProcessDurationExpired => DateTime.UtcNow > this.ProcessEndTime;
        }

        private class Metadata
        {
            internal const string AgentId = "agentId";
            internal const string ContainerId = "containerId";
            internal const string TipSessionId = "tipSessionId";
            internal const string ExperimentId = "experimentId";
            internal const string ExperimentStepId = "experimentStepId";
            internal const string ExperimentGroup = "experimentGroup";
            internal const string GroupId = "groupId";
            internal const string VirtualMachineName = "virtualMachineName";
            internal const string NodeId = "nodeId";
            internal const string NodeName = "nodeName";
            internal const string ClusterName = "clusterName";
            internal const string Context = "context";
        }
    }
}
