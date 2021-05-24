namespace Juno.Hosting.Common
{
    using System;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension methods for setting up the host application description and command-line
    /// requirements.
    /// </summary>
    public static class HostApplicationExtensions
    {
        private const string ConfigurationPathOptionTemplate = "-c | --configurationPath <path>";
        private const string ConfigurationPathOptionDescription = "The path to the environment configuration file on the local machine.";

        private const string EnvironmentOptionTemplate = "-e | --environment <environment>";
        private const string EnvironmentOptionDescription = "The target environment for which the host/agent is running.";

        private const string ContextIdOptionTemplate = "-ci | --contextId <contextId>";
        private const string ContextIdOptionDescription = "The context ID provided in GUID format.";

        private const string ClusterNameOptionTemplate = "-cl | --clusterName <clusterName>";
        private const string ClusterNameOptionDescription = "The name of the cluster.";

        private const string DisableMonitorsOptionTemplate = "--disableMonitors";
        private const string DisableMonitorsOptionDescription = "Flag used to disable background monitors.";

        private const string NodeNameOptionTemplate = "-n | --nodeName <nodeName>";
        private const string NodeNameOptionDescription = "The ID/name of the physical node on which the VM runs.";

        private const string AgentIdOptionTemplate = "-a | --agentId <agent_id>";
        private const string AgentIdOptionDescription = "The user provided agent id in the specified format.";

        private const string RegionOptionTemplate = "-r | --region <region>";
        private const string RegionOptionDescription = "The Azure region in which the VM is running.";

        private const string VmSkuOptionTemplate = "-sku | --vmSku <vm_sku>";
        private const string VmSkuOptionDescription = "The user provided VM SKU for the VM (e.g Standard_D2s_v3).";

        private const string WorkQueueOptionTemplate = "--workQueue <work_queue>";
        private const string WorkQueueOptionDescription = "The user provided experiment work/notice queue.";

        /// <summary>
        /// Adds the '--configurationPath' options to the command-line application.
        /// </summary>
        public static CommandOption AddOptionConfigurationPath(this CommandLineApplication application)
        {
            application.ThrowIfNull(nameof(application));

            return application.Option(
                HostApplicationExtensions.ConfigurationPathOptionTemplate,
                HostApplicationExtensions.ConfigurationPathOptionDescription,
                CommandOptionType.SingleValue);
        }

        /// <summary>
        /// Adds the '--environment' options to the command-line application.
        /// </summary>
        public static CommandOption AddOptionEnvironment(this CommandLineApplication application)
        {
            application.ThrowIfNull(nameof(application));

            return application.Option(
                HostApplicationExtensions.EnvironmentOptionTemplate,
                HostApplicationExtensions.EnvironmentOptionDescription,
                CommandOptionType.SingleValue);
        }

        /// <summary>
        /// Adds the '--contextId' options to the command-line application.
        /// </summary>
        public static CommandOption AddOptionContextId(this CommandLineApplication application)
        {
            application.ThrowIfNull(nameof(application));

            return application.Option(
                HostApplicationExtensions.ContextIdOptionTemplate,
                HostApplicationExtensions.ContextIdOptionDescription,
                CommandOptionType.SingleValue);
        }

        /// <summary>
        /// Adds the '--clusterName' options to the command-line application.
        /// </summary>
        public static CommandOption AddOptionClusterName(this CommandLineApplication application)
        {
            application.ThrowIfNull(nameof(application));

            return application.Option(
                HostApplicationExtensions.ClusterNameOptionTemplate,
                HostApplicationExtensions.ClusterNameOptionDescription,
                CommandOptionType.SingleValue);
        }

        /// <summary>
        /// Adds the '--disableMonitors' options to the command-line application.
        /// </summary>
        public static CommandOption AddOptionDisableMonitors(this CommandLineApplication application)
        {
            application.ThrowIfNull(nameof(application));

            return application.Option(
                HostApplicationExtensions.DisableMonitorsOptionTemplate,
                HostApplicationExtensions.DisableMonitorsOptionDescription,
                CommandOptionType.NoValue);
        }

        /// <summary>
        /// Adds the '--nodeName' options to the command-line application.
        /// </summary>
        public static CommandOption AddOptionNodeName(this CommandLineApplication application)
        {
            application.ThrowIfNull(nameof(application));

            return application.Option(
                HostApplicationExtensions.NodeNameOptionTemplate,
                HostApplicationExtensions.NodeNameOptionDescription,
                CommandOptionType.SingleValue);
        }

        /// <summary>
        /// Adds the '--agentId' options to the command-line application.
        /// </summary>
        public static CommandOption AddOptionAgentId(this CommandLineApplication application)
        {
            application.ThrowIfNull(nameof(application));

            return application.Option(
                HostApplicationExtensions.AgentIdOptionTemplate,
                HostApplicationExtensions.AgentIdOptionDescription,
                CommandOptionType.SingleValue);
        }

        /// <summary>
        /// Adds the '--region' option to the command-line application.
        /// </summary>
        public static CommandOption AddOptionRegion(this CommandLineApplication application)
        {
            application.ThrowIfNull(nameof(application));

            return application.Option(
                HostApplicationExtensions.RegionOptionTemplate,
                HostApplicationExtensions.RegionOptionDescription,
                CommandOptionType.SingleValue);
        }

        /// <summary>
        /// Adds the '--vmSku' option to the command-line application.
        /// </summary>
        public static CommandOption AddOptionVmSku(this CommandLineApplication application)
        {
            application.ThrowIfNull(nameof(application));

            return application.Option(
                HostApplicationExtensions.VmSkuOptionTemplate,
                HostApplicationExtensions.VmSkuOptionDescription,
                CommandOptionType.SingleValue);
        }

        /// <summary>
        /// Adds the '--workQueue' options to the command-line application.
        /// </summary>
        public static CommandOption AddOptionWorkQueue(this CommandLineApplication application)
        {
            application.ThrowIfNull(nameof(application));

            return application.Option(
                HostApplicationExtensions.WorkQueueOptionTemplate,
                HostApplicationExtensions.WorkQueueOptionDescription,
                CommandOptionType.SingleValue);
        }

        /// <summary>
        /// Creates the command-line application from the arguments provided.
        /// </summary>
        /// <param name="application">The command-line application being setup.</param>
        /// <param name="description">Provides information describing the host executable.</param>
        /// <returns>
        /// The Imaging Service Agent console command-line application.
        /// </returns>
        public static CommandLineApplication DescribeHost(this CommandLineApplication application, HostDescription description)
        {
            application.ThrowIfNull(nameof(application));
            description.ThrowIfNull(nameof(description));

            description.ThrowIfInvalid(
                nameof(description),
                opt => !string.IsNullOrWhiteSpace(opt.Name),
                $"The host name must be defined.");

            description.ThrowIfInvalid(
                nameof(description),
                opt => opt.Version != null,
                $"The host version must be defined.");

            // McMaster.Extensions.CommandLineUtils is a direct fork of the Microsoft.Extensions.CommandLineUtils
            // library forked by Nate McMaster on the .NET Core team who wrote the original.  The .NET Core team did
            // not intend to release the original as a supported package.  Nate moved the project to an open-source
            // forum where it is supported by the community.
            //
            // Repo/NuGet Links:
            // https://natemcmaster.github.io/CommandLineUtils/
            // https://github.com/natemcmaster/CommandLineUtils/blob/9c52eeab20c6db7a1eb58555f32f33553f2a7803/docs/docs/intro.md
            // https://www.nuget.org/packages/McMaster.Extensions.CommandLineUtils/
            //
            // Usage Reference Links:
            // https://www.areilly.com/2017/04/21/command-line-argument-parsing-in-net-core-with-microsoft-extensions-commandlineutils/
            // https://github.com/anthonyreilly/ConsoleArgs/blob/master/Program.cs
            //
            // Response File Handling;
            // https://github.com/natemcmaster/CommandLineUtils/blob/9c52eeab20c6db7a1eb58555f32f33553f2a7803/docs/docs/concepts/response-file-parsing.md

            application.Name = description.Name;
            application.FullName = description.FullName;
            application.Description = description.Description;
            application.ResponseFileHandling = ResponseFileHandling.ParseArgsAsLineSeparated;

            application.VersionOption("-ver|--version", () =>
            {
                Version appVersion = description.Version;
                return $"(v{appVersion.Major}.{appVersion.Minor}.{appVersion.Build})";
            });

            return application;
        }
    }
}