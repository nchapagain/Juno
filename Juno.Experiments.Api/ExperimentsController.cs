namespace Juno.Experiments.Api
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Contracts.Validation;
    using Juno.Execution.Providers.Cancellation;
    using Juno.Extensions.AspNetCore;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

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
    [Route("/api/experiments")]
    public partial class ExperimentsController : ControllerBase
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
        public ExperimentsController(ExecutionClient executionClient, IConfiguration configuration, ILogger logger = null)
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
        /// Cancels the execution of an experiment running in the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment to delete.</param>
        /// <param name="cancel">True to cancel the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="204">No Content. The experiment instance was deleted successfully.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpPut("{experimentId}")]
        [Description("Cancels an existing experiment in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CancelExperimentAsync(string experimentId, [FromQuery] bool cancel, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNull(nameof(experimentId));
            cancel.ThrowIfInvalid(nameof(cancel), (arg) => arg != false);

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId);

            return await this.ExecuteApiOperationAsync(EventNames.CancelExperiment, telemetryContext, this.Logger, async () =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ExperimentComponent cancellationStep = new ExperimentComponent(
                        typeof(CancellationProvider).FullName,
                        "Cancel Experiment",
                        "Cancels the current running experiment.",
                        "*");

                    HttpResponseMessage response = await this.Client.CreateExperimentStepsAsync(experimentId, 0, cancellationStep, cancellationToken)
                        .ConfigureDefaults();

                    telemetryContext.AddContext(response);

                    if (!response.IsSuccessStatusCode)
                    {
                        return await ExperimentsController.CreateErrorResponseAsync(response).ConfigureDefaults();
                    }
                }

                return this.NoContent();

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Creates a new experiment instance in the system.
        /// </summary>
        /// <param name="experiment">The experiment definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="workQueue">Optional. The name of the queue to which the notice-of-work should be posted.</param>
        /// <param name="validate">True to have the experiment validated but not submitted.</param>
        /// <response code="201">Created. The experiment instance was created successfully.</response>
        /// <response code="400">Bad Request. The schema of the experiment is invalid.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpPost]
        [Consumes("application/json")]
        [Description("Creates a new experiment instance in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> CreateExperimentAsync([FromBody] Experiment experiment, CancellationToken cancellationToken, [FromQuery] string workQueue = null, [FromQuery] bool validate = false)
        {
            experiment.ThrowIfNull(nameof(experiment));
            if (validate)
            {
                return this.ValidateExperimentAsync(experiment, cancellationToken);
            }
            else
            {
                return this.CreateExperimentAsync(workQueue, experiment, cancellationToken);
            }
        }

        /// <summary>
        /// Creates a new experiment instance in the system.
        /// </summary>
        /// <param name="experimentTemplate">The experiment template JSON and overwrites (if applicable).</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="workQueue">Optional. The name of the queue to which the notice-of-work should be posted.</param>
        /// <response code="201">Created. The experiment instance was created successfully.</response>
        /// <response code="400">Bad Request. The schema of the experiment is invalid.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response> 
        [HttpPost("template")]
        [Consumes("application/json")]
        [Description("Creates a new experiment instance in the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateExperimentAsync([FromBody] ExperimentTemplate experimentTemplate, CancellationToken cancellationToken, [FromQuery] string workQueue = null)
        {
            if (experimentTemplate == null || experimentTemplate.Experiment == null || experimentTemplate.Override == null)
            {
                throw new ArgumentException("ExperimentOverwrite parameter is required.");
            }

            Experiment experiment = experimentTemplate.Experiment;
            JObject o1 = JObject.Parse(JsonConvert.SerializeObject(experimentTemplate.Experiment));
            JObject o2 = JObject.Parse(experimentTemplate.Override);

            o1.Merge(o2, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Union
            });

            try
            {
                experiment = o1.ToObject<Experiment>();
            }
            catch (JsonException)
            {
                return this.Error("The schema of the experiment override is not valid", StatusCodes.Status400BadRequest);
            }

            return await this.CreateExperimentAsync(workQueue, experiment, cancellationToken).ConfigureDefaults();
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
                HttpResponseMessage response = await this.Client.GetExperimentAsync(experimentId, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);

                if (!response.IsSuccessStatusCode)
                {
                    return await ExperimentsController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                ExperimentInstance instance = await response.Content.ReadAsJsonAsync<ExperimentInstance>().ConfigureDefaults();

                return this.Ok(instance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets all of the execution steps defined in the system for a given experiment.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="view">Flag to deletmine the verbosity of the response object</param>
        /// <response code="200">OK. The experiment step instances were found in the system.</response>
        /// <response code="404">Not Found. The experiment step instances were not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet("{experimentId}/steps")]
        [Consumes("application/json")]
        [Description("Gets all of the execution steps defined in the system for a given experiment.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentStepsAsync(string experimentId, CancellationToken cancellationToken, [FromQuery] View view = View.Summary)
        {
            experimentId.ThrowIfNull(nameof(experimentId));

            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId);

            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentSteps, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.GetExperimentStepsAsync(experimentId, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);

                if (!response.IsSuccessStatusCode)
                {
                    return await ExperimentsController.CreateErrorResponseAsync(response).ConfigureDefaults();
                }

                IEnumerable<ExperimentStepInstance> steps = await response.Content.ReadAsJsonAsync<IEnumerable<ExperimentStepInstance>>()
                    .ConfigureDefaults();

                object result = null;
                if (view == View.Summary)
                {
                    result = steps.Select(step => new
                    {
                        id = step.Id,
                        name = step.Definition.Name,
                        experimentGroup = step.ExperimentGroup,
                        status = step.Status.ToString(),
                        startTime = step.StartTime,
                        endTime = step.EndTime
                    });
                    return this.Ok(result);
                }

                return this.Ok(steps);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets an experiment runtime context/metadata instance from the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment context instance was found in the system.</response>
        /// <response code="404">Not Found. The experiment context instance was not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet("{experimentId}/resources")]
        [Consumes("application/json")]
        [Description("Gets an existing experiment context instance from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExperimentResourcesAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNull(nameof(experimentId));

            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(experimentId), experimentId);

            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentResources, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage response = await this.Client.GetExperimentContextAsync(experimentId, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(response);
                ExperimentMetadataInstance metadataInstance = JsonConvert.DeserializeObject<ExperimentMetadataInstance>(await response.Content.ReadAsStringAsync()
                    .ConfigureDefaults());

                return this.Ok(metadataInstance.Extensions.ContainsKey("entitiesProvisioned") ? metadataInstance.Extensions["entitiesProvisioned"] : null);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets list of Providers from the system with relted info.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <response code="200">OK. The experiment context instance was found in the system.</response>
        /// <response code="404">Not Found. The experiment context instance was not found in the system.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        [HttpGet("providers")]
        [Consumes("application/json")]
        [Description("Gets List of Workflow Providers from the system.")]
        [ApiExplorerSettings(GroupName = ExperimentsController.V1)]
        [ProducesResponseType(typeof(ExperimentInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public IActionResult GetProvidersListAsync(CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid());
            List<dynamic> providersList = new List<dynamic>();
            var assemblyDirectory = Path.GetDirectoryName(Assembly.GetAssembly(typeof(ExperimentsController)).Location);

            try
            {
                ExperimentProviderTypeCache.Instance.LoadProviders(assemblyDirectory);

                if (ExperimentProviderTypeCache.Instance?.Any() == true && !cancellationToken.IsCancellationRequested)
                {
                    foreach (var typeEntry in ExperimentProviderTypeCache.Instance)
                    {
                        Type providerType = typeEntry.Value;
                        var provider = new { name = providerType.FullName.ToString(), parameters = new List<dynamic>(), providerInfo = new List<dynamic>() };
                        var supportedParams = providerType.GetCustomAttributes<SupportedParameterAttribute>(true);
                        var providerInfo = providerType.GetCustomAttributes<ProviderInfoAttribute>(true);
                        foreach (var param in supportedParams)
                        {
                            var parameter = new { name = param.Name, required = param.Required };
                            provider.parameters.Add(parameter);
                        }

                        if (providerInfo.Any())
                        {
                            var providerInformation = new { name = providerInfo.FirstOrDefault().Name, description = providerInfo.FirstOrDefault().Description, fullDescription = providerInfo.FirstOrDefault().FullDescription };
                            provider.providerInfo.Add(providerInformation);
                        }

                        providersList.Add(provider);
                    }
                }

            }
            catch (Exception ex)
            {
                return this.Error($"Error Code: {ex.HResult},  {ex.Message}");
            }

            return this.Ok(providersList);
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

        private async Task<IActionResult> CreateExperimentAsync(string workQueue, Experiment experiment, CancellationToken cancellationToken)
        {
            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(experiment);

            return await this.ExecuteApiOperationAsync(EventNames.CreateExperiment, telemetryContext, this.Logger, async () =>
            {
                HttpResponseMessage step1Response = null;
                HttpResponseMessage step2Response = null;
                HttpResponseMessage step3Response = null;
                HttpResponseMessage step4Response = null;

                try
                {
                    Experiment inlinedExperiment = experiment.Inlined();
                    ValidationResult validationResult = ExperimentValidation.Instance.Validate(inlinedExperiment);

                    if (!validationResult.IsValid)
                    {
                        throw new SchemaException(
                            $"The experiment provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                            $"{string.Join($"{Environment.NewLine} -", validationResult.ValidationErrors)}");
                    }

                    // 1) Create the experiment instance
                    step1Response = await this.Client.CreateExperimentAsync(inlinedExperiment, cancellationToken)
                        .ConfigureDefaults();

                    if (!step1Response.IsSuccessStatusCode)
                    {
                        return await ExperimentsController.CreateErrorResponseAsync(step1Response).ConfigureDefaults();
                    }

                    ExperimentInstance instance = await step1Response.Content.ReadAsJsonAsync<ExperimentInstance>().ConfigureDefaults();
                    telemetryContext.AddContext("experimentId", instance.Id);

                    // 2) Create the experiment context object alongside the experiment.
                    step2Response = await this.Client.CreateExperimentContextAsync(instance.Id, new ExperimentMetadata(instance.Id), cancellationToken)
                        .ConfigureDefaults();

                    if (!step2Response.IsSuccessStatusCode)
                    {
                        return await ExperimentsController.CreateErrorResponseAsync(step2Response).ConfigureDefaults();
                    }

                    // 3) Create the execution/orchestration steps for the experiment.
                    step3Response = await this.Client.CreateExperimentStepsAsync(instance.Id, cancellationToken).ConfigureDefaults();
                    if (!step3Response.IsSuccessStatusCode)
                    {
                        return await ExperimentsController.CreateErrorResponseAsync(step3Response).ConfigureDefaults();
                    }

                    string targetWorkQueue = workQueue;
                    if (string.IsNullOrWhiteSpace(targetWorkQueue))
                    {
                        targetWorkQueue = EnvironmentSettings.Initialize(this.Configuration).ExecutionSettings.WorkQueueName;
                    }

                    // 4) Queue experiment notice-of-work
                    step4Response = await this.Client.CreateNoticeAsync(targetWorkQueue, new ExperimentMetadata(instance.Id), cancellationToken)
                        .ConfigureDefaults();

                    if (!step4Response.IsSuccessStatusCode)
                    {
                        return await ExperimentsController.CreateErrorResponseAsync(step4Response).ConfigureDefaults();
                    }

                    return this.CreatedAtAction(nameof(this.GetExperimentAsync), new { experimentId = instance.Id }, instance);
                }
                finally
                {
                    telemetryContext.AddContext(new List<HttpResponseMessage>
                    {
                        step1Response,
                        step2Response,
                        step3Response,
                        step4Response
                    });
                }
            }).ConfigureDefaults();
        }

        private Task<IActionResult> ValidateExperimentAsync(Experiment experiment, CancellationToken cancellationToken)
        {
            // Persist the activity ID so that it can be used to correlate telemetry events
            // down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(experiment);

            return this.ExecuteApiOperationAsync(EventNames.ValidateExperiment, telemetryContext, this.Logger, () =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Experiment inlinedExperiment = experiment.Inlined();
                    ValidationResult validationResult = ExperimentValidation.Instance.Validate(inlinedExperiment);

                    if (!validationResult.IsValid)
                    {
                        throw new SchemaException(
                            $"The experiment provided failed schema validation. The following errors were found:{Environment.NewLine}" +
                            $"{string.Join($"{Environment.NewLine} -", validationResult.ValidationErrors)}");
                    }
                }

                return Task.FromResult(this.Ok() as IActionResult);
            });
        }

        /// <summary>
        /// Provides the names of telemetry events emitted by the controller.
        /// </summary>
        private static class EventNames
        {
            public static readonly string CancelExperiment = EventContext.GetEventName(ExperimentsController.ApiName, "CancelExperiment");
            public static readonly string CreateExperiment = EventContext.GetEventName(ExperimentsController.ApiName, "CreateExperiment");
            public static readonly string GetExperiment = EventContext.GetEventName(ExperimentsController.ApiName, "GetExperiment");
            public static readonly string GetExperimentContext = EventContext.GetEventName(ExperimentsController.ApiName, "GetExperimentContext");
            public static readonly string GetExperimentSteps = EventContext.GetEventName(ExperimentsController.ApiName, "GetExperimentSteps");
            public static readonly string GetExperimentResources = EventContext.GetEventName(ExperimentsController.ApiName, "GetExperimentResources");
            public static readonly string ValidateExperiment = EventContext.GetEventName(ExperimentsController.ApiName, "ValidateExperiment");
            public static readonly string GetProviderList = EventContext.GetEventName(ExperimentsController.ApiName, "GetProviderList");
        }
    }
}
