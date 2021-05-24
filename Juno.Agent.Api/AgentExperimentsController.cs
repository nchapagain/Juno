namespace Juno.Agent.Api
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
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Juno execution REST API controller for managing agent experiment data
    /// in the system.
    /// </summary>
    [ApiController]
    [Route("/api/experiments")]
    [Produces("application/json")]
    public partial class AgentExperimentsController : ControllerBase
    {
        /// <summary>
        /// The name of the API.
        /// </summary>
        public const string ApiName = "AgentApi";
        private const string V1 = "v1";

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentExperimentsController"/> class.
        /// </summary>
        /// <param name="dataManager">The data manager for experiment data, documents and steps.</param>
        /// <param name="logger">The trace/telemetry logger for the controller.</param>
        public AgentExperimentsController(IExperimentDataManager dataManager, ILogger logger = null)
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
        /// Gets the experiment for which the agent is associated.
        /// </summary>
        /// <param name="agentId">The Id of the agent</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">Ok. The experiment step is found.</response>
        /// <response code="400">Bad Request. The agent id is invalid.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpGet]
        [Description("Gets the experiment in the system for a given agent.")]
        [ApiExplorerSettings(GroupName = AgentExperimentsController.V1)]
        [ProducesResponseType(typeof(AgentHeartbeatInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAgentExperimentAsync([FromQuery] string agentId, CancellationToken cancellationToken)
        {
            agentId.ThrowIfNullOrWhiteSpace(agentId);

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(agentId), agentId);

            return await this.ExecuteApiOperationAsync(EventNames.GetAgentExperiment, telemetryContext, this.Logger, async () =>
            {
                ExperimentInstance experiment = await this.DataManager.GetAgentExperimentAsync(agentId, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(experiment);

                if (experiment == null)
                {
                    throw new DataStoreException(
                        $"Experiment not found. The agent with ID '{agentId}' is not associated with any experiment.",
                        DataErrorReason.DataNotFound);
                }

                telemetryContext.AddContext("experimentId", experiment.Id);

                return this.Ok(experiment);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets all of the execution steps defined in the system for a given agent
        /// </summary>
        /// <param name="agentId">The Id of an agent</param>
        /// <param name="filter">The step execution status filters</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">Ok. The experiment step is found.</response>
        /// <response code="400">Bad Request. The agent id is invalid.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpGet("agent-steps")]
        [Description("Gets all of the execution steps defined in the system for a given agent running as part of an experiment.")]
        [ApiExplorerSettings(GroupName = AgentExperimentsController.V1)]
        [ProducesResponseType(typeof(AgentHeartbeatInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAgentStepsAsync([FromQuery] string agentId, CancellationToken cancellationToken, [FromQuery] string filter = null)
        {
            agentId.ThrowIfNullOrWhiteSpace(agentId);

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(agentId), agentId)
                .AddContext(nameof(filter), filter);

            return await this.ExecuteApiOperationAsync(EventNames.GetAgentSteps, telemetryContext, this.Logger, async () =>
            {
                QueryFilter queryFilter = !string.IsNullOrWhiteSpace(filter)
                    ? new QueryFilter(filter)
                    : null;

                IEnumerable<ExperimentStepInstance> instances = await this.DataManager.GetAgentStepsAsync(
                    agentId,
                    cancellationToken,
                    queryFilter).ConfigureDefaults();

                telemetryContext.AddContext(instances);

                return this.Ok(instances);
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
        [ApiExplorerSettings(GroupName = AgentExperimentsController.V1)]
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
        [ApiExplorerSettings(GroupName = AgentExperimentsController.V1)]
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
        [ApiExplorerSettings(GroupName = AgentExperimentsController.V1)]
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
        /// Updates an existing experiment step in the system for a given agent.
        /// </summary>
        /// <param name="stepId">The unique ID of the step to update.</param>
        /// <param name="step">The step to be updated</param>
        /// <param name="cancellationToken"></param>
        /// <response code="200">OK. The experiment step instance was updated successfully.</response>
        /// <response code="400">Bad Request. The ExperimentStepInstance is invalid.</response>
        /// <response code="404">Not Found. The experiment step instance does not exist in the system.</response>
        /// <response code="412">Precondition Failed. The experiment step instance provided has a mismatched eTag or partition key.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpPut("agent-steps/{stepId}")]
        [Description("Updates an existing experiment step in the system for a given agent running in the environment.")]
        [ApiExplorerSettings(GroupName = AgentExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateAgentStepAsync(string stepId, ExperimentStepInstance step, CancellationToken cancellationToken)
        {
            stepId.ThrowIfNullOrWhiteSpace(nameof(stepId));

            stepId.ThrowIfInvalid(
                nameof(stepId),
                (id) => string.Equals(id, step.Id, StringComparison.OrdinalIgnoreCase),
                $"The step provided has an ID that does not match the step ID provided.");

            step.ThrowIfNull(nameof(step));
            step.AgentId.ThrowIfNullOrWhiteSpace(
                nameof(step),
                $"The step provided does not define the ID of the agent to which it is targeted.");

            // Persist the activity ID so that it can be used down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(stepId), stepId)
                .AddContext(step);

            return await this.ExecuteApiOperationAsync(EventNames.UpdateAgentStep, telemetryContext, this.Logger, async () =>
            {
                ExperimentStepInstance instance = await this.DataManager.UpdateAgentStepAsync(step, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext("eTag", step.GetETag());
                telemetryContext.AddContext("partition", step.GetPartition());

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
        [Description("Updates an existing experiment instance runtime context definition in the system.")]
        [ApiExplorerSettings(GroupName = AgentExperimentsController.V1)]
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

                telemetryContext.AddContext("eTag", context.GetETag());
                telemetryContext.AddContext("partition", context.GetPartition());

                return this.Ok(instance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Provides the names of telemetry events emitted by the controller.
        /// </summary>
        private static class EventNames
        {
            public static readonly string GetExperiment = EventContext.GetEventName(AgentExperimentsController.ApiName, "GetExperiment");
            public static readonly string CreateExperimentContext = EventContext.GetEventName(AgentExperimentsController.ApiName, "CreateExperimentContext");
            public static readonly string GetExperimentContext = EventContext.GetEventName(AgentExperimentsController.ApiName, "GetExperimentContext");
            public static readonly string GetAgentExperiment = EventContext.GetEventName(AgentExperimentsController.ApiName, "GetAgentExperiment");
            public static readonly string GetAgentSteps = EventContext.GetEventName(AgentExperimentsController.ApiName, "GetAgentSteps");
            public static readonly string UpdateAgentStep = EventContext.GetEventName(AgentExperimentsController.ApiName, "UpdateAgentStep");
            public static readonly string UpdateExperimentContext = EventContext.GetEventName(AgentExperimentsController.ApiName, "UpdateExperimentContext");
        }
    }
}
