namespace Juno.Scheduler.Preconditions
{
    using System;

    /// <summary>
    ///  Type safe settings class to hold Juno OFR settings
    /// </summary>
    internal class JunoOFRNode
    {
        /// <summary>
        /// Timestamp for tipNodeStartTime
        /// When Juno first interacted with the node
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// TipSessionId in which Juno caused OFR
        /// </summary>
        public string TipSessionId { get; set; }

        /// <summary>
        /// NodeId associated with TipSessionId
        /// </summary>
        public string NodeId { get; set; }

        /// <summary>
        /// Name of the Juno Experiment
        /// </summary>
        public string ExperimentName { get; set; }

        /// <summary>
        /// Id associated with Juno Experiment
        /// </summary>
        public string ExperimentId { get; set; }
    }
}
