namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Contract for execution goals. Contains provider definitions, 
    /// metadata and parameters required for scheduler execution.
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class GoalBasedSchedule : IEquatable<GoalBasedSchedule>
    {
        private int? hashCode;

        /// <summary>
        /// Json Constructor for a Goal Based Schedule
        /// </summary>
        /// <param name="experimentName">Name of the experiment the execution goal will execute.</param>
        /// <param name="description">Description of Schedule</param>
        /// <param name="metadata">Metadata about the execution goal.</param>
        /// <param name="experiment">Represents an experiment definition</param>
        /// <param name="targetGoals">List of Goals that are to be met for this schedule</param>
        /// <param name="controlGoals">List of Goals that are to control execution of Schedule</param>
        /// <param name="parameters">List of Parameters to allow succesfull execution</param>
        [JsonConstructor]
        public GoalBasedSchedule(
            string experimentName,
            string description,
            Experiment experiment,
            List<TargetGoal> targetGoals,
            List<Goal> controlGoals,
            IDictionary<string, IConvertible> metadata = null,
            IDictionary<string, IConvertible> parameters = null)
        {
            experimentName.ThrowIfNullOrWhiteSpace(nameof(experimentName));
            description.ThrowIfNullOrWhiteSpace(nameof(description));
            experiment.ThrowIfNull(nameof(experiment));
            targetGoals.ThrowIfNullOrEmpty(nameof(targetGoals));
            controlGoals.ThrowIfNullOrEmpty(nameof(controlGoals));

            this.ExperimentName = experimentName;
            this.Description = description;
            this.Experiment = experiment;

            this.TargetGoals = new List<TargetGoal>(targetGoals);
            this.ControlGoals = new List<Goal>(controlGoals);

            this.Parameters = parameters == null
                ? this.Parameters = new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase)
                : this.Parameters = new Dictionary<string, IConvertible>(parameters, StringComparer.OrdinalIgnoreCase);

            this.Metadata = metadata == null
                ? this.Metadata = new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase)
                : this.Metadata = new Dictionary<string, IConvertible>(metadata, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Copy Constructor for Goal Based Scheduler 
        /// </summary>
        /// <param name="other">
        /// GoalBasedSchedule to copy into this instance.
        /// </param>
        public GoalBasedSchedule(GoalBasedSchedule other)
            : this(
                  other?.ExperimentName,
                  other?.Description,
                  other?.Experiment,
                  other?.TargetGoals,
                  other?.ControlGoals,
                  other?.Metadata,
                  other?.Parameters)
        {
        }

        /// <summary>
        /// Name of the schedule
        /// </summary>
        [JsonProperty(PropertyName = "experimentName", Required = Required.Always, Order = 00)]
        public string ExperimentName { get; }

        /// <summary>
        /// Description of the schedule
        /// </summary>
        [JsonProperty(PropertyName = "description", Required = Required.Always, Order = 10)]
        public string Description { get; }

        /// <summary>
        /// Experiment parameters that are overridden in the schedule
        /// </summary>
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        [JsonProperty(PropertyName = "parameters", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 20)]
        public IDictionary<string, IConvertible> Parameters { get; }

        /// <summary>
        /// Metadata for the schedule. It will include information like the teamName etc.
        /// </summary>
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        [JsonProperty(PropertyName = "metadata", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 30)]
        public IDictionary<string, IConvertible> Metadata { get; }

        /// <summary>
        /// List of Target Goals
        /// </summary>
        [JsonProperty(PropertyName = "targetGoals", Required = Required.Always, Order = 40)]
        public List<TargetGoal> TargetGoals { get; }

        /// <summary>
        /// List of Control Goals
        /// </summary>
        [JsonProperty(PropertyName = "controlGoals", Required = Required.Always, Order = 50)]
        public List<Goal> ControlGoals { get; }

        /// <summary>
        /// Represents an experiment definition
        /// </summary>
        [JsonProperty(PropertyName = "experiment", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 60)]
        public Experiment Experiment { get; }

        /// <summary>
        /// Gets the team name that owns the execution goal.
        /// </summary>
        [JsonIgnore]
        public string TeamName => this.Metadata.GetValue<string>(ExecutionGoalMetadata.TeamName, string.Empty);

        /// <summary>
        /// Gets the owner that owns the execution goal.
        /// </summary>
        [JsonIgnore]
        public string Owner => this.Metadata.GetValue<string>(ExecutionGoalMetadata.Owner, string.Empty);

        /// <summary>
        /// Gets whether or not monitoring is enabled.
        /// </summary>
        [JsonIgnore]
        public bool MonitoringEnabled => this.Metadata.GetValue<bool>(ExecutionGoalMetadata.MonitoringEnabled, true);

        /// <summary>
        /// Gets the version of the execution goal.
        /// </summary>
        [JsonIgnore]
        public string Version => this.Metadata.GetValue<string>(ExecutionGoalMetadata.Version, string.Empty);

        /// <summary>
        /// Gets the tenant id of the execution goal.
        /// </summary>
        [JsonIgnore]
        public string TenantId => this.Metadata.GetValue<string>(ExecutionGoalMetadata.TenantId, string.Empty);

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            if (!(obj is GoalBasedSchedule))
            {
                return false;
            }
            
            return this.Equals(obj as GoalBasedSchedule);
        }

        /// <summary>
        /// Method determines if the other object is equal to this instance
        /// </summary>
        /// <param name="other">Defines the other object to compare against</param>
        /// <returns>True if the objects are equal, false otherwise</returns>
        public virtual bool Equals(GoalBasedSchedule other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Override method returns a unique integer hash code
        /// Calls the base class for now to suppress warnings
        /// </summary>
        /// <returns> 
        /// Type: System.Int32
        /// A unique identifier for the class instance.   
        /// </returns>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                StringBuilder builder = new StringBuilder();
                List<Goal> goals = new List<Goal>();
                goals.AddRange(this.TargetGoals);
                goals.AddRange(this.ControlGoals);

                foreach (Goal goal in goals)
                {
                    builder.Append(goal.GetHashCode().ToString());
                }

                this.hashCode = builder
                    .AppendProperties(this.ExperimentName, this.Description)
                    .AppendParameters(this.Metadata)
                    .AppendParameters(this.Parameters)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
