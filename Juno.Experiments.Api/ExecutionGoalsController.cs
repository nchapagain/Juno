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

    /// <summary>
    /// Juno experiments REST API controller for managing execution goals
    /// in the system.
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    [Route("api/executionGoals")]
    public class ExecutionGoalsController : ControllerBase
    {
        /// <summary>
        /// The name of the API.
        /// </summary>
        public const string ApiName = "ExecutionGoalsApi";
        private const string V1 = "v1";

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionGoalsController"/>
        /// </summary>
        /// <param name="executionClient">The Juno Execution API client to use for communications with the Juno system.</param>
        /// <param name="configuration">Configuration settings for the environment.</param>
        /// <param name="logger">The trace/telemetry logger for the controller.</param>
        public ExecutionGoalsController(ExecutionClient executionClient, IConfiguration configuration, ILogger logger = null)
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
        /// Creates a new execution goal in the system
        /// </summary>
        /// <param name="executionGoal">The execution goal to place in the system</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        [HttpPost]
        [Consumes("application/json")]
        [Description("Creates a new execution goal in the system.")]
        [ApiExplorerSettings(GroupName = ExecutionGoalsController.V1)]
        [ProducesResponseType(typeof(Item<GoalBasedSchedule>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> CreateExecutionGoalAsync([FromBody] Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
            .AddContext(nameof(executionGoal), executionGoal);

            return this.CreateExecutionGoalAsync(executionGoal, telemetryContext, cancellationToken);
        }

        /// <summary>
        /// Creates a new execution goal from a template in the system
        /// </summary>
        /// <param name="executionGoalParameters"><see cref="ExecutionGoalParameter"/> necessary to inline with Execution Goal Template</param>
        /// <param name="templateId">The execution goal template that execution goal will be based on.</param>
        /// <param name="teamName">The name of the team that owns the execution goal template</param>
        /// <param name="token">A cancellation token that can be used to cancel the current thread of execution.</param>
        [HttpPost("{templateId}")]
        [Consumes("application/json")]
        [Description("Creates a new execution goal from a template in the system.")]
        [ApiExplorerSettings(GroupName = ExecutionGoalsController.V1)]
        [ProducesResponseType(typeof(Item<GoalBasedSchedule>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateExecutionGoalFromTemplateAsync(string templateId, [FromQuery] string teamName, [FromBody] ExecutionGoalParameter executionGoalParameters, CancellationToken token)
        {
            executionGoalParameters.ThrowIfNull(nameof(executionGoalParameters));
            templateId.ThrowIfNullOrWhiteSpace(nameof(templateId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(templateId), templateId)
                .AddContext(nameof(teamName), teamName)
                .AddContext(nameof(executionGoalParameters), executionGoalParameters);

            return await this.ExecuteApiOperationAsync(EventNames.CreateExecutionGoalFromTemplate, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.CreateExecutionGoalFromTemplateAsync(executionGoalParameters, templateId, teamName, token)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);
                if (!response.IsSuccessStatusCode)
                {
                    return await ExecutionGoalsController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                Item<GoalBasedSchedule> executionGoal = await response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>()
                    .ConfigureDefaults();

                return this.CreatedAtAction(nameof(this.GetExecutionGoalAsync), new { executionGoalId = executionGoal.Id, teamName = executionGoal.Definition.TeamName }, executionGoal);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an existing Execution Goal in the system from an execution goal template.
        /// </summary>
        /// <param name="executionGoalParameters"><see cref="ExecutionGoalParameter"/> necessary to inline with Execution Goal Template</param>
        /// <param name="templateId">The execution goal template that execution goal will be based on.</param>
        /// <param name="executionGoalId">The id of the execution goal to update.</param>
        /// <param name="teamName">The name of the team that owns the execution goal template</param>
        /// <param name="token"><see cref="CancellationToken"/></param>
        [HttpPut("{templateId}")]
        [Consumes("application/json")]
        [Description("Updates an existing Execution Goal in the system from an execution goal template")]
        [ApiExplorerSettings(GroupName = ExecutionGoalsController.V1)]
        [ProducesResponseType(typeof(Item<GoalBasedSchedule>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateExecutionGoalFromTemplateAsync(string templateId, [FromQuery] string executionGoalId, [FromQuery] string teamName, [FromBody] ExecutionGoalParameter executionGoalParameters, CancellationToken token)
        {
            executionGoalParameters.ThrowIfNull(nameof(executionGoalParameters));
            templateId.ThrowIfNullOrWhiteSpace(nameof(templateId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
                .AddContext(nameof(templateId), templateId)
                .AddContext(nameof(executionGoalId), executionGoalId)
                .AddContext(nameof(teamName), teamName)
                .AddContext(nameof(executionGoalParameters), executionGoalParameters);

            return await this.ExecuteApiOperationAsync(EventNames.UpdateExecutionGoalFromTemplate, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.UpdateExecutionGoalFromTemplateAsync(executionGoalParameters, templateId, executionGoalId, teamName, token)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);
                if (!response.IsSuccessStatusCode)
                {
                    return await ExecutionGoalsController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                Item<GoalBasedSchedule> updatedInstance = await response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>()
                    .ConfigureDefaults();

                return this.Ok(updatedInstance);

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Retrieves an execution goal from the system
        /// </summary>
        /// <param name="executionGoalId">The Id of the execution goal</param>
        /// <param name="teamName">The team that owns the execution goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <param name= "view">Resource Type of the execution goal (optional)</param>
        [HttpGet("{executionGoalId}")]
        [Consumes("application/json")]
        [Description("Gets an existing execution goal from the system.")]
        [ApiExplorerSettings(GroupName = ExecutionGoalsController.V1)]
        [ProducesResponseType(typeof(Item<GoalBasedSchedule>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IEnumerable<Item<GoalBasedSchedule>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IEnumerable<TargetGoalTimeline>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExecutionGoalAsync(string executionGoalId, [FromQuery] string teamName, CancellationToken cancellationToken, [FromQuery] ExecutionGoalView view = ExecutionGoalView.Full)
        {
            executionGoalId.ThrowIfNullOrWhiteSpace(nameof(executionGoalId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(executionGoalId), executionGoalId)
                .AddContext(nameof(teamName), teamName);

            return await this.ExecuteApiOperationAsync(EventNames.GetExecutionGoal, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.GetExecutionGoalsAsync(cancellationToken, teamName, executionGoalId, view)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);
                if (!response.IsSuccessStatusCode)
                {
                    return await ExecutionGoalsController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                if (view == ExecutionGoalView.Status)
                {
                    IEnumerable<ExperimentInstanceStatus> executionGoalStatus = await response.Content.ReadAsJsonAsync<IEnumerable<ExperimentInstanceStatus>>()
                        .ConfigureDefaults();

                    return this.Ok(executionGoalStatus);
                }

                Item<GoalBasedSchedule> executionGoal = await response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>()
                    .ConfigureDefaults();

                return this.Ok(executionGoal);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Retrieves all execution goals associated with a team name from the system
        /// </summary>
        /// <param name="teamName">The team that owns the execution goal</param>
        /// <param name="cancellationToken">Cancellation token used for cancelling current thread of execution.</param>
        /// <param name="executionGoalId">The id of the execution goal (optional)</param>
        /// <param name= "view">Resource Type of the execution goal (optional)</param>
        [HttpGet]
        [Consumes("application/json")]
        [Description("Gets all execution goals associated with a team name.")]
        [ApiExplorerSettings(GroupName = ExecutionGoalsController.V1)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExecutionGoalsAsync(CancellationToken cancellationToken, [FromQuery] string teamName = null, [FromQuery] string executionGoalId = null, [FromQuery] ExecutionGoalView view = ExecutionGoalView.Full)
        {
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(teamName), teamName)
                .AddContext(nameof(executionGoalId), executionGoalId);

            return await this.ExecuteApiOperationAsync(EventNames.GetExecutionGoals, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.GetExecutionGoalsAsync(cancellationToken, teamName, executionGoalId, view)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);
                if (!response.IsSuccessStatusCode)
                {
                    return await ExecutionGoalsController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                if (view == ExecutionGoalView.Status)
                {
                    IEnumerable<ExperimentInstanceStatus> executionGoalStatus = await response.Content.ReadAsJsonAsync<IEnumerable<ExperimentInstanceStatus>>()
                        .ConfigureDefaults();

                    return this.Ok(executionGoalStatus);
                }
                else if (view == ExecutionGoalView.Timeline)
                {
                    IList<TargetGoalTimeline> executionGoalStatus = await response.Content.ReadAsJsonAsync<IList<TargetGoalTimeline>>()
                        .ConfigureDefaults();

                    return this.Ok(executionGoalStatus);
                }
                else if (view == ExecutionGoalView.Summary)
                {
                    IEnumerable<ExecutionGoalSummary> executionGoalMetadatas = await response.Content.ReadAsJsonAsync<IEnumerable<ExecutionGoalSummary>>()
                            .ConfigureDefaults();

                    return this.Ok(executionGoalMetadatas);
                }

                return executionGoalId == null
                ? this.Ok(await response.Content.ReadAsJsonAsync<IEnumerable<Item<GoalBasedSchedule>>>().ConfigureDefaults())
                : this.Ok(await response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>().ConfigureDefaults());
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an Execution Goal from the system.
        /// </summary>
        /// <param name="executionGoal">The Execution Goal to update in the system</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        [HttpPut]
        [Consumes("application/json")]
        [Description("Updates an existing Execution Goal in the System")]
        [ApiExplorerSettings(GroupName = ExecutionGoalsController.V1)]
        [ProducesResponseType(typeof(Item<GoalBasedSchedule>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateExecutionGoalAsync([FromBody] Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(executionGoal), executionGoal);

            return await this.ExecuteApiOperationAsync(EventNames.UpdateExecutionGoal, telemetryContext, this.Logger, async () =>
            {
                ValidationResult validationResult = ExecutionGoalValidation.Instance.Validate(executionGoal.Definition);
                if (!validationResult.IsValid)
                {
                    throw new SchemaException(
                        $"The execution goal provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                        $"{string.Join($"{Environment.NewLine} -", validationResult.ValidationErrors)}");
                }

                HttpResponseMessage response = null;
                try
                {
                    response = await this.Client.UpdateExecutionGoalAsync(executionGoal, cancellationToken).ConfigureDefaults();
                    if (!response.IsSuccessStatusCode)
                    {
                        return await ExecutionGoalsController.CreateErrorResponseAsync(response).ConfigureDefaults();
                    }

                    Item<GoalBasedSchedule> updatedInstance = await response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>().ConfigureDefaults();

                    telemetryContext.AddContext("executionGoalId", executionGoal.Id);
                    return this.Ok(updatedInstance);
                }
                finally
                {
                    telemetryContext.AddContext(response);
                }
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Deletes an Execution Goal from the System
        /// </summary>
        /// <param name="executionGoalId">Unique Id of the Execution Goal</param>
        /// <param name="teamName">Name of the team that owns the Execution Goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <response code="204">No Content. The execution goal was deleted successfully.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpDelete("{executionGoalId}")]
        [Description("Deletes an existing execution goal")]
        [ApiExplorerSettings(GroupName = ExecutionGoalsController.V1)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteExecutionGoalAsync(string executionGoalId, [FromQuery] string teamName, CancellationToken cancellationToken)
        {
            executionGoalId.ThrowIfNullOrWhiteSpace(nameof(executionGoalId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(executionGoalId), executionGoalId)
                .AddContext(nameof(teamName), teamName);

            return await this.ExecuteApiOperationAsync(EventNames.DeleteExecutionGoal, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.DeleteExecutionGoalAsync(executionGoalId, teamName, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);
                if (!response.IsSuccessStatusCode)
                {
                    return await ExecutionGoalsController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                return this.NoContent();
            }).ConfigureDefaults();
        }

        private static async Task<IActionResult> CreateErrorResponseAsync(HttpResponseMessage response)
        {
            return new ObjectResult(await response.Content.ReadAsJsonAsync<ProblemDetails>().ConfigureDefaults())
            {
                StatusCode = (int)response.StatusCode
            };
        }

        private async Task<IActionResult> CreateExecutionGoalAsync(Item<GoalBasedSchedule> executionGoal, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            return await this.ExecuteApiOperationAsync(EventNames.CreateExecutionGoal, telemetryContext, this.Logger, async () =>
            {
                ValidationResult validationResult = ExecutionGoalValidation.Instance.Validate(executionGoal.Definition);
                if (!validationResult.IsValid)
                {
                    throw new SchemaException(
                        $"The execution goal provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                        $"{string.Join($"{Environment.NewLine} -", validationResult.ValidationErrors)}");
                }

                HttpResponseMessage response = await this.Client.CreateExecutionGoalAsync(executionGoal, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);

                if (!response.IsSuccessStatusCode)
                {
                    return await ExecutionGoalsController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                Item<GoalBasedSchedule> createdInstance = await response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>()
                    .ConfigureDefaults();

                telemetryContext.AddContext("executionGoalId", executionGoal.Id);
                return this.CreatedAtAction(nameof(this.GetExecutionGoalAsync), new { executionGoalId = createdInstance.Id, teamName = createdInstance.Definition.TeamName }, createdInstance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Event names used for logging telemetry
        /// </summary>
        private static class EventNames
        {
            public static readonly string CreateExecutionGoal = EventContext.GetEventName(ExecutionGoalsController.ApiName, "CreateExecutionGoal");
            public static readonly string GetExecutionGoal = EventContext.GetEventName(ExecutionGoalsController.ApiName, "GetExecutionGoal");
            public static readonly string GetExecutionGoalTemplateMetadata = EventContext.GetEventName(ExecutionGoalsController.ApiName, "GetExecutionGoalMetadata");
            public static readonly string GetExecutionGoalTemplatesList = EventContext.GetEventName(ExecutionGoalsController.ApiName, "GetExecutionGoalTemplates");
            public static readonly string GetExecutionGoals = EventContext.GetEventName(ExecutionGoalsController.ApiName, "GetExecutionGoals");
            public static readonly string DeleteExecutionGoal = EventContext.GetEventName(ExecutionGoalsController.ApiName, "DeleteExecutionGoal");
            public static readonly string UpdateExecutionGoal = EventContext.GetEventName(ExecutionGoalsController.ApiName, "UpdateExecutionGoal");
            public static readonly string CreateExecutionGoalFromTemplate = EventContext.GetEventName(ExecutionGoalsController.ApiName, "CreateExecutionGoalFromTemplate");
            public static readonly string UpdateExecutionGoalFromTemplate = EventContext.GetEventName(ExecutionGoalsController.ApiName, "UpdateExecutionGoalFromTemplate");

        }
    }
}
