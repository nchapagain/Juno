namespace Juno.Execution.AgentRuntime.CommandLine
{
    using System.CommandLine;
    using System.CommandLine.Parsing;

    /// <summary>
    /// Provides a factory for the creation of Command Options used by application command line operations.
    /// </summary>
    public static class OptionFactory
    {
        /// <summary>
        /// An option to set an access token.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateAccessTokenOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                aliases: new string[] { "--accessToken", "--token" },
                description: "Token to authenticate with keyvault.")
            {
                Name = "AccessToken"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set an agent identification.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateAgentIdOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                aliases: new string[] { "--agentId" },
                description: "The user provided agent id in the specified format.")
            {
                Name = "AgentId"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set an application insight instrumentation key.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateAppInsightsInstrumentationKeyOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--appInsightsInstrumentationKey", "--instrumentationKey" },
                    description: "InstrumentationKey for Application Insights.")
            {
                Name = "AppInsightsInstrumentationKey"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set a certificate name.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateCertificateNameOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--certificateName", "--cert" },
                    description: "Name of certificate to access keyvault.")
            {
                Name = "CertificateName"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set a cluster name.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateClusterNameOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--clusterName", "--cluster" },
                    description: "The name of the cluster.")
            {
                Name = "ClusterName"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set a configuration path.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateConfigurationPathOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--configurationPath", "--config" },
                    description: "The path to the environment configuration file on the local machine.")
            {
                Name = "ConfigurationPath"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set a context identification.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateContextIdOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--contextId" },
                    description: "The context ID provided in GUID format.")
            {
                Name = "ContextId"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set an environment.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateEnvironmentOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--environment", "--env" },
                    description: "The target environment for which the host/agent is running.")
            {
                Name = "Environment"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set an EventHub connection string.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateEventHubConnectionStringOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--eventHubConnectionString" },
                    description: "The EventHub connection string used to set up logging.")
            {
                Name = "EventHubConnectionString"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set an EventHub name.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateEventHubOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--eventHub" },
                    description: "The EventHub name used to set up logging.")
            {
                Name = "EventHub"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set an experiment ID.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateExperimentIdOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--experimentId" },
                    description: "The ID of the experiment for which the host is associated.")
            {
                Name = "ExperimentId"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set an install path.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateInstallPathOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--installPath" },
                    description: "Path to install guest agent.")
            {
                Name = "InstallPath"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set a key vault certificate.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateKeyVaultUriOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--keyVaultUri", "--keyVault" },
                    description: "Url to the keyvault certificate required for bootstrapping.")
            {
                Name = "KeyVaultUri"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set a node name.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateNodeNameOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--nodeName", "--node" },
                    description: "The ID/name of the physical node on which the VM runs.")
            {
                Name = "NodeName"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set a nuget feed url.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateNuGetFeedOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--nugetFeedUri", "--feed" },
                    description: "NuGet feed url.")
            {
                Name = "NuGetFeedUri"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set a nuget personal access token secret url.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateNuGetPatOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--nugetPat", "--pat" },
                    description: "NuGet Personal access token secret url in keyvault.")
            {
                Name = "NuGetPat"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set a package version.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreatePackageVersionOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--packageVersion" },
                    description: "An explicit NuGet package version of the Guest Agent to install.")
            {
                Name = "PackageVersion"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set a region.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateRegionOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--region" },
                    description: "The Azure region in which the VM is running.")
            {
                Name = "Region"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to display the current projects' version.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateVersionOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<bool> option = new Option<bool>(
                    aliases: new string[] { "--version" },
                    description: "This projects' current build version.")
            {
                Name = "Version"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        /// <summary>
        /// An option to set a vm sku.
        /// </summary>
        /// <param name="required">Sets this option as required.</param>
        /// <param name="defaultValue">Sets the default value when none is provided.</param>
        /// <param name="validator">Sets custom validation.</param>
        /// <returns>An Option.</returns>
        public static Option CreateVmSkuOption(bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            Option<string> option = new Option<string>(
                    aliases: new string[] { "--vmSku", "--sku" },
                    description: "The user provided VM SKU for the VM (e.g Standard_D2s_v3).")
            {
                Name = "VmSku"
            };

            OptionFactory.SetOptionRequirements(option, required, defaultValue, validator);

            return option;
        }

        private static Option SetOptionRequirements(Option option, bool required = false, object defaultValue = null, ValidateSymbol<OptionResult> validator = null)
        {
            option.IsRequired = required;

            if (defaultValue != null)
            {
                option.Argument.SetDefaultValue(defaultValue);
            }

            if (validator != null)
            {
                option.AddValidator(validator);
            }

            return option;
        }
    }
}
