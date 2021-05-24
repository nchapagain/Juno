namespace Juno.GuestAgent.Installer
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.Invocation;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Reflection;
    using System.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.AgentRuntime;
    using Juno.Execution.AgentRuntime.CommandLine;
    using Juno.Execution.NuGetIntegration;
    using Juno.Hosting.Common;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Azure.CRC.Platform;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.KeyVault.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NuGet.Versioning;
    using Polly;
    using static Microsoft.Azure.KeyVault.KeyVaultClient;
    using Common = Microsoft.Azure.CRC;

    /// <summary>
    /// A class that combines command line instructions with a project execution plan for installing a Guest Agent
    /// </summary>
    public class AgentInstallationCommand : CommandExecution
    {
        private const string ServiceDescription = "Juno Guest agent/service used to execute experiment steps and workloads on the VM.";
        private static readonly string ServiceName = (PlatformUtil.CurrentPlatform == PlatformID.Unix) ? "juno.guestagent" : "Juno.GuestAgent";
        private static readonly string PackageExePath = (PlatformUtil.CurrentPlatform == PlatformID.Unix) ? "tools/linux-x64" : @"tools\win-x64";
        private static readonly string PackageExeName = (PlatformUtil.CurrentPlatform == PlatformID.Unix) ? "Juno.GuestAgent" : @"Juno.GuestAgent.exe";

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentInstallationCommand"/> class.
        /// </summary>
        public AgentInstallationCommand()
        {
            this.RetryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(retryCount: 5, (retries) => TimeSpan.FromSeconds(Math.Pow(2, retries)));
        }

        /// <summary>
        /// JSON web token (JWT) used for initial authenticate with the Key Vault.
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// The unique ID of the agent.
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Instrumentation key used to write telemetry to Application Insights.
        /// </summary>
        public string AppInsightsInstrumentationKey { get; set; }

        /// <summary>
        /// Name of certificate to install from the Key Vault.
        /// </summary>
        public string CertificateName { get; set; }

        /// <summary>
        /// The EventHub name used for setting up telemetry.
        /// </summary>
        public string EventHub { get; set; }

        /// <summary>
        /// The EventHub connection string used for setting up telemetry.
        /// </summary>
        public string EventHubConnectionString { get; set; }

        /// <summary>
        /// The ID of the experiment for which the Guest Agent is associated.
        /// </summary>
        public string ExperimentId { get; set; }

        /// <summary>
        /// Path to install guest agent.
        /// </summary>
        public string InstallPath { get; set; }

        /// <summary>
        /// Url to the Key Vault containing secrets and certificates required for the
        /// installation of the Guest Agent.
        /// </summary>
        public Uri KeyVaultUri { get; set; }

        /// <summary>
        /// The URI to the NuGet feed from which the Guest Agent package can be downloaded.
        /// </summary>
        public Uri NuGetFeedUri { get; set; }

        /// <summary>
        /// The personal access token (PAT) to use for authentication to the NuGet feed. This may
        /// be (and often will be) a reference to the name of a secret in the Key Vault that contains
        /// the actual personal access token (e.g. [secret:keyvault]=NuGetAccessToken).
        /// </summary>
        public string NuGetPat { get; set; }

        /// <summary>
        /// The explicit NuGet package version of the Guest Agent to install.
        /// </summary>
        public string PackageVersion { get; set; }

        /// <summary>
        /// The Azure region in which the VM/Guest Agent is running (e.g. East US, West US 2).
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// The precise VM SKU for the VM (e.g Standard_D2s_v3).
        /// </summary>
        public string VmSku { get; set; }

        /// <summary>
        /// Retry Policy
        /// </summary>
        public IAsyncPolicy RetryPolicy { get; set; }

        /// <summary>
        /// Creates the command line option definitions/descriptions for the agent installation
        /// command.
        /// </summary>
        /// <param name="args">The command line arguments passed into the installer.</param>
        /// <param name="cancellationToken">The host cancellation token.</param>
        public static CommandLineBuilder CreateBuilder(string[] args, CancellationToken cancellationToken)
        {
            RootCommand rootCommand = new RootCommand("Install Juno Guest Agent on Virtual Machine.")
            {
                OptionFactory.CreateAccessTokenOption(required: true),
                OptionFactory.CreateAgentIdOption(required: true),
                OptionFactory.CreateAppInsightsInstrumentationKeyOption(),
                OptionFactory.CreateCertificateNameOption(required: true),
                OptionFactory.CreateEnvironmentOption(required: true),
                OptionFactory.CreateEventHubConnectionStringOption(),
                OptionFactory.CreateEventHubOption(),
                OptionFactory.CreateExperimentIdOption(),
                OptionFactory.CreateInstallPathOption(defaultValue: Path.Combine(Directory.GetDirectoryRoot(AppContext.BaseDirectory), "Juno")),
                OptionFactory.CreateKeyVaultUriOption(required: true),
                OptionFactory.CreateNuGetFeedOption(required: true),
                OptionFactory.CreateNuGetPatOption(required: true),
                OptionFactory.CreatePackageVersionOption(defaultValue: "latest"),
                OptionFactory.CreateRegionOption(),
                OptionFactory.CreateVmSkuOption()
            };

            rootCommand.Handler = CommandHandler.Create<AgentInstallationCommand>((handler) => handler.ExecuteAsync(args, cancellationToken));

            return new CommandLineBuilder(rootCommand);
        }

        /// <inheritdoc />
        [SuppressMessage("Readability", "AZCA1006:PrefixStaticCallsWithClassName Rule", Justification = "Code analysis is incorrect here.")]
        public override async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken, IServiceCollection services = null)
        {
            int result = 0;
            try
            {
                EventContext.Persist(Guid.NewGuid());

                this.InitializeLogging();
                this.Logger.LogTelemetry($"{Program.HostName}.CommandLineArguments", LogLevel.Information, EventContext.Persisted()
                    .AddContext(nameof(args), Common.SensitiveData.ObscureSecrets(string.Join(" ", args))));

                INuGetPackageInstaller nugetInstaller = null;
                if (services?.TryGetService(out nugetInstaller) != true)
                {
                    nugetInstaller = new NuGetPackageInstaller();
                }

                ICertificateManager certificateManager = null;
                if (services?.TryGetService(out certificateManager) != true)
                {
                    certificateManager = new CertificateManager();
                }

                ISystemManager systemManager = null;
                if (services?.TryGetService(out systemManager) != true)
                {
                    systemManager = SystemManagerFactory.Get();
                }

                IFileSystem fileSystem = null;
                if (services?.TryGetService(out fileSystem) != true)
                {
                    fileSystem = new FileSystem();
                }

                IKeyVaultClient keyVaultClient = null;
                if (services?.TryGetService(out keyVaultClient) != true)
                {
                    AuthenticationCallback tokenCallback = (string authority, string resource, string scope) =>
                    {
                        return Task.FromResult(this.AccessToken);
                    };

                    keyVaultClient = new KeyVaultClient(tokenCallback);
                }

                // 1) Install Guest Agent certificate from the Key Vault (e.g. juno-dev01-guestagent).
                await this.InstallCertificatesAsync(keyVaultClient, certificateManager, PlatformUtil.CurrentPlatform, cancellationToken)
                    .ConfigureDefaults();

                // 2) Install/download the Guest Agent from the NuGet feed.
                NuGetVersion installedVersion = await this.DownloadAgentNuGetPackageAsync(keyVaultClient, nugetInstaller, cancellationToken)
                    .ConfigureDefaults();

                // 3) Install the Guest Agent as a service/daemon on the VM.
                await this.InstallAgentAsServiceAsync(systemManager, fileSystem, installedVersion.ToString(), cancellationToken)
                    .ConfigureDefaults();

            }
            catch (OperationCanceledException)
            {
                // Expected when the Ctrl-C is pressed to cancel operation.
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.ToString());
                EventContext errorContext = EventContext.Persisted().AddError(exc);

                this.Logger?.LogTelemetry($"{Program.HostName}.InstallationError", LogLevel.Critical, errorContext);
                result = 1;
            }
            finally
            {
                HostDependencies.FlushTelemetry();
            }

            return result;
        }

        /// <summary>
        /// Creates the startup response file for the Guest Agent which supplies initial commandline
        /// parameters.
        /// </summary>
        /// <param name="fileSystem">Provides file system operations.</param>
        /// <param name="workingDirectory">The working directory where the Guest Agent is installed.</param>
        /// <param name="responseFileArguments">The command line arguments to write to the response file.</param>
        /// <returns>
        /// The name of the response file (e.g. Startup.rsp).
        /// </returns>
        protected async Task<string> CreateAgentResponseFileAsync(IFileSystem fileSystem, string workingDirectory, IDictionary<string, IConvertible> responseFileArguments)
        {
            fileSystem.ThrowIfNull(nameof(fileSystem));
            workingDirectory.ThrowIfNullOrWhiteSpace(nameof(workingDirectory));
            responseFileArguments.ThrowIfNullOrEmpty(nameof(responseFileArguments));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(workingDirectory), workingDirectory)
                .AddContext(nameof(responseFileArguments), responseFileArguments);

            return await this.Logger.LogTelemetryAsync($"{Program.HostName}.CreateAgentResponseFile", telemetryContext, async () =>
            {
                string responseFileName = "Startup.rsp";
                StringBuilder responseFileContents = new StringBuilder();
                responseFileArguments.ToList().ForEach(entry => responseFileContents.AppendLine($"{entry.Key}=\"{entry.Value}\""));

                string responseFilePath = Path.Combine(workingDirectory, responseFileName);
                await fileSystem.File.WriteAllTextAsync(responseFilePath, responseFileContents.ToString().Trim())
                    .ConfigureDefaults();

                return responseFilePath;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Downloads the Guest Agent from a NuGet feed to the local machine.
        /// </summary>
        /// <param name="keyVaultClient">The Key Vault client used to get secrets from the system.</param>
        /// <param name="nugetInstaller">The NuGet package installer.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        protected Task<NuGetVersion> DownloadAgentNuGetPackageAsync(IKeyVaultClient keyVaultClient, INuGetPackageInstaller nugetInstaller, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persisted()
                .AddContext("keyVaultUri", this.KeyVaultUri)
                .AddContext("feedUri", this.NuGetFeedUri)
                .AddContext("packageVersion", this.PackageVersion);

            return this.Logger.LogTelemetryAsync($"{Program.HostName}.DownloadAgentNuGetPackage", telemetryContext, async () =>
            {
                SecretBundle nuGetPat = await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await keyVaultClient.GetSecretAsync(this.KeyVaultUri.AbsoluteUri, this.NuGetPat, cancellationToken).ConfigureDefaults();
                }).ConfigureDefaults();

                NuGetPackageInfo packageInfo = new NuGetPackageInfo(
                    this.NuGetFeedUri,
                    AgentInstallationCommand.ServiceName,
                    this.PackageVersion,
                    this.InstallPath,
                    "Juno",
                    nuGetPat.Value);

                NuGetVersion downloadedVersion = await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await nugetInstaller.InstallPackageAsync(packageInfo, cancellationToken).ConfigureDefaults();
                }).ConfigureDefaults();

                telemetryContext.AddContext("downloadedVersion", downloadedVersion.ToString());

                return downloadedVersion;
            });
        }

        /// <summary>
        /// Returns the logon password for the VM admin account (e.g. junovmadmin) as defined
        /// in the local Key Vault in the resource group.
        /// </summary>
        /// <param name="vmName">The name of the VM (e.g. 2fdc26fcd34-2).</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns></returns>
        protected virtual Task<SecureString> GetServiceLogonPasswordAsync(string vmName, CancellationToken cancellationToken)
        {
            vmName.ThrowIfNullOrWhiteSpace(nameof(vmName));

            // Ex: Given a resource group 'rg-2fdc26fcd34' and a VM name '2fdc26fcd34-2'
            // 1) The Key Vault URI = https://kv-2fdc26fcd34.vault.azure.net
            // 2) The password secret = 2fdc26fcd34-2-pw

            string resourcePrefix = vmName.Split('-').FirstOrDefault();
            Uri keyvaultUri = new Uri(string.Concat("https://kv-", resourcePrefix, ".vault.azure.net"));
            string passwordSecretName = string.Concat(vmName, "-pw");

            AuthenticationCallback authenticationCallback = (string authority, string resource, string scope) =>
            {
                // The access token provided to the installer is for the Guest Agent AAD principal. This principal
                // will have access to the Key Vault in the resource group.
                return Task.FromResult(this.AccessToken);
            };

            IKeyVaultClient keyVaultClient = new KeyVaultClient(authenticationCallback);
            IAzureKeyVault keyVault = new AzureKeyVault(keyVaultClient, keyvaultUri);

            return keyVault.GetSecretAsync(passwordSecretName, cancellationToken);
        }

        /// <summary>
        /// Installs the required certificates on the 
        /// </summary>
        /// <param name="keyVaultClient">The Key Vault client used to get secrets from the system.</param>
        /// <param name="certificateManager">The certificate manager used to install certificates on the local machine.</param>
        /// <param name="platform">The system platform (e.g. Windows, Linux).</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        protected Task InstallCertificatesAsync(IKeyVaultClient keyVaultClient, ICertificateManager certificateManager, PlatformID platform, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persisted();

            return this.Logger.LogTelemetryAsync($"{Program.HostName}.InstallCertificates", telemetryContext, async () =>
            {
                var keyVault = new AzureKeyVault(keyVaultClient, new Uri(this.KeyVaultUri.AbsoluteUri), this.RetryPolicy);
                X509Certificate2 cert = await keyVault.GetCertificateAsync(this.CertificateName, cancellationToken, true).ConfigureDefaults();
                telemetryContext.AddContext("certificateSubject", cert.Subject);
                telemetryContext.AddContext("certificateThumbprint", cert.Thumbprint);

                // According to https://docs.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography
                // Linux cannot open StoreLocation.LocalMachine with "ReadWrite".
                // So, instead we have to use StoreLocation.CurrentUser.
                await certificateManager.InstallCertificateToStoreAsync(
                    cert,
                    StoreName.My,
                    platform == PlatformID.Unix ? StoreLocation.CurrentUser : StoreLocation.LocalMachine).ConfigureDefaults();
            });
        }

        /// <summary>
        /// Installs the Guest Agent as a Windows Service (or Linux Daemon) and starts it.
        /// </summary>
        /// <param name="systemManager">Provides methods for installing the agent service/daemon.</param>
        /// <param name="fileSystem">Provides methods for interfacing with the file system.</param>
        /// <param name="installedVersion">The installed package version of the Guest Agent.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        protected async Task InstallAgentAsServiceAsync(ISystemManager systemManager, IFileSystem fileSystem, string installedVersion, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(installedVersion), installedVersion);

            await this.Logger.LogTelemetryAsync($"{Program.HostName}.InstallAgentAsService", telemetryContext, async () =>
            {
                // Example: C:\Juno\Juno.GuestAgent\1.8.5\tools\win-x64
                string workingDirectory = Path.Combine(this.InstallPath, AgentInstallationCommand.ServiceName, installedVersion, AgentInstallationCommand.PackageExePath);
                telemetryContext.AddContext(nameof(workingDirectory), workingDirectory);

                IDictionary<string, IConvertible> responseFileArguments = new Dictionary<string, IConvertible>
                {
                    // The response file contains ALL of the commandline parameters that will be supplied
                    // to the Guest Agent when the application/service starts up. There were issues the formatting
                    // of parameters provided directly on the commandline conflicting with the SC.exe installer used to
                    // install the Guest Agent as a service. The use of the response file solves this conflict.
                    { "--environment", this.Environment },
                    { "--agentId", this.AgentId }
                };

                if (!string.IsNullOrWhiteSpace(this.VmSku))
                {
                    responseFileArguments["--vmSku"] = this.VmSku;
                }

                if (!string.IsNullOrWhiteSpace(this.Region))
                {
                    responseFileArguments["--region"] = this.Region;
                }

                telemetryContext.AddContext("responseFileArguments", responseFileArguments);

                string responseFilePath = await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.CreateAgentResponseFileAsync(fileSystem, workingDirectory, responseFileArguments).ConfigureDefaults();
                }).ConfigureDefaults();

                // Example: C:\Juno\Juno.GuestAgent\1.8.5\tools\win-x64\Juno.GuestAgent.exe
                string executePath = Path.Combine(workingDirectory, AgentInstallationCommand.PackageExeName);
                telemetryContext.AddContext("responseFilePath", responseFilePath);
                telemetryContext.AddContext("serviceExecutePath", executePath);
                telemetryContext.AddContext("serviceWorkingDirectory", workingDirectory);

                Console.WriteLine($"GuestAgent has been installed as a service named '{AgentInstallationCommand.ServiceName}'."); // Some information which doesn't need to be logged to telemetry.
                Console.WriteLine($"Working Directory: {workingDirectory}");

                string vmName = new AgentIdentification(this.AgentId).VirtualMachineName;
                string username = $"{vmName}\\{VmAdminAccounts.Default}";
                telemetryContext.AddContext("serviceRunAsUser", username);

                await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    SecureString pwd;
                    if (System.Environment.OSVersion.Platform == PlatformID.Unix)
                    {
                        // We are using SSH key now so there's no password.
                        pwd = string.Empty.ToSecureString();
                    }
                    else
                    {
                        pwd = await this.GetServiceLogonPasswordAsync(vmName, cancellationToken).ConfigureDefaults();
                    }

                    await systemManager.InstallServiceAsync(
                        AgentInstallationCommand.ServiceName,
                        AgentInstallationCommand.ServiceDescription,
                        executePath,
                        workingDirectory,
                        $"@{Path.GetFileName(responseFilePath)}",
                        username,
                        pwd?.ToOriginalString()).ConfigureDefaults();
                }).ConfigureDefaults();
            }).ConfigureDefaults();
        }

        private void InitializeLogging()
        {
            AgentIdentification agentId = new AgentIdentification(this.AgentId);
            EventContext.PersistentProperties.AddRange(new Dictionary<string, object>
            {
                ["host"] = System.Environment.MachineName,
                ["agentId"] = this.AgentId,
                ["agentRegion"] = this.Region,
                ["agentCluster"] = agentId.ClusterName,
                ["agentNodeId"] = agentId.NodeName,
                ["agentContextId"] = agentId.Context,
                ["agentVmName"] = System.Environment.MachineName,
                ["agentVmSku"] = this.VmSku,
                ["agentVersion"] = this.PackageVersion,
                ["experimentId"] = this.ExperimentId,
                ["environment"] = this.Environment,
                ["scope"] = "installers/guest",
                ["version"] = new
                {
                    guestAgentInstaller = Assembly.GetAssembly(typeof(Program)).GetName().Version.ToString(),
                    contracts = Assembly.GetAssembly(typeof(Experiment)).GetName().Version.ToString(),
                    agentRuntime = Assembly.GetAssembly(typeof(CommandExecution)).GetName().Version.ToString(),
                    hostingCommon = Assembly.GetAssembly(typeof(HostDependencies)).GetName().Version.ToString()
                }
            });

            AppInsightsSettings appInsightsSettings = null;
            if (!string.IsNullOrWhiteSpace(this.AppInsightsInstrumentationKey))
            {
                appInsightsSettings = new AppInsightsSettings
                {
                    Id = "Telemetry",
                    InstrumentationKey = this.AppInsightsInstrumentationKey
                };
            }

            EventHubSettings eventHubSettings = null;
            if (!string.IsNullOrWhiteSpace(this.EventHubConnectionString) && !string.IsNullOrWhiteSpace(this.EventHub))
            {
                eventHubSettings = new EventHubSettings
                {
                    Id = "Telemetry",
                    ConnectionString = this.EventHubConnectionString,
                    EventHub = this.EventHub
                };
            }

            this.Logger = HostDependencies.CreateLogger(
                Program.HostName,
                appInsightsInstrumentationKey: this.AppInsightsInstrumentationKey,
                eventHubConnectionString: this.EventHubConnectionString,
                eventHubName: this.EventHub,
                enableDiagnostics: true);

            // Enable logging at the entry point level.
            Program.Logger = this.Logger;
        }
    }
}