namespace Juno.CRCTipBladeCertification
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using Juno.Contracts.Configuration;
    using Juno.CRCTipBladeCertification.Providers;
    using Juno.Execution;
    using Juno.Hosting.Common;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.OData;
    using Org.XmlUnit.Builder;
    using Org.XmlUnit.Diff;

    /// <summary>
    /// Entry point for config logger exe.
    /// </summary>
    public static class Program
    {
        private static ILogger logger;

        private static IConfiguration configuration;

        private static EnvironmentSettings environmentSettings;

        internal static Assembly HostAssembly { get; } = Assembly.GetAssembly(typeof(Program));

        internal static string HostExePath { get; } = Path.GetDirectoryName(Program.HostAssembly.Location);

        /// <summary>
        /// The entry point for the blade config collector executable.
        /// </summary>
        public static int Main(string[] args)
        {
            CommandLineApplication application = new CommandLineApplication()
                    .DescribeHost(new HostDescription
                    {
                        Name = Program.HostAssembly.GetName().Name,
                        FullName = "CRC Tip Blade Certification",
                        Description = "Certifies a Tip blade before/after Juno experiments",
                        Version = Program.HostAssembly.GetName().Version
                    });

            CommandOption environmentOption = application.AddOptionEnvironment();
            var collectOption = application.Option<bool>("-c|--collect <collect>", "Collect information", CommandOptionType.NoValue);

            application.OnExecute(new Func<int>(() =>
            {
                int exitCode = 0;
                if (application.HelpOption().HasValue())
                {
                    application.ShowHelp();
                }
                else if (!environmentOption.HasValue())
                {
                    exitCode = 1;
                    application.ShowHelp();
                }
                else
                {
                    try
                    {
                        Program.configuration = HostingContext.GetEnvironmentConfiguration(Program.HostExePath, environmentOption.Value());
                        Program.environmentSettings = EnvironmentSettings.Initialize(Program.configuration);
                        Program.logger = HostDependencies.CreateLogger(Program.environmentSettings, "CRCTipBladeCertification");
                        EventContext context = new EventContext(Guid.NewGuid());

                        var certificationManager = new CertificationManager();

                        if (collectOption.HasValue())
                        {
                            certificationManager.CollectBaseline();
                            var message = $"Successfully collected baseline";
                            Program.logger.LogInformation(message);
                        }
                        else
                        {
                            var result = certificationManager.CompareWithBaseline(out var errors);
                            var message = $"Finished certification with result PassedCertification: {result}. Errors: {errors}";
                            if (result)
                            {
                                Program.logger.LogInformation(message);
                            }
                            else
                            {
                                Program.logger.LogError(message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // this is useful to debug in XTS.
                        Console.WriteLine("Unable to start CRCTipBladeCertification " + ex.StackTrace);
                        exitCode = 1;
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
}