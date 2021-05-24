namespace Juno.EnvironmentSelection.Api
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.EnvironmentSelection.Service;
    using Juno.Extensions.AspNetCore;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
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
        /// <param name="service"></param>
        /// <param name="logger">Logger used to capture telemetry</param>
        public EnvironmentSelectionController(IEnvironmentSelectionService service, ILogger logger = null)
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
        /// Returns up to 2 EnvironmentCandidates that fit the environment filter criteria.
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
                IEnumerable<EnvironmentCandidate> candidates;
                try
                {
                    candidates = await this.Service.GetEnvironmentCandidatesAsync(query, cancellationToken).ConfigureDefaults();
                    await this.Service.ReserveEnvironmentCandidatesAsync(candidates, TimeSpan.FromMinutes(15), cancellationToken).ConfigureDefaults();
                }

                // This method call reserves the EnvironmentSelectionException exclusively for a query that asks for a specific resource,
                // but was unable to find it.
                catch (EnvironmentSelectionException exc)
                {
                    return this.DataNotFound(exc.Message);
                }

                // Validation errors occured during runtime
                catch (SchemaException exc)
                {
                    return this.DataSchemaInvalid(exc.Message);
                }

                // A provider was given that doesnt exist in the app domain.
                catch (TypeLoadException exc)
                {
                    return this.DataSchemaInvalid(exc.Message);
                }

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
