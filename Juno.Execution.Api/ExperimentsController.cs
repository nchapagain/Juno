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
    using Juno.Extensions.Telemetry;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
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
    [Route("/api/experiments")]
    public partial class ExperimentsController : ControllerBase
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
        public ExperimentsController(IExperimentDataManager dataManager, ILogger logger = null)
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
        /// Creates a new experiment instance in the system.
        /// </summary>
        /// <param name="experiment">The experiment definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="201">Created. The experiment instance was created successfully.</response>
        /// <response code="400">Bad Request. The schema of the experiment is invalid.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpPost]
        [Consumes("application/json")]
        [Description("Creates a new experiment instance in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateExperimentAsync([FromBody] Experiment experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(experiment);

            return await this.ExecuteApiOperationAsync(EventNames.CreateExperiment, telemetryContext, this.Logger, async () =>
            {
                Experiment inlinedExperiment = experiment.Inlined();
                ValidationResult validationResult = ExperimentValidation.Instance.Validate(inlinedExperiment);

                if (!validationResult.IsValid)
                {
                    throw new SchemaException(
                        $"The experiment provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                        $"{string.Join($"{Environment.NewLine} -", validationResult.ValidationErrors)}");
                }

                // Notes:
                // We do not have a consensus on where the recommendation ID should be created or the format of it. We are
                // leaving this here for reference as we come back to this so that we do not lose track of the work we did
                // to integrate it and can simply refactor that to match the requisite semantics in the future.
                // inlinedExperiment.AddRecommendationId();

                ExperimentInstance instance = await this.DataManager.CreateExperimentAsync(inlinedExperiment, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext("experimentId", instance?.Id);

                return this.CreatedAtAction(nameof(this.GetExperimentAsync), new { experimentId = instance.Id }, instance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Creates a new experiment context/metadata instance in the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment for which the context/metadata is related.</param>
        /// <param name="context">The context/metadata definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <response code="201">Created. The experiment context instance was created successfully.</response>
        /// <response code="404">Not Found. An experiment with the ID provided does not exist.</response>
        /// <response code="409">Conflict. A set of steps already exist for the experiment with the ID provided.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpPost("{experimentId}/context")]
        [Consumes("application/json")]
        [Description("Creates a new experiment context/metadata instance in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentMetadataInstance), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateExperimentContextAsync(string experimentId, [FromBody] ExperimentMetadata context, CancellationToken cancellationToken, [FromQuery] string contextId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            context.ThrowIfNull(nameof(context));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId)
                .AddContext(context);

            return await this.ExecuteApiOperationAsync(EventNames.CreateExperimentContext, telemetryContext, this.Logger, async () =>
            {
                IActionResult result = null;
                ExperimentInstance experiment = await this.DataManager.GetExperimentAsync(experimentId, cancellationToken)
                    .ConfigureDefaults();

                if (experiment == null)
                {
                    result = this.DataNotFound($"A matching experiment with ID '{experimentId}' does not exist.");
                }
                else
                {
                    ExperimentMetadataInstance instance = await this.DataManager.CreateExperimentContextAsync(context, cancellationToken, contextId)
                        .ConfigureDefaults();

                    telemetryContext.AddContext(instance);
                    result = this.CreatedAtAction(nameof(this.GetExperimentContextAsync), new { experimentId = experiment.Id, contextId = contextId }, instance);
                }

                return result;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Creates a new set of steps in system that define the execution requirements of the experiment.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment in the system.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="definition">An optional workflow step definition to create for the experiment in the system.</param>
        /// <param name="sequence">An optional parameter defines the the sequence for the step.</param>
        /// <response code="201">Created. The experiment steps were created successfully.</response>
        /// <response code="404">Not Found. An experiment with the ID provided does not exist.</response>
        /// <response code="409">Conflict. A set of steps already exist for the experiment with the ID provided.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpPost("{experimentId}/steps")]
        [Consumes("application/json")]
        [Description("Creates a new set of steps in system that define the execution requirements of the experiment.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentStepInstance), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateExperimentStepsAsync(string experimentId, CancellationToken cancellationToken, [FromBody] ExperimentComponent definition = null, [FromQuery] int? sequence = null)
        {
            experimentId.ThrowIfNull(nameof(experimentId));
            if (definition != null)
            {
                sequence.ThrowIfNull(nameof(sequence));
            }

            if (sequence != null)
            {
                definition.ThrowIfNull(nameof(definition));
            }

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId)
                .AddContext(definition)
                .AddContext(nameof(sequence), sequence);

            return await this.ExecuteApiOperationAsync(EventNames.CreateExperimentSteps, telemetryContext, this.Logger, async () =>
            {
                IActionResult result = null;
                ExperimentInstance experiment = await this.DataManager.GetExperimentAsync(experimentId, cancellationToken)
                    .ConfigureDefaults();

                if (experiment == null)
                {
                    result = this.DataNotFound($"A matching experiment with ID '{experimentId}' does not exist.");
                }
                else
                {
                    List<ExperimentStepInstance> instances = new List<ExperimentStepInstance>();

                    if (definition != null)
                    {
                        instances.AddRange(await this.DataManager.CreateExperimentStepsAsync(experimentId, sequence.Value, definition, cancellationToken).ConfigureDefaults());
                    }
                    else
                    {
                        instances.AddRange(await this.DataManager.CreateExperimentStepsAsync(experiment, cancellationToken).ConfigureDefaults());
                    }

                    instances?.ToList().ForEach(async instance =>
                    {
                        EventContext relatedContext = telemetryContext.Clone(withProperties: true)
                            .AddContext(nameof(experimentId), experimentId)
                            .AddContext(instance);

                        await this.Logger.LogTelemetryAsync(EventNames.CreateExperimentStep, LogLevel.Information, relatedContext)
                            .ConfigureDefaults();
                    });

                    telemetryContext.AddContext(instances);
                    result = this.CreatedAtAction(nameof(this.GetExperimentStepsAsync), new { experimentId = experiment.Id }, instances);
                }

                return result;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Creates a new set of steps in system that define the execution requirements of the experiment for given agent.
        /// </summary>
        /// <param name="definition">An experiment component that provides the definition for the agent step.</param>
        /// <param name="experimentId">The unique ID of the experiment to which the agent step is related.</param>
        /// <param name="agentId">The unique ID of the agent step.</param>
        /// <param name="parentStepId">The unique ID or the parent step.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        [HttpPost("{experimentId}/agent-steps")]
        [Description(" Creates a new step in system that defines an execution requirement for a specific agent running in an experiment.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateExperimentAgentStepAsync([FromBody] ExperimentComponent definition, string experimentId, [FromQuery] string agentId, [FromQuery] string parentStepId, CancellationToken cancellationToken)
        {
            definition.ThrowIfNull(nameof(definition));
            parentStepId.ThrowIfNullOrWhiteSpace(nameof(parentStepId));
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(definition)
                .AddContext(nameof(parentStepId), parentStepId)
                .AddContext(nameof(agentId), agentId);

            return await this.ExecuteApiOperationAsync(EventNames.CreateExperimentAgentStep, telemetryContext, this.Logger, async () =>
            {
                IActionResult result = null;

                ExperimentStepInstance parentStep = await this.DataManager.GetExperimentStepAsync(
                    experimentId,
                    parentStepId,
                    cancellationToken).ConfigureDefaults();

                telemetryContext.AddContext(nameof(parentStep), parentStep);

                IEnumerable<ExperimentStepInstance> agentSteps = await this.DataManager.CreateAgentStepsAsync(
                    parentStep,
                    definition,
                    agentId,
                    cancellationToken).ConfigureDefaults();

                telemetryContext.AddContext(nameof(agentSteps), agentSteps);

                result = this.CreatedAtAction(nameof(this.GetExperimentAgentStepsAsync), new { experimentId }, agentSteps);

                return result;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Deletes an experiment instance from the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment to delete.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="204">No Content. The experiment instance was deleted successfully.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpDelete("{experimentId}")]
        [Description("Deletes an existing experiment from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteExperimentAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNull(nameof(experimentId));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId);

            return await this.ExecuteApiOperationAsync(EventNames.DeleteExperiment, telemetryContext, this.Logger, async () =>
            {
                await this.DataManager.DeleteExperimentAsync(experimentId, cancellationToken).ConfigureDefaults();
                return this.NoContent();
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Deletes an experiment instance from the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment to delete.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <response code="204">No Content. The experiment context instance was deleted successfully.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpDelete("{experimentId}/context")]
        [Description("Deletes an existing experiment from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteExperimentContextAsync(string experimentId, CancellationToken cancellationToken, [FromQuery] string contextId = null)
        {
            experimentId.ThrowIfNull(nameof(experimentId));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId);

            return await this.ExecuteApiOperationAsync(EventNames.DeleteExperimentContext, telemetryContext, this.Logger, async () =>
            {
                await this.DataManager.DeleteExperimentContextAsync(experimentId, cancellationToken, contextId)
                    .ConfigureDefaults();

                return this.NoContent();
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Deletes the steps for an experiment from the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment whose steps to delete.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="204">No Content. The experiment step instances were deleted successfully.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpDelete("{experimentId}/steps")]
        [Description("Deletes the steps for an experiment from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteExperimentStepsAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNull(nameof(experimentId));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId);

            return await this.ExecuteApiOperationAsync(EventNames.DeleteExperimentSteps, telemetryContext, this.Logger, async () =>
            {
                await this.DataManager.DeleteExperimentStepsAsync(experimentId, cancellationToken).ConfigureDefaults();
                return this.NoContent();
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Deletes the steps for an experiment from the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment whose steps to delete.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="204">No Content. The experiment step instances were deleted successfully.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpDelete("{experimentId}/agent-steps")]
        [Description("Deletes all agent steps for an experiment from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteExperimentAgentStepsAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNull(nameof(experimentId));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId);

            return await this.ExecuteApiOperationAsync(EventNames.DeleteExperimentAgentSteps, telemetryContext, this.Logger, async () =>
            {
                await this.DataManager.DeleteAgentStepsAsync(experimentId, cancellationToken).ConfigureDefaults();
                return this.NoContent();
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets an experiment instance from the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment instance was found in the system.</response>
        /// <response code="404">Not Found. The experiment instance was not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet("{experimentId}")]
        [Consumes("application/json")]
        [Description("Gets an existing experiment instance from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNull(nameof(experimentId));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId);

            return await this.ExecuteApiOperationAsync(EventNames.GetExperiment, telemetryContext, this.Logger, async () =>
            {
                ExperimentInstance instance = await this.DataManager.GetExperimentAsync(experimentId, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(instance);

                return this.Ok(instance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets an experiment runtime context/metadata instance from the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <response code="200">OK. The experiment context instance was found in the system.</response>
        /// <response code="404">Not Found. The experiment context instance was not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet("{experimentId}/context")]
        [Consumes("application/json")]
        [Description("Gets an existing experiment instance from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentContextAsync(string experimentId, CancellationToken cancellationToken, [FromQuery] string contextId = null)
        {
            experimentId.ThrowIfNull(nameof(experimentId));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId);

            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentContext, telemetryContext, this.Logger, async () =>
            {
                ExperimentMetadataInstance instance = await this.DataManager.GetExperimentContextAsync(experimentId, cancellationToken, contextId)
                    .ConfigureDefaults();

                telemetryContext.AddContext(instance);

                return this.Ok(instance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets all of the execution steps defined in the system for a given experiment.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="filter">An OData query filter to apply to the agent step search criteria (e.g. (Status eq 'InProgress or Status eq 'InProgressContinue') ).</param>
        /// <response code="200">OK. The experiment step instances were found in the system.</response>
        /// <response code="404">Not Found. The experiment step instances were not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        /// <remarks>
        /// Additional Examples:
        /// 
        /// https://docs.microsoft.com/en-us/dynamics-nav/using-filter-expressions-in-odata-uris
        /// 
        /// GET /api/experiments/{experimentId}/steps?filter=(Status eq 'InProgress or Status eq 'InProgressContinue')
        /// </remarks>
        [HttpGet("{experimentId}/steps")]
        [Consumes("application/json")]
        [Description("Gets all of the execution steps defined in the system for a given experiment.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentStepsAsync(string experimentId, CancellationToken cancellationToken, [FromQuery] string filter = null)
        {
            experimentId.ThrowIfNull(nameof(experimentId));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId)
                .AddContext(nameof(filter), filter);

            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentSteps, telemetryContext, this.Logger, async () =>
            {
                QueryFilter queryFilter = !string.IsNullOrWhiteSpace(filter)
                    ? new QueryFilter(filter)
                    : null;

                IEnumerable<ExperimentStepInstance> instances = await this.DataManager.GetExperimentStepsAsync(experimentId, cancellationToken, queryFilter)
                    .ConfigureDefaults();

                telemetryContext.AddContext(instances);

                return this.Ok(instances);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets all of the agent execution steps defined in the system for a given parent step.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment for which the agent steps are related.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="filter">
        /// An OData query filter to apply to the agent step search criteria (e.g. (Status eq 'InProgress or Status eq 'InProgressContinue') ).
        /// </param>
        /// <remarks>
        /// Additional Examples:
        /// 
        /// https://docs.microsoft.com/en-us/dynamics-nav/using-filter-expressions-in-odata-uris
        /// 
        /// GET /api/experiments/{experimentId}/agents/steps?filter=(ParentStepId eq '72B92AC8-1FBE-4745-B2AD-501784279806')
        /// 
        /// GET /api/experiments/{experimentId}/agents/steps?filter=(Status eq 'InProgress or Status eq 'InProgressContinue')
        /// </remarks>
        [HttpGet("{experimentId}/agent-steps")]
        [Description("Gets all of the experiment agent steps defined in the system for a given experiment and/or parent step.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentAgentStepsAsync(string experimentId, CancellationToken cancellationToken, [FromQuery] string filter = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId)
                .AddContext(nameof(filter), filter);

            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentAgentSteps, telemetryContext, this.Logger, async () =>
            {
                QueryFilter queryFilter = !string.IsNullOrWhiteSpace(filter)
                    ? new QueryFilter(filter)
                    : null;

                IEnumerable<ExperimentStepInstance> matchingSteps = await this.DataManager.GetExperimentAgentStepsAsync(experimentId, cancellationToken, queryFilter)
                    .ConfigureDefaults();

                telemetryContext.AddContext(matchingSteps);

                return this.Ok(matchingSteps);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an existing experiment instance in the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment to update.</param>
        /// <param name="experiment">The updated experiment definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment instance was updated successfully.</response>
        /// <response code="400">Bad Request. The schema of the experiment is invalid.</response>
        /// <response code="404">Not Found. The experiment instance does not exist in the system.</response>
        /// <response code="412">Precondition Failed. The experiment instance provided has a mismatched eTag or partition key.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpPut("{experimentId}")]
        [Consumes("application/json")]
        [Description("Updates an existing experiment instance in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateExperimentAsync(string experimentId, [FromBody] ExperimentInstance experiment, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            experiment.ThrowIfNull(nameof(experiment));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId)
                .AddContext(experiment);

            return await this.ExecuteApiOperationAsync(EventNames.UpdateExperiment, telemetryContext, this.Logger, async () =>
            {
                ValidationResult validationResult = ExperimentValidation.Instance.Validate(experiment.Definition);
                if (!validationResult.IsValid)
                {
                    throw new SchemaException(
                        $"The experiment provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                        $"{string.Join($"{Environment.NewLine} -", validationResult.ValidationErrors)}");
                }

                ExperimentInstance instance = await this.DataManager.UpdateExperimentAsync(experiment, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext("eTag", instance.GetETag());
                telemetryContext.AddContext("partition", instance.GetPartition());

                return this.Ok(instance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an existing experiment context/metadata instance in the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="context">The updated context/metdata definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <response code="200">OK. The experiment context instance was updated successfully.</response>
        /// <response code="404">Not Found. The experiment context instance does not exist in the system.</response>
        /// <response code="412">Precondition Failed. The experiment context instance provided has a mismatched eTag or partition key.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpPut("{experimentId}/context")]
        [Consumes("application/json")]
        [Description("Updates an existing experiment context instance definition in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateExperimentContextAsync(string experimentId, [FromBody] ExperimentMetadataInstance context, CancellationToken cancellationToken, [FromQuery] string contextId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            context.ThrowIfNull(nameof(context));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId)
                .AddContext(context);

            return await this.ExecuteApiOperationAsync(EventNames.UpdateExperimentContext, telemetryContext, this.Logger, async () =>
            {
                ExperimentMetadataInstance instance = await this.DataManager.UpdateExperimentContextAsync(context, cancellationToken, contextId)
                    .ConfigureDefaults();

                telemetryContext.AddContext("eTag", instance.GetETag());
                telemetryContext.AddContext("partition", instance.GetPartition());

                return this.Ok(instance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates a existing experiment step in the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="stepId">The unique ID of the experiment step.</param>
        /// <param name="step">The updated step definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment step instance was updated successfully.</response>
        /// <response code="404">Not Found. The experiment step instance does not exist in the system.</response>
        /// <response code="412">Precondition Failed. The experiment step instance provided has a mismatched eTag or partition key.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpPut("{experimentId}/steps/{stepId}")]
        [Consumes("application/json")]
        [Description("Updates a step instance for a given experiment.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateExperimentStepAsync(string experimentId, string stepId, [FromBody] ExperimentStepInstance step, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            stepId.ThrowIfNullOrWhiteSpace(nameof(stepId));
            step.ThrowIfNull(nameof(step));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId)
                .AddContext(step);

            return await this.ExecuteApiOperationAsync(EventNames.UpdateExperimentStep, telemetryContext, this.Logger, async () =>
            {
                ExperimentStepInstance instance = await this.DataManager.UpdateExperimentStepAsync(step, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext("eTag", instance.GetETag());
                telemetryContext.AddContext("partition", instance.GetPartition());

                return this.Ok(instance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an existing experiment step in the system for a given agent running in
        /// the environment.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="stepId">The unique ID of the step.</param>
        /// <param name="step">The updated step definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment agent step instance was updated successfully.</response>
        /// <response code="404">Not Found. The experiment agent step instance does not exist in the system.</response>
        /// <response code="412">Precondition Failed. The experiment agent step instance provided has a mismatched eTag or partition key.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpPut("{experimentId}/agent-steps/{stepId}")]
        [Consumes("application/json")]
        [Description("Updates an existing experiment agent step in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateExperimentAgentStepAsync(string experimentId, string stepId, [FromBody] ExperimentStepInstance step, CancellationToken cancellationToken)
        {
            try
            {
                experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
                stepId.ThrowIfNullOrWhiteSpace(nameof(stepId));
                step.ThrowIfNull(nameof(step));

                // Persist the activity ID so that it can be used to correlate telemetry events
                // down the callstack.
                EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                    .AddContext(nameof(experimentId), experimentId)
                    .AddContext(step);

                return await this.ExecuteApiOperationAsync(EventNames.UpdateExperimentAgentStep, telemetryContext, this.Logger, async () =>
                {
                    ExperimentStepInstance instance = await this.DataManager.UpdateAgentStepAsync(step, cancellationToken)
                        .ConfigureDefaults();

                    telemetryContext.AddContext("eTag", instance.GetETag());
                    telemetryContext.AddContext("partition", instance.GetPartition());

                    return this.Ok(instance);

                }).ConfigureDefaults();
            }
            catch (ArgumentException exc)
            {
                return this.Error(exc, StatusCodes.Status400BadRequest);
            }
        }

        /// <summary>
        /// Provides the names of telemetry events emitted by the controller.
        /// </summary>
        private static class EventNames
        {
            public static readonly string CreateExperimentAgentStep = EventContext.GetEventName(ExperimentsController.ApiName, "CreateExperimentAgentStep");
            public static readonly string CreateExperimentContext = EventContext.GetEventName(ExperimentsController.ApiName, "CreateExperimentContext");
            public static readonly string CreateExperiment = EventContext.GetEventName(ExperimentsController.ApiName, "CreateExperiment");
            public static readonly string CreateExperimentStep = EventContext.GetEventName(ExperimentsController.ApiName, "CreateExperimentStep");
            public static readonly string CreateExperimentSteps = EventContext.GetEventName(ExperimentsController.ApiName, "CreateExperimentSteps");

            public static readonly string DeleteExperimentAgentSteps = EventContext.GetEventName(ExperimentsController.ApiName, "DeleteExperimentAgentSteps");
            public static readonly string DeleteExperiment = EventContext.GetEventName(ExperimentsController.ApiName, "DeleteExperiment");
            public static readonly string DeleteExperimentContext = EventContext.GetEventName(ExperimentsController.ApiName, "DeleteExperimentContext");
            public static readonly string DeleteExperimentSteps = EventContext.GetEventName(ExperimentsController.ApiName, "DeleteExperimentSteps");

            public static readonly string GetExperimentAgentSteps = EventContext.GetEventName(ExperimentsController.ApiName, "GetExperimentAgentSteps");
            public static readonly string GetExperiment = EventContext.GetEventName(ExperimentsController.ApiName, "GetExperiment");
            public static readonly string GetExperimentContext = EventContext.GetEventName(ExperimentsController.ApiName, "GetExperimentContext");
            public static readonly string GetExperimentSteps = EventContext.GetEventName(ExperimentsController.ApiName, "GetExperimentSteps");

            public static readonly string UpdateExperimentAgentStep = EventContext.GetEventName(ExperimentsController.ApiName, "UpdateExperimentAgentStep");
            public static readonly string UpdateExperiment = EventContext.GetEventName(ExperimentsController.ApiName, "UpdateExperiment");
            public static readonly string UpdateExperimentContext = EventContext.GetEventName(ExperimentsController.ApiName, "UpdateExperimentContext");
            public static readonly string UpdateExperimentStep = EventContext.GetEventName(ExperimentsController.ApiName, "UpdateExperimentStep");
        }
    }
}
