namespace Juno.Execution.TipIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using TipGateway.Entities;
    using TipGateway.FabricApi.Requests;

    /// <summary>
    /// Interface to interact with TIP service.
    /// </summary>
    public interface ITipClient
    {
        /// <summary>
        /// Apply pilotfish on existing TIP session.
        /// </summary>
        /// <param name="tipSessionId">Tip session.</param>
        /// <param name="pilotfishServices">Pilot fish services to apply.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        Task<TipNodeSessionChange> ApplyPilotFishServicesAsync(string tipSessionId, List<KeyValuePair<string, string>> pilotfishServices, CancellationToken cancellationToken);

        /// <summary>
        /// Apply pilotfish on existing TIP session which resides on SoC hardware.
        /// </summary>
        /// <param name="tipSessionId">Tip session.</param>
        /// <param name="pilotfishServices">Pilot fish services to apply.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An awaitable task containg the change details of the request.</returns>
        Task<TipNodeSessionChange> ApplyPilotFishServicesOnSocAsync(string tipSessionId, List<KeyValuePair<string, string>> pilotfishServices, CancellationToken cancellationToken);

        /// <summary>
        /// Deploy hosting environment on existing TIP session.
        /// </summary>
        /// <param name="tipSessionId">Tip session.</param>
        /// <param name="hostingEnvironments">HE component to apply.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        Task<TipNodeSessionChange> DeployHostingEnvironmentAsync(string tipSessionId, List<HostingEnvironmentLineItem> hostingEnvironments, CancellationToken cancellationToken);

        /// <summary>
        /// Create a Tip session.
        /// </summary>
        /// <param name="tipParameters">Parameters for Tip session.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tip session id.</returns>
        Task<TipNodeSessionChange> CreateTipSessionAsync(TipParameters tipParameters, CancellationToken cancellationToken);

        /// <summary>
        /// Get all Tip Session associated with given Application ID
        /// </summary>
        /// <param name="appPrincipleId">Application Id associated with compute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Tip Session List</returns>
        Task<IEnumerable<TipNodeSession>> GetTipSessionsByAppIdAsync(string appPrincipleId, CancellationToken cancellationToken);

        /// <summary>
        /// Create a Tip session.
        /// </summary>
        /// <param name="clusterName">Name of cluster.</param>
        /// <param name="region">Region of the cluster.</param>
        /// <param name="durationInMinutes">Duration of Tip</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="nodeIdList">List of candicate node ids.</param>
        /// <param name="details">Arbitrary string information for tagging.</param>
        /// <param name="autopilotServices">Pilot fish services</param>
        /// <param name="allowAmberNodes">Specifies whether amber nodes are allowed</param>
        /// <returns>Tip session id</returns>
        Task<TipNodeSessionChange> CreateTipSessionAsync(
            string clusterName,
            string region,
            int durationInMinutes,
            CancellationToken cancellationToken,
            List<string> nodeIdList = null,
            string details = null,
            List<KeyValuePair<string, string>> autopilotServices = null,
            bool allowAmberNodes = false);

        /// <summary>
        /// Delete a Tip session
        /// </summary>
        /// <param name="tipSessionId">The tip session id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The tip session change id</returns>
        Task<TipNodeSessionChange> DeleteTipSessionAsync(string tipSessionId, CancellationToken cancellationToken);

        /// <summary>
        /// Extend Tip session duration.
        /// </summary>
        /// <param name="tipSessionId">Tip session Id.</param>
        /// <param name="extendExpirationTimeInMinutes">Extend expiration time in minutes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        Task<TipNodeSessionChange> ExtendTipSessionAsync(string tipSessionId, int extendExpirationTimeInMinutes, CancellationToken cancellationToken);

        /// <summary>
        /// Get a TIP session detail.
        /// </summary>
        /// <param name="tipSessionId">The tip session id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The tip session detail</returns>
        Task<TipNodeSession> GetTipSessionAsync(string tipSessionId, CancellationToken cancellationToken);

        /// <summary>
        /// Get a TIP session change detail.
        /// </summary>
        /// <param name="tipSessionId">The tip session id.</param>
        /// <param name="tipSessionChangeId">The tip session change id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The tip session change detail.</returns>
        Task<TipNodeSessionChangeDetails> GetTipSessionChangeAsync(string tipSessionId, string tipSessionChangeId, CancellationToken cancellationToken);

        /// <summary>
        /// Check if a tip session is created successfully.
        /// </summary>
        /// <param name="tipSessionId">Tip session id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Whether tip session is created.</returns>
        Task<bool> IsTipSessionCreatedAsync(string tipSessionId, CancellationToken cancellationToken);

        /// <summary>
        /// Check if a tip sessions are created successfully.
        /// </summary>
        /// <param name="tipSessionIds">Tip session id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Whether tip session is created.</returns>
        Task<IDictionary<string, bool>> IsTipSessionCreatedAsync(IEnumerable<string> tipSessionIds, CancellationToken cancellationToken);

        /// <summary>
        /// Execute node command via fabric api.
        /// </summary>
        /// <param name="tipSessionId">Tip session id.</param>
        /// <param name="nodeId">Node id.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="timeout">Timeout for the command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The tip session change detail.</returns>
        Task<TipNodeSessionChange> ExecuteNodeCommandAsync(string tipSessionId, string nodeId, string command, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Resets a node's health status
        /// </summary>
        /// <param name="tipSessionId">Tip session id.</param>
        /// <param name="nodeId">Node id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The tip session change detail.</returns>
        Task<TipNodeSessionChange> ResetNodeHealthAsync(string tipSessionId, string nodeId, CancellationToken cancellationToken);

        /// <summary>
        /// Makes a Fabric API request to force the TiP node state specified (Supported states = Raw, Excluded). Excluded state ensures
        /// that the node is excluded from any automatic healing operations by the the Fabric Anvil service.
        /// </summary>
        /// <param name="tipSessionId">The ID of the TiP session.</param>
        /// <param name="tipNodeId">The ID of the TiP node.</param>
        /// <param name="nodeState">The node state (e.g. Raw, Excluded).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The session change details.</returns>
        Task<TipNodeSessionChange> SetNodeStateAsync(string tipSessionId, string tipNodeId, NodeState nodeState, CancellationToken cancellationToken);

        /// <summary>
        /// Sets a node's power state i.e. powerOn, powerOff, powerCycle
        /// </summary>
        /// <param name="tipSessionId">Tip session id.</param>
        /// <param name="nodeId">Node id.</param>
        /// <param name="powerAction">The power action to use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The tip session change detail.</returns>
        Task<TipNodeSessionChange> SetNodePowerStateAsync(string tipSessionId, string nodeId, PowerAction powerAction, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the real time node status from Fabric
        /// </summary>
        /// <param name="tipSessionId">Tip session id.</param>
        /// <param name="nodeId">Node id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The tip session change detail.</returns>
        Task<TipNodeSessionChange> GetNodeStatusAsync(string tipSessionId, string nodeId, CancellationToken cancellationToken);

        /// <summary>
        /// Check if a tip session change is failed.
        /// </summary>
        /// <param name="tipSessionId">Tip session id.</param>
        /// <param name="tipSessionChangeId">Tip session change id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Whether tip session is created.</returns>
        Task<bool> IsTipSessionChangeFailedAsync(string tipSessionId, string tipSessionChangeId, CancellationToken cancellationToken);
    }
}