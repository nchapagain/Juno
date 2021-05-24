namespace Juno.Contracts
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents a single unique step within an experiment
    /// (e.g. environment
    /// </summary>
    [DebuggerDisplay("{Sequence}: {Id}")]
    public class ExperimentStepInstance : Item<ExperimentComponent>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentStepInstance"/> class.
        /// </summary>
        /// <param name="id">The unique ID of the experiment step.</param>
        /// <param name="experimentId">The ID of the experiment with which the steps is associated.</param>
        /// <param name="experimentGroup">The experiment group for which the step is targeted. Use a wildcard '*' if the step is targeted for all groups.</param>
        /// <param name="stepType">The type of provider that handles the step.</param>
        /// <param name="status">The status of the step (e.g. Pending, Succeeded, Failed).</param>
        /// <param name="sequence">The sequence in which the step should be executed in relation to other steps.</param>
        /// <param name="attempts">The number of attempts that have been made to complete the step.</param>
        /// <param name="definition">The experiment step/component definition itself.</param>
        /// <param name="startTime">The time the step execution started.</param>
        /// <param name="endTime">The time the step execution ended.</param>
        /// <param name="agentId">The unique ID of the agent for which the step if it is targeted for an individual agent.</param>
        /// <param name="parentStepId">Parent stepid if it is targeted for an individual agent.</param>
        public ExperimentStepInstance(
            string id,
            string experimentId,
            string experimentGroup,
            SupportedStepType stepType,
            ExecutionStatus status,
            int sequence,
            int attempts,
            ExperimentComponent definition,
            DateTime? startTime = null,
            DateTime? endTime = null,
            string agentId = null,
            string parentStepId = null)
            : this(id, experimentId, experimentGroup, stepType, status, sequence, attempts, definition, DateTime.UtcNow, DateTime.UtcNow, startTime, endTime, agentId, parentStepId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentStepInstance"/> class.
        /// </summary>
        /// <param name="id">The unique ID of the experiment step.</param>
        /// <param name="experimentId">The ID of the experiment with which the steps is associated.</param>
        /// <param name="experimentGroup">The experiment group for which the step is targeted. Use a wildcard '*' if the step is targeted for all groups.</param>
        /// <param name="stepType">The type of provider that handles the step.</param>
        /// <param name="status">The status of the step (e.g. Pending, Succeeded, Failed).</param>
        /// <param name="sequence">The sequence in which the step should be executed in relation to other steps.</param>
        /// <param name="attempts">The number of attempts that have been made to complete the step.</param>
        /// <param name="definition">The experiment step/component definition itself.</param>
        /// <param name="created">The date/time at which the experiment step was created.</param>
        /// <param name="lastModified">The date/time at which the experiment step was last modified.</param>
        /// <param name="startTime">The time the step execution started.</param>
        /// <param name="endTime">The time the step execution ended.</param>
        /// <param name="agentId">The unique ID of the agent for which the step if it is targeted for an individual agent.</param>
        /// <param name="parentStepId">Parent stepid if it is targeted for an individual agent.</param>
        [JsonConstructor]
        public ExperimentStepInstance(
            string id,
            string experimentId,
            string experimentGroup,
            SupportedStepType stepType,
            ExecutionStatus status,
            int sequence,
            int attempts,
            ExperimentComponent definition,
            DateTime created,
            DateTime lastModified,
            DateTime? startTime = null,
            DateTime? endTime = null,
            string agentId = null,
            string parentStepId = null)
            : base(id, created, lastModified, definition)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            experimentGroup.ThrowIfNullOrWhiteSpace(nameof(experimentGroup));
            sequence.ThrowIfInvalid(nameof(sequence), (param) => param >= 0);
            attempts.ThrowIfInvalid(nameof(attempts), (param) => param >= 0);
            definition.ThrowIfNull(nameof(definition));

            this.ExperimentId = experimentId;
            this.ExperimentGroup = experimentGroup;
            this.AgentId = agentId;
            this.ParentStepId = parentStepId;
            this.Sequence = sequence;
            this.StepType = stepType;
            this.Status = status;
            this.Attempts = attempts;
            this.StartTime = startTime;
            this.EndTime = endTime;
        }

        /// <summary>
        /// Gets the ID of the experiment for which the step
        /// is associated.
        /// </summary>
        [JsonProperty("experimentId", Required = Required.Always, Order = 10)]
        public string ExperimentId { get; }

        /// <summary>
        /// Gets the experiment group in which the step is targeted (e.g. Group A, Group B).
        /// A wildcard '*' indicates the step is targeted for all groups either explicitly
        /// or implicitly.
        /// </summary>
        [JsonProperty("experimentGroup", Required = Required.Always, Order = 11)]
        public string ExperimentGroup { get; }

        /// <summary>
        /// Gets the ID of the agent for which the steps if it is targeted for a 
        /// specific agent.
        /// </summary>
        [JsonProperty("agentId", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 12)]
        public string AgentId { get; }

        /// <summary>
        /// Gets the ID of the parent step
        /// </summary>
        [JsonProperty("parentStepId", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 13)]
        public string ParentStepId { get; }

        /// <summary>
        /// Gets the type of provider that handles the execution of the
        /// step (e.g. EnvironmentCleanup, EnvironmentSetup, Workload).
        /// </summary>
        [JsonProperty("stepType", Required = Required.Always, Order = 14)]
        [JsonConverter(typeof(StringEnumConverter))]
        public SupportedStepType StepType { get; }

        /// <summary>
        /// Gets or sets the current status of the step.
        /// </summary>
        [JsonProperty("status", Required = Required.Always, Order = 15)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ExecutionStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the sequence number.
        /// </summary>
        [JsonProperty("sequence", Required = Required.Always, Order = 16)]
        public int Sequence { get; set; }

        /// <summary>
        /// Gets or sets the total attempts to process this step so far.
        /// </summary>
        [JsonProperty("attempts", Required = Required.Always, Order = 17)]
        public int Attempts { get; set; }

        /// <summary>
        /// Gets or sets the step start time in UTC.
        /// </summary>
        [JsonProperty("startTime", Required = Required.Default, NullValueHandling = NullValueHandling.Include, Order = 18)]
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the step end time in UTC.
        /// </summary>
        [JsonProperty("endTime", Required = Required.Default, NullValueHandling = NullValueHandling.Include, Order = 19)]
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Override method returns a unique integer hash code
        /// identifier for the class instance
        /// </summary>
        /// <returns>
        /// Type:  System.Int32
        /// A unique integer identifier for the class instance
        /// </returns>
        public override int GetHashCode()
        {
            return new StringBuilder()
                .AppendProperties(
                    this.AgentId,
                    this.Attempts,
                    this.Created,
                    this.EndTime,
                    this.ExperimentId,
                    this.ExperimentGroup,
                    this.Id,
                    this.LastModified,
                    this.ParentStepId,
                    this.Sequence,
                    this.StartTime,
                    this.Status.ToString(),
                    this.StepType.ToString())
                .AppendComponent(this.Definition)
                .AppendExtensions(this.Extensions)
                .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
        }
    }
}
