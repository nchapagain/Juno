namespace Juno.DataManagement
{
    using System;
    using Microsoft.Azure.CRC.Repository.Cosmos;

    /// <summary>
    /// Represents a Target Goal entity
    /// </summary>
    public class TargetGoalTableEntity : ItemEntity
    {
        /// <summary>
        /// Instantiates a <see cref="TargetGoalTableEntity"/>
        /// </summary>
        public TargetGoalTableEntity()
        {
            this.Created = DateTime.UtcNow;
            this.Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Timer Trigger for the Target Goal
        /// </summary>
        public string CronExpression { get; set; }

        /// <summary>
        /// Whether or not the Target Goal is enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// The Name of the Experiment that the target goal resides
        /// </summary>
        public string ExperimentName { get; set; }

        /// <summary>
        /// Team name that authored Target goalS
        /// </summary>
        public string TeamName { get; set; }
        
        /// <summary>
        /// Version of Schedule
        /// </summary>
        public string ExecutionGoal { get; set; }
    }
}
