namespace Juno.EnvironmentSelection.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.EnvironmentSelection.Service;
    using Juno.Extensions.AspNetCore;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Juno Execution Rest API controller for notifying 
    /// the environment selection service of reserved nodes.
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    [Route("/api/environments/reservedNodes")]
    public class NodeReservationController : ControllerBase
    {
        /// <summary>
        /// The name of the Api
        /// </summary>
        public const string ApiName = "EnvironmentReservedNodesApi";
        private const string V1 = "v1";
        private const string DefaultValue = "*";
        private static readonly TimeSpan ReservationDuration = TimeSpan.FromHours(2);

        /// <summary>
        /// Initializes a new instance of <see cref="NodeReservationController"/>
        /// </summary>
        /// <param name="service"></param>
        /// <param name="logger"></param>
        public NodeReservationController(IEnvironmentSelectionService service, ILogger logger = null)
        {
            service.ThrowIfNull(nameof(service));

            this.Service = service;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// The selection service that selects aspects of an environment
        /// </summary>
        protected IEnvironmentSelectionService Service { get; }

        /// <summary>
        /// Logger used for capturing telemetry
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Informs ESS to add the input list of nodes to its reserved list.
        /// </summary>
        /// <param name="reservedNodes">List of nodes to be marked as used.</param>
        /// <param name="cancellationToken">Token used for cancelling current thread of execution.</param>
        [HttpPost]
        [Consumes("application/json")]
        [ApiExplorerSettings(GroupName = NodeReservationController.V1)]
        [ProducesResponseType(typeof(IEnumerable<EnvironmentCandidate>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateReservedNodesAsync([FromBody] ReservedNodes reservedNodes, CancellationToken cancellationToken)
        {
            reservedNodes.ThrowIfNull(nameof(reservedNodes));

            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
                .AddContext(nameof(reservedNodes), reservedNodes);

            return await this.ExecuteApiOperationAsync(EventNames.CreateReservedNodes, telemetryContext, this.Logger, async () =>
            {
                if (reservedNodes.Nodes.Any(node => node.NodeId.Equals(NodeReservationController.DefaultValue, StringComparison.OrdinalIgnoreCase) 
                    || node.ClusterId.Equals(NodeReservationController.DefaultValue, StringComparison.OrdinalIgnoreCase))) 
                {
                    return this.BadRequest($"{nameof(EnvironmentCandidate.NodeId)} and {nameof(EnvironmentCandidate.ClusterId)} must be non-default values to reserve successfully.");
                }

                IEnumerable<EnvironmentCandidate> nodesSuccessfullyReserved = await this.Service.ReserveEnvironmentCandidatesAsync(reservedNodes.Nodes, NodeReservationController.ReservationDuration, cancellationToken).ConfigureDefaults();
                
                telemetryContext.AddContext(nameof(nodesSuccessfullyReserved), nodesSuccessfullyReserved);

                return this.Ok(nodesSuccessfullyReserved);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Informs ESS to remove the input list of nodes from its reserved list.
        /// </summary>
        /// <param name="reservedNodes">List of nodes to be marked as unused.</param>
        /// <param name="cancellationToken">Token used for cancelling current thread of execution.</param>
        [HttpDelete]
        [ApiExplorerSettings(GroupName = NodeReservationController.V1)]
        [ProducesResponseType(typeof(IEnumerable<EnvironmentCandidate>), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteReservedNodesAsync([FromBody] ReservedNodes reservedNodes, CancellationToken cancellationToken)
        {
            reservedNodes.ThrowIfNull(nameof(reservedNodes));

            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
                .AddContext(nameof(reservedNodes), reservedNodes);

            return await this.ExecuteApiOperationAsync(EventNames.DeleteReservedNodes, telemetryContext, this.Logger, async () =>
            {
                if (reservedNodes.Nodes.Any(node => node.NodeId.Equals(NodeReservationController.DefaultValue, StringComparison.OrdinalIgnoreCase)
                    || node.ClusterId.Equals(NodeReservationController.DefaultValue, StringComparison.OrdinalIgnoreCase)))
                {
                    return this.BadRequest($"{nameof(EnvironmentCandidate.NodeId)} and {nameof(EnvironmentCandidate.ClusterId)} must be non-default values to delete reservation successfully.");
                }

                IEnumerable<EnvironmentCandidate> deletedNodes = await this.Service.DeleteReservationsAsync(reservedNodes.Nodes, cancellationToken).ConfigureDefaults();
                telemetryContext.AddContext(nameof(deletedNodes), deletedNodes);

                return this.Accepted(deletedNodes);
            }).ConfigureDefaults();
        }

        private static class EventNames
        {
            public static readonly string CreateReservedNodes = EventContext.GetEventName(NodeReservationController.ApiName, "CreateReservedNodes");
            public static readonly string DeleteReservedNodes = EventContext.GetEventName(NodeReservationController.ApiName, "DeleteReservedNodes");
        }
    }
}
