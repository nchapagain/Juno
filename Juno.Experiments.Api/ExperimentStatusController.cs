namespace Juno.Experiments.Api
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Juno.DataManagement;
    using Juno.Extensions.AspNetCore;
    using Juno.Extensions.Telemetry;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Juno execution REST API controller for querying experiment status data
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
    [Route("/api/experimentstatus")]
    public partial class ExperimentStatusController : ControllerBase
    {
        /// <summary>
        /// The name of the API.
        /// </summary>
        public const string ApiName = "ExperimentApi";
        private const string V1 = "v1";

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentsController"/> class.
        /// </summary>
        /// <param name="executionClient">The Juno Execution API client to use for communications with the Juno system.</param>
        /// <param name="configuration">Configuration settings for the environment.</param>
        /// <param name="logger">The trace/telemetry logger for the controller.</param>
        public ExperimentStatusController(ExecutionClient executionClient, IConfiguration configuration, ILogger logger = null)
        {
            executionClient.ThrowIfNull(nameof(executionClient), $"An execution client must be provided to the controller.");
            configuration.ThrowIfNull(nameof(configuration), "The environment configuration/configuration settings must be provided to the controller.");

            this.Client = executionClient;
            this.Configuration = configuration;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the data layer component that provides data management functionality
        /// to the controller.
        /// </summary>
        protected ExecutionClient Client { get; }

        /// <summary>
        /// Gets the configuration settings for the environment.
        /// </summary>
        protected IConfiguration Configuration { get; }

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
        [Description("Gets a set of status definitions for all experiment instances that are part of an end-to-end experiment.")]
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
                HttpResponseMessage response = await this.Client.GetExperimentInstanceStatusesAsync(experimentName, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);

                if (!response.IsSuccessStatusCode)
                {
                    return await ExperimentStatusController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                IEnumerable<JObject> results = (await response.Content.ReadAsStringAsync().ConfigureDefaults()).FromJson<IEnumerable<JObject>>();
                return this.Ok(results);
            }).ConfigureDefaults();
        }

        private static async Task<IActionResult> CreateErrorResponseAsync(HttpResponseMessage response)
        {
            ProblemDetails errorDetails = null;
            if (response.Content != null)
            {
                errorDetails = await response.Content.ReadAsJsonAsync<ProblemDetails>().ConfigureDefaults();
            }

            return new ObjectResult(errorDetails)
            {
                StatusCode = (int)response.StatusCode
            };
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
