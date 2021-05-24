﻿namespace Juno.Experiments.Api.WebAppHost
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
    using Juno.Execution.Providers.Environment;
    using Juno.Hosting.Common;
    using Juno.Providers;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Juno Experiments API WebApp host executable.
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
        /// The host name to use in telemetry.
        /// </summary>
        internal static string HostName { get; } = $"{ExperimentsController.ApiName}Host";

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
                        using (IWebHost experimentsApiHost = Program.CreateHost(configuration))
                        {
                            Task[] hostedTasks = new Task[]
                            {
                                experimentsApiHost.RunAsync()
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

                    Program.Logger?.LogTelemetry($"{Program.HostName}.StartupError", LogLevel.Error, errorContext);

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
            AadPrincipalSettings experimentApiPrincipal = settings.ExecutionSettings.AadPrincipals.Get(Setting.ExperimentsApi);
            IAzureKeyVault keyVaultClient = HostDependencies.CreateKeyVaultClient(experimentApiPrincipal, settings.KeyVaultSettings.Get(Setting.Default));

            AppInsightsSettings appInsightsTelemetrySettings = settings.AppInsightsSettings.Get(Setting.Telemetry);
            AppInsightsSettings appInsightsTracingSettings = settings.AppInsightsSettings.Get(Setting.Tracing);
            EventHubSettings eventHubTelemetrySettings = settings.EventHubSettings.Get(Setting.ApiTelemetry);

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

        private static void InitializePersistentTelemetryInfo()
        {
            EventContext.PersistentProperties.AddRange(new Dictionary<string, object>
            {
                ["host"] = Environment.MachineName,
                ["environment"] = HostingContext.GetEnvironment(),
                ["scope"] = "api/experiments",
                ["slot"] = HostFactory.GetDeploymentSlot() ?? "production",
                ["version"] = new
                {
                    api = Assembly.GetAssembly(typeof(ExperimentsController)).GetName().Version.ToString(),
                    apiWebApp = Assembly.GetAssembly(typeof(Program)).GetName().Version.ToString(),
                    contracts = Assembly.GetAssembly(typeof(Experiment)).GetName().Version.ToString(),
                    hostCommon = Assembly.GetAssembly(typeof(HostDependencies)).GetName().Version.ToString(),
                    executionProviders = Assembly.GetAssembly(typeof(InstallGuestAgentProvider)).GetName().Version.ToString(),
                    providers = Assembly.GetAssembly(typeof(ExperimentProvider)).GetName().Version.ToString(),
                    telemetry = Assembly.GetAssembly(typeof(EventContext)).GetName().Version.ToString()
                }
            });
        }
    }
}
