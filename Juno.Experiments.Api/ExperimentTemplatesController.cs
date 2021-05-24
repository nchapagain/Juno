namespace Juno.Experiments.Api
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Net.Http;
    using System.Text.RegularExpressions;
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
    /// Juno experiments REST API controller for managing experiment templates in the system.
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    [Route("/api/experimentTemplates")]
    public partial class ExperimentTemplatesController : ControllerBase
    {
        /// <summary>
        /// The name of the API.
        /// </summary>
        public const string ApiName = "ExperimentTemplatesApi";
        private const string V1 = "v1";

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentsController"/> class.
        /// </summary>
        /// <param name="executionClient">The Juno Execution API client to use for communications with the Juno system.</param>
        /// <param name="logger">The trace/telemetry logger for the controller.</param>
        public ExperimentTemplatesController(ExecutionClient executionClient, ILogger logger = null)
        {
            executionClient.ThrowIfNull(nameof(executionClient), $"An execution client must be provided to the controller.");
            
            this.Client = executionClient;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the data layer component that provides data management functionality
        /// to the controller.
        /// </summary>
        protected ExecutionClient Client { get; }

        /// <summary>
        /// Gets the trace/telemetry logger for the controller.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Creates a new experiment template in the system.
        /// </summary>
        /// <param name="experiment">The experiment template.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="201">Created. The experiment instance was created successfully.</response>
        /// <response code="400">Bad Request. The schema of the experiment is invalid.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpPost]
        [Consumes("application/json")]
        [Description("Creates a new experiment definition in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentTemplatesController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateExperimentTemplateAsync([FromBody] Experiment experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));
            experiment.ThrowIfNull(experiment.Metadata["teamName"]?.ToString());
            experiment.ThrowIfNull(experiment.Name);

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experiment), experiment);

            return await this.ExecuteApiOperationAsync(EventNames.CreateExperimentTemplate, telemetryContext, this.Logger, async () =>
            {
                // Ref : https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.documents.resource.id?view=azure-dotnet
                string unsuportedCharacters = "[/?#/\\\\]";
                if (Regex.Matches(experiment.Name, unsuportedCharacters).Count > 0)
                {
                    throw new FormatException(string.Format("Experiment Name contains invalid characters {0}", unsuportedCharacters));
                }

                ValidationResult validationResult = ExperimentValidation.Instance.Validate(experiment.Inlined());
                if (!validationResult.IsValid)
                {
                    throw new SchemaException(
                        $"The experiment provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                        $"{string.Join($"{Environment.NewLine} -", validationResult.ValidationErrors)}");
                }
                else
                {
                    HttpResponseMessage response = await this.Client.CreateExperimentTemplateAsync(experiment, cancellationToken)
                        .ConfigureDefaults();

                    telemetryContext.AddContext(response);
                    if (response.IsSuccessStatusCode)
                    {
                        ExperimentItem createdInstance = await response.Content.ReadAsJsonAsync<ExperimentItem>().ConfigureDefaults();
                        telemetryContext.AddContext("experimentTemplateId", createdInstance.Id);
                        return this.CreatedAtAction(nameof(this.CreateExperimentTemplateAsync), new { experimentId = createdInstance.Id, teamName = createdInstance.Definition.Metadata["teamName"].ToString() }, createdInstance);
                    }
                    else
                    {
                        return await ExperimentTemplatesController.CreateErrorResponseAsync(response).ConfigureDefaults();
                    }
                }
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Deletes an existing experiment definition from the system.
        /// </summary>
        /// <param name="experimentTemplateId">Id of the Template to be deleted</param>
        /// <param name="teamName">Team to which template belongs</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="204">No Content. The experiment instance was deleted successfully.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpDelete("{teamName}/{experimentTemplateId}")]
        [Consumes("application/json")]
        [Description("Deletes an existing experiment definition in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentTemplatesController.V1)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteExperimentTemplateAsync(string teamName, string experimentTemplateId, CancellationToken cancellationToken)
        {
            experimentTemplateId.ThrowIfNullOrEmpty(nameof(experimentTemplateId));
            teamName.ThrowIfNullOrEmpty(nameof(teamName));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(teamName), teamName)
                .AddContext(nameof(experimentTemplateId), experimentTemplateId);

            return await this.ExecuteApiOperationAsync(EventNames.DeleteExperimentTemplate, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.DeleteExperimentTemplateAsync(experimentTemplateId, teamName, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);
                return this.NoContent();
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets a list of experiment templates from the system for the given teamName
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>        
        /// <response code="200">OK. The experiment instance was found in the system.</response>
        /// <response code="404">Not Found. The experiment instance was not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet]
        [Consumes("application/json")]
        [Description("Gets an existing experiment template list from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentTemplatesController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentTemplatesListAsync(CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid());
            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentTemplate, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.GetExperimentTemplatesListAsync(cancellationToken)
                    .ConfigureDefaults();

                return this.Ok(JsonConvert.DeserializeObject<List<ExperimentItem>>(await response.Content.ReadAsStringAsync().ConfigureDefaults()));
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets an experiment instance from the system.
        /// </summary>
        /// <param name="experimentTemplateId">The unique ID of the experiment template.</param>
        /// <param name="teamName">The team name who owns the experiment definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment instance was found in the system.</response>
        /// <response code="404">Not Found. The experiment instance was not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet("{teamName}/{experimentTemplateId}")]
        [Consumes("application/json")]
        [Description("Gets an existing experiment instance from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentTemplatesController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentTemplateAsync(string teamName, string experimentTemplateId, CancellationToken cancellationToken)
        {
            teamName.ThrowIfNullOrEmpty(nameof(teamName));
            teamName.ThrowIfNullOrEmpty(nameof(experimentTemplateId));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(teamName), teamName);

            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentTemplate, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.GetExperimentTemplateAsync(teamName, experimentTemplateId, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);
                if (!response.IsSuccessStatusCode)
                {
                    return await ExperimentTemplatesController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                ExperimentItem experimentItem = await response.Content.ReadAsJsonAsync<ExperimentItem>().ConfigureDefaults();
                return this.Ok(experimentItem);
            }).ConfigureDefaults();
    }

        /// <summary>
        /// Gets a list of experiment templates from the system for the given teamName
        /// </summary>
        /// <param name="teamName">The team name who owns the experiment definition.</param>    
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>        
        /// <response code="200">OK. The experiment instance was found in the system.</response>
        /// <response code="404">Not Found. The experiment instance was not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet("{teamName}")]
        [Consumes("application/json")]
        [Description("Gets an existing experiment template list from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentTemplatesController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentTemplatesListAsync(string teamName, CancellationToken cancellationToken)
        {
            teamName.ThrowIfNullOrEmpty(nameof(teamName));
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(teamName), teamName);

            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentTemplate, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.GetExperimentTemplatesListAsync(teamName, cancellationToken)
                    .ConfigureDefaults();

                return this.Ok(JsonConvert.DeserializeObject<List<ExperimentItem>>(await response.Content.ReadAsStringAsync().ConfigureDefaults()));
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an existing experiment definition in the system.
        /// </summary>
        /// <param name="experiment">The experiment definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment instance was updated successfully.</response>
        /// <response code="400">Bad Request. The schema of the experiment is invalid.</response>
        /// <response code="404">Not Found. The experiment instance does not exist in the system.</response>
        /// <response code="412">Precondition Failed. The experiment instance provided has a mismatched eTag or partition key.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpPut]
        [Consumes("application/json")]
        [Description("Updates an existing experiment definition in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentTemplatesController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateExperimentTemplateAsync([FromBody] ExperimentItem experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));
            experiment.ThrowIfNull(experiment.Id);
            experiment.ThrowIfNull(experiment.Definition.Metadata["teamName"].ToString());

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experiment), experiment);

            return await this.ExecuteApiOperationAsync(EventNames.UpdateExperimentTemplate, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = null;
                try
                {
                    response = await this.Client.UpdateExperimentTemplateAsync(experiment, cancellationToken)
                        .ConfigureDefaults();

                    if (!response.IsSuccessStatusCode)
                    {
                        return await ExperimentTemplatesController.CreateErrorResponseAsync(response).ConfigureDefaults();
                    }
                
                    ExperimentItem updatedInstance = await response.Content.ReadAsJsonAsync<ExperimentItem>().ConfigureDefaults();
                    string teamName = updatedInstance.Definition.Metadata["teamName"].ToString();
                    telemetryContext.AddContext("ExperimentTemplateId", updatedInstance.Id);
                    return this.CreatedAtAction(nameof(this.GetExperimentTemplateAsync), new { teamName= teamName, experimentTemplateId = updatedInstance.Id }, updatedInstance);
                }
                finally
                {
                    telemetryContext.AddContext(response);
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

        /// <summary>
        /// Provides the names of telemetry events emitted by the controller.
        /// </summary>
        private static class EventNames
        {
            public static readonly string CreateExperimentTemplate = EventContext.GetEventName(ExperimentsController.ApiName, "CreateExperimentTemplate");
            public static readonly string DeleteExperimentTemplate = EventContext.GetEventName(ExperimentsController.ApiName, "DeleteExperimentTemplate");
            public static readonly string GetAllExperimentTemplatesList = EventContext.GetEventName(ExperimentsController.ApiName, "GetAllExperimentTemplatesList");
            public static readonly string GetExperimentTemplate = EventContext.GetEventName(ExperimentsController.ApiName, "GetExperimentTemplate");
            public static readonly string GetExperimentTemplatesList = EventContext.GetEventName(ExperimentsController.ApiName, "GetExperimentTemplatesList");
            public static readonly string UpdateExperimentTemplate = EventContext.GetEventName(ExperimentsController.ApiName, "UpdateExperimentTemplate");
        }
    }
}    
