namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Security;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Polly;

    /// <summary>
    /// A virtual client monitor that starts and manages the virtual client process
    /// </summary>
    public class VirtualClientMonitorTask : AgentMonitorTask<string>
    {
        private const string FilePath = ".\\VirtualClient\\VirtualClientMonitor.exe";

        /// <summary>
        /// Initializes an new instance of the <see cref="VirtualClientMonitorTask"/> class.
        /// </summary>
        /// <param name="services">
        /// Services collection provides required dependencies to the host task/operation.
        /// </param>
        /// <param name="settings">Configuration settings for the environment.</param>
        /// <param name="retryPolicy">A retry policy to apply to the execution of the task operation.</param>
        /// <param name="processManager">A process manager to manage the virtual client process.</param>
        public VirtualClientMonitorTask(IServiceCollection services, EnvironmentSettings settings, IAsyncPolicy retryPolicy = null, IProcessManager processManager = null)
            : base(services, settings, retryPolicy)
        {
            VirtualClientMonitorTask.VCProcessManager = processManager;
        }

        /// <summary>
        /// A process manager used to manage the virtual client process
        /// </summary>
        protected static IProcessManager VCProcessManager { get; set; }

        /// <summary>
        /// The experiment that's running this task
        /// </summary>
        protected ExperimentInstance CurrentExperiment { get; set; }

        /// <summary>
        /// The settings specific to the <see cref="VirtualClientMonitorTask"/>
        /// </summary>
        protected VCMonitorSettings MonitorSettings
        {
            get
            {
                return this.Settings.AgentSettings.AgentMonitoringSettings.VCMonitorSettings;
            }
        }

        /// <summary>
        /// Gets the Agent Identification from the services.
        /// </summary>
        protected AgentIdentification AgentId
        {
            get
            {
                return this.Services.HasService<AgentIdentification>()
                    ? this.Services.GetService<AgentIdentification>()
                    : null;
            }
        }

        /// <summary>
        /// Executes logic in the background to run the virtual client
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A task that can be used to execute the monitor task asynchronously.
        /// </returns>
        public override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    EventContext telemetryContext = EventContext.Persisted();

                    this.Logger.LogTelemetry($"{nameof(VirtualClientMonitorTask)}.Execute", telemetryContext, () =>
                    {
                        if (this.GetExperimentInstance(cancellationToken))
                        {
                            this.RetryPolicy.ExecuteAsync(async () =>
                            {
                                // VC has not been started before or this task might have lost the manager somehow 
                                if (VirtualClientMonitorTask.VCProcessManager == null)
                                {
                                    if (!this.TryGetVirtualClientProcess(out IProcessManager processManager))
                                    {
                                        // process has not been created or all was lost in some apocalypse
                                        telemetryContext.AddContext("processStarted", false);
                                        telemetryContext.AddContext("processScenario", "initializing");
                                        await this.CreateAndStartProcessManagerAsync(telemetryContext, cancellationToken).ConfigureDefaults();
                                    }
                                    else
                                    {
                                        // retrieve a lost process/manager
                                        VirtualClientMonitorTask.VCProcessManager = processManager;
                                    }
                                }
                                else if (VirtualClientMonitorTask.VCProcessManager != null && !VirtualClientMonitorTask.VCProcessManager.IsProcessRunning())
                                {
                                    // VC was started before, but the process is not running
                                    // try to get the exit code if possible and restart the process
                                    if (VirtualClientMonitorTask.VCProcessManager.TryGetProcessExitCode(out int? exitCode))
                                    {
                                        telemetryContext.AddContext("processExitCode", exitCode);
                                    }

                                    telemetryContext.AddContext("processRestarted", true);
                                    telemetryContext.AddContext("processScenario", "processNotRunning");
                                    await this.StartProcessAsync(telemetryContext, cancellationToken).ConfigureDefaults();
                                }

                                return Task.CompletedTask;

                            }).GetAwaiter().GetResult();
                        }
                    });
                }
                catch
                {
                    // We don't want to surface the exception. The logging framework will capture
                    // the error information and this is sufficient.
                }
            });
        }

        /// <summary>
        /// Creates the metadata properties to supply to the VirtualClient.exe.
        /// </summary>
        /// <param name="experiment">The experiment information.</param>
        /// <param name="agentId">The ID of the Juno Guest agent in which the provider is running.</param>
        /// <param name="telemetryContext">Provides context properties to associate with the metadata.</param>
        /// <returns>
        /// A set of one or more metadata properties to pass to the VirtualClient.exe.
        /// </returns>
        protected static IDictionary<string, string> CreateMetadata(ExperimentInstance experiment, AgentIdentification agentId, EventContext telemetryContext)
        {
            experiment.ThrowIfNull(nameof(experiment));
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
                { Metadata.ExperimentId, experiment.Id },
                { Metadata.VirtualMachineName, agentId.VirtualMachineName },
                { Metadata.ClusterName, agentId.ClusterName }
            };
        }

        /// <summary>
        /// Attempt to get a process and return a new manager to manage it. Note that an error event has not been created/attached to this process from here and is assumed to already have been made for it.
        /// </summary>
        /// <param name="processManager">The process manager that the found process will be assigned to.</param>
        protected virtual bool TryGetVirtualClientProcess(out IProcessManager processManager)
        {
            IProcessProxy process;

            bool foundProcess = ProcessProxy.TryGetProxy("VirtualClient", out process);

            processManager = foundProcess ? new ProcessManager(process, logger: this.Logger) : null;

            return foundProcess;
        }

        /// <summary>
        /// Starts the process of the <see cref="VirtualClientMonitorTask"/> process manager
        /// </summary>
        /// <param name="telemetryContext">The telemetry context.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A task that can be used to execute the monitor task asynchronously.
        /// </returns>
        protected virtual Task StartProcessAsync(EventContext telemetryContext, CancellationToken cancellationToken)
        {
            return VirtualClientMonitorTask.VCProcessManager.StartProcessAsync(telemetryContext, cancellationToken);
        }

        /// <summary>
        /// Creates a process manager and starts its process
        /// </summary>
        /// <param name="telemetryContext">The telemetry context.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A task that can be used to execute the monitor task asynchronously.
        /// </returns>
        protected async virtual Task CreateAndStartProcessManagerAsync(EventContext telemetryContext, CancellationToken cancellationToken)
        {
            VirtualClientMonitorTask.VCProcessManager = new ProcessManager(
                VirtualClientMonitorTask.FilePath,
                await this.GetCommandArgumentsAsync(telemetryContext, cancellationToken).ConfigureDefaults(),
                this.RetryPolicy,
                this.Logger);
            await this.StartProcessAsync(telemetryContext, cancellationToken).ConfigureDefaults();
        }

        /// <summary>
        /// Creates the process that will host the VirtualClient.exe workload.
        /// </summary>
        /// <param name="telemetryContext">The event context information.</param>
        /// <param name="cancellationToken">Cancellation token to talk to AzureKeyvault</param>
        /// <returns></returns>
        protected async virtual Task<string> GetCommandArgumentsAsync(
            EventContext telemetryContext, CancellationToken cancellationToken)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            AgentIdentification agentId = this.Services.GetService<AgentIdentification>();
            DateTime processEndTime = DateTime.UtcNow.Add(this.MonitorSettings.MonitorInterval);
            IDictionary<string, string> metadata = VirtualClientMonitorTask.CreateMetadata(this.CurrentExperiment, agentId, telemetryContext);

            string metadataArguments = string.Join(",,,", metadata.Select(entry => $"{entry.Key}={entry.Value}"));
            int durationMins = (int)TimeSpan.FromDays(7).TotalMinutes;

            // The Virtual Client command-line contract includes the following parameters
            // 1) timeout  - the timeout (in-minutes) the client process will run before exiting.
            // 2) metadata - key/value pairs of metadata to pass to the client (for telemetry) delimited by ',,,'
            string commandArguments = $"--profile=RealTimeDataMonitor.json --platform=Juno --timeout={durationMins} --multipleInstances=true --metadata=\"{metadataArguments}\"";

            EventHubSettings eventHubTelemetrySettings = this.Settings.EventHubSettings.Get(Setting.VirtualClientTelemetry);
            string eventHubConnectionString = eventHubTelemetrySettings.ConnectionString;
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
                VirtualClientMonitorTask.ThrowOnDuplicateParameters(commandArguments, parameter);
            }

            this.Logger.LogTelemetry($"{nameof(VirtualClientMonitorTask)}.CommandPath", LogLevel.Information, EventContext.Persisted()
                .AddContext("experimentId", this.CurrentExperiment.Id)
                .AddContext("commandFullPath", VirtualClientMonitorTask.FilePath)
                .AddContext("commandArguments", commandArguments));

            return commandArguments;
        }

        /// <summary>
        /// Retrieves the experiment associated with this task.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to talk to AzureKeyvault</param>
        /// <returns></returns>
        protected virtual bool GetExperimentInstance(CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    EventContext telemetryContext = EventContext.Persisted()
                        .AddContext("agentId", this.AgentId?.ToString());

                    int attempts = 0;
                    List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                    if (this.CurrentExperiment == null)
                    {
                        this.Logger.LogTelemetry($"{nameof(VirtualClientMonitorTask)}.GetExperiment", telemetryContext, () =>
                        {
                            try
                            {
                                ClientPool<AgentClient> apiPool = this.Services.GetService<ClientPool<AgentClient>>();
                                AgentClient apiClient = apiPool.GetClient(ApiClientType.AgentApi);

                                this.RetryPolicy.ExecuteAsync(() =>
                                {
                                    attempts++;
                                    HttpResponseMessage response = apiClient.GetAgentExperimentAsync(this.AgentId.ToString(), cancellationToken)
                                        .GetAwaiter().GetResult()
                                        .Handle(rsp =>
                                        {
                                            responses.Add(rsp);
                                            rsp.ThrowOnError<ExperimentException>();
                                        });

                                    this.CurrentExperiment = response.Content.ReadAsJsonAsync<ExperimentInstance>()
                                        .GetAwaiter().GetResult();

                                    return Task.CompletedTask;
                                }).GetAwaiter().GetResult();
                            }
                            finally
                            {
                                telemetryContext.AddContext(this.CurrentExperiment);
                                telemetryContext.AddContext(responses);
                                telemetryContext.AddContext(nameof(attempts), attempts);
                            }
                        });
                    }

                    telemetryContext.AddContext("experimentId", this.CurrentExperiment.Id);
                }
                catch
                {
                    // We do not want to crash the task thread nor the agent on an exception.
                    // We are capturing the exception in telemetry.
                }
            }

            return this.CurrentExperiment != null;
        }

        private static void ThrowOnDuplicateParameters(string commandLineArguments, string parameter)
        {
            MatchCollection matches = Regex.Matches(commandLineArguments, parameter, RegexOptions.IgnoreCase);
            if (matches != null && matches.Count > 1)
            {
                throw new ProviderException(
                    $"Invalid command line usage. Duplicate command line parameters found: '{parameter}'.",
                    ErrorReason.InvalidUsage);
            }
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
