namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Schedule definition contract
    /// Defines differntiation between Control and Target Goals
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class GoalBasedSchedule : IEquatable<GoalBasedSchedule>
    {
        private int? hashCode;

        /// <summary>
        /// Json Constructor for a Goal Based Schedule
        /// </summary>
        /// <param name="experimentName">Name of the experiment the execution goal belongs to</param>
        /// <param name="executionGoalId">Unique ID for the execution goal</param>
        /// <param name="name">Name of Schedule</param>
        /// <param name="teamName">Name of the Team that owns the Execution Goal</param>
        /// <param name="description">Description of Schedule</param>
        /// <param name="metaData">Meta Data about Scheudle. (i.e. Teamname, etc)</param>
        /// <param name="enabled">True if this schedule is to be exectuted</param>
        /// <param name="version">The version of the schedule (Version is Date version was established)</param>
        /// <param name="experiment">Represents an experiment definition</param>
        /// <param name="targetGoals">List of Goals that are to be met for this schedule</param>
        /// <param name="controlGoals">List of Goals that are to control execution of Schedule</param>
        /// <param name="parameters">List of Parameters to allow succesfull execution</param>
        [JsonConstructor]
        public GoalBasedSchedule(
            string experimentName,
            string executionGoalId,
            string name,
            string teamName,
            string description,
            Dictionary<string, IConvertible> metaData,
            bool? enabled,
            string version,
            Experiment experiment,
            List<Goal> targetGoals,
            List<Goal> controlGoals,
            Dictionary<string, IConvertible> parameters = null)
        {
            experimentName.ThrowIfNullOrWhiteSpace(nameof(experimentName));
            executionGoalId.ThrowIfNullOrWhiteSpace(nameof(executionGoalId));
            name.ThrowIfNullOrWhiteSpace(nameof(name));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));
            description.ThrowIfNullOrWhiteSpace(nameof(description));
            enabled.ThrowIfNull(nameof(enabled));

            version.ThrowIfInvalid(
                nameof(version),
                (executionGoalVersion) =>
                {
                    return ContractExtension.SupportedExecutionGoalVersions.Contains(version, StringComparer.OrdinalIgnoreCase);
                },
                $"The execution goal provided failed schema validation. The version provided is not a supported version. {Environment.NewLine}" +
                $"Version provided: {version} Supported versions: {ContractExtension.SupportedExecutionGoalVersions}");

            experiment.ThrowIfInvalid(
                nameof(experiment),                    
                (exp) =>
                {
                    if (GoalBasedScheduleExtensions.IsExecutionGoalVersion20200727(version))
                    {
                        return experiment == null ? true : false;
                    }

                    return experiment == null ? false : true;
                },
                $"Invalid {nameof(GoalBasedSchedule.Version)}. " +
                $"{nameof(GoalBasedSchedule.Experiment)} is only supported by the latest {nameof(GoalBasedSchedule.Version)} : 2021-01-01");

            // Add additional filter for target goal providers
            targetGoals.ThrowIfNullOrEmpty(nameof(targetGoals));
            controlGoals.ThrowIfNullOrEmpty(nameof(controlGoals));

            this.Name = name;
            this.TeamName = teamName;
            this.Description = description;
            this.Enabled = (bool)enabled;
            this.Version = version;
            this.ExperimentName = experimentName;
            this.ExecutionGoalId = executionGoalId;
            this.Experiment = experiment;

            this.TargetGoals = new List<Goal>(targetGoals);
            this.ControlGoals = new List<Goal>(controlGoals);

            if (parameters?.Any() == true)
            {
                this.Parameters = new Dictionary<string, IConvertible>(parameters, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                this.Parameters = new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase);
            }

            if (metaData?.Any() == true)
            {
                this.ScheduleMetadata = new Dictionary<string, IConvertible>(metaData, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                this.ScheduleMetadata = new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase);
            }
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
                  other?.ExecutionGoalId,
                  other?.Name,
                  other?.TeamName,
                  other?.Description,
                  other?.ScheduleMetadata,
                  other?.Enabled,
                  other?.Version,
                  other?.Experiment,
                  other?.TargetGoals,
                  other?.ControlGoals,
                  other?.Parameters)
        {
        }

        /// <summary>
        /// Name of Experiment Template
        /// </summary>
        [JsonProperty(PropertyName = "experimentName", Required = Required.Always)]
        public string ExperimentName { get; }

        /// <summary>
        /// The Id of the Experiment Goal ID
        /// </summary>
        [JsonProperty(PropertyName = "executionGoalId", Required = Required.Always)]
        public string ExecutionGoalId { get; }

        /// <summary>
        /// Name of the schedule
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; }

        /// <summary>
        /// Team that owns the Execution Goal
        /// </summary>
        [JsonProperty(PropertyName = "teamName", Required = Required.Always)]
        public string TeamName { get; }

        /// <summary>
        /// Description of the schedule
        /// </summary>
        [JsonProperty(PropertyName = "description", Required = Required.Always)]
        public string Description { get; }

        /// <summary>
        /// Metadata for the schedule. It will include information like the teamName etc.
        /// </summary>
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        [JsonProperty(PropertyName = "metadata", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, IConvertible> ScheduleMetadata { get; }

        /// <summary>
        /// Indicates whether a schedule is enabled
        /// </summary>
        [JsonProperty(PropertyName = "enabled", Required = Required.Always)]
        public bool Enabled { get; }

        /// <summary>
        /// Indicates the version of the schedule
        /// </summary>
        [JsonProperty(PropertyName = "version", Required = Required.Always)]
        public string Version { get; }

        /// <summary>
        /// Represents an experiment definition
        /// </summary>
        [JsonProperty(PropertyName = "experiment", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public Experiment Experiment { get; }

        /// <summary>
        /// List of Target Goals
        /// </summary>
        [JsonProperty(PropertyName = "targetGoals", Required = Required.Always)]
        public List<Goal> TargetGoals { get; }

        /// <summary>
        /// List of Control Goals
        /// </summary>
        [JsonProperty(PropertyName = "controlGoals", Required = Required.Always)]
        public List<Goal> ControlGoals { get; }

        /// <summary>
        /// Experiment parameters that are overridden in the schedule
        /// </summary>
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        [JsonProperty(PropertyName = "parameters", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, IConvertible> Parameters { get; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            bool areEqual = false;

            if (object.ReferenceEquals(this, obj))
            {
                areEqual = true;
            }
            else 
            {
                GoalBasedSchedule itemDescription = obj as GoalBasedSchedule;
                if (itemDescription != null)
                {
                    areEqual = this.Equals(itemDescription);
                }
            }

            return areEqual;
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
                    .AppendProperties(this.Name, this.TeamName, this.Version, this.ExperimentName, this.ExecutionGoalId)
                    .AppendParameters(this.ScheduleMetadata)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
