namespace Juno.Contracts
{
    using System;
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
            string name,
            string cronExpression,
            bool enabled,
            string version,
            string teamName,
            DateTime created,
            DateTime lastModified)
            : base(id, created, lastModified)
        {
            executionGoal.ThrowIfNullOrWhiteSpace(nameof(executionGoal));
            name.ThrowIfNullOrWhiteSpace(nameof(name));
            cronExpression.ThrowIfNullOrWhiteSpace(nameof(cronExpression));
            version.ThrowIfNullOrWhiteSpace(nameof(version));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            this.ExecutionGoal = executionGoal;
            this.Name = name;
            this.CronExpression = cronExpression;
            this.Enabled = enabled;
            this.Version = version;
            this.TeamName = teamName;
        }

        /// <summary>
        /// Id of Execution Goal the target goal resides in
        /// </summary>
        [JsonProperty("executionGoal", Required = Required.Always)]
        public string ExecutionGoal { get; }

        /// <summary>
        /// Name of Target Goal
        /// </summary>
        [JsonProperty("name", Required = Required.Always)]
        public string Name { get; }
        
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
        /// Version of the Execution Goal
        /// </summary>
        [JsonProperty("version", Required = Required.Always)]
        public string Version { get; }

        /// <summary>
        /// Identifies the team that owns the target goal.
        /// </summary>
        [JsonProperty("teamName", Required = Required.Always)]
        public string TeamName { get; }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder(base.GetHashCode().ToString())
                    .AppendProperties(this.TeamName, this.ExecutionGoal, this.Name)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
