namespace Juno.Experiments.Api
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Juno.Extensions.AspNetCore;
    using Juno.Extensions.Telemetry;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Newtonsoft.Json;

    /// <summary>
    /// Juno experiments REST API controller for managing Execution Goal Templates
    /// in the system.
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    [Route("api/executionGoalTemplates")]
    public class ExecutionGoalTemplatesController : ControllerBase
    {
        /// <summary>
        /// The name of the API.
        /// </summary>
        public const string ApiName = "ExecutionGoalTemplateApi";
        private const string V1 = "v1";

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionGoalTemplatesController"/>
        /// </summary>
        /// <param name="executionClient">The Juno Execution API client to use for communications with the Juno system.</param>
        /// <param name="configuration">Configuration settings for the environment.</param>
        /// <param name="logger">The trace/telemetry logger for the controller.</param>
        public ExecutionGoalTemplatesController(ExecutionClient executionClient, IConfiguration configuration, ILogger logger = null)
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
        /// Creates a new execution goal template in the system
        /// </summary>
        /// <param name="executionGoalTemplate">The execution goal template item to place in the system</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        [HttpPost]
        [Consumes("application/json")]
        [Description("Creates a new execution goal in the system.")]
        [ApiExplorerSettings(GroupName = ExecutionGoalTemplatesController.V1)]
        [ProducesResponseType(typeof(GoalBasedSchedule), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> CreateExecutionGoalTemplateAsync([FromBody] Item<GoalBasedSchedule> executionGoalTemplate, CancellationToken cancellationToken)
        {
            executionGoalTemplate.ThrowIfNull(nameof(executionGoalTemplate));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
            .AddContext(nameof(executionGoalTemplate), executionGoalTemplate);

            return this.CreateExecutionGoalTemplateAsync(executionGoalTemplate, telemetryContext, cancellationToken);
        }

        /// <summary>
        /// Updates an existing execution goal template in the system.
        /// Team Name and ID must match.
        /// </summary>
        /// <param name="executionGoalTemplate">The Execution goal Template ID.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment instance was updated successfully.</response>
        /// <response code="400">Bad Request. The schema of the experiment is invalid.</response>
        /// <response code="404">Not Found. The experiment instance does not exist in the system.</response>
        /// <response code="412">Precondition Failed. The experiment instance provided has a mismatched eTag or partition key.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpPut]
        [Consumes("application/json")]
        [Description("Updates an existing experiment definition in the system.")]
        [ApiExplorerSettings(GroupName = ExecutionGoalTemplatesController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateExecutionGoalTemplateAsync([FromBody] Item<GoalBasedSchedule> executionGoalTemplate, CancellationToken cancellationToken)
        {
            executionGoalTemplate.ThrowIfNull(nameof(executionGoalTemplate));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(executionGoalTemplate), executionGoalTemplate);

            return await this.ExecuteApiOperationAsync(EventNames.UpdateExecutionGoalTemplate, telemetryContext, this.Logger, async () =>
            {
                ValidationResult validationResult = ExecutionGoalTemplateValidation.Instance.Validate(executionGoalTemplate.Definition);
                if (!validationResult.IsValid)
                {
                    throw new SchemaException(
                        $"The execution goal template provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                        $"{string.Join($"{Environment.NewLine} -", validationResult.ValidationErrors)}");
                }

                HttpResponseMessage response = null;

                response = await this.Client.UpdateExecutionGoalTemplateAsync(executionGoalTemplate, cancellationToken).ConfigureDefaults();
                telemetryContext.AddContext(response);
                if (!response.IsSuccessStatusCode)
                {
                    return await ExecutionGoalTemplatesController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                Item<GoalBasedSchedule> updatedInstance = await response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>()
                    .ConfigureDefaults();

                telemetryContext.AddContext(nameof(updatedInstance), updatedInstance);
                return this.Ok(updatedInstance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Deletes an Execution Goal Template from the System
        /// </summary>
        /// <param name="templateId">Unique Id of the Execution Goal</param>
        /// <param name="teamName">Name of the team that owns the Execution Goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <response code="204">No Content. The execution goal was deleted successfully.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpDelete("{templateId}")]
        [Description("Deletes an existing execution goal")]
        [ApiExplorerSettings(GroupName = ExecutionGoalTemplatesController.V1)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteExecutionGoalTemplateAsync(string templateId, [FromQuery] string teamName, CancellationToken cancellationToken)
        {
            templateId.ThrowIfNullOrWhiteSpace(nameof(templateId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(templateId), templateId)
                .AddContext(nameof(teamName), teamName);

            return await this.ExecuteApiOperationAsync(EventNames.DeleteExecutionGoalTemplate, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.DeleteExecutionGoalTemplateAsync(templateId, teamName, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);
                if (!response.IsSuccessStatusCode)
                {
                    return await ExecutionGoalTemplatesController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                return this.NoContent();
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets a Execution Goal Template from the system for the given teamName and templateId
        /// </summary>
        /// <param name="templateId">The unique ID of the execution goal template.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="teamName">Name of the team that owns the Execution Goal</param>
        /// <param name="view">The type of the view to be returned to the user</param>        
        /// <response code="200">OK. The experiment instance was found in the system.</response>
        /// <response code="404">Not Found. The experiment instance was not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet]
        [Consumes("application/json")]
        [Description("Lists execution goal template (or metadata)")]
        [ApiExplorerSettings(GroupName = ExecutionGoalTemplatesController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExecutionGoalTemplatesAsync(CancellationToken cancellationToken, [FromQuery] string teamName = null, [FromQuery] string templateId = null, [FromQuery] View view = View.Full)
        {
            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(teamName), teamName)
                .AddContext(nameof(templateId), templateId);

            string eventName = view == View.Full ?
                EventNames.GetExecutionGoalTemplate : EventNames.GetExecutionGoalMetadata;

            return await this.ExecuteApiOperationAsync(eventName, telemetryContext, this.Logger, async () =>
            {
                if (teamName == null && templateId != null)
                {
                    return this.BadRequest($"null teamName and non-null templateId combination is not supported.");
                }

                HttpResponseMessage response = await this.Client.GetExecutionGoalTemplatesAsync(cancellationToken, teamName, templateId, view)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);
                if (!response.IsSuccessStatusCode)
                {
                    return await ExecutionGoalTemplatesController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                if (view == View.Full)
                {
                    if (templateId == null)
                    {
                        IEnumerable<Item<GoalBasedSchedule>> executionGoalTemplates = await response.Content.ReadAsJsonAsync<IEnumerable<Item<GoalBasedSchedule>>>()
                            .ConfigureDefaults();

                        telemetryContext.AddContext(nameof(executionGoalTemplates), executionGoalTemplates);
                        return this.Ok(executionGoalTemplates);
                    }

                    Item<GoalBasedSchedule> executionGoalTemplate = await response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>()
                        .ConfigureDefaults();

                    telemetryContext.AddContext(nameof(executionGoalTemplate), executionGoalTemplate);
                    return this.Ok(executionGoalTemplate);

                }
                else
                {
                    if (templateId == null)
                    {
                        IEnumerable<ExecutionGoalSummary> executionGoalMetadatas = await response.Content.ReadAsJsonAsync<IEnumerable<ExecutionGoalSummary>>()
                            .ConfigureDefaults();

                        telemetryContext.AddContext(nameof(executionGoalMetadatas), executionGoalMetadatas);
                        return this.Ok(executionGoalMetadatas);
                    }

                    ExecutionGoalSummary executionGoalMetadata = await response.Content.ReadAsJsonAsync<ExecutionGoalSummary>()
                        .ConfigureDefaults();

                    telemetryContext.AddContext(nameof(executionGoalMetadata), executionGoalMetadata);
                    return this.Ok(executionGoalMetadata);

                }
            }).ConfigureDefaults();
        }

        private static async Task<IActionResult> CreateErrorResponseAsync(HttpResponseMessage response)
        {
            return new ObjectResult(await response.Content.ReadAsJsonAsync<ProblemDetails>().ConfigureDefaults())
            {
                StatusCode = (int)response.StatusCode
            };
        }

        private async Task<IActionResult> CreateExecutionGoalTemplateAsync(Item<GoalBasedSchedule> executionGoalTemplate, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            return await this.ExecuteApiOperationAsync(EventNames.CreateExecutionGoalTemplate, telemetryContext, this.Logger, async () =>
            {
                if (!(ContractExtension.SupportedExecutionGoalVersions.Contains(executionGoalTemplate.Definition.Version, StringComparer.Ordinal)))
                {
                    throw new ArgumentException(
                        $"The execution goal provided failed schema validation. The version provided is not a supported version. {Environment.NewLine}" +
                        $"Version provided: {executionGoalTemplate.Definition.Version} Supported versions: {ContractExtension.SupportedExecutionGoalVersions}");
                }

                ValidationResult validationResult = ExecutionGoalTemplateValidation.Instance.Validate(executionGoalTemplate.Definition);
                if (!validationResult.IsValid)
                {
                    throw new SchemaException(
                        $"The execution goal template provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                        $"{string.Join($"{Environment.NewLine} -", validationResult.ValidationErrors)}");
                }

                HttpResponseMessage response = await this.Client.CreateExecutionGoalTemplateAsync(executionGoalTemplate, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);

                if (!response.IsSuccessStatusCode)
                {
                    return await ExecutionGoalTemplatesController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                Item<GoalBasedSchedule> createdInstance = await response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>()
                    .ConfigureDefaults();

                telemetryContext.AddContext(nameof(createdInstance), createdInstance);
                return this.CreatedAtAction(nameof(this.GetExecutionGoalTemplatesAsync), new { teamName = createdInstance.Definition.TeamName, templateId = createdInstance.Id }, createdInstance);
                // return this.Ok(createdInstance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Event names used for logging telemetry
        /// </summary>
        private static class EventNames
        {
            public static readonly string GetExecutionGoalTemplate = EventContext.GetEventName(ExecutionGoalTemplatesController.ApiName, "GetExecutionGoalTemplate");
            public static readonly string GetExecutionGoalMetadata = EventContext.GetEventName(ExecutionGoalTemplatesController.ApiName, "GetExecutionGoalMetadata");
            public static readonly string CreateExecutionGoalTemplate = EventContext.GetEventName(ExecutionGoalTemplatesController.ApiName, "CreateExecutionGoalTemplate");
            public static readonly string UpdateExecutionGoalTemplate = EventContext.GetEventName(ExecutionGoalTemplatesController.ApiName, "UpdateExecutionGoalTemplate");
            public static readonly string DeleteExecutionGoalTemplate = EventContext.GetEventName(ExecutionGoalTemplatesController.ApiName, "DeleteExecutionGoalTemplate");
        }
    }
}
