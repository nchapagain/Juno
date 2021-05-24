namespace Juno.Contracts
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Describe the status details of Target Goal
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class TargetGoalTimeline : IEquatable<TargetGoalTimeline>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="TargetGoalTimeline"/> class.
        /// </summary>
        /// <param name="targetGoal">Target Goal Name</param>
        /// <param name="executionGoalId">Execution Goal Name</param>
        /// <param name="experimentName"> Experiment Name</param>
        /// <param name="environment">Environment </param>
        /// <param name="targetExperimentInstances"> Target Successful Experiment Instances</param>
        /// <param name="successfulExperimentInstances">Total Successful Experiment Instances</param>
        /// <param name="estimatedTimeOfCompletion"> Estimated Time of Completion</param>
        /// <param name="status">Status</param>
        /// <param name="lastModifiedTime">Last Modified Time</param>
        /// <param name="teamName">Team Name</param>
        [JsonConstructor]
        public TargetGoalTimeline(
            string targetGoal,
            string executionGoalId,
            string experimentName,
            string environment,
            string teamName,
            int targetExperimentInstances,
            int successfulExperimentInstances,
            DateTime lastModifiedTime,
            ExperimentStatus status,
            DateTime estimatedTimeOfCompletion)
        {
            targetGoal.ThrowIfNullOrWhiteSpace(nameof(targetGoal));
            executionGoalId.ThrowIfNullOrWhiteSpace(nameof(executionGoalId));
            experimentName.ThrowIfNullOrWhiteSpace(nameof(experimentName));
            environment.ThrowIfNullOrWhiteSpace(nameof(environment));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            this.TargetGoal = targetGoal;
            this.ExecutionGoalId = executionGoalId;
            this.ExperimentName = experimentName;
            this.Environment = environment;
            this.TeamName = teamName;
            this.TargetExperimentInstances = targetExperimentInstances;
            this.SuccessfulExperimentInstances = successfulExperimentInstances;
            this.LastModifiedTime = lastModifiedTime;
            this.EstimatedTimeOfCompletion = estimatedTimeOfCompletion;
            this.Status = status;
        }

        /// <summary>
        /// Target Goal Name (e.g. IERR_Repro_Gen6_Mitigation_KStress-CRC AIR).
        /// </summary>
        [JsonProperty(PropertyName = "targetGoal", Required = Required.Always, Order = 1)]
        public string TargetGoal { get; }

        /// <summary>
        /// Execution Goal ID (e.g. MCU2020_2.Gen7.ExecutionGoal.v1.json).
        /// </summary>
        [JsonProperty(PropertyName = "executionGoalId", Required = Required.Always, Order = 2)]
        public string ExecutionGoalId { get; }

        /// <summary>
        /// Experiment Name (e.g. MCU2020.2_Gen6_PV).
        /// </summary>
        [JsonProperty(PropertyName = "experimentName", Required = Required.Always, Order = 3)]
        public string ExperimentName { get; }

        /// <summary>
        /// Experiment Name (e.g. MCU2020.2_Gen6_PV).
        /// </summary>
        [JsonProperty(PropertyName = "environment", Required = Required.Always, Order = 4)]
        public string Environment { get; }

        /// <summary>
        /// Team Name (e.g. CRC AIR).
        /// </summary>
        [JsonProperty(PropertyName = "teamName", Required = Required.Always, Order = 5)]
        public string TeamName { get; }

        /// <summary>
        /// Defines how many instances of experiments need to be successful
        /// Precondition parameters for Juno.Scheduler.Preconditions.SuccessfulExperimentsProvider
        /// </summary>
        [JsonProperty(PropertyName = "targetExperimentInstances", Required = Required.Always, Order = 6)]
        public int TargetExperimentInstances { get; }

        /// <summary>
        /// Defines how many instances of experiments have succeeded
        /// </summary>
        [JsonProperty(PropertyName = "successfulExperimentInstances", Required = Required.Always, Order = 7)]
        public int SuccessfulExperimentInstances { get; }

        /// <summary>
        /// Last updated time.
        /// </summary>
        [JsonProperty(PropertyName = "lastModifiedTime", Required = Required.Always, Order = 8)]
        public DateTime LastModifiedTime { get; }

        /// <summary>
        /// Defines when Target Goal will succeeded
        /// </summary>
        [JsonProperty(PropertyName = "estimatedTimeOfCompletion", Required = Required.Always, Order = 9)]
        public DateTime EstimatedTimeOfCompletion { get; }

        /// <summary>
        /// Last updated time.
        /// </summary>
        [JsonProperty(PropertyName = "status", Required = Required.Always, Order = 10)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ExperimentStatus Status { get; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            TargetGoalTimeline itemDescription = obj as TargetGoalTimeline;
            if (itemDescription == null)
            {
                return false;
            }

            return this.Equals(itemDescription);
        }

        /// <summary>
        /// Determines if the other TargetGoalTimeline is equal to this instance
        /// </summary>
        /// <param name="other">
        /// Defines the other object to compare against
        /// </param>
        /// <returns></returns>
        public virtual bool Equals(TargetGoalTimeline other)
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
                    .AppendProperties(
                        this.TargetGoal,
                        this.ExecutionGoalId,
                        this.Environment,
                        this.TargetExperimentInstances,
                        this.SuccessfulExperimentInstances,
                        this.LastModifiedTime)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
