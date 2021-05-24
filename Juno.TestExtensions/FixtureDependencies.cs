namespace Juno
{
    using System.IO;
    using System.IO.Abstractions;
    using System.Reflection;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts.Configuration;
    using Juno.DataManagement;
    using Juno.Execution.NuGetIntegration;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;

    /// <summary>
    /// Provides common mock dependencies used to test Juno components.
    /// </summary>
    public class FixtureDependencies : Fixture
    {
        private static Assembly testAssembly = Assembly.GetAssembly(typeof(FixtureDependencies));

        /// <summary>
        /// Creates an instance of the <see cref="FixtureDependencies"/> class.
        /// </summary>
        public FixtureDependencies(MockBehavior mockBehavior = MockBehavior.Default)
            : base()
        {
            this.Initialize(mockBehavior);
        }

        /// <summary>
        /// Mock valid Juno environment configuration definition.
        /// </summary>
        public IConfiguration Configuration { get; set; }

        /// <summary>
        /// Mock <see cref="ICertificateManager"/> instance.
        /// </summary>
        public Mock<ICertificateManager> CertificateManager { get; set; }

        /// <summary>
        /// A mock <see cref="IExperimentDataManager"/> instance.
        /// </summary>
        public Mock<IExperimentDataManager> DataManager { get; set; }

        /// <summary>
        /// A mock <see cref="IFileSystem"/> instance.
        /// </summary>
        public Mock<IFileSystem> FileSystem { get; set; }

        /// <summary>
        /// A mock <see cref="IAgentHeartbeatManager"/> instance.
        /// </summary>
        public Mock<IAgentHeartbeatManager> HeartbeatManager { get; set; }

        /// <summary>
        /// A mock <see cref="IAzureKeyVault"/> instance.
        /// </summary>
        public Mock<IAzureKeyVault> KeyVault { get; set; }

        /// <summary>
        ///  A mock <see cref="IKeyVaultClient"/> instance.
        /// </summary>
        public Mock<IKeyVaultClient> KeyVaultClient { get; set; }

        /// <summary>
        /// A mock <see cref="ILogger"/> instance.
        /// </summary>
        public Mock<ILogger> Logger { get; set; }

        /// <summary>
        /// A mock <see cref="IExperimentNotificationManager"/> instance.
        /// </summary>
        public Mock<IExperimentNotificationManager> NotificationManager { get; set; }

        /// <summary>
        /// A mock <see cref="INuGetPackageInstaller"/> instance.
        /// </summary>
        public Mock<INuGetPackageInstaller> NuGetInstaller { get; set; }

        /// <summary>
        /// A mock <see cref="IRestClient"/> instance.
        /// </summary>
        public Mock<IRestClient> RestClient { get; set; }

        /// <summary>
        /// A mock <see cref="IServiceCollection"/> instance.
        /// </summary>
        public IServiceCollection Services { get; set; }

        /// <summary>
        /// Mock environment settings (derived from the <see cref="IConfiguration"/> property).
        /// </summary>
        public EnvironmentSettings Settings { get; set; }

        /// <summary>
        /// A mock <see cref="IExperimentClient"/> instance.
        /// </summary>
        public Mock<IExperimentClient> ExperimentClient { get; set; }

        private void Initialize(MockBehavior mockBehavior)
        {
            this.CertificateManager = new Mock<ICertificateManager>(mockBehavior);
            this.DataManager = new Mock<IExperimentDataManager>(mockBehavior);
            this.FileSystem = new Mock<IFileSystem>(mockBehavior);
            this.NotificationManager = new Mock<IExperimentNotificationManager>(mockBehavior);
            this.NuGetInstaller = new Mock<INuGetPackageInstaller>(mockBehavior);
            this.HeartbeatManager = new Mock<IAgentHeartbeatManager>(mockBehavior);
            this.KeyVault = new Mock<IAzureKeyVault>(mockBehavior);
            this.KeyVaultClient = new Mock<IKeyVaultClient>(mockBehavior);
            this.Logger = new Mock<ILogger>(mockBehavior);
            this.RestClient = new Mock<IRestClient>(mockBehavior);
            this.ExperimentClient = new Mock<IExperimentClient>(mockBehavior);

            string configurationFilePath = Path.Combine(
                Path.GetDirectoryName(FixtureDependencies.testAssembly.Location),
                @"Configuration\juno-dev01.environmentsettings.json");

            if (!File.Exists(configurationFilePath))
            {
                throw new FileNotFoundException(
                    $"Expected configuration file not found. The {nameof(FixtureDependencies)} class depends upon this file to setup testing/mock dependencies.");
            }

            this.Configuration = new ConfigurationBuilder()
                .AddJsonFile(configurationFilePath)
                .Build();

            this.Services = new ServiceCollection();
            this.Settings = EnvironmentSettings.Initialize(this.Configuration);
        }
    }
}
