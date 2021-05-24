namespace Juno.Hosting.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.EventHubs.Producer;
    using Juno.Api.Client;
    using Juno.Contracts.Configuration;
    using Juno.DataManagement;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Repository.Storage;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.ApplicationInsights;
    using Microsoft.Azure.CRC.Telemetry.EventHub;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.Storage.Blob;
    using Microsoft.Azure.Storage.Queue;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provides a factory for the hosts startup, such as mapping interfece to concrete implemenations
    /// for depedency injections
    /// </summary>
    public static class HostDependencies
    {
        /// <summary>
        /// Gets the set of telemetry channels that must be flushed before the application
        /// shuts down.
        /// </summary>
        private static readonly List<IFlushableChannel> TelemetryChannels = new List<IFlushableChannel>();

        /// <summary>
        /// Creates a <see cref="VegaClient"/> that can be used to communicate with the Vega API
        /// </summary>
        /// <param name="certThumbprint">certThumbprint</param>
        /// <returns></returns>
        public static VegaClient CreateVegaApiClient(string certThumbprint)
        {
            certThumbprint.ThrowIfNull(nameof(certThumbprint));
            return new VegaClient(certThumbprint);
        }

        /// <summary>
        /// Creates an <see cref="AgentClient"/> that can be used to communicate with the Juno Agents API
        /// service.
        /// </summary>
        /// <param name="clientPrincipal">Defines the AAD principal of the client application (e.g. GuestAgent, HostAgent).</param>
        /// <param name="apiPrincipal">Defines the AAD principal that the API service uses for identity/authentication (e.g. AgentsApi).</param>
        /// <param name="apiUri">The URI of the Agents API/service.</param>
        public static AgentClient CreateAgentsApiClient(AadPrincipalSettings clientPrincipal, AadPrincipalSettings apiPrincipal, Uri apiUri)
        {
            clientPrincipal.ThrowIfNull(nameof(clientPrincipal));
            apiPrincipal.ThrowIfNull(nameof(apiPrincipal));
            apiUri.ThrowIfNull(nameof(apiUri));

            IRestClient restClient = new RestClientBuilder()
                .WithAutoRefreshToken(
                    clientPrincipal.AuthorityUri,
                    clientPrincipal.PrincipalId,
                    apiPrincipal.PrincipalId,
                    clientPrincipal.PrincipalCertificateThumbprint)
                .AddAcceptedMediaType(MediaType.Json)
                .Build();

            return new AgentClient(restClient, apiUri);
        }

        /// <summary>
        /// Creates an <see cref="IExperimentClient"/> that can be used to communicate with the Juno Experiments API
        /// service.
        /// </summary>
        /// <param name="clientPrincipal">Defines the AAD principal of the client application (e.g. ExecutionSvc).</param>
        /// <param name="apiPrincipal">Defines the AAD principal that the API service uses for identity/authentication (e.g. ExperimentsApi).</param>
        /// <param name="apiUri">The URI of the Experiments API/service.</param>
        public static IExperimentClient CreateExperimentsApiClient(AadPrincipalSettings clientPrincipal, AadPrincipalSettings apiPrincipal, Uri apiUri)
        {
            clientPrincipal.ThrowIfNull(nameof(clientPrincipal));
            apiPrincipal.ThrowIfNull(nameof(apiPrincipal));
            apiUri.ThrowIfNull(nameof(apiUri));

            IRestClient restClient = new RestClientBuilder()
                .WithAutoRefreshToken(
                    clientPrincipal.AuthorityUri,
                    clientPrincipal.PrincipalId,
                    apiPrincipal.PrincipalId,
                    clientPrincipal.PrincipalCertificateThumbprint)
                .AddAcceptedMediaType(MediaType.Json)
                .Build();

            return new ExperimentsClient(restClient, apiUri);
        }

        /// <summary>
        /// Creates an <see cref="ExecutionClient"/> that can be used to communicate with the Juno Execution API
        /// service.
        /// </summary>
        /// <param name="clientPrincipal">Defines the AAD principal of the client application (e.g. ExecutionSvc).</param>
        /// <param name="apiPrincipal">Defines the AAD principal that the API service uses for identity/authentication (e.g. ExecutionApi).</param>
        /// <param name="apiUri">The URI of the Execution API/service.</param>
        public static ExecutionClient CreateExecutionApiClient(AadPrincipalSettings clientPrincipal, AadPrincipalSettings apiPrincipal, Uri apiUri)
        {
            clientPrincipal.ThrowIfNull(nameof(clientPrincipal));
            apiPrincipal.ThrowIfNull(nameof(apiPrincipal));
            apiUri.ThrowIfNull(nameof(apiUri));

            IRestClient restClient = new RestClientBuilder()
                .WithAutoRefreshToken(
                    clientPrincipal.AuthorityUri,
                    clientPrincipal.PrincipalId,
                    apiPrincipal.PrincipalId,
                    clientPrincipal.PrincipalCertificateThumbprint)
                .AddAcceptedMediaType(MediaType.Json)
                .Build();

            return new ExecutionClient(restClient, apiUri);
        }

        /// <summary>
        /// Creates an <see cref="ExecutionClient"/> that can be used to communicate with the Juno Execution API
        /// service.
        /// </summary>
        /// <param name="clientPrincipal">Defines the AAD principal of the client application (e.g. ExecutionSvc).</param>
        /// <param name="apiPrincipal">Defines the AAD principal that the API service uses for identity/authentication (e.g. ExecutionApi).</param>
        /// <param name="apiUri">The URI of the Execution API/service.</param>
        public static IEnvironmentClient CreateEnvironmentApiClient(AadPrincipalSettings clientPrincipal, AadPrincipalSettings apiPrincipal, Uri apiUri)
        {
            clientPrincipal.ThrowIfNull(nameof(clientPrincipal));
            apiPrincipal.ThrowIfNull(nameof(apiPrincipal));
            apiUri.ThrowIfNull(nameof(apiUri));

            IRestClient restClient = new RestClientBuilder()
                .WithAutoRefreshToken(
                    clientPrincipal.AuthorityUri,
                    clientPrincipal.PrincipalId,
                    apiPrincipal.PrincipalId,
                    clientPrincipal.PrincipalCertificateThumbprint)
                .AddAcceptedMediaType(MediaType.Json)
                .Build();

            return new EnvironmentClient(restClient, apiUri);
        }

        /// <summary>
        /// Create an <see cref="IAzureKeyVault"/> client to manage access to secrets (e.g. connection strings) within
        /// the API environment.
        /// </summary>
        /// <param name="kvSettings">Keyvault settings for the environment in which the API will run (e.g. juno-dev01).</param>
        /// <param name="aadPrincipal">The aad principal to access the keyvault.</param>
        /// <returns>
        /// Type: <see cref="IAzureKeyVault"/>
        /// </returns>
        public static IAzureKeyVault CreateKeyVaultClient(AadPrincipalSettings aadPrincipal, KeyVaultSettings kvSettings)
        {
            kvSettings.ThrowIfNull(nameof(kvSettings));
            aadPrincipal.ThrowIfNull(nameof(aadPrincipal));

            IKeyVaultClient keyVaultClient = KeyVaultClientFactory.CreateClient(
                aadPrincipal.PrincipalId,
                aadPrincipal.PrincipalCertificateThumbprint);

            return new AzureKeyVault(keyVaultClient, kvSettings.Uri);
        }

        /// <summary>
        /// Creates an <see cref="IExperimentDataManager"/> instance to manage experiment/experiment step data within
        /// the API environment.
        /// </summary>
        /// <param name="settings">Configuration settings for the environment in which the API will run (e.g. juno-dev01).</param>
        /// <param name="stepFactory">A factory for creating execution steps associated with an experiment definition.</param>
        /// <param name="keyVaultClient">Client for communications with Azure Key Vault.</param>
        /// <param name="logger">A logger that can be used to capture telemetry for data manager operations.</param>
        /// <returns>
        /// Type: <see cref="IExperimentDataManager"/>
        /// </returns>
        public static IExperimentDataManager CreateExperimentDataManager(EnvironmentSettings settings, IExperimentStepFactory stepFactory, IAzureKeyVault keyVaultClient, ILogger logger = null)
        {
            settings.ThrowIfNull(nameof(settings));
            keyVaultClient.ThrowIfNull(nameof(keyVaultClient));

            CosmosClient cosmosClient = null;
            CloudTableClient cosmosTableClient = null;

            // Secrets MUST be stored in the Key Vault. They are referenced within the configuration using
            // a format like: [secret:keyvault]=CosmosDBAccountKey.
            CosmosSettings cosmosDbSettings = settings.CosmosSettings.Get(Setting.Experiments);
            CosmosSettings cosmosTableSettings = settings.CosmosSettings.Get(Setting.ExperimentSteps);

            using (SecureString accountKey = keyVaultClient.ResolveSecretAsync(cosmosDbSettings.AccountKey, CancellationToken.None)
               .GetAwaiter().GetResult())
            {
                cosmosClient = new CosmosClient(cosmosDbSettings.Uri.AbsoluteUri, accountKey.ToOriginalString());
            }

            using (SecureString accountKey = keyVaultClient.ResolveSecretAsync(cosmosTableSettings.AccountKey, CancellationToken.None)
               .GetAwaiter().GetResult())
            {
                StorageCredentials credentials = new StorageCredentials(
                        cosmosTableSettings.Uri.Host.Split('.').First(),
                        accountKey.ToOriginalString());

                cosmosTableClient = new CloudTableClient(cosmosTableSettings.Uri, credentials);
            }

            AzureCosmosDbStore documentStore = new AzureCosmosDbStore(cosmosClient);
            AzureCosmosTableStore tableStore = new AzureCosmosTableStore(cosmosTableClient);

            return new ExperimentDataManager(documentStore, tableStore, stepFactory, logger: logger);
        }

        /// <summary>
        /// Creates an <see cref="IExperimentFileManager"/> instance to manage files/content within
        /// the API environment.
        /// </summary>
        /// <param name="settings">Configuration settings for the environment in which the API will run (e.g. juno-dev01).</param>
        /// <param name="keyVaultClient">Client for communications with Azure Key Vault.</param>
        /// <param name="logger">A logger that can be used to capture telemetry for file manager operations.</param>
        /// <returns>
        /// Type: <see cref="IExperimentFileManager"/>
        /// </returns>
        public static IExperimentFileManager CreateExperimentFileManager(EnvironmentSettings settings, IAzureKeyVault keyVaultClient, ILogger logger = null)
        {
            settings.ThrowIfNull(nameof(settings));
            keyVaultClient.ThrowIfNull(nameof(keyVaultClient));

            CloudBlobClient blobClient = null;
            StorageAccountSettings accountSettings = settings.StorageAccountSettings.Get(Setting.FileStore);

            using (SecureString accountKey = keyVaultClient.ResolveSecretAsync(accountSettings.AccountKey, CancellationToken.None)
               .GetAwaiter().GetResult())
            {
                var credentials = new Microsoft.Azure.Storage.Auth.StorageCredentials(
                        accountSettings.Uri.Host.Split('.').First(),
                        accountKey.ToOriginalString());

                blobClient = new CloudBlobClient(accountSettings.Uri, credentials);
            }

            AzureBlobStore blobStore = new AzureBlobStore(blobClient);

            return new ExperimentFileManager(blobStore, logger);
        }

        /// <summary>
        /// Creates an <see cref="IAgentHeartbeatManager"/> instance to manage agent heartbeat data within
        /// the API environment.
        /// </summary>
        /// <param name="settings">Configuration settings for the environment in which the API will run (e.g. juno-dev01).</param>
        /// <param name="keyVaultClient">Client for communications with Azure Key Vault.</param>
        /// <param name="logger">A logger that can be used to capture telemetry for data manager operations.</param>
        /// <returns>
        /// Type: <see cref="IAgentHeartbeatManager"/>
        /// </returns>
        public static IAgentHeartbeatManager CreateAgentHeartbeatDataManager(EnvironmentSettings settings, IAzureKeyVault keyVaultClient, ILogger logger = null)
        {
            settings.ThrowIfNull(nameof(settings));
            keyVaultClient.ThrowIfNull(nameof(keyVaultClient));

            CloudTableClient cosmosTableClient = null;
            CosmosSettings cosmosTableSettings = settings.CosmosSettings.Get(Setting.Heartbeats);

            using (SecureString accountKey = keyVaultClient.ResolveSecretAsync(cosmosTableSettings.AccountKey, CancellationToken.None)
               .GetAwaiter().GetResult())
            {
                var credentials = new StorageCredentials(
                        cosmosTableSettings.Uri.Host.Split('.').First(),
                        accountKey.ToOriginalString());

                cosmosTableClient = new CloudTableClient(cosmosTableSettings.Uri, credentials);
            }

            AzureCosmosTableStore tableStore = new AzureCosmosTableStore(cosmosTableClient);

            return new AgentHeartbeatManager(tableStore, logger);
        }

        /// <summary>
        /// Creates an <see cref="IExperimentNotificationManager"/> instance to manage notifications within
        /// the API environment.
        /// </summary>
        /// <param name="settings">Configuration settings for the environment in which the API will run (e.g. juno-dev01).</param>
        /// <param name="keyVaultClient">Client for communications with Azure Key Vault.</param>
        /// <param name="logger">A logger that can be used to capture telemetry for notification manager operations.</param>
        /// <returns>
        /// Type: <see cref="IExperimentNotificationManager"/>
        /// </returns>
        public static IExperimentNotificationManager CreateExperimentNotificationManager(EnvironmentSettings settings, IAzureKeyVault keyVaultClient, ILogger logger = null)
        {
            settings.ThrowIfNull(nameof(settings));
            keyVaultClient.ThrowIfNull(nameof(keyVaultClient));

            CloudQueueClient queueClient = null;
            StorageAccountSettings accountSettings = settings.StorageAccountSettings.Get(Setting.NotificationsQueue);

            using (SecureString accountKey = keyVaultClient.ResolveSecretAsync(accountSettings.AccountKey, CancellationToken.None)
               .GetAwaiter().GetResult())
            {
                var credentials = new Microsoft.Azure.Storage.Auth.StorageCredentials(
                        accountSettings.Uri.Host.Split('.').First(),
                        accountKey.ToOriginalString());

                queueClient = new CloudQueueClient(accountSettings.Uri, credentials);
            }

            var queueStore = new AzureQueueStore(queueClient);

            return new ExperimentNotificationManager(queueStore, logger);
        }

        /// <summary>
        /// Creates an <see cref="ILogger"/> instance to capture log traces and telemetry.
        /// </summary>
        /// <param name="settings">Configuration settings for the host.</param>
        /// <param name="categoryName">The logging category.</param>
        /// <returns>
        /// An <see cref="ILogger"/> instance configured to route tracing vs. structured
        /// telemetry information to different loggers.
        /// </returns>
        public static ILogger CreateLogger(EnvironmentSettings settings, string categoryName)
        {
            settings.ThrowIfNull(nameof(settings));
            categoryName.ThrowIfNullOrWhiteSpace(nameof(categoryName));

            AppInsightsSettings telemetrySettings = settings.AppInsightsSettings.Get(Setting.Telemetry);
            AppInsightsSettings tracingSettings = settings.AppInsightsSettings.Get(Setting.Tracing);

            return HostDependencies.CreateLogger(
                telemetrySettings.InstrumentationKey,
                tracingSettings.InstrumentationKey,
                categoryName);
        }

        /// <summary>
        /// Creates an <see cref="ILogger"/> instance to capture log traces and telemetry.
        /// </summary>
        /// <param name="telemetryInstrumentationKey">The Application Insights instrumentation key for structured telemetry events.</param>
        /// <param name="tracingInstrumentationKey">The Application Insights instrumentation key for general tracing events.</param>
        /// <param name="categoryName">The logging category.</param>
        /// <returns>
        /// An <see cref="ILogger"/> instance configured to route tracing vs. structured
        /// telemetry information to different loggers.
        /// </returns>
        public static ILogger CreateLogger(string telemetryInstrumentationKey, string tracingInstrumentationKey, string categoryName)
        {
            telemetryInstrumentationKey.ThrowIfNullOrWhiteSpace(nameof(telemetryInstrumentationKey));
            tracingInstrumentationKey.ThrowIfNullOrWhiteSpace(nameof(tracingInstrumentationKey));
            categoryName.ThrowIfNullOrWhiteSpace(nameof(categoryName));

            // Tracing goes to one Applications Insights endpoint/resource.
            ReliableInMemoryChannel tracingChannel = new ReliableInMemoryChannel();
            TelemetryConfiguration tracingConfiguration = new TelemetryConfiguration(
                tracingInstrumentationKey,
                tracingChannel);

            // Structured telemetry goes to a different Applications Insights endpoint/resource.
            ReliableInMemoryChannel telemetryChannel = new ReliableInMemoryChannel();
            TelemetryConfiguration telemetryConfiguration = new TelemetryConfiguration(
                telemetryInstrumentationKey,
                telemetryChannel);

            // The telemetry must be able to be flushed in the future before any application using
            // the logger exits.
            HostDependencies.TelemetryChannels.Add(tracingChannel);
            HostDependencies.TelemetryChannels.Add(telemetryChannel);

            List<ILoggerProvider> loggingProviders = new List<ILoggerProvider>
            {
                // Log only structured telemetry events.
                new AppInsightsTelemetryLoggerProvider(telemetryConfiguration)
                    .WithFilter((id, state) => (state as EventContext) != null),

                // Log everything other than the structured telemetry to the tracing
                // instance of App Insights and to console.
                new AppInsightsTelemetryLoggerProvider(tracingConfiguration)
                    .WithFilter((id, state) => (state as EventContext) == null),

                new ConsoleLoggerProvider()
                    .WithFilter((id, state) => (state as EventContext) == null),
            };

            ILogger logger = null;
            using (ILoggerFactory loggerFactory = new LoggerFactory(loggingProviders))
            {
                logger = loggerFactory.CreateLogger(categoryName);
            }

            return logger;
        }

        /// <summary>
        /// Creates an <see cref="ILogger"/> instance to capture log traces and telemetry.
        /// </summary>
        /// <param name="appInsightsInstrumentationKey">The Application Insights instrumentation key for structured telemetry events.</param>
        /// <param name="eventHubConnectionString">The EventHub instrumentation key for structured telemetry events.</param>
        /// <param name="eventHubName">The name of the EventHub for telemetry.</param>
        /// <param name="categoryName">The logging category.</param>
        /// <param name="enableDiagnostics">True/false to enable logger/channel diagnostics.</param>
        /// <param name="eventHubChannelConfiguration">Allows Event Hub channel configurations to be set.</param>
        /// <returns>
        /// An <see cref="ILogger"/> instance configured to route tracing vs. structured
        /// telemetry information to different loggers.
        /// </returns>
        public static ILogger CreateLogger(
            string categoryName,
            string appInsightsInstrumentationKey = null, 
            string eventHubConnectionString = null,
            string eventHubName = null,
            bool enableDiagnostics = false,
            Action<EventHubTelemetryChannel> eventHubChannelConfiguration = null)
        {
            List<ILoggerProvider> loggingProviders = new List<ILoggerProvider>
            {
                new ConsoleLoggerProvider().WithFilter((id, state) => (state as EventContext) == null),
            };

            if (appInsightsInstrumentationKey != null)
            {
                // Structured telemetry goes to a different Applications Insights endpoint/resource.
                ReliableInMemoryChannel telemetryChannel = new ReliableInMemoryChannel();
                TelemetryConfiguration telemetryConfiguration = new TelemetryConfiguration(
                    appInsightsInstrumentationKey,
                    telemetryChannel);

                // The telemetry must be able to be flushed in the future before any application using
                // the logger exits.
                HostDependencies.TelemetryChannels.Add(telemetryChannel);

                // Apply a filter that ensures only structured telemetry events are logged to the telemetry
                // endpoint.
                loggingProviders.Add(new AppInsightsTelemetryLoggerProvider(telemetryConfiguration)
                    .WithFilter((id, state) => (state as EventContext) != null));
            }

            if (eventHubConnectionString != null && eventHubName != null)
            {
                EventHubTelemetryChannel eventHubTelemetryChannel = new EventHubTelemetryChannel(
                    new EventHubProducerClient(eventHubConnectionString, eventHubName),
                    enableDiagnostics: enableDiagnostics);

                eventHubChannelConfiguration?.Invoke(eventHubTelemetryChannel);

                HostDependencies.TelemetryChannels.Add(eventHubTelemetryChannel);
                loggingProviders.Add(new EventHubTelemetryLoggerProvider(eventHubTelemetryChannel)
                    .WithFilter((id, state) => (state as EventContext) != null));
            }

            ILogger logger = null;
            using (ILoggerFactory loggerFactory = new LoggerFactory(loggingProviders))
            {
                logger = loggerFactory.CreateLogger(categoryName);
            }

            return logger;
        }

        /// <summary>
        /// Creates an <see cref="ILogger"/> instance to capture log traces and telemetry.
        /// </summary>
        /// <param name="categoryName">The logging category.</param>
        /// <param name="appInsightsTelemetrySettings">
        /// Settings that describe the Application Insights instance into which to write structure telemetry data.
        /// </param>
        /// <param name="appInsightsTracingSettings">
        /// Settings that describe the Application Insights instance into which to write general application tracing data.
        /// </param>
        /// <param name="eventHubTelemetrySettings">
        /// Settings that describe the Event Hub instance into which to write structure telemetry data.
        /// </param>
        /// <param name="eventHubTracingSettings">
        /// Settings that describe the Event Hub instance into which to write general application tracing data.
        /// </param>
        /// <param name="keyVaultClient">
        /// A client for getting secrets from an Azure Key Vault that are required to create the loggers (e.g. connection strings).
        /// </param>
        /// <param name="enableDiagnostics">True/false to enable logger/channel diagnostics.</param>
        /// <param name="eventHubChannelConfiguration">Allows Event Hub channel configurations to be set.</param>
        /// <returns>
        /// An <see cref="ILogger"/> instance configured to route tracing vs. structured
        /// telemetry information to different loggers.
        /// </returns>
        public static ILogger CreateLogger(
            string categoryName,
            AppInsightsSettings appInsightsTelemetrySettings = null,
            AppInsightsSettings appInsightsTracingSettings = null,
            EventHubSettings eventHubTelemetrySettings = null,
            EventHubSettings eventHubTracingSettings = null,
            IAzureKeyVault keyVaultClient = null,
            bool enableDiagnostics = false,
            Action<EventHubTelemetryChannel> eventHubChannelConfiguration = null)
        {
            List<ILoggerProvider> loggingProviders = new List<ILoggerProvider>
            {
                new ConsoleLoggerProvider().WithFilter((id, state) => (state as EventContext) == null),
            };

            if (appInsightsTelemetrySettings != null)
            {
                // Structured telemetry goes to a different Applications Insights endpoint/resource.
                ReliableInMemoryChannel telemetryChannel = new ReliableInMemoryChannel();
                TelemetryConfiguration telemetryConfiguration = new TelemetryConfiguration(
                    appInsightsTelemetrySettings.InstrumentationKey,
                    telemetryChannel);

                // The telemetry must be able to be flushed in the future before any application using
                // the logger exits.
                HostDependencies.TelemetryChannels.Add(telemetryChannel);

                // Apply a filter that ensures only structured telemetry events are logged to the telemetry
                // endpoint.
                loggingProviders.Add(new AppInsightsTelemetryLoggerProvider(telemetryConfiguration)
                    .WithFilter((id, state) => (state as EventContext) != null));
            }

            if (appInsightsTracingSettings != null)
            {
                // Tracing goes to one Applications Insights endpoint/resource.
                ReliableInMemoryChannel tracingChannel = new ReliableInMemoryChannel();
                TelemetryConfiguration tracingConfiguration = new TelemetryConfiguration(
                    appInsightsTracingSettings.InstrumentationKey,
                    tracingChannel);

                // The telemetry must be able to be flushed in the future before any application using
                // the logger exits.
                HostDependencies.TelemetryChannels.Add(tracingChannel);

                // Apply a filter that ensures non-structured logging events are logged to the tracing
                // endpoint.
                loggingProviders.Add(new AppInsightsTelemetryLoggerProvider(tracingConfiguration)
                    .WithFilter((id, state) => (state as EventContext) == null));
            }

            if (eventHubTelemetrySettings != null)
            {
                keyVaultClient.ThrowIfNull(
                    nameof(keyVaultClient),
                    "An Azure Key Vault client must be provided to get the connection string required for the Event Hub logger.");

                using (SecureString connectionString = keyVaultClient.ResolveSecretAsync(eventHubTelemetrySettings.ConnectionString, CancellationToken.None)
                    .GetAwaiter().GetResult())
                {
                    EventHubTelemetryChannel telemetryChannel = new EventHubTelemetryChannel(
                        new EventHubProducerClient(connectionString.ToOriginalString(), eventHubTelemetrySettings.EventHub),
                        enableDiagnostics: enableDiagnostics);

                    eventHubChannelConfiguration?.Invoke(telemetryChannel);

                    HostDependencies.TelemetryChannels.Add(telemetryChannel);
                    loggingProviders.Add(new EventHubTelemetryLoggerProvider(telemetryChannel)
                        .WithFilter((id, state) => (state as EventContext) != null));
                }
            }

            if (eventHubTracingSettings != null)
            {
                keyVaultClient.ThrowIfNull(
                    nameof(keyVaultClient),
                    "An Azure Key Vault client must be provided to get the connection string required for the Event Hub logger.");

                using (SecureString connectionString = keyVaultClient.ResolveSecretAsync(eventHubTracingSettings.ConnectionString, CancellationToken.None)
                    .GetAwaiter().GetResult())
                {
                    EventHubTelemetryChannel tracingChannel = new EventHubTelemetryChannel(
                        new EventHubProducerClient(connectionString.ToOriginalString(), eventHubTracingSettings.EventHub),
                        enableDiagnostics: enableDiagnostics);

                    eventHubChannelConfiguration?.Invoke(tracingChannel);

                    HostDependencies.TelemetryChannels.Add(tracingChannel);
                    loggingProviders.Add(new EventHubTelemetryLoggerProvider(tracingChannel)
                        .WithFilter((id, state) => (state as EventContext) != null));
                }
            }

            ILogger logger = null;
            using (ILoggerFactory loggerFactory = new LoggerFactory(loggingProviders))
            {
                logger = loggerFactory.CreateLogger(categoryName);
            }

            return logger;
        }

        /// <summary>
        /// Create an instance of <see cref="IKustoQueryIssuer"/> client to connect with Kusto cluster.
        /// </summary>
        /// <param name="settings">Configuration settings for the environment in which the API will run (e.g. juno-dev01).</param>
        /// <returns>
        /// Type: <see cref="IKustoQueryIssuer"/>
        /// </returns>
        public static IKustoQueryIssuer CreateKustoClient(EnvironmentSettings settings)
        {
            settings.ThrowIfNull(nameof(settings));

            KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
            AadPrincipalSettings principalSettings = kustoSettings.AadPrincipals.Get(Setting.Default);

            return new KustoQueryIssuer(
                principalSettings.PrincipalId,
                principalSettings.PrincipalCertificateThumbprint,
                principalSettings.TenantId);
        }

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

        /// <summary>
        /// Flushes telemetry for all channels that are being tracked.
        /// </summary>
        public static void FlushTelemetry()
        {
            Parallel.ForEach(
                HostDependencies.TelemetryChannels,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                (channel) =>
                {
                    channel.Flush(TimeSpan.FromSeconds(60));
                });
        }

        /// <summary>
        /// Create an list of <see cref="SubscriptionManager"/> client to  manage azure subscriptions per subscription ID in dev/prod environment
        /// </summary>
        /// <param name="envSettings">Configuration settings for the environment in which the API will run (e.g. juno-dev01).</param>
        /// <param name="subscriptionIDs">Subscription IDs that are within the environment to manage</param>
        /// <returns>
        /// Type: List of <see cref="SubscriptionManager"/>
        /// </returns>
        public static IList<ISubscriptionManager> CreateAzureSubscriptionManager(EnvironmentSettings envSettings, IEnumerable<string> subscriptionIDs)
        {
            envSettings.ThrowIfNull(nameof(envSettings));
            subscriptionIDs.ThrowIfNullOrEmpty(nameof(subscriptionIDs));

            AadPrincipalSettings executionSettings = envSettings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc);

            SubscriptionSettings subscriptionSettings = new SubscriptionSettings()
            {
                TenantId = executionSettings.TenantId,
                PrincipalId = executionSettings.PrincipalId,
                PrincipalCertificateThumbprint = executionSettings.PrincipalCertificateThumbprint
            };

            IList<ISubscriptionManager> subscriptionManagers = new List<ISubscriptionManager>();
            subscriptionIDs.ForEach(x => subscriptionManagers.Add(new SubscriptionManager(subscriptionSettings, x)));

            return subscriptionManagers;
        }
    }
}
