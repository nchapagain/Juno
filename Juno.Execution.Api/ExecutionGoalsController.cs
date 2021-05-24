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
    using Kusto.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Juno execution REST API controller for managing execution goal data
    /// in the system.
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    [Route("/api/executionGoals")]
    public partial class ExecutionGoalsController : ControllerBase
    {
        /// <summary>
        /// The name of the API.
        /// </summary>
        public const string ApiName = "ExecutionGoalApi";
        private const string V1 = "v1";

        /// <summary>
        /// Initailizes a new instance of <see cref="ExecutionGoalsController"/> class.
        /// </summary>
        /// <param name="executionGoalDataManager"><see cref="IScheduleDataManager"/></param>
        /// <param name="targetGoalDataManager"><see cref="IScheduleTimerDataManager"/></param>
        /// <param name="executionGoalTelemetryDataManager"><see cref="IExperimentKustoTelemetryDataManager"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        public ExecutionGoalsController(IScheduleDataManager executionGoalDataManager, IScheduleTimerDataManager targetGoalDataManager, IExperimentKustoTelemetryDataManager executionGoalTelemetryDataManager, ILogger logger = null)
        {
            executionGoalDataManager.ThrowIfNull(nameof(executionGoalDataManager));
            targetGoalDataManager.ThrowIfNull(nameof(targetGoalDataManager));
            executionGoalTelemetryDataManager.ThrowIfNull(nameof(executionGoalTelemetryDataManager));

            this.ExecutionGoalDataManager = executionGoalDataManager;
            this.TargetGoalDataManager = targetGoalDataManager;
            this.ExecutionGoalTelemetryDataManager = executionGoalTelemetryDataManager;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// The data layer componenet that provides management of execution goals.
        /// </summary>
        protected IScheduleDataManager ExecutionGoalDataManager { get; }

        /// <summary>
        /// The data layer component that provides management of target goals.
        /// </summary>
        protected IScheduleTimerDataManager TargetGoalDataManager { get; }

        /// <summary>
        /// The data layer component that provides telemetry of execution goals.
        /// </summary>
        protected IExperimentKustoTelemetryDataManager ExecutionGoalTelemetryDataManager { get; }

        /// <summary>
        /// Gets the trace/telemetry logger for the controller.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Creates a new execution goal from an execution goal template.
        /// </summary>
        /// <param name="executionGoalParameters"><see cref="ExecutionGoalParameter"/> necessary to inline with Execution Goal Template</param>
        /// <param name="templateId">The execution goal template that execution goal will be based on.</param>
        /// <param name="teamName">The name of the team that owns the execution goal</param>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <response code="201">Created. The execution goal item was created successfully.</response>
        /// <response code="400">Bad Request. The schema of the execution goal is invalid.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpPost("{templateId}")]
        [Consumes("application/json")]
        [Description("Creates a new execution goal in the system from template")]
        [ProducesResponseType(typeof(Item<GoalBasedSchedule>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
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
                Item<GoalBasedSchedule> executionGoalTemplateItem = await this.ExecutionGoalDataManager.GetExecutionGoalTemplateAsync(templateId, teamName, token)
                    .ConfigureDefaults();

                GoalBasedSchedule template = executionGoalTemplateItem.Definition;

                Item<GoalBasedSchedule> executionGoal = new Item<GoalBasedSchedule>(executionGoalParameters.ExecutionGoalId, template.Inlined(executionGoalParameters));

                return await this.CreateExecutionGoalAsync(teamName, executionGoal, token).ConfigureDefaults();
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an existing Execution Goal in the system from an execution goal template.
        /// </summary>
        /// <param name="executionGoalParameters"><see cref="ExecutionGoalParameter"/> necessary to inline with Execution Goal Template</param>
        /// <param name="templateId">The execution goal template that execution goal will be based on.</param>
        /// <param name="teamName">The name of the team that owns the execution goal template</param>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <response code="200">OK. The Execution Goal was updated successfully.</response>
        /// <response code="400">Bad Request. The schema of the Execution Goal is invalid.</response>
        /// <response code="404">Not Found. The Execution Goal does not exist in the system.</response>
        /// <response code="412">Precondition Failed. The execution goal provided has a mismatched eTag or partition key.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>        
        [HttpPut("{templateId}")]
        [Consumes("application/json")]
        [Description("Updates an existing Execution Goal in the system from an execution goal template")]
        [ApiExplorerSettings(GroupName = ExecutionGoalsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateExecutionGoalFromTemplateAsync(string templateId, [FromQuery] string teamName, [FromBody] ExecutionGoalParameter executionGoalParameters, CancellationToken token)
        {
            executionGoalParameters.ThrowIfNull(nameof(executionGoalParameters));
            templateId.ThrowIfNullOrWhiteSpace(nameof(templateId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
                .AddContext(nameof(templateId), templateId)
                .AddContext(nameof(teamName), teamName)
                .AddContext(nameof(executionGoalParameters), executionGoalParameters);

            return await this.ExecuteApiOperationAsync(EventNames.UpdateExecutionGoalFromTemplate, telemetryContext, this.Logger, async () =>
            {
                Item<GoalBasedSchedule> executionGoalTemplateItem = await this.ExecutionGoalDataManager.GetExecutionGoalTemplateAsync(templateId, teamName, token)
                    .ConfigureDefaults();

                GoalBasedSchedule template = executionGoalTemplateItem.Definition;

                Item<GoalBasedSchedule> executionGoal = new Item<GoalBasedSchedule>(executionGoalParameters.ExecutionGoalId, template.Inlined(executionGoalParameters));

                return await this.UpdateExecutionGoalAsync(executionGoal, token).ConfigureDefaults();
            }).ConfigureDefaults();

        }

        /// <summary>
        /// Creates a new Execution Goal item in the system
        /// </summary>
        /// <param name="executionGoal">The Exuection Goal to create</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <response code="201">Created. The execution goal item was created successfully.</response>
        /// <response code="400">Bad Request. The schema of the execution goal is invalid.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpPost]
        [Consumes("application/json")]
        [Description("Creates a new execution goal item in the system")]
        [ApiExplorerSettings(GroupName = ExecutionGoalsController.V1)]
        [ProducesResponseType(typeof(Item<GoalBasedSchedule>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> CreateExecutionGoalAsync([FromBody] Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            return this.CreateExecutionGoalAsync(executionGoal.Definition.TeamName, executionGoal, cancellationToken);
        }

        /// <summary>
        /// Retrieves execution goals from the data store
        /// </summary>
        /// <param name="executionGoalId">The id of the execution goal to retrieve</param>
        /// <param name="teamName">The team name that owns the execution goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <param name= "view">Resource Type of the execution goal (optional)</param>
        /// <response code="200">OK. The execution goal was found in the system.</response>
        /// <response code="404">Not Found. The execution goal instance was not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet]
        [Description("Gets an existing execution goal from the system")]
        [ApiExplorerSettings(GroupName = ExecutionGoalsController.V1)]
        [ProducesResponseType(typeof(Item<GoalBasedSchedule>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IEnumerable<Item<GoalBasedSchedule>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IEnumerable<TargetGoalTimeline>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IEnumerable<ExperimentInstanceStatus>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExecutionGoalsAsync(CancellationToken cancellationToken, [FromQuery] string teamName = null, [FromQuery] string executionGoalId = null, [FromQuery] ExecutionGoalView view = ExecutionGoalView.Full)
        {
            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
                .AddContext(nameof(teamName), teamName)
                .AddContext(nameof(executionGoalId), executionGoalId);

            return await this.ExecuteApiOperationAsync(EventNames.GetExecutionGoals, telemetryContext, this.Logger, async () =>
            {
                if (string.IsNullOrEmpty(teamName) && !string.IsNullOrEmpty(executionGoalId))
                {
                    return this.DataSchemaInvalid($"Route is missing {nameof(teamName)} argument.");
                }

                if (view == ExecutionGoalView.Full)
                {
                    return await this.GetFullExecutionGoalsAsync(teamName, executionGoalId, cancellationToken).ConfigureDefaults();
                }
                else if (view == ExecutionGoalView.Status)
                {
                    return await this.GetStatusExecutionGoalsAsync(teamName, executionGoalId, cancellationToken).ConfigureDefaults();
                }
                else if (view == ExecutionGoalView.Timeline)
                {
                    return await this.GetTimelineExecutionGoalsAsync(teamName, executionGoalId, cancellationToken).ConfigureDefaults();
                }
                else if (view == ExecutionGoalView.Summary)
                {
                    return await this.GetSummaryExecutionGoalsAsync(teamName, executionGoalId, cancellationToken).ConfigureDefaults();
                }

                return this.DataSchemaInvalid($"{nameof(view)}: {view} is not implemented.");

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an Execution Goal from the system.
        /// </summary>
        /// <param name="executionGoal">The Execution Goal to update in the system</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <response code="200">OK. The Execution Goal was updated successfully.</response>
        /// <response code="400">Bad Request. The schema of the Execution Goal is invalid.</response>
        /// <response code="404">Not Found. The Execution Goal does not exist in the system.</response>
        /// <response code="412">Precondition Failed. The execution goal provided has a mismatched eTag or partition key.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>        
        [HttpPut]
        [Consumes("application/json")]
        [Description("Updates an existing Execution Goal in the System")]
        [ApiExplorerSettings(GroupName = ExecutionGoalsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateExecutionGoalAsync([FromBody] Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
                .AddContext(nameof(executionGoal), executionGoal);

            return await this.ExecuteApiOperationAsync(EventNames.UpdateExecutionGoal, telemetryContext, this.Logger, async () =>
            {
                ValidationResult result = ExecutionGoalValidation.Instance.Validate(executionGoal.Definition);
                if (!result.IsValid)
                {
                    throw new SchemaException(
                        $"The execution goal provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                        $"{string.Join($"{Environment.NewLine} -", result.ValidationErrors)}");
                }

                try
                {
                    await this.TargetGoalDataManager.UpdateTargetGoalTriggersAsync(executionGoal.Definition, cancellationToken)
                        .ConfigureDefaults();
                }
                catch (StorageException exc)
                {
                    return this.DataConflict($"The target goals defined in the execution goal: {executionGoal.Id} has conflicts with" +
                        $"existing execution goals {Environment.NewLine} {exc.Message}");
                }

                Item<GoalBasedSchedule> item = await this.ExecutionGoalDataManager.UpdateExecutionGoalAsync(executionGoal, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext("id", item.Id);

                return this.Ok(item);
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

            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
                .AddContext(nameof(executionGoalId), executionGoalId)
                .AddContext(nameof(teamName), teamName);

            return await this.ExecuteApiOperationAsync(EventNames.DeleteExecutionGoal, telemetryContext, this.Logger, async () =>
            {
                try
                {
                    await this.TargetGoalDataManager.DeleteTargetGoalTriggersAsync(executionGoalId, cancellationToken)
                        .ConfigureDefaults();
                }
                catch (StorageException exc)
                {
                    return this.DataConflict($"The target goals defined in the execution goal: {executionGoalId} has experienced conflicts with" +
                        $"existing execution goals {Environment.NewLine} {exc.Message}");
                }

                await this.ExecutionGoalDataManager.DeleteExecutionGoalAsync(executionGoalId, teamName, cancellationToken)
                    .ConfigureDefaults();

                return this.NoContent();
            }).ConfigureDefaults();
        }

        private async Task<IActionResult> CreateExecutionGoalAsync(string teamName, Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
               .AddContext(nameof(executionGoal), executionGoal)
               .AddContext(nameof(teamName), teamName);
            return await this.ExecuteApiOperationAsync(EventNames.CreateExecutionGoal, telemetryContext, this.Logger, async () =>
            {
                if (!teamName.Equals(executionGoal.Definition.TeamName, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"The execution goal provided failed schema validation. The team name supplied in the URL: {teamName} {Environment.NewLine}" +
                        $"does not match the team name in the execution goal: {executionGoal.Definition.TeamName}");
                }

                ValidationResult validationResult = ExecutionGoalValidation.Instance.Validate(executionGoal.Definition);
                if (!validationResult.IsValid)
                {
                    throw new SchemaException(
                        $"The execution goal provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                        $"{string.Join($"{Environment.NewLine} -", validationResult.ValidationErrors)}");
                }

                try
                {
                    await this.TargetGoalDataManager.CreateTargetGoalsAsync(executionGoal.Definition, cancellationToken)
                        .ConfigureDefaults();
                }
                catch (StorageException exc)
                {
                    return this.DataConflict($"The target goals defined in the execution goal: {executionGoal.Id} has conflicts with" +
                        $"existing execution goals {Environment.NewLine} {exc.Message}");
                }

                Item<GoalBasedSchedule> instance = await this.ExecutionGoalDataManager.CreateExecutionGoalAsync(executionGoal, cancellationToken)
                    .ConfigureDefaults();

                return this.CreatedAtAction(nameof(this.GetExecutionGoalsAsync), new { executionGoalId = instance.Id, teamName = instance.Definition.TeamName }, instance);
            }).ConfigureDefaults();
        }

        private async Task<IActionResult> GetFullExecutionGoalsAsync(string teamName, string executionGoalId, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
               .AddContext(nameof(executionGoalId), executionGoalId)
               .AddContext(nameof(teamName), teamName);

            return await this.ExecuteApiOperationAsync(EventNames.GetFullExecutionGoals, telemetryContext, this.Logger, async () =>
            {
                if (!string.IsNullOrEmpty(teamName) && !string.IsNullOrEmpty(executionGoalId))
                {
                    Item<GoalBasedSchedule> executionGoal = await this.ExecutionGoalDataManager.GetExecutionGoalAsync(executionGoalId, teamName, cancellationToken)
                        .ConfigureDefaults();

                    telemetryContext.AddContext(nameof(executionGoal), executionGoal);
                    return this.Ok(executionGoal);
                }

                IEnumerable<Item<GoalBasedSchedule>> executionGoals = await this.ExecutionGoalDataManager.GetExecutionGoalsAsync(cancellationToken, teamName)
                        .ConfigureDefaults();

                telemetryContext.AddContext(nameof(executionGoals), executionGoals);
                return this.Ok(executionGoals);
            }).ConfigureDefaults();
        }

        private async Task<IActionResult> GetTimelineExecutionGoalsAsync(string teamName, string executionGoalId, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
               .AddContext(nameof(executionGoalId), executionGoalId)
               .AddContext(nameof(teamName), teamName);

            return await this.ExecuteApiOperationAsync(EventNames.GetTimelineExecutionGoals, telemetryContext, this.Logger, async () =>
            {
                if (string.IsNullOrEmpty(teamName) || string.IsNullOrEmpty(executionGoalId))
                {
                    return this.DataSchemaInvalid($"Route is missing {nameof(teamName)} and/or {nameof(executionGoalId)} argument.");
                }

                Item<GoalBasedSchedule> executionGoal = await this.ExecutionGoalDataManager.GetExecutionGoalAsync(executionGoalId, teamName, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(nameof(executionGoal), executionGoal);

                IList<TargetGoalTimeline> executionGoalsStatuses = await this.ExecutionGoalTelemetryDataManager.GetExecutionGoalTimelineAsync(executionGoal.Definition, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(nameof(executionGoalsStatuses), executionGoalsStatuses);

                return this.Ok(executionGoalsStatuses);
            }).ConfigureDefaults();
        }

        private async Task<IActionResult> GetStatusExecutionGoalsAsync(string teamName, string executionGoalId, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
               .AddContext(nameof(executionGoalId), executionGoalId)
               .AddContext(nameof(teamName), teamName);

            return await this.ExecuteApiOperationAsync(EventNames.GetStatusExecutionGoals, telemetryContext, this.Logger, async () =>
            {
                if (string.IsNullOrEmpty(teamName) || string.IsNullOrEmpty(executionGoalId))
                {
                    return this.DataSchemaInvalid($"Route is missing {nameof(teamName)} and/or {nameof(executionGoalId)} argument.");
                }

                IList<ExperimentInstanceStatus> executionGoalStatus = await this.ExecutionGoalTelemetryDataManager.GetExecutionGoalStatusAsync(executionGoalId, cancellationToken, teamName)
                    .ConfigureDefaults();

                telemetryContext.AddContext($"{nameof(executionGoalStatus)}.Count", executionGoalStatus.Count);
                return this.Ok(executionGoalStatus);
            }).ConfigureDefaults();
        }

        private async Task<IActionResult> GetSummaryExecutionGoalsAsync(string teamName, string executionGoalId, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
               .AddContext(nameof(executionGoalId), executionGoalId)
               .AddContext(nameof(teamName), teamName);

            return await this.ExecuteApiOperationAsync(EventNames.GetSummaryExecutionGoals, telemetryContext, this.Logger, async () =>
            {
                IEnumerable<ExecutionGoalSummary> executionGoal = await this.ExecutionGoalDataManager.GetExecutionGoalsInfoAsync(cancellationToken, teamName, executionGoalId)
                        .ConfigureDefaults();

                telemetryContext.AddContext(nameof(executionGoal), executionGoal);
                return this.Ok(executionGoal);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Event names used for logging telemetry
        /// </summary>
        private static class EventNames
        {
            public static readonly string GetExecutionGoalTemplate = EventContext.GetEventName(ExecutionGoalsController.ApiName, "GetExecutionGoalTemplate");
            public static readonly string SubmitExperimentGoal = EventContext.GetEventName(ExecutionGoalsController.ApiName, "SubmitExperimentGoal");
            public static readonly string CreateExecutionGoal = EventContext.GetEventName(ExecutionGoalsController.ApiName, "CreateExecutionGoal");
            public static readonly string GetExecutionGoals = EventContext.GetEventName(ExecutionGoalsController.ApiName, "GetExecutionGoals");
            public static readonly string GetFullExecutionGoals = EventContext.GetEventName(ExecutionGoalsController.ApiName, "GetFullExecutionGoals");
            public static readonly string GetTimelineExecutionGoals = EventContext.GetEventName(ExecutionGoalsController.ApiName, "GetTimelineExecutionGoals");
            public static readonly string GetStatusExecutionGoals = EventContext.GetEventName(ExecutionGoalsController.ApiName, "GetStatusExecutionGoals");
            public static readonly string GetSummaryExecutionGoals = EventContext.GetEventName(ExecutionGoalsController.ApiName, "GetSummaryExecutionGoals");
            public static readonly string GetResourceByExecutionGoal = EventContext.GetEventName(ExecutionGoalsController.ApiName, "GetResourceByExecutionGoal");
            public static readonly string UpdateExecutionGoal = EventContext.GetEventName(ExecutionGoalsController.ApiName, "UpdateExecutionGoal");
            public static readonly string DeleteExecutionGoal = EventContext.GetEventName(ExecutionGoalsController.ApiName, "DeleteExecutionGoal");
            public static readonly string CreateExecutionGoalFromTemplate = EventContext.GetEventName(ExecutionGoalsController.ApiName, "CreateExecutionGoalFromTemplate");
            public static readonly string UpdateExecutionGoalFromTemplate = EventContext.GetEventName(ExecutionGoalsController.ApiName, "UpdateExecutionGoalFromTemplate");
        }

    }
}