namespace Juno.Execution.Providers.Watchdog
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.ArmIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;

    /// <summary>
    /// This provider updates the configuration used for Geneva monitoring agent. MA itself is installed as an extension
    /// using security policies. See https://genevamondocs.azurewebsites.net/collect/environments/vmextension.html
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Watchdog, SupportedStepTarget.ExecuteOnVirtualMachine)]
    [SupportedParameter(Name = Parameters.GenevaEndpoint, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.CertificateKey, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.GenevaRoleName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.GenevaAccountName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.GenevaTenantName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.GenevaConfigVersion, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.GenevaNamespace, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.GenevaRegion, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.CertificateThumbprint, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = StepParameters.Platform, Type = typeof(string), Required = false)]
    [ProviderInfo(Name = "Configure Geneva Monitoring Agent", Description = "Configure Geneva monitoring agents on the VMs in the specified experiment group", FullDescription = "Step to configure Geneva Monitoring Agent and it's extensions on virtual machines running as part of Juno experiment.")]
    public class GenevaConfigProvider : ExperimentProvider
    {
        /// <summary>
        /// Default timeout for the geneva config to be updated.
        /// </summary>
        private TimeSpan defaultTimeout = TimeSpan.FromMinutes(60);

        /// <summary>
        /// Initializes a new instance of the <see cref="GenevaConfigProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public GenevaConfigProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            if (!cancellationToken.IsCancellationRequested)
            {
                string platform = component.Parameters.GetValue<string>(StepParameters.Platform, VmPlatform.WinX64);
                if (VmPlatform.IsLinux(platform))
                {
                    // Temporary workaround until we have Linux platform support for the GenevaConfigProvider
                    return new ExecutionResult(ExecutionStatus.Succeeded);
                }

                var state = await this.GetStateAsync<GenevaConfigProviderState>(context, cancellationToken, sharedState: false).ConfigureDefaults()
                    ?? new GenevaConfigProviderState()
                    {
                        CertificateInstalled = false,
                        StepInitializationTime = DateTime.UtcNow
                    };

                // if the step has timedout, just return failure
                if (this.HasStepTimedOut(component, state))
                {
                    throw new ProviderException("The step has timed out before the geneva config could be successfully updated", ErrorReason.Timeout);
                }

                if (!state.CertificateInstalled)
                {
                    // install the certificate from KV if it hasn't been installed yet
                    IAzureKeyVault keyVault = this.Services.GetService<IAzureKeyVault>();
                    await this.InstallMonitoringAgentCertificateAsync(component, keyVault, telemetryContext, cancellationToken)
                        .ConfigureDefaults();

                    state.CertificateInstalled = true;
                    await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
                }

                // if we have the certificate now installed, try applying the config
                if (state.CertificateInstalled)
                {
                    if (await this.ApplyConfigAsync(component, telemetryContext, cancellationToken).ConfigureDefaults())
                    {
                        result = new ExecutionResult(ExecutionStatus.Succeeded);
                    }
                    else
                    {
                        state.LastConfigUpdateTime = DateTime.UtcNow;
                        await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
                    }
                }
            }

            return result;
        }

        internal static string FindResourcePath()
        {
            if (!Directory.Exists(GenevaAgentConstants.GenevaResourceSearchStart))
            {
                return null;
            }

            var directoryForJsonDrop = Directory.GetDirectories(GenevaAgentConstants.GenevaResourceSearchStart, GenevaAgentConstants.GenevaResourceDirectoryName, SearchOption.AllDirectories);
            return directoryForJsonDrop.FirstOrDefault();
        }
        
        internal static GenevaConfigContract BuildGenevaConfiguration(ExperimentComponent component)
        {
            var genevaAccount = component.Parameters.GetValue<string>(Parameters.GenevaAccountName);
            var tenantName = component.Parameters.GetValue<string>(Parameters.GenevaTenantName);
            var certThumbprint = component.Parameters.GetValue<string>(Parameters.CertificateThumbprint);
            var genevaNamespace = component.Parameters.GetValue<string>(Parameters.GenevaNamespace);
            var genevaRegion = component.Parameters.GetValue<string>(Parameters.GenevaRegion);
            var genevaConfigVersion = component.Parameters.GetValue<string>(Parameters.GenevaConfigVersion);
            var genevaRole = component.Parameters.GetValue<string>(Parameters.GenevaRoleName);
            var genevaEndpoint = component.Parameters.GetValue<string>(Parameters.GenevaEndpoint);
            var configContract = new GenevaConfigContract()
            {
                ConstantVariables = new ConstantVariables
                {
                    MonitoringRole = genevaRole,
                    MonitoringTenant = tenantName
                },
                ExpandVariables = new ExpandVariables()
                {
                    MonitoringRoleInstance = GenevaAgentConstants.ComputerNameEnvironmentVariable
                },
                ServiceArguments = new ServiceArguments()
                {
                    Version = GenevaAgentConstants.GenevaServiceContractVersion
                },
                UserArguments = new UserArguments()
                {
                    GenevaConfigVersion = genevaConfigVersion,
                    GcsEnvironment = genevaEndpoint,
                    GcsCertStore = GenevaAgentConstants.GenevaCertStore,
                    GcsExactVersion = GenevaAgentConstants.GenevaExactVersionValue,
                    GcsGenevaAccount = genevaAccount,
                    GcsNamespace = genevaNamespace,
                    GcsRegion = genevaRegion,
                    GcsThumbprint = certThumbprint,
                    LocalPath = GenevaAgentConstants.GenevaDataLocalPath,
                    StartEvent = $"{genevaAccount}_{genevaNamespace}_{genevaConfigVersion}_Start",
                    StopEvent = $"{genevaAccount}_{genevaNamespace}_{genevaConfigVersion}_Stop"
                }
            };
            return configContract;
        }

        internal bool HasStepTimedOut(ExperimentComponent component, GenevaConfigProviderState state)
        {
            TimeSpan timeoutValue = this.defaultTimeout;
            if (component.Parameters?.ContainsKey(StepParameters.Timeout) == true)
            {
                timeoutValue = TimeSpan.Parse(component.Parameters.GetValue<string>(StepParameters.Timeout));
            }

            if (state.StepInitializationTime.Add(timeoutValue) <= DateTime.UtcNow)
            {
                return true;
            }

            return false;
        }

        internal async Task InstallMonitoringAgentCertificateAsync(ExperimentComponent component, IAzureKeyVault keyVault, EventContext telemetryContext, CancellationToken token)
        {
            EventContext relatedContext = telemetryContext.Clone();
            await this.Logger.LogTelemetryAsync($"{nameof(GenevaConfigProvider)}.InstallCertificate", relatedContext, async () =>
            {
                var kvKeyName = component.Parameters.GetValue<string>(Parameters.CertificateKey);
                relatedContext.AddContext("certificate", kvKeyName);

                var certificate = await keyVault.GetCertificateAsync(kvKeyName, token, true).ConfigureDefaults();

                if (certificate == null)
                {
                    throw new ProviderException($"Unable to find certificate with key {kvKeyName}");
                }

                try
                {
                    ICertificateManager certificateManager;
                    if (!this.Services.TryGetService<ICertificateManager>(out certificateManager))
                    {
                        certificateManager = new CertificateManager();
                    }

                    await certificateManager.InstallCertificateToStoreAsync(certificate, StoreName.My, StoreLocation.LocalMachine)
                        .ConfigureDefaults();
                }
                catch (Exception e)
                {
                    throw new ProviderException("Unable to install keyvault certificate", e);
                }
            }).ConfigureDefaults();
        }

        internal virtual Task<bool> ApplyConfigAsync(ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone();
            return this.Logger.LogTelemetryAsync($"{nameof(GenevaConfigProvider)}.ApplyConfiguration", relatedContext, () =>
            {
                bool configApplied = false;
                if (!cancellationToken.IsCancellationRequested)
                {
                    var configuration = GenevaConfigProvider.BuildGenevaConfiguration(component);
                    var resourcePath = GenevaConfigProvider.FindResourcePath();

                    relatedContext.AddContext(nameof(configuration), configuration)
                        .AddContext(nameof(resourcePath), resourcePath)
                        .AddContext("searchPath", GenevaAgentConstants.GenevaResourceSearchStart);

                    // if resource path is null, it's possible MA extension hasn't started on the machine yet
                    if (resourcePath != null)
                    {
                        var configFileName = $"{configuration.UserArguments.StartEvent}.json";
                        var configFilePath = Path.Combine(resourcePath, configFileName);

                        relatedContext.AddContext("configurationFilePath", configFilePath);
                        try
                        {
                            File.WriteAllText(configFilePath, JsonConvert.SerializeObject(configuration));
                            configApplied = true;
                        }
                        catch (Exception e)
                        {
                            throw new ProviderException("Unable to write the JSON configuration for Geneva agent", e);
                        }
                    }
                }

                return Task.FromResult(configApplied);
            });
        }

        internal class GenevaConfigProviderState
        {
            public DateTime LastConfigUpdateTime { get; set; }

            public bool CertificateInstalled { get; set; }

            public DateTime StepInitializationTime { get; set; }
        }

        /// <summary>
        /// Parameters class defines the keys that are expected from the user
        /// </summary>
        internal class Parameters
        {
            internal const string CertificateKey = "certificateKey";
            internal const string CertificateThumbprint = "certificateThumbprint";
            internal const string GenevaTenantName = "genevaTenantName";
            internal const string GenevaAccountName = "genevaAccountName";
            internal const string GenevaNamespace = "genevaNamespace";
            internal const string GenevaRegion = "genevaRegion";
            internal const string GenevaConfigVersion = "genevaConfigVersion";
            internal const string GenevaRoleName = "genevaRoleName";
            internal const string GenevaEndpoint = "genevaEndpoint";
        }

        /// <summary>
        /// Constants contains the fixed values used for geneva configuration
        /// </summary>
        internal class GenevaAgentConstants
        {
            internal const string ComputerNameEnvironmentVariable = "%COMPUTERNAME%";
            internal const string GenevaDataLocalPath = "C:\\MA";
            internal const string GenevaServiceContractVersion = "1.0";
            internal const string GenevaCertStore = "LOCAL_MACHINE\\MY";
            internal const string GenevaExactVersionValue = "true";

            /// <summary>
            /// This location is a contract with Azure Guest Agent
            /// </summary>
            internal const string GenevaResourceSearchStart = "C:\\WindowsAzure\\Resources";

            /// <summary>
            /// This location is a contract with Azure Guest Agent
            /// </summary>
            internal const string GenevaResourceDirectoryName = "__json";
        }
    }
}
