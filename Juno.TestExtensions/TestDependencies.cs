namespace Juno
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.Storage.Queue;
    using Microsoft.Extensions.Configuration;
    using Polly;
    using BlobStorage = Microsoft.Azure.Storage;

    using CosmosTable = Microsoft.Azure.Cosmos.Table;

    /// <summary>
    /// Test dependency initializer
    /// </summary>
    public static class TestDependencies
    {
        private const string DefaultEnvironment = "juno-dev01";
        private static readonly object LockObj = new object();
        private static bool isInitialized;

        /// <summary>
        /// Gets the environment for the tests.  This name corresponds with the name
        /// of configuration files (e.g. *.testsettings) that exist in the 'Configuration'
        /// directory within the project.
        /// </summary>
        public static string Environment { get; set; }

        /// <summary>
        /// Gets the Cosmos DB table client to use in the tests.
        /// </summary>
        public static CloudTableClient CosmosDbTableClient { get; private set; }

        /// <summary>
        /// Gets the Cosmos DB client to use in the tests.
        /// </summary>
        public static CosmosClient CosmosDbClient { get; private set; }

        /// <summary>
        /// Gets the Key Vault client to use in the tests.
        /// </summary>
        public static IKeyVaultClient KeyVaultClient { get; private set; }

        /// <summary>
        /// Gets the Key Vault client base URI.
        /// </summary>
        public static Uri KeyVaultUri { get; private set; }

        /// <summary>
        /// Gets the storage account blob client to use in the tests.
        /// </summary>
        public static CloudQueueClient QueueClient { get; private set; }

        /// <summary>
        /// Gets a no-retries policy.
        /// </summary>
        public static IAsyncPolicy NoRetries { get; private set; }

        /// <summary>
        /// Initializes all test dependencies, environment set to juno-dev01 if not provided
        /// </summary>
        public static void Initialize()
        {
            if (!TestDependencies.isInitialized)
            {
                lock (TestDependencies.LockObj)
                {
                    if (!TestDependencies.isInitialized)
                    {
                        // ****************************************************************
                        // Notes for the Developer
                        // ****************************************************************
                        // Take a moment to read the README.md file for the project so that you
                        // understand the requirements of running tests in this project.

                        Assembly thisAssembly = Assembly.GetAssembly(typeof(TestDependencies));
                        IConfiguration testConfiguration = new ConfigurationBuilder()
                            .AddLocalTestSettings(thisAssembly, string.IsNullOrWhiteSpace(TestDependencies.Environment) ? TestDependencies.DefaultEnvironment : TestDependencies.Environment)
                            .Build();

                        IConfigurationSection keyVaultSettings = testConfiguration.GetSection("KeyVaultSettings");
                        IConfigurationSection storageSettings = testConfiguration.GetSection("StorageSettings");

                        // Intialize the Key Vault client
                        TestDependencies.KeyVaultClient = KeyVaultClientFactory.CreateClient(
                            keyVaultSettings["PrincipalApplicationID"],
                            keyVaultSettings["PrincipalCertificateThumbprint"],
                            StoreName.My);

                        // ALL of the account keys for the Cosmos DB, Storage Account etc... come from the Azure Key Vault.  This is
                        // required security protocol for source code that uses secrets.  Secrets cannot exist in plain-text on the
                        // file system as part of code compliance requirements.
                        TestDependencies.KeyVaultUri = new Uri(keyVaultSettings["KeyVaultUri"]);
                        AzureKeyVault keyVault = new AzureKeyVault(TestDependencies.KeyVaultClient, TestDependencies.KeyVaultUri);

                        // Initialize the blob client.  The 'StorageAccountAccountKey' secret noted MUST exist in the Key Vault.
                        using (SecureString storageAccountKey = keyVault.GetSecretAsync("StorageAccountAccountKey", CancellationToken.None)
                            .GetAwaiter().GetResult())
                        {
                            Uri blobEndpointUri = new Uri(storageSettings["StorageAccountBlobUri"]);
                            Uri queueEndpointUri = new Uri(storageSettings["StorageAccountQueueUri"]);

                            TestDependencies.QueueClient = new CloudQueueClient(
                                queueEndpointUri,
                                new BlobStorage.Auth.StorageCredentials(
                                    queueEndpointUri.Host.Split('.').First(),
                                    storageAccountKey.ToOriginalString()));
                        }

                        // Initialize the Cosmos DB client.  The 'CosmosDBAccountKey' secret noted MUST exist in the Key Vault.
                        using (SecureString accountKey = keyVault.GetSecretAsync("CosmosDBAccountKey", CancellationToken.None)
                           .GetAwaiter().GetResult())
                        {
                            TestDependencies.CosmosDbClient = new CosmosClient(
                                storageSettings["CosmosDocumentsUri"],
                                accountKey.ToOriginalString());
                        }

                        // Initialize the Cosmos DB table client.  The 'CosmosDBTableAccountKey' secret noted MUST exist in the Key Vault.
                        using (SecureString accountKey = keyVault.GetSecretAsync("CosmosDBTableAccountKey", CancellationToken.None)
                           .GetAwaiter().GetResult())
                        {
                            Uri endpointUri = new Uri(storageSettings["CosmosTableUri"]);

                            TestDependencies.CosmosDbTableClient = new CloudTableClient(
                                endpointUri,
                                new CosmosTable.StorageCredentials(
                                    endpointUri.Host.Split('.').First(),
                                    accountKey.ToOriginalString()));
                        }

                        TestDependencies.NoRetries = Policy.NoOpAsync();
                        TestDependencies.isInitialized = true;
                    }
                }
            }
        }
    }
}