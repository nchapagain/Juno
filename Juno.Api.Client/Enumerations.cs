namespace Juno.Api.Client
{
    /// <summary>
    /// Represents the different type of Juno API target endpoints.
    /// </summary>
    public enum ApiClientType
    {
        /// <summary>
        /// The Agent API/service.
        /// </summary>
        AgentApi,

        /// <summary>
        /// The Agent Heartbeat API/service.
        /// </summary>
        AgentHeartbeatApi,

        /// <summary>
        /// The Agent File Upload API/service.
        /// </summary>
        AgentFileUploadApi,

        /// <summary>
        /// The Execution API/service.
        /// </summary>
        ExecutionApi,

        /// <summary>
        /// The Experiments API/service.
        /// </summary>
        ExperimentsApi
    }
}
