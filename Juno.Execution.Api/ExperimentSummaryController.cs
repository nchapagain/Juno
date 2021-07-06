namespace Juno.Execution.Api
{
    using System;
    using System.Collections.Generic;
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
    /// Juno execution REST API controller for managing experiment data
    /// in the system.
    /// </summary>
    /// <remarks>
    /// Introduction to ASP.NET Core
    /// https://docs.microsoft.com/en-us/aspnet/core/?view=aspnetcore-2.1
    ///
    /// ASP.NET Core MVC Controllers
    /// https://docs.microsoft.com/en-us/aspnet/core/mvc/controllers/actions?view=aspnetcore-2.1
    ///
    /// Kestrel Web Server (Self-Hosting)
    /// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-2.1
    ///
    /// Async/Await/ConfigureAwait Overview
    /// https://www.skylinetechnologies.com/Blog/Skyline-Blog/December_2018/async-await-configureawait
    /// </remarks>
    [ApiController]
    [Produces("application/json")]
    [Route("/api/experimentSummary")]
    public partial class ExperimentSummaryController : ControllerBase
    {
        /// <summary>
        /// The name of the API.
        /// </summary>
        public const string ApiName = "ExecutionApi";

        private const string V1 = "v1";

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentSummaryController"/> class.
        /// </summary>
        /// <param name="analysisCacheManager">The data manager for analysis cache data.</param>
        /// <param name="logger">The trace/telemetry logger for the controller.</param>
        public ExperimentSummaryController(IAnalysisCacheManager analysisCacheManager, ILogger logger = null)
        {
            analysisCacheManager.ThrowIfNull(nameof(analysisCacheManager), $"A analysis cache manager must be provided to the controller.");

            this.AnalysisCacheManager = analysisCacheManager;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the analysis cache manager using which the controller can read data from analysis cache.
        /// </summary>
        protected IAnalysisCacheManager AnalysisCacheManager { get; }

        /// <summary>
        /// Gets the trace/telemetry logger for the controller.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets summaries of all the experiments in the system.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK.</response>
        /// <response code="404">Not Found.</response>
        /// <response code="500">Internal Server Error.</response>
        [HttpGet("")]
        [Consumes("application/json")]
        [Description("Gets summaries of all the experiments in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentSummaryController.V1)]
        [ProducesResponseType(typeof(IEnumerable<ExperimentSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentSummaryAsync(CancellationToken cancellationToken)
        {
            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid());

            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentSummary, telemetryContext, this.Logger, async () =>
            {
                IEnumerable<BusinessSignal> businessSignals = await this.AnalysisCacheManager.GetBusinessSignalsAsync(
                    Resources.Queries.BusinessSignalsQuery, cancellationToken)
                    .ConfigureDefaults();

                IEnumerable<ExperimentProgress> progresses = await this.AnalysisCacheManager.GetExperimentsProgressAsync(
                    Resources.Queries.ExperimentsProgress, cancellationToken)
                    .ConfigureDefaults();

                IEnumerable<ExperimentSummary> summaries = ExperimentSummaryExtensions.DeriveExperimentSummary(
                    businessSignals, progresses);

                telemetryContext.AddContext(summaries);

                return this.Ok(summaries);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Provides the names of telemetry events emitted by the controller.
        /// </summary>
        private static class EventNames
        {
            public static readonly string GetExperimentSummary = EventContext.GetEventName(ExperimentSummaryController.ApiName, "GetExperimentSummary");
        }
    }
}