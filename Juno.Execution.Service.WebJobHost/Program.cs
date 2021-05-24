namespace Juno.Execution.Service
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.Management;
    using Juno.Execution.Providers.Environment;
    using Juno.Hosting.Common;
    using Juno.Providers;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Juno execution management webjob host executable.
    /// </summary>
    public class Program
    {
        internal const string HostName = "ExecutionService";

        internal static Assembly HostAssembly { get; } = Assembly.GetAssembly(typeof(Program));

        internal static string HostExePath { get; } = Path.GetDirectoryName(Program.HostAssembly.Location);

        internal static ILogger DiagnosticsLogger { get; private set; }

        internal static ILogger Logger { get; private set; }

        internal static Guid SessionCorrelationId { get; } = Guid.NewGuid();

        internal static string OverrideWorkQueue { get; private set; }

        /// <summary>
        /// Entry point for the WebJob host executable.
        /// </summary>
        public static int Main(string[] args)
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                using (CommandLineApplication application = new CommandLineApplication()
                   .DescribeHost(new HostDescription
                   {
                       Name = Program.HostAssembly.GetName().Name,
                       FullName = "Juno Experiment Execution Service",
                       Description = "Runs Juno experiment execution/orchestration steps.",
                       Version = Program.HostAssembly.GetName().Version
                   }))
                {
                    CommandOption environmentOption = application.AddOptionEnvironment();
                    CommandOption configurationPathOption = application.AddOptionConfigurationPath();
                    CommandOption workQueueOption = application.AddOptionWorkQueue();

                    application.OnExecute(new Func<int>(() =>
                    {
                        int exitCode = 0;

                        try
                        {
                            Console.CancelKeyPress += (sender, e) =>
                            {
                                tokenSource.Cancel();
                            };

                            IConfiguration configuration = Program.GetEnvironmentConfiguration(environmentOption, configurationPathOption);
                            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

                            Program.InitializePersistentTelemetryInfo();
                            Program.InitializeLogging(settings);
                            // Autotriage provider now has the ConfigureServicesAsync call to add in needed dependencies
                            // Program.InitializeExperimentDiagnostics(settings);

                            // We do not need to persist the correlation ID at this point. A correlation ID is persisted in the
                            // execution function for each individual round of execution work.
                            EventContext telemetryContext = new EventContext(Program.SessionCorrelationId);
                            Program.LogStartup(telemetryContext);
                            Program.OverrideWorkQueue = workQueueOption.Value();

                            Program.Logger.LogTelemetry(Program.HostName, telemetryContext, () =>
                            {
                                var builder = new HostBuilder();
                                builder.ConfigureWebJobs(b =>
                                {
                                    b.AddAzureStorageCoreServices();
                                    b.AddAzureStorage();
                                });

                                builder.ConfigureLogging((context, b) =>
                                {
                                    b.AddConsole();
                                    b.AddApplicationInsightsWebJobs(o => o.InstrumentationKey = settings.AppInsightsSettings.Get(Setting.Tracing).InstrumentationKey);
                                });

                                // Depedency injection registration.
                                builder.ConfigureServices(services =>
                                {
                                    services.AddSingleton<IConfiguration>(configuration);
                                    services.AddSingleton<ILogger>(Program.Logger);
                                });

                                // Ensure provider types are loaded. Providers are referenced in experiment components by their
                                // fully qualified name. In order for this to work, the Type definitions from the assembly must have
                                // been loaded. We are forcing the App Domain to load the types if they are not already loaded.
                                ExperimentProviderTypeCache.Instance.LoadProviders(Program.HostExePath);

                                using (IHost host = builder.Build())
                                {
                                    var jobHost = host.Services.GetService(typeof(IJobHost)) as JobHost;
                                    host.StartAsync().Wait();

                                    // Start the job when the host start for continues running job. This is also called manual triggered.
                                    jobHost.CallAsync("ExecuteAsync", tokenSource.Token).Wait(tokenSource.Token);
                                    host.StopAsync().Wait();

                                    // Try to ensure a graceful shutdown as best as possible.
                                    tokenSource.Cancel();
                                    ExecutionManager.WaitHandle.Wait();
                                }
                            });
                        }
                        catch (OperationCanceledException)
                        {
                            // Expected when the Ctrl-C is pressed to cancel operation.
                        }
                        catch (Exception exc)
                        {
                            exitCode = 1;
                            EventContext errorContext = new EventContext(Program.SessionCorrelationId)
                                .AddError(exc);

                            Program.Logger?.LogTelemetry($"{Program.HostName}.StartupError", LogLevel.Error, errorContext);

                            // For the case the telemetry loggers have not been initialized
                            // successfully.
                            Trace.TraceError($"[{Program.HostName}StartupError{Environment.NewLine}{exc.Message}{Environment.NewLine}{exc.StackTrace}");
                        }
                        finally
                        {
                            EventContext errorContext = new EventContext(Program.SessionCorrelationId)
                                .AddContext(nameof(exitCode), exitCode);

                            Program.Logger?.LogTelemetry($"{Program.HostName}.Shutdown", LogLevel.Information, errorContext);
                            HostDependencies.FlushTelemetry();
                        }

                        return exitCode;
                    }));

                    return application.Execute(args);
                }
            }
        }

        /// <summary>
        /// Returns true if the execution service is running in a 'staging' deployment
        /// slot (vs. the production slot).
        /// </summary>
        internal static bool IsRunningInStagingSlot()
        {
            return string.Equals(HostingContext.GetWebAppDeploymentSlot(), "staging", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns true if the Web App has signalled a shutdown is requested. This is used to
        /// allow the service to perform a graceful shutdown.
        /// </summary>
        internal static bool IsShutdownRequested()
        {
            bool shutdownRequested = false;

            try
            {
                return HostingContext.IsWebJobShutdownRequested();
            }
            catch (Exception exc)
            {
                Program.Logger.LogTelemetry($"{Program.HostName}.ShutdownRequestVerificationError", LogLevel.Error, new EventContext(Program.SessionCorrelationId)
                    .AddError(exc));
            }

            return shutdownRequested;
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
            AadPrincipalSettings executionSvcPrincipal = settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc);
            IAzureKeyVault keyVaultClient = HostDependencies.CreateKeyVaultClient(executionSvcPrincipal, settings.KeyVaultSettings.Get(Setting.Default));

            AppInsightsSettings appInsightsTelemetrySettings = settings.AppInsightsSettings.Get(Setting.Telemetry);
            AppInsightsSettings appInsightsTracingSettings = settings.AppInsightsSettings.Get(Setting.Tracing);
            EventHubSettings eventHubTelemetrySettings = settings.EventHubSettings.Get(Setting.ExecutionTelemetry);

            Program.DiagnosticsLogger = HostDependencies.CreateLogger(Program.HostName, appInsightsTelemetrySettings);

            Program.Logger = HostDependencies.CreateLogger(
               Program.HostName,
               appInsightsTelemetrySettings,
               appInsightsTracingSettings,
               eventHubTelemetrySettings,
               keyVaultClient: keyVaultClient,
               enableDiagnostics: true,
               eventHubChannelConfiguration: (channel) =>
               {
                   channel.EventsDropped += (sender, args) =>
                   {
                       Program.DiagnosticsLogger.LogTelemetry(
                           $"{Program.HostName}.TelemetryEventsDropped", LogLevel.Warning, new EventContext(Guid.NewGuid())
                           .AddContext("count", args.Events?.Count()));
                   };

                   channel.EventTransmissionError += (sender, args) =>
                   {
                       Program.DiagnosticsLogger.LogTelemetry(
                           $"{Program.HostName}.TelemetryEventTransmissionError", LogLevel.Warning, new EventContext(Guid.NewGuid())
                           .AddContext("count", args.Events?.Count())
                           .AddError(args.Error));
                   };
               });
        }

        // Services and dependencies are now populated by the ConfigureServicesAsync function in the auto-triage provider.
        //        private static void InitializeExperimentDiagnostics(EnvironmentSettings settings)
        //        {
        // settings.ThrowIfNull(nameof(settings));

        // KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
        // kustoSettings.ThrowIfNull(nameof(kustoSettings));

        //// Create kusto query issuer.
        // AadPrincipalSettings aadPrincipal = kustoSettings.AadPrincipals.Get(Setting.Default);
        // aadPrincipal.ThrowIfNull(nameof(aadPrincipal));
        // aadPrincipal.PrincipalId.ThrowIfNullOrWhiteSpace(nameof(aadPrincipal.PrincipalId));
        // aadPrincipal.PrincipalCertificateThumbprint.ThrowIfNullOrWhiteSpace(nameof(aadPrincipal.PrincipalCertificateThumbprint));
        // aadPrincipal.AuthorityUri.ThrowIfNull(nameof(aadPrincipal.AuthorityUri));
        // IKustoQueryIssuer queryIssuer = new KustoQueryIssuer(aadPrincipal.PrincipalId, aadPrincipal.PrincipalCertificateThumbprint, aadPrincipal.AuthorityUri.AbsoluteUri);

        // AadPrincipalSettings executionSvcPrincipal = settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc);
        // AadPrincipalSettings executionApiPrincipal = settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionApi);
        // ExecutionClient apiClient = HostDependencies.CreateExecutionApiClient(
        //    executionSvcPrincipal,
        //    executionApiPrincipal,
        //    settings.ExecutionSettings.ExecutionApiUri);

        //// Create arm client
        // IRestClient restClient = new RestClientBuilder()
        //    .WithAutoRefreshToken(
        //        settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc).AuthorityUri,
        //        settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc).PrincipalId,
        //        "https://management.core.windows.net/",
        //        settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc).PrincipalCertificateThumbprint)
        //    .AddAcceptedMediaType(MediaType.Json)
        //    .Build();

        // IArmClient armClient = new ArmClient(restClient);
        // IServiceCollection services = new ServiceCollection()
        //    .AddSingleton<ExecutionClient>(apiClient)
        //    .AddSingleton<IProviderDataClient>((provider) => new ProviderDataClient(apiClient))
        //    .AddSingleton<IKustoQueryIssuer>(queryIssuer)
        //    .AddSingleton<IArmClient>(armClient);

        //// Create microcode update failure kusto diagnostics handler.
        // DiagnosticsProvider microcodeUpdateFailureDiagnosticsHandler = new MicrocodeUpdateFailureKustoDiagnostics(services);

        //// Create tip deployment failure kusto diagnostics handler.
        // DiagnosticsProvider tipDeploymentFailureDiagnosticsHandler = new TipDeploymentFailureKustoDiagnostics(services);

        //// Create tip deployment failure kusto diagnostics handler.
        // DiagnosticsProvider tipApiServiceFailureDiagnosticsHandler = new TipApiServiceFailureDiagnostics(services);

        //// Create arm deployment failure kusto diagnostics handler.
        // DiagnosticsProvider armvmDeploymentFailureKustoDiagnosticsHandler = new ArmVmDeploymentFailureKustoDiagnostics(services);

        //// Create armvm deployment failure activity log diagnostics handler.
        // DiagnosticsProvider armvmDeploymentFailureActivityLogDiagnosticsHandler = new ArmVmDeploymentFailureActivityLogDiagnostics(services);
        //        }

        private static void InitializePersistentTelemetryInfo()
        {
            EventContext.PersistentProperties.AddRange(new Dictionary<string, object>
            {
                ["host"] = Environment.MachineName,
                ["hostStartTime"] = DateTime.UtcNow.ToString("o"),
                ["environment"] = HostingContext.GetEnvironment(),
                ["scope"] = "services/execution",
                ["slot"] = Program.IsRunningInStagingSlot() ? "staging" : "production",
                ["version"] = new
                {
                    contracts = Assembly.GetAssembly(typeof(Experiment)).GetName().Version.ToString(),
                    hostCommon = Assembly.GetAssembly(typeof(HostDependencies)).GetName().Version.ToString(),
                    executionWebJob = Assembly.GetAssembly(typeof(Program)).GetName().Version.ToString(),
                    executionManagement = Assembly.GetAssembly(typeof(ExecutionManager)).GetName().Version.ToString(),
                    executionProviders = Assembly.GetAssembly(typeof(InstallGuestAgentProvider)).GetName().Version.ToString(),
                    providers = Assembly.GetAssembly(typeof(ExperimentProvider)).GetName().Version.ToString(),
                    telemetry = Assembly.GetAssembly(typeof(EventContext)).GetName().Version.ToString()
                }
            });
        }

        private static void LogStartup(EventContext telemetryContext)
        {
            Program.Logger?.LogInformation(
                $"{Program.HostName} starting up. {string.Join(", ", EventContext.PersistentProperties.Select(p => $"{p.Key}={p.Value.ToString()}"))}");

            Program.Logger?.LogTelemetry($"{Program.HostName}.Startup", LogLevel.Information, telemetryContext);
        }
    }
}