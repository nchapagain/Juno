namespace Juno.Agent.Api
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Extensions.AspNetCore;
    using Juno.Extensions.Telemetry;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Agent controller for getting and storing agent hearbeat data
    /// </summary>
    [Route("/api/heartbeats")]
    [Produces("application/json")]
    [ApiController]
    public class AgentHeartbeatsController : ControllerBase
    {
        private const string V1 = "v1";

        private readonly IAgentHeartbeatManager agentHeartbeatManager;
        private readonly ILogger logger;

        /// <summary>
        /// Create new instance of <see cref="AgentHeartbeatsController"/>
        /// </summary>
        /// <param name="heartbeatManager">Agent hearbeat data manager</param>
        /// <param name="logger">The trace/telemetry logger for the controller.</param>
        public AgentHeartbeatsController(IAgentHeartbeatManager heartbeatManager, ILogger logger = null)
        {
            heartbeatManager.ThrowIfNull(nameof(heartbeatManager));
            this.logger = logger ?? NullLogger.Instance;
            this.agentHeartbeatManager = heartbeatManager;
        }

        /// <summary>
        /// Store agent hearbeat data in cosmos table
        /// </summary>
        /// <param name="agentHeartbeat">Agent heartbeat </param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <response code="201">Created. The agent heartbeat was created successfully.</response>
        /// <response code="400">Bad Request. The agentheart beat data is invalid.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        /// <returns></returns>
        [HttpPost]
        [Description("Creates agent heartbeat data")]
        [ApiExplorerSettings(GroupName = AgentHeartbeatsController.V1)]
        [ProducesResponseType(typeof(AgentHeartbeatInstance), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateHeartbeatAsync(AgentHeartbeat agentHeartbeat, CancellationToken cancellationToken)
        {
            agentHeartbeat.ThrowIfNull(nameof(agentHeartbeat));

            // Persist the activity ID so that it can be used down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(agentHeartbeat);

            return await this.ExecuteApiOperationAsync(EventNames.CreateHeartbeat, telemetryContext, this.logger, async () =>
            {
                var hearbeatInstance = await this.agentHeartbeatManager.CreateHeartbeatAsync(agentHeartbeat, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(hearbeatInstance);

                return this.CreatedAtAction(nameof(this.GetHeartbeatAsync), new { agentId = hearbeatInstance.AgentId }, hearbeatInstance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets the current status of the agent.
        /// </summary>
        /// <param name="agentId">The Id of an agent</param>
        /// <param name="cancellationToken"></param>
        /// <response code="200">Ok. The agent heartbeat data is found.</response>
        /// <response code="400">Bad Request. The agent id is invalid.</response>
        /// <response code="404">The agent id is not found.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        /// <returns></returns>
        [HttpGet]
        [Description("Gets the current status of the agent.")]
        [ApiExplorerSettings(GroupName = AgentHeartbeatsController.V1)]
        [ProducesResponseType(typeof(AgentHeartbeatInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetHeartbeatAsync([FromQuery] string agentId, CancellationToken cancellationToken)
        {
            agentId.ThrowIfNullOrWhiteSpace(agentId);

            // Persist the activity ID so that it can be used down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(agentId), agentId);

            return await this.ExecuteApiOperationAsync(EventNames.GetHeartbeat, telemetryContext, this.logger, async () =>
            {
                var heartbeatInstance = await this.agentHeartbeatManager.GetHeartbeatAsync(new AgentIdentification(agentId), cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(heartbeatInstance);

                return heartbeatInstance == null ? this.NotFound() : (IActionResult)this.Ok(heartbeatInstance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Provides the names of telemetry events emitted by the controller.
        /// </summary>
        private static class EventNames
        {
            internal static readonly string CreateHeartbeat = EventContext.GetEventName(AgentExperimentsController.ApiName, "CreateHeartbeat");
            internal static readonly string GetHeartbeat = EventContext.GetEventName(AgentExperimentsController.ApiName, "GetHeartbeat");
        }
    }
}
