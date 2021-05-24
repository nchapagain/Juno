namespace Juno.Experiments.Api
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Extensions.AspNetCore;
    using Juno.Extensions.Telemetry;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Juno Execution REST API controller for directing access to 
    /// the Environment Selection Service.
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    [Route("/api/environments")]
    public partial class EnvironmentSelectionController : ControllerBase
    {
        /// <summary>
        /// The name of the Api
        /// </summary>
        public const string ApiName = "EnvironmentSelectionApi";
        private const string V1 = "v1";

        /// <summary>
        /// Initializes a new instance of <see cref="EnvironmentSelectionController"/>
        /// </summary>
        /// <param name="client">Client used to send requests to API</param>
        /// <param name="logger">Logger used to capture telemetry</param>
        public EnvironmentSelectionController(IEnvironmentClient client, ILogger logger = null)
        {
            client.ThrowIfNull(nameof(client));

            this.Client = client;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// The selection service that selects aspects of an environment
        /// </summary>
        protected IEnvironmentClient Client { get; }

        /// <summary>
        /// Logger used for capturing telemetry
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Returns up to 2 nodes that fit the environment filter criteria.
        /// </summary>
        [HttpPost]
        [Consumes("application/json")]
        [ApiExplorerSettings(GroupName = EnvironmentSelectionController.V1)]
        [ProducesResponseType(typeof(IEnumerable<EnvironmentCandidate>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReserveEnvironmentsAsync([FromBody] EnvironmentQuery query, CancellationToken cancellationToken)
        {
            query.ThrowIfNull(nameof(query));

            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
                .AddContext(nameof(query), query);

            return await this.ExecuteApiOperationAsync(EventNames.GetEnvironments, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.ReserveEnvironmentsAsync(query, cancellationToken).ConfigureDefaults();
                if (!response.IsSuccessStatusCode)
                {
                    return new ObjectResult(await response.Content.ReadAsJsonAsync<ProblemDetails>().ConfigureDefaults())
                    {
                        StatusCode = (int)response.StatusCode
                    };
                }

                telemetryContext.AddContext(response);

                IEnumerable<EnvironmentCandidate> candidates = await response.Content.ReadAsJsonAsync<IEnumerable<EnvironmentCandidate>>()
                    .ConfigureDefaults();

                telemetryContext.AddContext(nameof(candidates), candidates);

                return this.Ok(candidates);
            }).ConfigureDefaults();
        }

        private static class EventNames
        {
            public static readonly string GetEnvironments = EventContext.GetEventName(EnvironmentSelectionController.ApiName, "GetEnvironments");
        }
    }
}
