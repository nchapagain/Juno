namespace Juno.Execution.Api
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Juno.DataManagement;
    using Juno.Extensions.AspNetCore;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Juno execution REST API controller for managing execution goal template data in the system.
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    [Route("/api/executionGoalTemplates")]
    public partial class ExecutionGoalTemplateController : ControllerBase
    {
        /// <summary>
        /// The name of the API.
        /// </summary>
        public const string ApiName = "ExecutionGoalTemplateApi";
        private const string V1 = "v1";

        /// <summary>
        /// Initailizes a new instance of <see cref="ExecutionGoalTemplateController"/> class.
        /// </summary>
        /// <param name="executionGoalDataManager"><see cref="IScheduleDataManager"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        public ExecutionGoalTemplateController(IScheduleDataManager executionGoalDataManager, ILogger logger = null)
        {
            executionGoalDataManager.ThrowIfNull(nameof(executionGoalDataManager));

            this.ExecutionGoalDataManager = executionGoalDataManager;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// The data layer componenet that provides management of execution goals.
        /// </summary>
        protected IScheduleDataManager ExecutionGoalDataManager { get; }

        /// <summary>
        /// Gets the trace/telemetry logger for the controller.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Creates a new Execution Goal Template item in the system
        /// </summary>
        /// <param name="executionGoalTemplate">The Exuection Goal to create</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <response code="201">Created. The execution goal item was created successfully.</response>
        /// <response code="400">Bad Request. The schema of the execution goal is invalid.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpPost]
        [Consumes("application/json")]
        [Description("Creates a new execution goal template item in the system")]
        [ApiExplorerSettings(GroupName = ExecutionGoalTemplateController.V1)]
        [ProducesResponseType(typeof(GoalBasedSchedule), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> CreateExecutionGoalTemplateAsync([FromBody] Item<GoalBasedSchedule> executionGoalTemplate, CancellationToken cancellationToken)
        {
            executionGoalTemplate.ThrowIfNull(nameof(executionGoalTemplate));
            return this.CreateExecutionGoalTemplateAsync(executionGoalTemplate, false, cancellationToken);
        }

        /// <summary>
        /// Updates an Execution Goal Template from the system.
        /// </summary>
        /// <param name="executionGoalTemplate">The Execution Goal to update in the system</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        [HttpPut]
        [Consumes("application/json")]
        [Description("Updates an existing Execution Goal in the System")]
        [ApiExplorerSettings(GroupName = ExecutionGoalTemplateController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> UpdateExecutionGoalTemplateAsync([FromBody] Item<GoalBasedSchedule> executionGoalTemplate, CancellationToken cancellationToken)
        {
            executionGoalTemplate.ThrowIfNull(nameof(executionGoalTemplate));
            return this.CreateExecutionGoalTemplateAsync(executionGoalTemplate, true, cancellationToken);
        }

        /// <summary>
        /// Deletes an Execution Goal Template from the System
        /// </summary>
        /// <param name="templateId">Unique Id of the Execution Goal Template</param>
        /// <param name="teamName">Name of the team that owns the Execution Goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <response code="204">No Content. The execution goal was deleted successfully.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpDelete("{templateId}")]
        [Description("Deletes an existing execution goal")]
        [ApiExplorerSettings(GroupName = ExecutionGoalTemplateController.V1)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteExecutionGoalTemplateAsync(string templateId, [FromQuery] string teamName, CancellationToken cancellationToken)
        {
            templateId.ThrowIfNullOrWhiteSpace(nameof(templateId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
                .AddContext(nameof(templateId), templateId)
                .AddContext(nameof(teamName), teamName);

            return await this.ExecuteApiOperationAsync(EventNames.DeleteExecutionGoalTemplate, telemetryContext, this.Logger, async () =>
            {
                await this.ExecutionGoalDataManager.DeleteExecutionGoalTemplateAsync(templateId, teamName, cancellationToken)
                    .ConfigureDefaults();

                return this.NoContent();
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets an Execution goal template from the system.
        /// </summary>
        /// <param name="templateId">The unique ID of the execution goal template.</param>
        /// <param name="view">The type of the view to be returned to the user</param>
        /// <param name="teamName">The team name who owns the experiment definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment instance was found in the system.</response>
        /// <response code="404">Not Found. The experiment instance was not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet]
        [Consumes("application/json")]
        [Description("Gets an existing experiment instance from the system.")]
        [ApiExplorerSettings(GroupName = ExecutionGoalTemplateController.V1)]
        [ProducesResponseType(typeof(IEnumerable<Item<GoalBasedSchedule>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IEnumerable<ExecutionGoalSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Item<GoalBasedSchedule>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ExecutionGoalSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExecutionGoalTemplatesAsync(CancellationToken cancellationToken, [FromQuery] string teamName = null, [FromQuery] string templateId = null, [FromQuery] View view = View.Full)
        {
            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(teamName), teamName)
                .AddContext(nameof(templateId), templateId);

            return await this.ExecuteApiOperationAsync(EventNames.GetExecutionGoalTemplate, telemetryContext, this.Logger, async () =>
            {
                if (teamName == null && templateId != null)
                {
                    return this.BadRequest($"null teamName and non-null templateId combination is not supported.");
                }

                if (view == View.Full)
                {
                    if (templateId == null)
                    {
                        IEnumerable<Item<GoalBasedSchedule>> executionGoalTemplates = await this.ExecutionGoalDataManager.GetExecutionGoalTemplatesAsync(cancellationToken, teamName)
                            .ConfigureDefaults();

                        telemetryContext.AddContext(nameof(executionGoalTemplates), executionGoalTemplates);

                        return this.Ok(executionGoalTemplates);
                    }

                    Item<GoalBasedSchedule> executionGoalTemplate = await this.ExecutionGoalDataManager.GetExecutionGoalTemplateAsync(templateId, teamName, cancellationToken)
                        .ConfigureDefaults();

                    telemetryContext.AddContext(nameof(executionGoalTemplate), executionGoalTemplate);

                    return this.Ok(executionGoalTemplate);
                }
                else
                {
                    if (templateId == null)
                    {
                        IEnumerable<ExecutionGoalSummary> metadatas = await this.ExecutionGoalDataManager.GetExecutionGoalTemplateInfoAsync(cancellationToken, teamName)
                            .ConfigureDefaults();

                        telemetryContext.AddContext(nameof(metadatas), metadatas);

                        return this.Ok(metadatas);
                    }

                    IEnumerable<ExecutionGoalSummary> metadata = await this.ExecutionGoalDataManager.GetExecutionGoalTemplateInfoAsync(cancellationToken, teamName, templateId)
                        .ConfigureDefaults();

                    ExecutionGoalSummary instance = metadata.First();
                    instance.ThrowIfNull(nameof(instance));

                    return this.Ok(instance);
                }
            }).ConfigureDefaults();
        }

        private async Task<IActionResult> CreateExecutionGoalTemplateAsync(Item<GoalBasedSchedule> executionGoalTemplate, bool isItemUpdating, CancellationToken cancellationToken)
        {
            string teamName = executionGoalTemplate.Definition.TeamName;
            string templateId = executionGoalTemplate.Id;

            teamName.ThrowIfNullOrEmpty(nameof(teamName));
            templateId.ThrowIfNullOrEmpty(nameof(templateId));
            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
                .AddContext(nameof(isItemUpdating), isItemUpdating)
                .AddContext(nameof(teamName), teamName)
                .AddContext(nameof(templateId), templateId)
                .AddContext(nameof(executionGoalTemplate), executionGoalTemplate);

            return await this.ExecuteApiOperationAsync(EventNames.CreateExecutionGoalTemplate, telemetryContext, this.Logger, async () =>
            {
                ValidationResult validationResult = ExecutionGoalTemplateValidation.Instance.Validate(executionGoalTemplate.Definition);
                if (!validationResult.IsValid)
                {
                    throw new SchemaException(
                        $"The execution goal template provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                        $"{string.Join($"{Environment.NewLine} -", validationResult.ValidationErrors)}");
                }

                Item<GoalBasedSchedule> instance = await this.ExecutionGoalDataManager.CreateExecutionGoalTemplateAsync(executionGoalTemplate, isItemUpdating, cancellationToken)
                    .ConfigureDefaults();

                return this.CreatedAtAction(nameof(this.GetExecutionGoalTemplatesAsync), new { teamName = teamName, templateId = templateId }, instance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Event names used for logging telemetry
        /// </summary>
        private static class EventNames
        {
            public static readonly string CreateExecutionGoalTemplate = EventContext.GetEventName(ExecutionGoalTemplateController.ApiName, "CreateExecutionGoalTemplate");
            public static readonly string GetExecutionGoalTemplate = EventContext.GetEventName(ExecutionGoalTemplateController.ApiName, "GetExecutionGoal");
            public static readonly string DeleteExecutionGoalTemplate = EventContext.GetEventName(ExecutionGoalTemplateController.ApiName, "DeleteExecutionGoalTemplate");
        }
    }
}
