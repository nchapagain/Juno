namespace Juno.DataManagement.Cosmos
{
    using System;
    using Microsoft.Azure.CRC.Repository.Cosmos;

    /// <summary>
    /// Represents an experiment environment or workflow step.
    /// </summary>
    public class ExperimentStepTableEntity : ItemEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentStepTableEntity"/> class.
        /// </summary>
        public ExperimentStepTableEntity()
        {
            this.Attempts = 0;
            this.Created = DateTime.UtcNow;
            this.Sequence = 0;
            this.Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets or sets the ID of the experiment for which the step
        /// is associated.
        /// </summary>
        public string ExperimentId { get; set; }

        /// <summary>
        /// Gets or sets the unique ID of the parent step. 
       /// </summary>
        public string ParentStepId { get; set; }

        /// <summary>
        /// Gets or sets the experiment group for which the step is targeted.
        /// A wildcard '*' means the step is targeted for all groups.
        /// </summary>
        public string ExperimentGroup { get; set; }

        /// <summary>
        /// Gets or sets the ID associated with an agent for which the step
        /// is targeted (e.g. a specific Host or Guest agent).
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Gets or sets the workflow step name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the type of provider that handles the execution of the
        /// step (e.g. EnvironmentCleanup, EnvironmentSetup, Workload).
        /// </summary>
        public string StepType { get; set; }

        /// <summary>
        /// Gets or sets the current status of the step.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the sequence number.
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// Gets or sets the total attempts to process this workflow step so far.
        /// </summary>
        public int Attempts { get; set; }

        /// <summary>
        /// Gets or sets the step start time in UTC.
        /// </summary>
        public string StartTime { get; set; }

        /// <summary>
        /// Gets or sets the step end time in UTC.
        /// </summary>
        public string EndTime { get; set; }

        /// <summary>
        /// Gets or sets the steps definition/component (JSON-formatted).
        /// </summary>
        public string Definition { get; set; }

        /// <summary>
        /// Gets or sets any error details for the step.
        /// </summary>
        public string Error { get; set; }
    }
}