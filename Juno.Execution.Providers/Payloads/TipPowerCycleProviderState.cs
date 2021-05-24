namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Describes the power cycle state of a tip node
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Need to modify VMs.")]
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class TipNodePowerCycleProviderState
    {
        /// <summary>
        /// Describes the rest health status for the tip node change
        /// request.
        /// </summary>
        public TipChangeState ResetHealthState { get; set; }

        /// <summary>
        /// Describes the power cycle status for the tip node change
        /// request.
        /// </summary>
        public TipChangeState PowerCycleState { get; set; }

        /// <summary>
        /// Describes whether the power cycle is completed and verified
        /// </summary>
        public bool PowerCycleCompleted { get; set; }

        /// <summary>
        /// Keeps a count of the power cycle attempts
        /// </summary>
        public int PowerCycleAttempt { get; set; }

        /// <summary>
        /// Timeout for the step
        /// </summary>
        public DateTime StepTimeout { get; set; }

        /// <summary>
        /// Time for the latest power cycle retry
        /// </summary>
        public DateTime? LastRetryTime { get; set; }

        /// <summary>
        /// Resets the node health and power cycle
        /// </summary>
        public void ResetPowerCycleWorkFlow()
        {
            this.ResetHealthState.RequestInitiated = false;
            this.ResetHealthState.RequestCompleted = false;
            this.PowerCycleState.RequestInitiated = false;
            this.PowerCycleState.RequestCompleted = false;
            this.PowerCycleCompleted = false;
        }

    }

    /// <summary>
    /// Describes the status of a tip node change request such as
    /// power cycle a node or reset health
    /// </summary>
    public class TipChangeState
    {
        /// <summary>
        /// Has the request to TiP been initiated
        /// </summary>
        public bool RequestInitiated { get; set; }

        /// <summary>
        /// Has the request been completed by TiP
        /// </summary>
        public bool RequestCompleted { get; set; }

        /// <summary>
        /// The name of the physical node for which the TiP session is established.
        /// </summary>
        public string TipNodeId { get; set; }

        /// <summary>
        /// The TiP session ID associated with the physical node(s) isolated through
        /// the TiP service.
        /// </summary>
        public string TipNodeSessionId { get; set; }

        /// <summary>
        /// The TipNode request ID associated with the change request to deploy
        /// the microcode update. This is used to track the status of the request
        /// as the TiP service attempts to hand-off to the PilotFish service.
        /// </summary>
        public string TipNodeSessionChangeId { get; set; }
    }
}
