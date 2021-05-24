namespace Juno.Contracts
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Describes the status details of Experiment Instance
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class ExperimentInstanceStatus : IEquatable<ExperimentInstanceStatus>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentInstanceStatus"/> class.
        /// </summary>
        /// <param name="experimentId"> Experiment Id</param>
        /// <param name="experimentName"> Experiment Name</param>
        /// <param name="experimentStatus">Experiment Status</param>
        /// <param name="environment">Environment </param>
        /// <param name="executionGoal">Execution Goal Name</param>
        /// <param name="targetGoal">Target Goal Name</param>
        /// <param name="impactType">Impact Type</param>
        /// <param name="experimentStartTime">Experiment Start Time</param>
        /// <param name="lastIngestionTime">Last Ingestion Time</param>
        [JsonConstructor]
        public ExperimentInstanceStatus(
            string experimentId,
            string experimentName,
            ExperimentStatus experimentStatus,
            string environment,
            string executionGoal,
            string targetGoal,
            ImpactType impactType,
            DateTime experimentStartTime,
            DateTime lastIngestionTime)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            experimentName.ThrowIfNullOrWhiteSpace(nameof(experimentName));
            experimentStatus.ThrowIfNull(nameof(experimentStatus));
            environment.ThrowIfNullOrWhiteSpace(nameof(environment));
            executionGoal.ThrowIfNullOrWhiteSpace(nameof(executionGoal));
            targetGoal.ThrowIfNullOrWhiteSpace(nameof(targetGoal));
            impactType.ThrowIfNull(nameof(impactType));

            this.ExperimentId = experimentId;
            this.ExperimentName = experimentName;
            this.ExperimentStatus = experimentStatus;
            this.Environment = environment;
            this.ExecutionGoal = executionGoal;
            this.TargetGoal = targetGoal;
            this.ImpactType = impactType;
            this.ExperimentStartTime = experimentStartTime;
            this.LastIngestionTime = lastIngestionTime;
        }

        /// <summary>
        /// Experiment Id (e.g. 00000000-0000-0000-0000-000000000000).
        /// </summary>
        [JsonProperty(PropertyName = "experimentId", Required = Required.Always, Order = 1)]
        public string ExperimentId { get; }

        /// <summary>
        /// Experiment Name (e.g. MCU2020.2_Gen6_PV).
        /// </summary>
        [JsonProperty(PropertyName = "experimentName", Required = Required.Always, Order = 2)]
        public string ExperimentName { get; }

        /// <summary>
        /// Status of Experiment Instance
        /// </summary>
        [JsonProperty(PropertyName = "experimentStatus", Required = Required.Always, Order = 3)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ExperimentStatus ExperimentStatus { get; }

        /// <summary>
        /// Environment the experiment instance is running on (e.g. juno-dev01 or juno-prod01).
        /// </summary>
        [JsonProperty(PropertyName = "environment", Required = Required.Always, Order = 4)]
        public string Environment { get; }

        /// <summary>
        /// Execution Goal Name (e.g. MCU2020_2.Gen7.ExecutionGoal.v1.json).
        /// </summary>
        [JsonProperty(PropertyName = "executionGoal", Required = Required.Always, Order = 5)]
        public string ExecutionGoal { get; }

        /// <summary>
        /// Target Goal Name (e.g. IERR_Repro_Gen6_Mitigation_KStress-CRC AIR).
        /// </summary>
        [JsonProperty(PropertyName = "targetGoal", Required = Required.Always, Order = 6)]
        public string TargetGoal { get; }

        /// <summary>
        /// Impact Type of the experiment instance
        /// </summary>
        [JsonProperty(PropertyName = "impactType", Required = Required.Always, Order = 7)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ImpactType ImpactType { get; }

        /// <summary>
        /// Experiment Start Time 
        /// </summary>
        [JsonProperty(PropertyName = "experimentStartTime", Required = Required.Always, Order = 8)]
        public DateTime ExperimentStartTime { get; }

        /// <summary>
        /// Last updated time.
        /// </summary>
        [JsonProperty(PropertyName = "lastIngestionTime", Required = Required.Always, Order = 9)]
        public DateTime LastIngestionTime { get; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            ExperimentInstanceStatus itemDescription = obj as ExperimentInstanceStatus;
            if (itemDescription == null)
            {
                return false;
            }

            return this.Equals(itemDescription);
        }

        /// <summary>
        /// Determines if the other ExperimentInstanceStatus is equal to this instance
        /// </summary>
        /// <param name="other">
        /// Defines the other object to compare against
        /// </param>
        /// <returns></returns>
        public virtual bool Equals(ExperimentInstanceStatus other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Calculates the unique HashCode for instance
        /// </summary>
        /// <returns>
        /// Type: System.Int32
        /// A unique identifier for the class instance.
        /// </returns>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder()
                    .AppendProperties(this.ExperimentId, this.LastIngestionTime, this.ExperimentStatus)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}