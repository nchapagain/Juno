namespace Juno.GarbageCollector.WebJobHost
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.DataManagement;
    using Juno.Execution.TipIntegration;
    using Juno.Hosting.Common;
    using Kusto.Cloud.Platform.Utils;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Juno Garbage Collector webhost execuatable
    /// </summary>
    public class Program
    {
        internal const string HostName = "GarbageCollectorService";
        private const string ConfigurationPathOptionTemplate = "-c | --configurationPath <path>";
        private const string ConfigurationPathOptionDescription = "The path to the environment configuration file on the local machine.";
        private const string EnvironmentOptionTemplate = "-e | --environment <environment>";
        private const string EnvironmentOptionDescription = "The target environment for which the host/agent is running.";

        internal static Guid SessionCorrelationId { get; } = Guid.NewGuid();

        internal static ILogger DiagnosticsLogger { get; private set; }

        internal static ILogger Logger { get; private set; }

        private static Assembly HostAssembly { get; } = Assembly.GetAssembly(typeof(Program));

        private static string HostExePath { get; } = Path.GetDirectoryName(Program.HostAssembly.Location);

        /// <summary>
        /// Entry point for WebJob host executable.
        /// </summary>
        public static int Main(string[] args)
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                CommandLineApplication application = new CommandLineApplication()
                {
                    Name = Program.HostAssembly.GetName().Name,
                    FullName = "Juno Garbage Collector Service",
                    Description = "Runs the Juno Garbage Collector"
                };
                application.VersionOption("-ver|--version", () =>
                {
                    Version appVersion = Program.HostAssembly.GetName().Version;
                    return $"(v{appVersion.Major}.{appVersion.Minor}.{appVersion.Build})";
                });
                var configOption = application.Option(
                     Program.ConfigurationPathOptionTemplate,
                     Program.ConfigurationPathOptionDescription,
                     CommandOptionType.SingleValue);

                var envOption = application.Option(
                     Program.EnvironmentOptionTemplate,
                     Program.EnvironmentOptionDescription,
                     CommandOptionType.SingleValue);

                application.OnExecute(new Func<int>(() =>
                {
                    int exitCode = 0;
                    Guid correlationId = Guid.NewGuid();

                    try
                    {
                        Console.CancelKeyPress += (sender, e) =>
                        {
                            tokenSource.Cancel();
                        };

                        IConfiguration configuration = Program.GetEnvironmentConfiguration(envOption, configOption);
                        EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

                        Program.InitializePersistentTelemetryInfo();
                        Program.InitializeLogging(settings);

                        var envKey = settings.GarbageCollectorSettings.GarbageCollectorStorageAccount;
                        AadPrincipalSettings garbageCollectorPrincipal = settings.GarbageCollectorSettings.AadPrincipals.Get(Setting.GarbageCollector);
                        var kvClient = HostDependencies.CreateKeyVaultClient(garbageCollectorPrincipal, settings.KeyVaultSettings.Get(Setting.Default));
                        var storageKey = kvClient.ResolveSecretAsync(envKey, tokenSource.Token).GetAwaiter().GetResult();

                        var experimentClient = HostDependencies.CreateExperimentsApiClient(
                            garbageCollectorPrincipal,
                            settings.ExecutionSettings.AadPrincipals.Get(Setting.ExperimentsApi),
                            settings.GarbageCollectorSettings.ExperimentsApiUri);

                        var keyVaultClient = HostDependencies.CreateKeyVaultClient(garbageCollectorPrincipal, settings.KeyVaultSettings.Get(Setting.Default));
                        var kustoClient = HostDependencies.CreateKustoClient(settings);
                        ITipClient tipClient = new TipClient(configuration);
                        var experimentTemplateDataManager = HostDependencies.CreateExperimentTemplateDataManager(settings, keyVaultClient, Program.Logger);

                        var subScriptionManagers = HostDependencies.CreateAzureSubscriptionManager(settings, settings.GarbageCollectorSettings.EnabledSubscriptionIds);

                        // correlationId associated with every run of the garbage collector
                        EventContext telemetryContext = new EventContext(correlationId);

                        // These two are "magic" settings that are always required to get timer based webjobs working
                        // configuration["AzureWebJobsStorage"] = storageKey.ToOriginalString();
                        // configuration["AzureWebJobsDashboard"] = storageKey.ToOriginalString();

                        Program.Logger.LogTelemetry(Program.HostName, telemetryContext, () =>
                        {
                            var builder = new HostBuilder();
                            builder.ConfigureWebJobs(b =>
                            {
                                b.AddAzureStorageCoreServices();
                                b.AddAzureStorage();
                                // b.AddTimers();
                                // b.UseHostId(settings.Environment);
                            });

                            builder.ConfigureLogging((context, b) =>
                            {
                                b.AddConsole();
                                b.AddApplicationInsightsWebJobs(o => o.InstrumentationKey = settings.AppInsightsSettings.Get(Setting.Tracing).InstrumentationKey);
                            });

                            // Depedency injection registration.
                            builder.ConfigureServices(services =>
                            {
                                services.AddSingleton<IKustoQueryIssuer>(kustoClient);
                                services.AddSingleton<IConfiguration>(configuration);
                                services.AddSingleton<ILogger>(Program.Logger);
                                services.AddSingleton<IExperimentClient>(experimentClient);
                                services.AddSingleton<IAzureKeyVault>(keyVaultClient);
                                services.AddSingleton<ITipClient>(tipClient);
                                services.AddSingleton<IExperimentTemplateDataManager>(experimentTemplateDataManager);
                                services.AddSingleton<IList<ISubscriptionManager>>(subScriptionManagers);
                            });

                            using (IHost host = builder.Build())
                            {
                                // host.RunAsync(tokenSource.Token).GetAwaiter().GetResult();
                                var jobHost = host.Services.GetService(typeof(IJobHost)) as JobHost;
                                host.StartAsync().Wait();

                                // Start the job when the host start for continues running job. This is also called manual triggered.
                                jobHost.CallAsync("ExecuteAsync", tokenSource.Token).Wait(tokenSource.Token);
                                host.StopAsync().Wait();

                                // Try to ensure a graceful shutdown as best as possible.
                                tokenSource.Cancel();
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
                        EventContext errorContext = new EventContext(correlationId)
                            .AddError(exc);

                        Program.Logger?.LogTelemetry($"{Program.HostName}.StartupError", LogLevel.Error, errorContext);

                        Trace.TraceError($"[{Program.HostName}StartupError{Environment.NewLine}{exc.Message}{Environment.NewLine}{exc.StackTrace}");
                    }
                    finally
                    {
                        HostDependencies.FlushTelemetry();
                    }

                    return exitCode;
                }));

                return application.Execute(args);
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

        private static void InitializeLogging(EnvironmentSettings settings)
        {
            AppInsightsSettings appInsightsTelemetrySettings = settings.AppInsightsSettings.Get(Setting.Telemetry);
            AppInsightsSettings appInsightsTracingSettings = settings.AppInsightsSettings.Get(Setting.Tracing);

            AadPrincipalSettings gcPrincipal = settings.GarbageCollectorSettings.AadPrincipals.Get(Setting.GarbageCollector);
            IAzureKeyVault keyVaultClient = HostDependencies.CreateKeyVaultClient(gcPrincipal, settings.KeyVaultSettings.Get(Setting.Default));
            EventHubSettings eventHubTelemetrySettings = settings.EventHubSettings.Get(Setting.GarbageCollectionTelemetry);

            Program.DiagnosticsLogger = HostDependencies.CreateLogger(Program.HostName, appInsightsTelemetrySettings);

            Program.Logger = HostDependencies.CreateLogger(
               Program.HostName.ToString(),
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

        private static void InitializePersistentTelemetryInfo()
        {
            EventContext.PersistentProperties.AddRange(new Dictionary<string, object>
            {
                ["host"] = Environment.MachineName,
                ["environment"] = HostingContext.GetEnvironment(),
                ["scope"] = "services/garbageCollector",
                ["slot"] = Program.IsRunningInStagingSlot() ? "staging" : "production",
                ["version"] = new
                {
                    contracts = Assembly.GetAssembly(typeof(Experiment)).GetName().Version.ToString(),
                    hostCommon = Assembly.GetAssembly(typeof(HostDependencies)).GetName().Version.ToString(),
                    garbageCollectorWebJob = Assembly.GetAssembly(typeof(Program)).GetName().Version.ToString(),
                    telemetry = Assembly.GetAssembly(typeof(EventContext)).GetName().Version.ToString()
                }
            });
        }
    }
}