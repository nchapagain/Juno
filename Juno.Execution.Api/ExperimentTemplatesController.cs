using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Juno.Contracts;
using Juno.Contracts.Validation;
using Juno.DataManagement;
using Juno.Extensions.AspNetCore;
using Juno.Extensions.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CRC.Contracts;
using Microsoft.Azure.CRC.Extensions;
using Microsoft.Azure.CRC.Rest;
using Microsoft.Azure.CRC.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Juno.Execution.Api
{
    /// <summary>
    /// Juno experiments REST API controller for managing experiments
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
    [Route("/api/experimentTemplates")]
    public partial class ExperimentTemplatesController : ControllerBase
    {
        /// <summary>
        /// The name of the API.
        /// </summary>
        public const string ApiName = "ExperimentTemplateApi";
        private const string V1 = "v1";

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentTemplatesController"/> class.
        /// </summary>
        /// <param name="dataManager">The data manager for experiment data, documents and steps.</param>
        /// <param name="logger">The trace/telemetry logger for the controller.</param>
        public ExperimentTemplatesController(IExperimentTemplateDataManager dataManager, ILogger logger = null)
        {
            dataManager.ThrowIfNull(nameof(dataManager), $"A data manager must be provided to the controller.");

            this.DataManager = dataManager;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the data layer component that provides data management functionality
        /// to the controller.
        /// </summary>
        protected IExperimentTemplateDataManager DataManager { get; }

        /// <summary>
        /// Gets the trace/telemetry logger for the controller.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Creates a new experiment template in the system.
        /// </summary>
        /// <param name="experiment">The experiment template.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="201">Created. The experiment template was created successfully.</response>
        /// <response code="400">Bad Request. The schema of the experiment is invalid.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpPost]
        [Consumes("application/json")]
        [Description("Creates a new experiment template in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentTemplatesController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateExperimentTemplateAsync([FromBody] Experiment experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(experiment);

            return await this.ExecuteApiOperationAsync(EventNames.CreateExperimentTemplate, telemetryContext, this.Logger, async () =>
            {
                ValidationResult validationResult = ExperimentValidation.Instance.Validate(experiment.Inlined());
                if (!validationResult.IsValid)
                {
                    throw new SchemaException(
                        $"The experiment provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                        $"{string.Join($"{Environment.NewLine} -", validationResult.ValidationErrors)}");
                }
                else
                {
                    string documentId = experiment.Name;
                    ExperimentItem inputExperiment = new ExperimentItem(documentId, experiment);
                    ExperimentItem experimentItem = await this.DataManager.CreateExperimentTemplateAsync(inputExperiment, documentId, false, cancellationToken)
                        .ConfigureDefaults();

                    telemetryContext.AddContext("experimentId", experimentItem?.Id);
                    return this.CreatedAtAction(nameof(this.CreateExperimentTemplateAsync), new { experimentId = experimentItem.Id }, experimentItem);
                }
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Deletes an existing experiment template in the system.
        /// </summary>
        /// <param name="experimentTemplateId">Id of the Experiment template to be deleted</param>
        /// <param name="teamName">Name of the team that owns the template</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="204">No Content. The experiment instance was deleted successfully.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpDelete("{teamName}/{experimentTemplateId}")]
        [Consumes("application/json")]
        [Description("Deletes an existing experiment template in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentTemplatesController.V1)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteExperimentTemplateAsync(string teamName, string experimentTemplateId,  CancellationToken cancellationToken)
        {
            experimentTemplateId.ThrowIfNullOrEmpty(nameof(experimentTemplateId));
            teamName.ThrowIfNullOrEmpty(nameof(teamName));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(teamName), teamName)
                .AddContext(nameof(experimentTemplateId), experimentTemplateId);

            return await this.ExecuteApiOperationAsync(EventNames.DeleteExperimentTemplate, telemetryContext, this.Logger, async () =>
            {
                await this.DataManager.DeleteExperimentTemplateAsync(experimentTemplateId, teamName, cancellationToken)
                    .ConfigureDefaults();

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
        [Description("Gets a list of all existing experiment templates from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentTemplatesController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentTemplatesListAsync(CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid());
            
            return await this.ExecuteApiOperationAsync(EventNames.GetAllExperimentTemplatesList, telemetryContext, this.Logger, async () =>
            {
                IEnumerable<ExperimentItem> templateIdList = await this.DataManager.GetExperimentTemplatesListAsync(cancellationToken)
                    .ConfigureDefaults();

                List<ExperimentTemplateInfo> result = new List<ExperimentTemplateInfo>();
                foreach (ExperimentItem item in templateIdList)
                {
                    result.Add(new ExperimentTemplateInfo() { Id = item.Id, Description = item.Definition.Description, TeamName = item.Definition.Metadata["teamName"].ToString() });
                }

                telemetryContext.AddContext("ExperimentTemplateInfo", result);
                return this.Ok(templateIdList);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets an experiment template from the system.
        /// </summary>
        /// <param name="experimentTemplateId">The unique ID of the experiment template.</param>
        /// <param name="teamName">The team name who owns the experiment template.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment instance was found in the system.</response>
        /// <response code="404">Not Found. The experiment instance was not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet("{teamName}/{experimentTemplateId}")]
        [Consumes("application/json")]
        [Description("Gets an existing experiment template from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentTemplatesController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentTemplateAsync(string experimentTemplateId, string teamName, CancellationToken cancellationToken)
        {
            teamName.ThrowIfNullOrEmpty(nameof(teamName));
            teamName.ThrowIfNullOrEmpty(nameof(experimentTemplateId));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                        .AddContext(nameof(teamName), teamName)
                        .AddContext(nameof(experimentTemplateId), experimentTemplateId);

            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentTemplate, telemetryContext, this.Logger, async () =>
            {
                ExperimentItem definition = await this.DataManager.GetExperimentTemplateAsync(experimentTemplateId, teamName, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(definition, nameof(definition));
                return this.Ok(definition);
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
        [Description("Gets a list of existing experiment templates from the system for a particular team")]
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
                IEnumerable<ExperimentItem> templateIdList = await this.DataManager.GetExperimentTemplatesListAsync(teamName, cancellationToken)
                    .ConfigureDefaults();

                return this.Ok(templateIdList);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an existing experiment template in the system.
        /// </summary>
        /// <param name="experiment">The experiment template.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment instance was updated successfully.</response>
        /// <response code="400">Bad Request. The schema of the experiment is invalid.</response>
        /// <response code="404">Not Found. The experiment instance does not exist in the system.</response>
        /// <response code="412">Precondition Failed. The experiment instance provided has a mismatched eTag or partition key.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpPut]
        [Consumes("application/json")]
        [Description("Updates an existing experiment template in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentTemplatesController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateExperimentTemplateAsync([FromBody] ExperimentItem experiment, CancellationToken cancellationToken)
        {
            try
            {
                experiment.ThrowIfNull(nameof(experiment));
                experiment.ThrowIfNull(experiment.Id);
                experiment.ThrowIfNull(experiment.Definition.Metadata["teamName"].ToString());
            }
            catch (Exception ex)
            {
                return this.BadRequest(ex.Message);
            }

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experiment), experiment);

            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentTemplate, telemetryContext, this.Logger, async () =>
            {
                return this.Ok(await this.DataManager.CreateExperimentTemplateAsync(experiment, experiment.Id, true, cancellationToken)
                    .ConfigureDefaults());

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
            public static readonly string GetExperimentTemplatesList = EventContext.GetEventName(ExperimentsController.ApiName, "GetExperimentTemplatesList");
            public static readonly string GetExperimentTemplate = EventContext.GetEventName(ExperimentsController.ApiName, "GetExperimentTemplate");
            public static readonly string UpdateExperimentTemplate = EventContext.GetEventName(ExperimentsController.ApiName, "UpdateExperimentTemplate");
        }
    }
}
