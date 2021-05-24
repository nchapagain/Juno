namespace Juno.DataManagement.Cosmos
{
    using System;
    using Microsoft.Azure.CRC.Repository.Cosmos;

    /// <summary>
    /// Represents a mapping of an experiment to an agent that runs within the
    /// experiment.
    /// </summary>
    internal class ExperimentAgentTableEntity : ItemEntity
    {
        public ExperimentAgentTableEntity()
        {
            this.Created = DateTime.UtcNow;
            this.Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets or sets the ID associated with an agent for which the step
        /// is targeted (e.g. a specific Host or Guest agent).
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the experiment for which the step
        /// is associated.
        /// </summary>
        public string ExperimentId { get; set; }
    }
}