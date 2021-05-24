namespace Juno.Execution.Providers.Watchdog
{
    using Newtonsoft.Json;

    /// <summary>
    /// Contains the variables for connecting to a given geneva monitoring tenant
    /// </summary>
    public class UserArguments
    {
        /// <summary>
        /// Certificate store for GCS
        /// </summary>
        public string GcsCertStore { get; set; }

        /// <summary>
        /// GCS environment
        /// </summary>
        public string GcsEnvironment { get; set; }

        /// <summary>
        /// Should GCS use the exact version
        /// </summary>
        public string GcsExactVersion { get; set; }

        /// <summary>
        /// Geneva account name
        /// </summary>
        public string GcsGenevaAccount { get; set; }

        /// <summary>
        /// Namespace for geneva logs
        /// </summary>
        public string GcsNamespace { get; set; }

        /// <summary>
        /// GCS region for sending logs
        /// </summary>
        public string GcsRegion { get; set; }

        /// <summary>
        /// Thumbprint for the GCS certificate
        /// </summary>
        public string GcsThumbprint { get; set; }

        /// <summary>
        /// Version number for GCS config
        /// </summary>
        public string GenevaConfigVersion { get; set; }

        /// <summary>
        /// Local path where to write MA log data
        /// </summary>
        public string LocalPath { get; set; }

        /// <summary>
        /// Name of the Geneva start event
        /// </summary>
        public string StartEvent { get; set; }

        /// <summary>
        /// Name of Geneva stop event
        /// </summary>
        public string StopEvent { get; set; }
    }

    /// <summary>
    /// Contains the variables for identifying the geneva agent group
    /// </summary>
    public class ConstantVariables
    {
        /// <summary>
        /// Monitoring role used to identify the geneva agent
        /// </summary>
        [JsonProperty("MONITORING_ROLE")]
        public string MonitoringRole { get; set; }

        /// <summary>
        /// Monitoring tenant used to identify the geneva agent
        /// </summary>
        [JsonProperty("MONITORING_TENANT")]
        public string MonitoringTenant { get; set; }
    }

    /// <summary>
    /// Contains the variables for identifying individual instances
    /// </summary>
    public class ExpandVariables
    {
        /// <summary>
        /// Name for the given geneva agent instance
        /// </summary>
        [JsonProperty("MONITORING_ROLE_INSTANCE")]
        public string MonitoringRoleInstance { get; set; }
    }

    /// <summary>
    /// Contains the variables for service contract
    /// </summary>
    public class ServiceArguments
    {
        /// <summary>
        /// Version of this contract
        /// </summary>
        public string Version { get; set; }
    }

    /// <summary>
    /// Contract class for any geneva multi-tenant service tenant
    /// </summary>
    public class GenevaConfigContract
    {
        /// <summary>
        /// Variables for identifying geneva agent group
        /// </summary>
        public ConstantVariables ConstantVariables { get; set; }

        /// <summary>
        /// Particular instance related variables
        /// </summary>
        public ExpandVariables ExpandVariables { get; set; }

        /// <summary>
        /// Variables for service contract
        /// </summary>
        public ServiceArguments ServiceArguments { get; set; }

        /// <summary>
        /// Geneva account related variables
        /// </summary>
        public UserArguments UserArguments { get; set; }
    }
}
