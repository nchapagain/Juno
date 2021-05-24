namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a Target Goal Trigger
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class TargetGoalTrigger : ItemBase
    {
        private int? hashCode;

        /// <summary>
        /// Initializes an instance of <see cref="TargetGoalTrigger"/>
        /// </summary>
        [JsonConstructor]
        public TargetGoalTrigger(
            string id,
            string executionGoal,
            string targetGoal,
            string cronExpression,
            bool enabled,
            string experimentName,
            string teamName,
            string version,
            DateTime created,
            DateTime lastModified)
            : base(id, created, lastModified)
        {
            executionGoal.ThrowIfNullOrWhiteSpace(nameof(executionGoal));
            targetGoal.ThrowIfNullOrWhiteSpace(nameof(targetGoal));
            cronExpression.ThrowIfNullOrWhiteSpace(nameof(cronExpression));
            experimentName.ThrowIfNullOrWhiteSpace(nameof(experimentName));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));
            version.ThrowIfNullOrWhiteSpace(nameof(version));

            this.ExecutionGoal = executionGoal;
            this.TargetGoal = targetGoal;
            this.CronExpression = cronExpression;
            this.Enabled = enabled;
            this.ExperimentName = experimentName;
            this.TeamName = teamName;
            this.Version = version;
        }

        /// <summary>
        /// Name of Execution Goal the target goal resides in
        /// </summary>
        [JsonProperty("executionGoal", Required = Required.Always)]
        public string ExecutionGoal { get; }

        /// <summary>
        /// Name of Target Goal
        /// </summary>
        [JsonProperty("targetGoal", Required = Required.Always)]
        public string TargetGoal { get; }
        
        /// <summary>
        /// How often the Target Goal should be executed
        /// </summary>
        [JsonProperty("cronExpression", Required = Required.Always)]
        public string CronExpression { get; }

        /// <summary>
        /// Determines if the Target Goal is enabled
        /// </summary>
        [JsonProperty("enabled", Required = Required.Always)]
        public bool Enabled { get; set; }

        /// <summary>
        /// Name of Overall Experiment that the Execution Goal resides in
        /// </summary>
        [JsonProperty("experimentName", Required = Required.Always)]
        public string ExperimentName { get; }

        /// <summary>
        /// Team name that authored the Target Goal
        /// </summary>
        [JsonProperty("teamName", Required = Required.Always)]
        public string TeamName { get; }

        /// <summary>
        /// Version of the Execution Goal
        /// </summary>
        [JsonProperty("version", Required = Required.Always)]
        public string Version { get; }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder(base.GetHashCode().ToString())
                    .AppendProperties(this.ExperimentName, this.ExecutionGoal, this.TargetGoal)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
