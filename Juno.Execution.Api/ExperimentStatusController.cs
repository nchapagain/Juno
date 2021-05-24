namespace Juno.Execution.Api
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text.RegularExpressions;
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
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Juno execution REST API controller for getting experiment status data
    /// from the system.
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
    [Route("/api/experimentstatus")]
    public partial class ExperimentStatusController : ControllerBase
    {
        /// <summary>
        /// The name of the API.
        /// </summary>
        public const string ApiName = "ExecutionApi";
        private const string V1 = "v1";

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentsController"/> class.
        /// </summary>
        /// <param name="dataManager">The data manager for experiment data, documents and steps.</param>
        /// <param name="logger">The trace/telemetry logger for the controller.</param>
        public ExperimentStatusController(IExperimentDataManager dataManager, ILogger logger = null)
        {
            dataManager.ThrowIfNull(nameof(dataManager), $"A data manager must be provided to the controller.");

            this.DataManager = dataManager;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the data layer component that provides data management functionality
        /// to the controller.
        /// </summary>
        protected IExperimentDataManager DataManager { get; }

        /// <summary>
        /// Gets the trace/telemetry logger for the controller.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets a set of status definitions for all experiment instances that are part of an end-to-end experiment
        /// </summary>
        /// <param name="experimentName">The unique name of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment instance was found in the system.</response>
        /// <response code="404">Not Found. The experiment instance was not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet("{experimentName}")]
        [Consumes("application/json")]
        [Description("Gets a set of summary definitions for all experiment instances that are part of an end-to-end experiment.")]
        [ApiExplorerSettings(GroupName = ExperimentStatusController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentInstanceStatusesAsync(string experimentName, CancellationToken cancellationToken)
        {
            experimentName.ThrowIfNullOrWhiteSpace(nameof(experimentName));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentName), experimentName);

            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentInstanceStatuses, telemetryContext, this.Logger, async () =>
            {
                // References:
                // https://docs.microsoft.com/en-us/azure/cosmos-db/sql-query-getting-started
                //
                // Example:
                // SELECT exp.id, exp.definition.name, exp.status FROM exp WHERE exp.definition.name = 'QoS_Windows'";

                string query = Regex.Replace(string.Format(Resources.Queries.ExperimentSummaryQuery, experimentName), "\r\n|\n", " ");
                IEnumerable<JObject> summaries = await this.DataManager.QueryExperimentsAsync(query, cancellationToken).ConfigureDefaults();

                IActionResult result = null;
                if (summaries?.Any() != true)
                {
                    result = this.NotFound($"An experiment with the name '{experimentName}' does not exist.");
                }
                else
                {
                    result = this.Ok(summaries) as IActionResult;
                }

                return result;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Provides the names of telemetry events emitted by the controller.
        /// </summary>
        private static class EventNames
        {
            public static readonly string GetExperimentInstanceStatuses = EventContext.GetEventName(ExperimentStatusController.ApiName, "GetExperimentInstanceStatuses");
        }
    }
}
