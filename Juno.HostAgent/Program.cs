namespace Juno.HostAgent
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.AgentRuntime;
    using Juno.Execution.AgentRuntime.Contract;
    using Juno.Execution.AgentRuntime.Tasks;
    using Juno.Execution.AgentRuntime.Windows;
    using Juno.Execution.Management;
    using Juno.Execution.Management.Tasks;
    using Juno.Execution.Providers.Environment;
    using Juno.Hosting.Common;
    using Juno.Providers;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.KeyVault.Models;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Polly;
    using static Microsoft.Azure.KeyVault.KeyVaultClient;

    /// <summary>
    /// The entry point for host agent.
    /// </summary>
    public static class Program
    {
        private const string AccessTokenLocation = @"C:\Data\Juno.AccessToken.txt";
        private static ILogger logger;
        private static ILogger debugLogger;
        private static EnvironmentSettings settings;

        internal static AgentType AgentType { get; } = AgentType.HostAgent;

        internal static string Environment { get; private set; }

        internal static Assembly HostAssembly { get; } = Assembly.GetAssembly(typeof(Program));

        internal static string HostExePath { get; } = Path.GetDirectoryName(Program.HostAssembly.Location);

        internal static ILogger Logger
        {
            get
            {
                return Program.logger ?? Program.debugLogger;
            }
        }

        /// <summary>
        /// The entry point for the Juno Host Agent executable.
        /// </summary>
        public static int Main(string[] args)
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                CommandLineApplication application = new CommandLineApplication()
                    .DescribeHost(new HostDescription
                    {
                        Name = Program.HostAssembly.GetName().Name,
                        FullName = "Juno Host Agent",
                        Description = "Runs Juno experiment operations on physical nodes/blades.",
                        Version = Program.HostAssembly.GetName().Version
                    });

                CommandOption environmentOption = application.AddOptionEnvironment();
                CommandOption configurationPathOption = application.AddOptionConfigurationPath();
                CommandOption agentIdOption = application.AddOptionAgentId();
                CommandOption disableMonitorsOption = application.AddOptionDisableMonitors();

                application.OnExecute(new Func<int>(() =>
                {
                    int exitCode = 0;
                    if (application.HelpOption().HasValue())
                    {
                        application.ShowHelp();
                    }
                    else if (!environmentOption.HasValue() && !configurationPathOption.HasValue())
                    {
                        exitCode = 1;
                        application.ShowHelp();
                    }
                    else
                    {
                        Console.CancelKeyPress += (sender, e) =>
                        {
                            tokenSource.Cancel();
                        };

                        try
                        {
                            IConfiguration configuration = Program.GetEnvironmentConfiguration(environmentOption, configurationPathOption);
                            Program.settings = EnvironmentSettings.Initialize(configuration);

                            AgentIdentification agentId = Program.GetAgentIdentification(agentIdOption);

                            Program.InitializeTelemetry(agentId, Program.settings.Environment);
                            Program.InitializeDebugLogging(Program.settings);
                            Program.InstallHostAgentCertificateAsync(agentId, tokenSource.Token).GetAwaiter().GetResult();
                            Program.Logger.LogTelemetry($"{Program.AgentType}Starting", LogLevel.Information, new EventContext(Guid.NewGuid()));
                            Program.InitializeLogging(Program.settings);

                            IServiceCollection services = Program.SetupDependencies(agentId, configuration);
                            IEnumerable<Task> runtimeTasks = Program.StartRuntimeTasks(services, agentId, disableMonitorsOption.HasValue(), tokenSource.Token);
                            Task.WaitAll(runtimeTasks.ToArray());
                        }
                        catch (OperationCanceledException)
                        {
                            // Expected when the Ctrl-C is pressed to cancel operation.
                            Program.Logger?.LogTelemetry($"{Program.AgentType}.Cancelled", LogLevel.Warning, new EventContext(Guid.NewGuid()));
                        }
                        catch (Exception exc)
                        {
                            exitCode = 1;
                            Console.WriteLine(exc.ToString());
                            Program.Logger?.LogTelemetry($"{Program.AgentType}.Crash", LogLevel.Critical, new EventContext(Guid.NewGuid()).AddError(exc));

                            // Capture the unhandled exception event in the local event log as well to add another
                            // way for us to ensure we can identify the reasons we are crashing/exiting.
                            Program.WriteCrashInfoToEventLog(exc);
                        }
                        finally
                        {
                            HostDependencies.FlushTelemetry();
                        }
                    }

                    return exitCode;
                }));

                return application.Execute(args);
            }
        }

        private static AgentIdentification GetAgentIdentification(CommandOption agentIdOption)
        {
            AgentIdentification agentId;

            // Check whether user has provided agentId over commandline parameters. If yes, make use of it.
            if (agentIdOption.HasValue())
            {
                agentId = new AgentIdentification(agentIdOption.Value());
            }
            else
            {
                ISystemPropertyReader reader = SystemManagerFactory.Get().PropertyReader;

                agentId = new AgentIdentification(
                    reader.Read(AzureHostProperty.ClusterName),
                    reader.Read(AzureHostProperty.NodeId),
                    null,
                    reader.Read(AzureHostProperty.TipSessionId));
            }

            return agentId;
        }

        private static async Task<bool> CheckCertificateAsync(CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persisted();
            bool certificateInstalled = false;
            await Program.Logger.LogTelemetryAsync($"{Program.AgentType}.CheckCertificate", telemetryContext, async () =>
            {
                ICertificateManager certificateManager = new CertificateManager();
                string certificateThumbprint = Program.settings.AgentSettings.AadPrincipals.Get(Setting.HostAgent).PrincipalCertificateThumbprint;

                try
                {
                    X509Certificate2 cert = await certificateManager.GetCertificateFromStoreAsync(certificateThumbprint).ConfigureAwait(false);
                    if (cert != null)
                    {
                        certificateInstalled = true;
                    }
                }
                catch
                {
                    certificateInstalled = false;
                }
            }).ConfigureAwait(false);

            return certificateInstalled;
        }

        private static async Task<string> GetAccessTokenFromFileAsync(AgentIdentification agentId, CancellationToken cancellationToken)
        {
            string token = null;
            EventContext context = EventContext.Persisted();
            await Program.Logger.LogTelemetryAsync($"{Program.AgentType}.GetAccessTokenFromFile", context, async () =>
            {
                string encryptedToken = await File.ReadAllTextAsync(Program.AccessTokenLocation, cancellationToken).ConfigureAwait(false);
                context.AddContext("AccessTokenLength", encryptedToken.Length);

                // The file is encrypted so it's ok for it to leave a little longer until we investigate why the HA can't find the certificate on second run.
                // File.Delete(Program.AccessTokenLocation);
                token = AesCrypto.Decrypt(Convert.FromBase64String(encryptedToken), agentId.ToString(), agentId.Context);
            }).ConfigureAwait(false);

            return token;
        }

        private static IConfiguration GetEnvironmentConfiguration(CommandOption environmentOption, CommandOption configurationPathOption)
        {
            if (configurationPathOption.HasValue())
            {
                string path = configurationPathOption.Value();
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Configuration file not found at path '{path}'");
                }

                return new ConfigurationBuilder()
                    .AddJsonFile(path)
                    .Build();
            }
            else
            {
                return HostingContext.GetEnvironmentConfiguration(Program.HostExePath, environmentOption.Value());
            }
        }

        private static void InitializeLogging(EnvironmentSettings settings)
        {
            AadPrincipalSettings agentPrincipal = settings.AgentSettings.AadPrincipals.Get(Setting.HostAgent);
            IAzureKeyVault keyVaultClient = HostDependencies.CreateKeyVaultClient(agentPrincipal, settings.KeyVaultSettings.Get(Setting.Default));
            
            EventHubSettings eventHubTelemetrySettings = settings.EventHubSettings.Get(Setting.AgentTelemetry);
            Program.logger = HostDependencies.CreateLogger(
               Program.AgentType.ToString(),
               eventHubTelemetrySettings: eventHubTelemetrySettings,
               keyVaultClient: keyVaultClient,
               enableDiagnostics: true);
        }

        private static void InitializeDebugLogging(EnvironmentSettings settings)
        {
            AppInsightsSettings appInsightsTelemetrySettings = settings.AppInsightsSettings.Get(Setting.Telemetry);
            AppInsightsSettings appInsightsTracingSettings = settings.AppInsightsSettings.Get(Setting.Tracing);

            Program.debugLogger = HostDependencies.CreateLogger(
              Program.AgentType.ToString(),
              appInsightsTelemetrySettings,
              appInsightsTracingSettings);
        }

        private static void InitializeTelemetry(AgentIdentification agentId, string environment)
        {
            EventContext.PersistentProperties.AddRange(new Dictionary<string, object>
            {
                ["host"] = System.Environment.MachineName,
                ["agentId"] = agentId.ToString(),
                ["agentCluster"] = agentId.ClusterName,
                ["agentNodeId"] = agentId.NodeName,
                ["agentContextId"] = agentId.Context,
                ["environment"] = environment,
                ["scope"] = "agents/host",
                ["version"] = new
                {
                    hostAgent = Assembly.GetAssembly(typeof(Program)).GetName().Version.ToString(),
                    contracts = Assembly.GetAssembly(typeof(Experiment)).GetName().Version.ToString(),
                    execution = Assembly.GetAssembly(typeof(HostDependencies)).GetName().Version.ToString(),
                    executionManagement = Assembly.GetAssembly(typeof(AgentExperimentExecutionTask)).GetName().Version.ToString(),
                    executionProviders = Assembly.GetAssembly(typeof(InstallGuestAgentProvider)).GetName().Version.ToString(),
                    providers = Assembly.GetAssembly(typeof(ExperimentProvider)).GetName().Version.ToString()
                }
            });
        }

        private static async Task InstallHostAgentCertificateAsync(AgentIdentification agentId, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persisted();
            await Program.Logger.LogTelemetryAsync($"{Program.AgentType}.InstallHostAgentCertificate", telemetryContext, async () =>
            {
                // If this is called a second time, it's expected the certificate should already be installed.
                bool certificateInstalled = await Program.CheckCertificateAsync(cancellationToken).ConfigureAwait(false);
                telemetryContext.AddContext(nameof(certificateInstalled), certificateInstalled);
                if (!certificateInstalled)
                {
                    string jwtToken = await Program.GetAccessTokenFromFileAsync(agentId, cancellationToken).ConfigureAwait(false);
                    AuthenticationCallback tokenCallback = (string authority, string resource, string scope) =>
                    {
                        return Task.FromResult(jwtToken);
                    };

                    KeyVaultClient kvClient = new KeyVaultClient(tokenCallback);

                    // Certificate with private key are stored as secret in AzKv.
                    SecretBundle certificateBase64 = await kvClient.GetSecretAsync(
                        Program.settings.KeyVaultSettings.Get(Setting.Default).Uri.AbsoluteUri,
                        Program.settings.AgentSettings.HostAgentCertificateName,
                        cancellationToken).ConfigureAwait(false);

                    X509Certificate2 cert = new X509Certificate2(Convert.FromBase64String(certificateBase64.Value), string.Empty, X509KeyStorageFlags.PersistKeySet);

                    telemetryContext.AddContext("certificateSubject", cert.Subject);
                    telemetryContext.AddContext("certificateThumbprint", cert.Thumbprint);

                    ICertificateManager certificateManager = new CertificateManager();
                    await certificateManager.InstallCertificateToStoreAsync(cert, StoreName.My, StoreLocation.LocalMachine).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        private static IServiceCollection SetupDependencies(AgentIdentification agentId, IConfiguration configuration)
        {
            // Agent and API AAD principals used to authenticate. The Agent principal is used to authenticate with AAD
            // in order to get a JWT/token that can be used to authenticate with the Agents API.
            AadPrincipalSettings agentPrincipal = Program.settings.AgentSettings.AadPrincipals.Get(Setting.HostAgent);
            AadPrincipalSettings apiPrincipal = Program.settings.AgentSettings.AadPrincipals.Get(Setting.AgentsApi);

            ApiSettings agentApiEndpoint = Program.settings.AgentSettings.Apis.Get(Setting.AgentsApi);
            ApiSettings agentFileUploadApiEndpoint = Program.settings.AgentSettings.Apis.Get(Setting.AgentsFileUploadApi);
            ApiSettings agentHeartbeatApiEndpoint = Program.settings.AgentSettings.Apis.Get(Setting.AgentsHeartbeatApi);

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
                .AddSingleton<ILogger>(Program.Logger)
                .AddSingleton<AgentIdentification>(agentId)
                .AddSingleton<AgentClient>(agentApiClient)
                .AddSingleton<ClientPool<AgentClient>>(agentApiClientPool)
                .AddSingleton<IAzureKeyVault>(provider => HostDependencies.CreateKeyVaultClient(agentPrincipal, Program.settings.KeyVaultSettings.Get(Setting.Default)))
                .AddSingleton<IProviderDataClient>(new ProviderDataClient(agentApiClient, logger: Program.Logger))
                .AddSingleton<IMsr>(new Msr())
                .AddSingleton<IFirmwareReader<BiosInfo>>(new BiosReader())
                .AddSingleton<IFirmwareReader<IEnumerable<SsdWmicInfo>>>(new SsdReaderWmic())
                .AddSingleton<IFirmwareReader<IEnumerable<SsdInfo>>>(new SsdReader())
                .AddSingleton<IFirmwareReader<BmcInfo>>(new BmcReader())
                .AddSingleton<IConfiguration>(configuration);

            AgentExecutionManager executionManager = new AgentExecutionManager(services, configuration);
            services.AddSingleton<AgentExecutionManager>(executionManager);

            // Ensure provider types are loaded. Providers are referenced in experiment components by their
            // fully qualified name. In order for this to work, the Type definitions from the assembly must have
            // been loaded. We are forcing the App Domain to load the types if they are not already loaded.
            ExperimentProviderTypeCache.Instance.LoadProviders(Path.GetDirectoryName(Assembly.GetAssembly(typeof(Program)).Location));

            return services;
        }

        private static IEnumerable<Task> StartRuntimeTasks(IServiceCollection services, AgentIdentification agentId, bool disableMonitors, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persisted();
            return Program.Logger.LogTelemetry($"{Program.AgentType}.ConstructMonitors", telemetryContext, () =>
            {
                List<Task> agentTasks = new List<Task>
                {
                    // Task: Sends heartbeats to the Juno system on an interval.
                    new AgentHeartbeatTask(services, Program.settings, agentId, Program.AgentType)
                        .ExecuteAsync(Program.settings.AgentSettings.HeartbeatInterval, cancellationToken),

                    // Task: Processes Juno experiment steps targeted for on the physical node (e.g. payload application).
                    new AgentExperimentExecutionTask(services, Program.settings, agentId, Program.AgentType)
                        .ExecuteAsync(Program.settings.AgentSettings.WorkPollingInterval, cancellationToken)
                };

                if (!disableMonitors)
                {
                    // Monitoring Tasks:
                    // VM Uptime Monitor
                    AgentSystemMonitoring systemMonitoring = new AgentSystemMonitoring(
                        services,
                        Program.settings,
                        agentId,
                        Program.AgentType,
                        Policy.Handle<Exception>().WaitAndRetryAsync(10, (retries) => TimeSpan.FromMilliseconds(retries * 1000)));

                    agentTasks.AddRange(systemMonitoring.StartMonitors(cancellationToken));
                }

                return agentTasks;
            });
        }

        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Juno Host agent only runs on Windows.", Scope = "member", Target = "~M:Juno.HostAgent.Program.WriteCrashInfoToEventLog(System.Exception)")]
        private static void WriteCrashInfoToEventLog(Exception exc)
        {
            string desiredSource = "Juno";
            string backupSource = "Application";
            string crashMessage = $"The Juno {Program.AgentType} crashed with an unhandled exception:{System.Environment.NewLine}{exc.ToString(true, true)}";

            try
            {
                if (!EventLog.SourceExists(desiredSource))
                {
                    EventLog.CreateEventSource(desiredSource, "Application");
                }

                EventLog.WriteEntry(desiredSource, crashMessage, EventLogEntryType.Error);
            }
            catch (SecurityException)
            {
                // Capture the error in the default 'Application' source otherwise.
                EventLog.WriteEntry(backupSource, crashMessage, EventLogEntryType.Error);
            }
        }
    }
}
