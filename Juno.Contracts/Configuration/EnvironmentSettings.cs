namespace Juno.Contracts.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// 
    /// </summary>
    public class EnvironmentSettings
    {
        /// <summary>
        /// Gets the ID of the environment settings definition.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets the environment for which the settings defines.
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// Settings for the Juno host and VM agents.
        /// </summary>
        public AgentSettings AgentSettings { get; set; }

        /// <summary>
        /// Settings for Azure App Insights resources used in the environment.
        /// </summary>
        public IEnumerable<AppInsightsSettings> AppInsightsSettings { get; set; }

        /// <summary>
        /// Settings for Azure Cosmos DB/Table resources used in the environment.
        /// </summary>
        public IEnumerable<CosmosSettings> CosmosSettings { get; set; }

        /// <summary>
        /// Settings for Azure Event Hub resources used in the environment.
        /// </summary>
        public IEnumerable<EventHubSettings> EventHubSettings { get; set; }

        /// <summary>
        /// Settings used by Juno Scheduler
        /// </summary>
        public SchedulerSettings SchedulerSettings { get; set; }

        /// <summary>
        /// Settings used by Garbage Collector
        /// </summary>
        public GarbageCollectorSettings GarbageCollectorSettings { get; set; }

        /// <summary>
        /// Settings for the Juno Execution Service.
        /// </summary>
        public ExecutionSettings ExecutionSettings { get; set; }

        /// <summary>
        /// Settings for the Tip client.
        /// </summary>
        public TipSettings TipSettings { get; set; }

        /// <summary>
        /// Settings for Azure Key Vault resources used in the environment.
        /// </summary>
        public IEnumerable<KeyVaultSettings> KeyVaultSettings { get; set; }

        /// <summary>
        /// Settings for Azure Kusto/Data Explorer (ADX) resources used in the environment.
        /// </summary>
        public IEnumerable<KustoSettings> KustoSettings { get; set; }

        /// <summary>
        /// Settings for Azure Storage Account resources used in the environment.
        /// </summary>
        public IEnumerable<StorageAccountSettings> StorageAccountSettings { get; set; }

        /// <summary>
        /// Settings for executables that are ran on host.
        /// </summary>
        public IEnumerable<NodeExecutableSettings> NodeExecutableSettings { get; set; }

        /// <summary>
        /// Creates an instance of the <see cref="EnvironmentSettings"/> class with the 
        /// properties initialized to values defined in the configuration/configuration settings
        /// provided.
        /// </summary>
        /// <param name="configuration">The configuration/configuration settings.</param>
        public static EnvironmentSettings Initialize(IConfiguration configuration)
        {
            configuration.ThrowIfNull(nameof(configuration));

            ConfigurationBinder.Bind(configuration, nameof(EnvironmentSettings.AppInsightsSettings), new List<AppInsightsSettings>());
            ConfigurationBinder.Bind(configuration, nameof(EnvironmentSettings.EventHubSettings), new List<EventHubSettings>());
            ConfigurationBinder.Bind(configuration, nameof(EnvironmentSettings.CosmosSettings), new List<CosmosSettings>());
            ConfigurationBinder.Bind(configuration, nameof(EnvironmentSettings.ExecutionSettings), new ExecutionSettings());
            ConfigurationBinder.Bind(configuration, nameof(EnvironmentSettings.AgentSettings), new AgentSettings());
            ConfigurationBinder.Bind(configuration, nameof(EnvironmentSettings.TipSettings), new TipSettings());
            ConfigurationBinder.Bind(configuration, nameof(EnvironmentSettings.KeyVaultSettings), new List<KeyVaultSettings>());
            ConfigurationBinder.Bind(configuration, nameof(EnvironmentSettings.KustoSettings), new List<KustoSettings>());
            ConfigurationBinder.Bind(configuration, nameof(EnvironmentSettings.StorageAccountSettings), new List<StorageAccountSettings>());
            ConfigurationBinder.Bind(configuration, nameof(EnvironmentSettings.SchedulerSettings), new SchedulerSettings());
            ConfigurationBinder.Bind(configuration, nameof(EnvironmentSettings.NodeExecutableSettings), new List<NodeExecutableSettings>());
            return configuration.Get<EnvironmentSettings>();
        }
    }
}
