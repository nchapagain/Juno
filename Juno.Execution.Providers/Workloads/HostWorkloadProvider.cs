namespace Juno.Execution.Providers.Workloads
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Security;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Common = Microsoft.Azure.CRC;

    /// <summary>
    /// Provider that starts VirtualClient on the host and monitors its lifetime.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteOnNode)]
    [SupportedParameter(Name = StepParameters.CommandArguments, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = StepParameters.Duration, Type = typeof(TimeSpan), Required = true)]
    [SupportedParameter(Name = StepParameters.EventHubConnectionString, Type = typeof(string), Required = true)]
    [ProviderInfo(Name = "Run a workload on the host.", Description = "Run a performance or reliability workload on the host in a specified experiment group.", FullDescription = "This provider enables a step to run the virtual client workload on the host as part of a Juno experiment.")]
    public class HostWorkloadProvider : ExperimentProvider
    {
        private const string HostWorkloadsPattern = "HostWorkloads.TipNode_*";
        private const string VirtualClientFileName = "VirtualClient.exe";
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan ReevaluationExtension = TimeSpan.FromMinutes(5);
        private IFileSystem fileSystem;        

        /// <summary>
        /// Initializes a new instance of the <see cref="HostWorkloadProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public HostWorkloadProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <summary>
        /// A process manager used to manage the virtual client process
        /// </summary>
        protected static IProcessManager VirtualClientProcess { get; set; }

        /// <inheritdoc />
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            if (!this.Services.TryGetService<IFileSystem>(out IFileSystem fileSystem))
            {
                this.Services.AddSingleton<IFileSystem>(new FileSystem());
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgressContinue, extensionTimeout: HostWorkloadProvider.ReevaluationExtension);

            if (!cancellationToken.IsCancellationRequested)
            { 
                TimeSpan timeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, HostWorkloadProvider.DefaultTimeout);
                State state = await this.GetStateAsync<State>(context, cancellationToken, sharedState: false).ConfigureDefaults()
                    ?? new State()
                    {
                        StepTimeout = DateTime.UtcNow.Add(timeout)
                    };                
                this.fileSystem = this.Services.GetService<IFileSystem>();

                if (!state.DependenciesInstalled)
                {
                    // Making sure all required dependencies are available on the host.
                    this.VerifyDependenciesInstalled(telemetryContext, state);
                    this.CheckStepTimeout(state);
                }
                else if (!state.ProcessRunning)
                {
                    // Starting VC for the first time.
                    await this.StartVirtualClientAsync(context, component, telemetryContext, state, cancellationToken)
                        .ConfigureDefaults();
                    this.CheckStepTimeout(state);
                }
                else
                {
                    if (HostWorkloadProvider.VirtualClientProcess == null || !HostWorkloadProvider.VirtualClientProcess.IsProcessRunning())
                    {
                        // VC process lost or is not running.
                        if (state.IsProcessRestartedTooManyTimes)
                        {
                            throw new ProviderException("Process restarted too many times.");
                        }
                        else
                        {
                            if (!this.TryGetVirtualClientProcess(out IProcessManager processManager))
                            {
                                // VC or host crashed. Retry starting VC.
                                telemetryContext.AddContext("processRestarted", true);
                                telemetryContext.AddContext("processScenario", "processNotRunning");
                                await this.StartVirtualClientAsync(context, component, telemetryContext, state, cancellationToken)
                                     .ConfigureDefaults();

                                state.RestartCount += 1;
                            }
                            else
                            {
                                // Retrieve a lost process/manager
                                HostWorkloadProvider.VirtualClientProcess = processManager;
                            }
                        }
                    }
                    else if (state.IsProcessDurationExpired)
                    {
                        telemetryContext.AddContext("processEndTime", state.ProcessEndTime);
                        telemetryContext.AddContext("processDurationExpired", true);
                        result = new ExecutionResult(ExecutionStatus.Succeeded);
                        await HostWorkloadProvider.VirtualClientProcess.StopProcessAsync(telemetryContext, cancellationToken).ConfigureDefaults();
                    }
                    else
                    {
                        telemetryContext.AddContext("processRunning", true);
                    }
                }

                await this.SaveStateAsync(context, state, cancellationToken, sharedState: false)
                    .ConfigureDefaults();
            }

            return result;
        }

        /// <summary>
        /// Creates a <see cref="BreakoutProcessManager"/> instance.
        /// </summary>
        /// <param name="commandFullPath">The command path of the executable process</param>
        /// <param name="commandArguments">The command arguments to be ran with the executable process</param>
        /// <returns></returns>
        protected virtual IProcessManager CreateBreakoutProcessManager(string commandFullPath, string commandArguments)
        {
            return new BreakoutProcessManager(commandFullPath, commandArguments, logger: this.Logger);
        }

        /// <summary>
        /// Creates a <see cref="BreakoutProcessManager"/> instance.
        /// </summary>
        /// <param name="process">The process to be managed</param>
        /// <returns></returns>
        protected virtual IProcessManager CreateBreakoutProcessManager(IProcessProxy process)
        {
            return new BreakoutProcessManager(process, logger: this.Logger);
        }

        /// <summary>
        /// Attempt to get a VirtualClient process and return a new manager to manage it.
        /// </summary>
        /// <param name="processManager">The process manager that the found process will be assigned to.</param>
        protected virtual bool TryGetVirtualClientProcess(out IProcessManager processManager)
        {
            bool foundProcess = ProcessProxy.TryGetProxy("VirtualClient", out IProcessProxy process);
            processManager = foundProcess ? this.CreateBreakoutProcessManager(process) : null;
            return foundProcess;
        }

        private void CheckStepTimeout(State state)
        {
            if (DateTime.UtcNow > state.StepTimeout)
            {
                throw new ProviderException(
                   "Timeout expired. The VirtualClient process could not be started within the time range.",
                   ErrorReason.Timeout);
            }
        }

        private string[] GetHostWorkloadsPath()
        {
            string rootDirectory = this.fileSystem.Path.GetPathRoot(Environment.SystemDirectory);
            string appPath = $@"{rootDirectory}\App";
            string[] hostWorkloadsPath = this.fileSystem.Directory.GetDirectories(appPath, HostWorkloadProvider.HostWorkloadsPattern);
            return hostWorkloadsPath;
        }

        private string GetVirtualClientExePath()
        {
            string[] hostWorkloadsPath = this.GetHostWorkloadsPath();
            string[] virtualClientFiles = this.fileSystem.Directory.GetFiles(hostWorkloadsPath[0], HostWorkloadProvider.VirtualClientFileName, SearchOption.AllDirectories);
            if (virtualClientFiles == null || virtualClientFiles.Length == 0)
            {
                throw new FileNotFoundException($"Could not find {HostWorkloadProvider.VirtualClientFileName} in {hostWorkloadsPath[0]}.");
            }

            string virtualClientExe = virtualClientFiles[0];
            return virtualClientExe;
        }

        private static IDictionary<string, string> CreateMetadata(ExperimentContext context, AgentIdentification agentId)
        {
            IDictionary<string, string> metadata = new Dictionary<string, string>
            {
                { MetadataProperty.AgentId, agentId.ToString() },
                { MetadataProperty.AgentType, !string.IsNullOrWhiteSpace(agentId.VirtualMachineName) ? AgentType.GuestAgent.ToString() : AgentType.HostAgent.ToString() },
                { MetadataProperty.TipSessionId, agentId.Context },
                { MetadataProperty.NodeId, agentId.NodeName },
                { MetadataProperty.NodeName, agentId.NodeName },
                { MetadataProperty.ExperimentId, context.Experiment.Id },
                { MetadataProperty.ExperimentStepId, context.ExperimentStep.Id },
                { MetadataProperty.ExperimentGroup, context.ExperimentStep.ExperimentGroup },
                { MetadataProperty.GroupId, context.ExperimentStep.ExperimentGroup },
                { MetadataProperty.ClusterName, agentId.ClusterName }
            };

            if (context.Experiment.Definition.Metadata?.Any() == true)
            {
                context.Experiment.Definition.Metadata.ToList().ForEach(kvPair => metadata[kvPair.Key] = kvPair.Value?.ToString());
            }

            return metadata;
        }

        private static int GenerateSeedFromExperimentId(string experimentId)
        {
            return Guid.Parse(experimentId).GetHashCode();
        }

        private void VerifyDependenciesInstalled(EventContext telemetryContext, State state)
        {
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);

            this.Logger.LogTelemetry($"{nameof(HostWorkloadProvider)}.VerifyDependenciesInstalled", relatedContext, () =>
            {
                if (state.DependenciesInstallEndTime == null)
                {
                    state.DependenciesInstallEndTime = DateTime.UtcNow.Add(HostWorkloadProvider.DefaultTimeout);
                }

                string[] hostWorkloadsPath = this.GetHostWorkloadsPath();
                if (hostWorkloadsPath == null || hostWorkloadsPath.Length == 0)
                {
                    relatedContext.AddContext("isHostWorkloadsPackageFound", false);
                    if (state.IsDependencyInstallTimeoutExpired)
                    {
                        throw new DirectoryNotFoundException($"The HostWorkloads package was not found on the node.");
                    }
                }
                else
                {
                    relatedContext.AddContext("isHostWorkloadsPackageFound", true);
                    state.DependenciesInstalled = true;
                }
            });
        }

        [SuppressMessage("Readability", "AZCA1006:PrefixStaticCallsWithClassName Rule", Justification = "Code analysis is mistaken here.")]
        private async Task<IProcessManager> CreateProcessAsync(ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
        {
            AgentIdentification agentId = this.Services.GetService<AgentIdentification>();
            string commandFullPath = this.GetVirtualClientExePath();
            string commandArguments = component.Parameters.GetValue<string>(StepParameters.CommandArguments);
            IDictionary<string, string> metadata = HostWorkloadProvider.CreateMetadata(context, agentId);
            string metadataArguments = string.Join(",,,", metadata.Select(entry => $"{entry.Key}={entry.Value}"));
            int durationMins = (int)component.Parameters.GetTimeSpanValue(StepParameters.Duration).TotalMinutes;

            // The Virtual Client command-line contract includes the following parameters
            // 1) timeout  - the timeout (in-minutes) the client process will run before exiting.
            // 2) metadata - key/value pairs of metadata to pass to the client (for telemetry) delimited by ',,,'
            commandArguments = $"{commandArguments.Trim()} --timeout={durationMins} --multipleInstances=true --metadata=\"{metadataArguments}\"";

            // The gives virtual client the same seed for the same experiment.
            int seed = HostWorkloadProvider.GenerateSeedFromExperimentId(context.Experiment.Id);
            commandArguments += $" --seed={seed}";

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
            }

            this.Logger.LogTelemetry($"{nameof(HostWorkloadProvider)}.CommandPath", LogLevel.Information, EventContext.Persisted()
                .AddContext("experimentId", context.ExperimentId)
                .AddContext("commandFullPath", commandFullPath)
                .AddContext("commandArguments", Common.SensitiveData.ObscureSecrets(commandArguments)));

            return this.CreateBreakoutProcessManager(commandFullPath, commandArguments);
        }

        private async Task StartVirtualClientAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, State state, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                EventContext relatedContext = telemetryContext.Clone(withProperties: true);

                await this.Logger.LogTelemetryAsync($"{nameof(HostWorkloadProvider)}.StartVirtualClient", relatedContext, async () =>
                {                    
                    HostWorkloadProvider.VirtualClientProcess = await this.CreateProcessAsync(context, component, cancellationToken)
                        .ConfigureDefaults();
                    try
                    {                        
                        await HostWorkloadProvider.VirtualClientProcess.StartProcessAsync(relatedContext, cancellationToken)
                            .ConfigureDefaults();
                        
                        if (HostWorkloadProvider.VirtualClientProcess.TryGetProcess(out IProcessProxy virtualClientProcess))
                        {
                            state.ProcessRunning = true;
                            state.ProcessId = virtualClientProcess.Id;
                            state.ProcessName = virtualClientProcess.Name;
                            // Keep the original timeout if we are not running VC for the first time
                            if (state.ProcessEndTime == null)
                            {
                                state.ProcessEndTime = DateTime.UtcNow.Add(component.Parameters.GetTimeSpanValue(StepParameters.Duration));
                                relatedContext.AddContext("processEndTime", state.ProcessEndTime);
                            }
                        }
                        else
                        {
                            relatedContext.AddContext("processExitOnStart", true);
                        }
                    }
                    catch (Exception exc)
                    {
                        EventContext startupErrorContext = relatedContext.Clone()
                            .AddError(exc);
                        this.Logger.LogTelemetry($"{nameof(HostWorkloadProvider)}.UnexpectedStartupExit", LogLevel.Warning, startupErrorContext);
                    }
                }).ConfigureDefaults();
            }
        }

        internal class State
        {
            // TODO Check if all these properties are required
            public bool DependenciesInstalled { get; set; }

            public DateTime? DependenciesInstallEndTime { get; set; }

            public bool ProcessRunning { get; set; }

            public int ProcessId { get; set; }

            public string ProcessName { get; set; }

            public DateTime? ProcessEndTime { get; set; }

            public int RestartCount { get; set; }

            public DateTime StepTimeout { get; set; }

            [JsonIgnore]
            public bool IsDependencyInstallTimeoutExpired => DateTime.UtcNow > this.DependenciesInstallEndTime;

            [JsonIgnore]
            public bool IsProcessDurationExpired => DateTime.UtcNow > this.ProcessEndTime;

            [JsonIgnore]
            public bool IsProcessRestartedTooManyTimes => this.RestartCount > 50;
        }

        private class Metadata
        {
            internal const string AgentId = "agentId";
            internal const string TipSessionId = "tipSessionId";
            internal const string ExperimentId = "experimentId";
            internal const string ExperimentStepId = "experimentStepId";
            internal const string ExperimentGroup = "experimentGroup";
            internal const string GroupId = "groupId";
            internal const string NodeId = "nodeId";
            internal const string NodeName = "nodeName";
            internal const string ClusterName = "clusterName";
            internal const string Context = "context";
        }
    }
}
