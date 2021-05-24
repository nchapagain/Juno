using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using Juno.Api.Client;
using Juno.Contracts.Configuration;
using Juno.DataManagement;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.CRC.Extensions;
using Microsoft.Azure.CRC.Repository.Cosmos;
using Microsoft.Azure.CRC.Repository.KeyVault;
using Microsoft.Azure.CRC.Repository.Storage;
using Microsoft.Azure.CRC.Rest;
using Microsoft.Azure.CRC.Telemetry;
using Microsoft.Azure.CRC.Telemetry.ApplicationInsights;
using Microsoft.Azure.CRC.Telemetry.Logging;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.Queue;
using Microsoft.Extensions.Logging;

namespace Juno.Scheduler.WebJobHost
{
    /// <summary>
    /// Provides the "context" i.e. clients needed for this service.
    /// </summary>
    public static class SchedulerDependencies
    {
        /// <summary>
        /// Creates an <see cref="IScheduleTimerDataManager"/> data manager to read timer information
        /// </summary>
        /// <param name="settings">Configuration settings for the environment in which the API will run (e.g. juno-dev01).</param>
        /// <param name="keyVaultClient">KeyVault client</param>
        /// <param name="logger">Logger</param>
        /// <returns>Type: <see cref="IScheduleTimerDataManager"/></returns>
        public static IScheduleTimerDataManager CreateScheduleTimerDataManager(EnvironmentSettings settings, IAzureKeyVault keyVaultClient, ILogger logger = null)
        {
            settings.ThrowIfNull(nameof(settings));
            keyVaultClient.ThrowIfNull(nameof(keyVaultClient));

            CloudTableClient cosmosTableClient = null;

            // Secrets MUST be stored in the Key Vault. They are referenced within the configuration using
            // a format like: [secret:keyvault]=CosmosDBAccountKey.
            CosmosSettings cosmosTableSettings = settings.CosmosSettings.Get("ScheduleTables");

            using (SecureString accountKey = keyVaultClient.ResolveSecretAsync(cosmosTableSettings.AccountKey, CancellationToken.None)
                .GetAwaiter().GetResult())
            {
                StorageCredentials credentials = new StorageCredentials(
                    cosmosTableSettings.Uri.Host.Split('.').First(),
                    accountKey.ToOriginalString());

                cosmosTableClient = new CloudTableClient(cosmosTableSettings.Uri, credentials);
            }

            AzureCosmosTableStore tableStore = new AzureCosmosTableStore(cosmosTableClient);
            return new ScheduleTimerDataManager(tableStore, logger: logger);
        }

        /// <summary>
        /// Creates an <see cref="IScheduleDataManager"/> data manager to read timer information
        /// </summary>
        /// <param name="settings">Configuration settings for the environment in which the API will run (e.g. juno-dev01).</param>
        /// <param name="keyVaultClient">KeyVault client</param>
        /// <param name="logger">Logger</param>
        /// <returns>Type: <see cref="IScheduleDataManager"/></returns>
        public static IScheduleDataManager CreateScheduleDataManager(EnvironmentSettings settings, IAzureKeyVault keyVaultClient, ILogger logger = null)
        {
            settings.ThrowIfNull(nameof(settings));
            keyVaultClient.ThrowIfNull(nameof(keyVaultClient));

            CosmosClient cosmosClient = null;

            // Secrets MUST be stored in the Key Vault. They are referenced within the configuration using
            // a format like: [secret:keyvault]=CosmosDBAccountKey.
            CosmosSettings cosmosDbSettings = settings.CosmosSettings.Get("Schedules");

            using (SecureString accountKey = keyVaultClient.ResolveSecretAsync(cosmosDbSettings.AccountKey, CancellationToken.None)
                .GetAwaiter().GetResult())
            {
                cosmosClient = new CosmosClient(cosmosDbSettings.Uri.AbsoluteUri, accountKey.ToOriginalString());
            }

            AzureCosmosDbStore documentStore = new AzureCosmosDbStore(cosmosClient);

            return new ScheduleDataManager(documentStore, logger: logger);
        }

        /// <summary>
        /// Creates an <see cref="IExperimentTemplateDataManager"/> data manager to read timer information
        /// </summary>
        /// <param name="settings">Configuration settings for the environment in which the API will run (e.g. juno-dev01).</param>
        /// <param name="keyVaultClient">KeyVault client</param>
        /// <param name="logger">Logger</param>
        /// <returns>Type: <see cref="IExperimentTemplateDataManager"/></returns>
        public static IExperimentTemplateDataManager CreateExperimentTemplateDataManager(EnvironmentSettings settings, IAzureKeyVault keyVaultClient, ILogger logger = null)
        {
            settings.ThrowIfNull(nameof(settings));
            keyVaultClient.ThrowIfNull(nameof(keyVaultClient));

            CosmosClient cosmosClient = null;

            CosmosSettings cosmosDbSettings = settings.CosmosSettings.Get("Schedules");

            using (SecureString accountKey = keyVaultClient.ResolveSecretAsync(cosmosDbSettings.AccountKey, CancellationToken.None)
                .GetAwaiter().GetResult())
            {
                cosmosClient = new CosmosClient(cosmosDbSettings.Uri.AbsoluteUri, accountKey.ToOriginalString());
            }

            AzureCosmosDbStore documentStore = new AzureCosmosDbStore(cosmosClient);

            return new ExperimentTemplateDataManager(documentStore, cosmosClient, logger: logger);
        }
    }
}
