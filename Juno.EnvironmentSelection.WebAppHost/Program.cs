namespace Juno.EnvironmentSelection.Api.WebAppHost
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.EnvironmentSelection.Service;
    using Juno.Hosting.Common;
    using Juno.Providers;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Juno Environment API WebApp host executable.
    /// </summary>
    public class Program
    {
        internal static ILogger DiagnosticsLogger { get; private set; }

        /// <summary>
        /// The logger for the host.
        /// </summary>
        internal static ILogger Logger { get; private set; }

        /// <summary>
        /// The executing assembly directory path.
        /// </summary>
        internal static string ExePath
        {
            get
            {
                return Path.GetDirectoryName(Assembly.GetAssembly(typeof(Program)).Location);
            }
        }

        /// <summary>
        /// The name of the host to use in telemetry.
        /// </summary>
        internal static string HostName { get; } = $"{EnvironmentSelectionController.ApiName}Host";

        /// <summary>
        /// Entry point for the WebApp host executable.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA1801: Review unused parameters.", Justification = "Required in method signature regardless.")]
        [SuppressMessage("AsyncUsage", "AsyncFixer04:A disposable object used in a fire & forget async call", Justification = "Task awaited afterwards")]
        public static int Main(string[] args)
        {
            int exitCode = 0;
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                Guid correlationId = Guid.NewGuid();

                try
                {
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        tokenSource.Cancel();
                    };

                    IConfiguration configuration = HostingContext.GetEnvironmentConfiguration(Program.ExePath);
                    EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);

                    Program.InitializePersistentTelemetryInfo();
                    Program.InitializeLogging(settings);

                    EventContext telemetryContext = new EventContext(correlationId);
                    Program.Logger.LogTelemetry(Program.HostName, telemetryContext, () =>
                    {
                        using (IWebHost executionApiHost = Program.CreateHost(configuration))
                        {
                            Task[] hostedTasks = new Task[]
                            {
                                executionApiHost.RunAsync()
                            };

                            Task.WaitAny(hostedTasks, tokenSource.Token);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    // Expected when the Ctrl-C is pressed to cancel operation.
                }
                catch (Exception exc)
                {
                    EventContext errorContext = new EventContext(correlationId)
                        .AddError(exc);

                    Program.Logger?.LogTelemetry($"{Program.HostName}StartupError", LogLevel.Critical, errorContext);

                    // For the case the telemetry loggers have not been initialized
                    // successfully.
                    Trace.TraceError($"[{Program.HostName}StartupError{Environment.NewLine}{exc.Message}{Environment.NewLine}{exc.StackTrace}");

                    exitCode = 1;
                }
                finally
                {
                    HostDependencies.FlushTelemetry();
                }

                return exitCode;
            }
        }

        private static IWebHost CreateHost(IConfiguration configuration)
        {
            IWebHost webHost = null;
            if (HostingContext.IsLocalHostingRequested())
            {
                webHost = HostFactory.CreateLocalHost<Startup>(configuration);
            }
            else
            {
                webHost = HostFactory.CreateWebAppHost<Startup>(configuration);
            }

            return webHost;
        }

        private static void InitializeLogging(EnvironmentSettings settings)
        {
            try
            {
                AppInsightsSettings appInsightsTelemetrySettings = settings.AppInsightsSettings.Get(Setting.Telemetry);
                Program.DiagnosticsLogger = HostDependencies.CreateLogger(Program.HostName, appInsightsTelemetrySettings);

                AadPrincipalSettings executionApiPrincipal = settings.ExecutionSettings.AadPrincipals.Get(Setting.EnvironmentsApi);
                IAzureKeyVault keyVaultClient = HostDependencies.CreateKeyVaultClient(executionApiPrincipal, settings.KeyVaultSettings.Get(Setting.Default));

                EventHubSettings eventHubTelemetrySettings = settings.EventHubSettings.Get(Setting.ApiTelemetry);
                Program.Logger = HostDependencies.CreateLogger(
                   Program.HostName,
                   eventHubTelemetrySettings: eventHubTelemetrySettings,
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
            catch (Exception ex)
            {
                EventContext errorContext = EventContext.Persisted().AddError(ex);
                Program.DiagnosticsLogger?.LogTelemetry($"{Program.HostName}.LoggingInitializationError", LogLevel.Error, errorContext);
            }
        }

        private static void InitializePersistentTelemetryInfo()
        {
            EventContext.PersistentProperties.AddRange(new Dictionary<string, object>
            {
                ["host"] = Environment.MachineName,
                ["environment"] = HostingContext.GetEnvironment(),
                ["scope"] = "api/environments",
                ["slot"] = HostFactory.GetDeploymentSlot() ?? "production",
                ["version"] = new
                {
                    apiWebApp = Assembly.GetAssembly(typeof(Program)).GetName().Version.ToString(),
                    contracts = Assembly.GetAssembly(typeof(EnvironmentQuery)).GetName().Version.ToString(),
                    hostCommon = Assembly.GetAssembly(typeof(HostDependencies)).GetName().Version.ToString(),
                    providers = Assembly.GetAssembly(typeof(KustoFilterProvider)).GetName().Version.ToString(),
                    service = Assembly.GetAssembly(typeof(EnvironmentSelectionService)).GetName().Version.ToString(),
                    telemetry = Assembly.GetAssembly(typeof(EventContext)).GetName().Version.ToString()
                }
            });
        }
    }
}
