namespace Juno.GuestAgent
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.Invocation;
    using System.CommandLine.Parsing;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Reflection;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.AgentRuntime;
    using Juno.Execution.AgentRuntime.CommandLine;
    using Juno.Execution.AgentRuntime.Tasks;
    using Juno.Execution.Management;
    using Juno.Execution.Management.Tasks;
    using Juno.Execution.Providers.Environment;
    using Juno.Hosting.Common;
    using Juno.Providers;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Platform;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Polly;

    /// <summary>
    /// A class that combines command line instructions with a project execution plan for executing an experiment
    /// </summary>
    public class ExperimentExecutionCommand : CommandExecution
    {
        private readonly AgentType agentType = AgentType.GuestAgent;

        /// <summary>
        /// The user provided agent id in the specified format.
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// The Azure region in which the VM is running.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// The user provided VM SKU for the VM (e.g Standard_D2s_v3).
        /// </summary>
        public string VmSku { get; set; }

        /// <summary>
        /// The environment configuration/settings.
        /// </summary>
        protected EnvironmentSettings Settings { get; private set; }

        /// <summary>
        /// Creates the command line option definitions/descriptions for the agent installation
        /// command.
        /// </summary>
        /// <param name="args">The command line arguments passed into the agent.</param>
        /// <param name="cancellationToken">The host cancellation token.</param>
        public static CommandLineBuilder CreateBuilder(string[] args, CancellationToken cancellationToken)
        {
            RootCommand rootCommand = new RootCommand("Runs Juno experiment operations on virtual machines.")
            {
                OptionFactory.CreateEnvironmentOption(required: true, defaultValue: HostingContext.GetEnvironment()),
                OptionFactory.CreateAgentIdOption(required: true, defaultValue: HostingContext.GetAgentId(), validator: (option) =>
                {
                    string result = null;

                    try
                    {
                        _ = new AgentIdentification(option.GetValueOrDefault()?.ToString());
                    }
                    catch (Exception e)
                    {
                        result = e.Message;
                    }

                    // returning a null value means it passes validation
                    // otherwise it will display the string message to the console
                    return result;
                }),
                OptionFactory.CreateConfigurationPathOption(),
                OptionFactory.CreateRegionOption(),
                OptionFactory.CreateVmSkuOption()
            };

            rootCommand.Handler = CommandHandler.Create<ExperimentExecutionCommand>((handler) => handler.ExecuteAsync(args, cancellationToken));

            return new CommandLineBuilder(rootCommand);
        }

        /// <inheritdoc/>
        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "ServiceBase.Run will only execute on Win32NT")]
        public override async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken, IServiceCollection services = null)
        {
            int result = 0;
            try
            {
                IConfiguration configuration = this.GetEnvironmentConfiguration(this.Environment, this.ConfigurationPath);
                this.Settings = EnvironmentSettings.Initialize(configuration);

                AgentIdentification agentId = new AgentIdentification(this.AgentId);
                this.InitializeTelemetry(agentId, this.VmSku, this.Region);
                this.InitializeLogging(this.Settings);
                this.Logger.LogTelemetry($"{this.agentType}Starting", LogLevel.Information, new EventContext(Guid.NewGuid()));
                IServiceCollection dependencies = this.SetupDependencies(agentId, configuration);

                IEnumerable<Task> runtimeTasks = this.StartRuntimeTasks(dependencies, agentId, cancellationToken);

                if (PlatformUtil.CurrentPlatform == PlatformID.Win32NT)
                {
                    using (var host = new WindowsServiceBase(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)))
                    {
                        ServiceBase.Run(host);
                    }
                }

                await Task.WhenAll(runtimeTasks.ToArray()).ConfigureAwait(false);
            }
            finally
            {
                HostDependencies.FlushTelemetry();
            }

            return result;
        }

        private IConfiguration GetEnvironmentConfiguration(string environmentOption, string configurationPathOption)
        {
            if (!string.IsNullOrWhiteSpace(configurationPathOption))
            {
                if (!File.Exists(configurationPathOption))
                {
                    throw new FileNotFoundException($"Configuration file not found at path '{configurationPathOption}'");
                }

                return new ConfigurationBuilder()
                    .AddJsonFile(configurationPathOption)
                    .Build();
            }
            else
            {
                return HostingContext.GetEnvironmentConfiguration(Program.HostExePath, environmentOption);
            }
        }

        private void InitializeLogging(EnvironmentSettings settings)
        {
            AadPrincipalSettings agentPrincipal = settings.AgentSettings.AadPrincipals.Get(Setting.GuestAgent);
            IAzureKeyVault keyVaultClient = HostDependencies.CreateKeyVaultClient(agentPrincipal, settings.KeyVaultSettings.Get(Setting.Default));

            AppInsightsSettings appInsightsTelemetrySettings = settings.AppInsightsSettings.Get(Setting.Telemetry);
            AppInsightsSettings appInsightsTracingSettings = settings.AppInsightsSettings.Get(Setting.Tracing);
            EventHubSettings eventHubTelemetrySettings = settings.EventHubSettings.Get(Setting.AgentTelemetry);

            this.Logger = HostDependencies.CreateLogger(
               this.agentType.ToString(),
               appInsightsTelemetrySettings,
               appInsightsTracingSettings,
               eventHubTelemetrySettings,
               keyVaultClient: keyVaultClient,
               enableDiagnostics: true);

            // Enable logging at the entry point level.
            Program.Logger = this.Logger;
        }

        private void InitializeTelemetry(AgentIdentification agentId, string vmSku = null, string region = null)
        {
            ISystemPropertyReader reader = SystemManagerFactory.Get().PropertyReader;

            EventContext.PersistentProperties.AddRange(new Dictionary<string, object>
            {
                ["host"] = System.Environment.MachineName,
                ["hostStartTime"] = DateTime.UtcNow.ToString("o"),
                ["agentId"] = agentId.ToString(),
                ["agentRegion"] = region,
                ["agentCluster"] = agentId.ClusterName,
                ["agentNodeId"] = agentId.NodeName,
                ["agentContextId"] = agentId.Context,
                ["agentVmName"] = agentId.VirtualMachineName,
                ["agentVmSku"] = vmSku,
                ["environment"] = this.Environment,
                ["containerId"] = reader.ReadContainerId(),
                ["scope"] = "agents/guest",
                ["version"] = new
                {
                    guestAgent = Assembly.GetAssembly(typeof(ExperimentExecutionCommand)).GetName().Version.ToString(),
                    contracts = Assembly.GetAssembly(typeof(Experiment)).GetName().Version.ToString(),
                    execution = Assembly.GetAssembly(typeof(HostDependencies)).GetName().Version.ToString(),
                    executionManagement = Assembly.GetAssembly(typeof(AgentExperimentExecutionTask)).GetName().Version.ToString(),
                    executionProviders = Assembly.GetAssembly(typeof(InstallGuestAgentProvider)).GetName().Version.ToString(),
                    providers = Assembly.GetAssembly(typeof(ExperimentProvider)).GetName().Version.ToString()
                }
            });
        }

        private IServiceCollection SetupDependencies(AgentIdentification agentId, IConfiguration configuration)
        {
            // Agent and API AAD principals used to authenticate. The Agent principal is used to authenticate with AAD
            // in order to get a JWT/token that can be used to authenticate with the Agents API.
            AadPrincipalSettings agentPrincipal = this.Settings.AgentSettings.AadPrincipals.Get(Setting.GuestAgent);
            AadPrincipalSettings apiPrincipal = this.Settings.AgentSettings.AadPrincipals.Get(Setting.AgentsApi);

            ApiSettings agentApiEndpoint = this.Settings.AgentSettings.Apis.Get(Setting.AgentsApi);
            ApiSettings agentFileUploadApiEndpoint = this.Settings.AgentSettings.Apis.Get(Setting.AgentsFileUploadApi);
            ApiSettings agentHeartbeatApiEndpoint = this.Settings.AgentSettings.Apis.Get(Setting.AgentsHeartbeatApi);

            AgentClient agentApiClient = HostDependencies.CreateAgentsApiClient(agentPrincipal, apiPrincipal, agentApiEndpoint.Uri);
            AgentClient agentFileUploadApiClient = HostDependencies.CreateAgentsApiClient(agentPrincipal, apiPrincipal, agentFileUploadApiEndpoint.Uri);
            AgentClient agentHeartbeatApiClient = HostDependencies.CreateAgentsApiClient(agentPrincipal, apiPrincipal, agentHeartbeatApiEndpoint.Uri);

            // Enable the client processes to use different Agents API endpoints
            ClientPool<AgentClient> agentApiClientPool = new ClientPool<AgentClient>
            {
                [ApiClientType.AgentApi] = agentApiClient,
                [ApiClientType.AgentFileUploadApi] = agentFileUploadApiClient,
                [ApiClientType.AgentHeartbeatApi] = agentHeartbeatApiClient
            };

            IServiceCollection services = new ServiceCollection()
                .AddSingleton<ILogger>(this.Logger)
                .AddSingleton<IFileSystem>(new FileSystem())
                .AddSingleton<AgentIdentification>(agentId)
                .AddSingleton<AgentClient>(agentApiClient)
                .AddSingleton<ClientPool<AgentClient>>(agentApiClientPool)
                .AddSingleton<IAzureKeyVault>(provider => HostDependencies.CreateKeyVaultClient(agentPrincipal, this.Settings.KeyVaultSettings.Get(Setting.Default)))
                .AddSingleton<IProviderDataClient>(new ProviderDataClient(agentApiClient, logger: this.Logger))
                .AddSingleton<IConfiguration>(configuration);

            AgentExecutionManager executionManager = new AgentExecutionManager(services, configuration);
            services.AddSingleton<AgentExecutionManager>(executionManager);

            // Ensure provider types are loaded. Providers are referenced in experiment components by their
            // fully qualified name. In order for this to work, the Type definitions from the assembly must have
            // been loaded. We are forcing the App Domain to load the types if they are not already loaded.
            ExperimentProviderTypeCache.Instance.LoadProviders(Path.GetDirectoryName(Assembly.GetAssembly(typeof(ExperimentExecutionCommand)).Location));

            return services;
        }

        private IEnumerable<Task> StartRuntimeTasks(IServiceCollection services, AgentIdentification agentId, CancellationToken cancellationToken)
        {
            List<Task> agentTasks = new List<Task>
            {
                // Task: Sends heartbeats to the Juno system on an interval.
                new AgentHeartbeatTask(services, this.Settings, agentId, this.agentType)
                    .ExecuteAsync(this.Settings.AgentSettings.HeartbeatInterval, cancellationToken),

                // Task: Processes Juno experiment steps targeted for on the physical node (e.g. payload application).
                new AgentExperimentExecutionTask(services, this.Settings, agentId, this.agentType)
                    .ExecuteAsync(this.Settings.AgentSettings.WorkPollingInterval, cancellationToken)
            };

            // Monitoring Tasks:
            // VM Uptime Monitor
            AgentSystemMonitoring systemMonitoring = new AgentSystemMonitoring(
                services,
                this.Settings,
                agentId,
                this.agentType,
                Policy.Handle<Exception>().WaitAndRetryAsync(10, (retries) => TimeSpan.FromMilliseconds(retries * 1000)));

            agentTasks.AddRange(systemMonitoring.StartMonitors(cancellationToken));

            return agentTasks;
        }
    }
}
