namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Newtonsoft.Json.Linq;
    using Polly;

    /// <summary>
    /// Provides base methods required by providers to access data in the
    /// Juno system.
    /// </summary>
    public class ProviderDataClient : IProviderDataClient, IDisposable
    {
        private static Random randomGen = new Random();
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderDataClient"/> class.
        /// </summary>
        /// <param name="executionClient">A client for communications with the Juno Execution API service.</param>
        /// <param name="retryPolicy">Retry policy for provider data client.</param>
        /// <param name="logger">A logger to capture telemetry.</param>
        public ProviderDataClient(ExecutionClient executionClient, IAsyncPolicy retryPolicy = null, ILogger logger = null)
            : this(retryPolicy, logger)
        {
            executionClient.ThrowIfNull(nameof(executionClient));
            this.ExecutionApiClient = executionClient;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderDataClient"/> class.
        /// </summary>
        /// <param name="agentClient">A client for communications with the Juno Agent API service.</param>
        /// <param name="retryPolicy">Retry policy for provider data client.</param>
        /// <param name="logger">A logger to capture telemetry.</param>
        public ProviderDataClient(AgentClient agentClient, IAsyncPolicy retryPolicy = null, ILogger logger = null)
            : this(retryPolicy, logger)
        {
            agentClient.ThrowIfNull(nameof(agentClient));
            this.AgentApiClient = agentClient;
        }

        private ProviderDataClient(IAsyncPolicy retryPolicy = null, ILogger logger = null)
        {
            this.Logger = logger ?? NullLogger.Instance;
            this.RetryPolicy = retryPolicy ?? Policy.WrapAsync(
                Policy.Handle<Exception>().WaitAndRetryAsync(retryCount: 10, (retries) =>
                {
                    return TimeSpan.FromSeconds(retries + 10);
                }),
                Policy.Handle<ProviderException>(exc => exc.Reason == ErrorReason.DataAlreadyExists || exc.Reason == ErrorReason.DataETagMismatch)
                .WaitAndRetryAsync(retryCount: 100, (retries) =>
                {
                    // Some amount of randomization in the retry wait time/delta backoff helps address failures
                    // related to data conflict/eTag mismatch issues with state/context object updates.
                    return TimeSpan.FromSeconds(retries + ProviderDataClient.randomGen.Next(2, 10));
                }));
        }

        /// <summary>
        /// Gets the client for communications with the Juno Agent API service.
        /// </summary>
        protected AgentClient AgentApiClient { get; }

        /// <summary>
        /// Gets the client for communications with the Juno Execution API service.
        /// </summary>
        protected ExecutionClient ExecutionApiClient { get; }

        /// <summary>
        /// The logger to use for capturing telemetry.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the retry policy to apply to API calls to handle transient failures.
        /// </summary>
        protected IAsyncPolicy RetryPolicy { get; }

        /// <summary>
        /// Makes an API request to create an agent/child step for the parent step provided and
        /// that targets a specific agent.
        /// </summary>
        /// <param name="parentStep">The parent step of the agent/child step.</param>
        /// <param name="definition">The agent/child component definition.</param>
        /// <param name="agentId">The unique ID of the agent.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentStepInstance"/> created.
        /// </returns>
        public async Task<IEnumerable<ExperimentStepInstance>> CreateAgentStepsAsync(ExperimentStepInstance parentStep, ExperimentComponent definition, string agentId, CancellationToken cancellationToken)
        {
            parentStep.ThrowIfNull(nameof(parentStep));
            definition.ThrowIfNull(nameof(definition));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(EventProperty.ExperimentId, parentStep.ExperimentId)
               .AddContext(EventProperty.AgentId, agentId)
               .AddContext(parentStep, nameof(parentStep));

            return await this.Logger.LogTelemetryAsync($"{nameof(ProviderDataClient)}.CreateAgentSteps", telemetryContext, async () =>
            {
                if (this.ExecutionApiClient == null)
                {
                    throw new NotSupportedException(
                        $"The creation of new agent/child steps for a parent is not supported via the Juno Agent API service.");
                }

                int attempts = 0;
                IEnumerable<ExperimentStepInstance> agentSteps = null;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            attempts++;
                            HttpResponseMessage response = await this.ExecutionApiClient.CreateExperimentAgentStepsAsync(parentStep, definition, agentId, cancellationToken)
                                .ConfigureDefaults();

                            responses.Add(response);

                            ProviderDataClient.ThrowIfErrored(response);
                            agentSteps = ProviderDataClient.GetResponseData<IEnumerable<ExperimentStepInstance>>(response);
                        }
                    }).ConfigureDefaults();
                }
                finally
                {
                    telemetryContext.AddContext(agentSteps);
                    telemetryContext.AddContext(responses);
                    telemetryContext.AddContext(nameof(attempts), attempts);
                }

                return agentSteps;

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Disposes of resources used by the instance.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public async Task<AgentHeartbeatInstance> GetAgentHeartbeatAsync(string agentId, CancellationToken cancellationToken)
        {
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(EventProperty.AgentId, agentId);

            return await this.Logger.LogTelemetryAsync($"{nameof(ProviderDataClient)}.GetAgentHeartbeat", telemetryContext, async () =>
            {
                AgentHeartbeatInstance heartbeat = null;

                int attempts = 0;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            attempts++;
                            HttpResponseMessage response = null;
                            if (this.ExecutionApiClient != null)
                            {
                                response = await this.ExecutionApiClient.GetHeartbeatAsync(agentId, cancellationToken)
                                    .ConfigureDefaults();

                                responses.Add(response);
                            }
                            else if (this.AgentApiClient != null)
                            {
                                response = await this.AgentApiClient.GetHeartbeatAsync(agentId, cancellationToken)
                                    .ConfigureDefaults();

                                responses.Add(response);
                            }

                            if (response.StatusCode != HttpStatusCode.NotFound)
                            {
                                ProviderDataClient.ThrowIfErrored(response);
                                heartbeat = ProviderDataClient.GetResponseData<AgentHeartbeatInstance>(response);
                            }
                        }
                    }).ConfigureDefaults();
                }
                finally
                {
                    telemetryContext.AddContext(heartbeat);
                    telemetryContext.AddContext(responses);
                    telemetryContext.AddContext(nameof(attempts), attempts);
                }

                return heartbeat;

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Makes an API request to get all agent/child steps associated with the parent step.
        /// </summary>
        /// <param name="parentStep">The parent step of the agent/child step.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A <see cref="HttpResponseMessage"/> containing the step of agent/child <see cref="ExperimentStepInstance"/>
        /// objects.
        /// </returns>
        public async Task<IEnumerable<ExperimentStepInstance>> GetAgentStepsAsync(ExperimentStepInstance parentStep, CancellationToken cancellationToken)
        {
            parentStep.ThrowIfNull(nameof(parentStep));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(EventProperty.ExperimentId, parentStep.ExperimentId)
               .AddContext(parentStep, nameof(parentStep));

            return await this.Logger.LogTelemetryAsync($"{nameof(ProviderDataClient)}.GetAgentSteps", telemetryContext, async () =>
            {
                if (this.ExecutionApiClient == null)
                {
                    throw new NotSupportedException(
                        $"The ability to query for agent/child steps for a given parent is not supported via the Juno Agent API service.");
                }

                int attempts = 0;
                IEnumerable<ExperimentStepInstance> agentSteps = null;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            attempts++;
                            HttpResponseMessage response = await this.ExecutionApiClient.GetExperimentAgentStepsAsync(parentStep.ExperimentId, cancellationToken, parentStepId: parentStep.Id)
                                .ConfigureDefaults();

                            responses.Add(response);

                            ProviderDataClient.ThrowIfErrored(response);
                            agentSteps = ProviderDataClient.GetResponseData<IEnumerable<ExperimentStepInstance>>(response);
                        }
                    }).ConfigureDefaults();
                }
                finally
                {
                    telemetryContext.AddContext(agentSteps);
                    telemetryContext.AddContext(responses);
                    telemetryContext.AddContext(nameof(attempts), attempts);
                }

                return agentSteps;

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Makes an API request to get all steps associated with the experiment.
        /// </summary>
        /// <param name="experiment">The experiemnt.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A set of agent/child <see cref="ExperimentStepInstance"/> objects related to the experiment.
        /// </returns>
        public async Task<IEnumerable<ExperimentStepInstance>> GetExperimentStepsAsync(ExperimentInstance experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(EventProperty.ExperimentId, experiment.Id)
                .AddContext(experiment);

            return await this.Logger.LogTelemetryAsync($"{nameof(ProviderDataClient)}.GetExperimentSteps", telemetryContext, async () =>
            {
                if (this.ExecutionApiClient == null)
                {
                    throw new NotSupportedException(
                        $"The ability to query for experiment steps is not supported via the Juno Agent API service.");
                }

                int attempts = 0;
                IEnumerable<ExperimentStepInstance> steps = null;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            attempts++;
                            HttpResponseMessage response = await this.ExecutionApiClient.GetExperimentStepsAsync(experiment.Id, cancellationToken)
                                .ConfigureDefaults();

                            responses.Add(response);

                            ProviderDataClient.ThrowIfErrored(response);
                            steps = ProviderDataClient.GetResponseData<IEnumerable<ExperimentStepInstance>>(response);
                        }
                    }).ConfigureDefaults();
                }
                finally
                {
                    telemetryContext.AddContext(steps);
                    telemetryContext.AddContext(responses);
                    telemetryContext.AddContext(nameof(attempts), attempts);
                }

                return steps;

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Makes an API request to get the global context/metadata instance for an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="key">The key/name of the state object in the experiment context.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="stateId">Optional parameter defines the specific ID of the state/context object to retrieve.</param>
        /// <returns>
        /// A <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentMetadataInstance"/>.
        /// </returns>
        public async Task<TState> GetOrCreateStateAsync<TState>(string experimentId, string key, CancellationToken cancellationToken, string stateId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            key.ThrowIfNullOrWhiteSpace(nameof(key));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(EventProperty.ExperimentId, experimentId)
               .AddContext(nameof(key), key)
               .AddContext(nameof(stateId), stateId);

            return await this.Logger.LogTelemetryAsync($"{nameof(ProviderDataClient)}.GetState", telemetryContext, async () =>
            {
                int attempts = 0;
                TState state = default(TState);
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                await ProviderDataClient.semaphore.WaitAsync().ConfigureDefaults();

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            attempts++;
                            ExperimentMetadataInstance context = null;
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                context = await this.GetOrCreateExperimentStateAsync(experimentId, telemetryContext, cancellationToken, stateId)
                                    .ConfigureDefaults();

                                if (context.Extensions == null)
                                {
                                    telemetryContext.AddContext("nullContext", true);
                                }
                                else if (context.Extensions.ContainsKey(key))
                                {
                                    state = context.Extensions[key].ToObject<TState>();
                                }
                            }
                        }
                    }).ConfigureDefaults();
                }
                finally
                {
                    ProviderDataClient.semaphore.Release();
                    telemetryContext.AddContext(nameof(state), state);
                    telemetryContext.AddContext(responses);
                    telemetryContext.AddContext(nameof(attempts), attempts);
                }

                return state;

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Makes an API request to remove items in context/metadata instance based on id.
        /// </summary>
        /// <typeparam name="TState">The data type of the state object to save.</typeparam>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="key">The key/name of the state object in the experiment context.</param>
        /// <param name="state">The state object to save in the experiment context.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="stateId">Optionally defines the specific ID of the state/context object to retrieve. When not defined, returns the global context for the experiment.</param>
        public async Task RemoveStateItemsAsync<TState>(string experimentId, string key, IEnumerable<TState> state, CancellationToken cancellationToken, string stateId = null)
            where TState : IIdentifiable
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            key.ThrowIfNullOrWhiteSpace(nameof(key));
            state.ThrowIfNull(nameof(state));

            if (!cancellationToken.IsCancellationRequested)
            {
                EventContext telemetryContext = EventContext.Persisted()
                    .AddContext(EventProperty.ExperimentId, experimentId)
                    .AddContext(nameof(key), key)
                    .AddContext(nameof(state), state);

                await this.Logger.LogTelemetryAsync($"{nameof(ProviderDataClient)}.RemoveStateItems", telemetryContext, async () =>
                {
                    int attempts = 0;
                    List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                    await ProviderDataClient.semaphore.WaitAsync().ConfigureDefaults();

                    try
                    {
                        await this.RetryPolicy.ExecuteAsync(async () =>
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                // 1) Get the existing experiment context. Context objects are created when the experiment is initially
                                //    created.
                                attempts++;
                                ExperimentMetadataInstance experimentContext = await this.GetOrCreateExperimentStateAsync(experimentId, telemetryContext, cancellationToken, stateId)
                                    .ConfigureDefaults();

                                if (experimentContext.Extensions.ContainsKey(key))
                                {
                                    IEnumerable<TState> existingItems = experimentContext.Extensions[key].ToObject<IEnumerable<TState>>();
                                    experimentContext.Extensions[key] = JToken.FromObject(existingItems.Filter(state));

                                    // 3) Save the experiment context with the new state changes.
                                    HttpResponseMessage updateResponse = await this.UpdateExperimentStateAsync(experimentId, experimentContext, cancellationToken, stateId)
                                        .ConfigureDefaults();

                                    responses.Add(updateResponse);

                                    ProviderDataClient.ThrowIfErrored(updateResponse);
                                }
                            }
                        }).ConfigureDefaults();
                    }
                    finally
                    {
                        ProviderDataClient.semaphore.Release();
                        telemetryContext.AddContext(responses);
                        telemetryContext.AddContext(nameof(attempts), attempts);
                    }
                }).ConfigureDefaults();
            }
        }

        /// <summary>
        /// Makes an API request to update an existing experiment context/metadata instance.
        /// </summary>
        /// <typeparam name="TState">The data type of the state object to save.</typeparam>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="key">The key/name of the state object in the experiment context.</param>
        /// <param name="state">The state object to save in the experiment context.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="stateId">Optionally defines the specific ID of the state/context object to retrieve. When not defined, returns the global context for the experiment.</param>
        public async Task SaveStateAsync<TState>(string experimentId, string key, TState state, CancellationToken cancellationToken, string stateId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            key.ThrowIfNullOrWhiteSpace(nameof(key));
            state.ThrowIfNull(nameof(state));

            if (!cancellationToken.IsCancellationRequested)
            {
                EventContext telemetryContext = EventContext.Persisted()
                    .AddContext(EventProperty.ExperimentId, experimentId)
                    .AddContext(nameof(key), key)
                    .AddContext(nameof(state), state);

                await this.Logger.LogTelemetryAsync($"{nameof(ProviderDataClient)}.SaveState", telemetryContext, async () =>
                {
                    int attempts = 0;
                    List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                    await ProviderDataClient.semaphore.WaitAsync().ConfigureDefaults();

                    try
                    {
                        await this.RetryPolicy.ExecuteAsync(async () =>
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                // 1) Get the existing experiment context. Context objects are created when the experiment is initially
                                //    created.
                                attempts++;
                                ExperimentMetadataInstance experimentContext = await this.GetOrCreateExperimentStateAsync(experimentId, telemetryContext, cancellationToken, stateId)
                                    .ConfigureDefaults();

                                experimentContext.Extensions[key] = JToken.FromObject(state);

                                // 3) Save the experiment context with the new state changes.
                                HttpResponseMessage updateResponse = await this.UpdateExperimentStateAsync(experimentId, experimentContext, cancellationToken, stateId)
                                    .ConfigureDefaults();

                                responses.Add(updateResponse);

                                ProviderDataClient.ThrowIfErrored(updateResponse);
                            }
                        }).ConfigureDefaults();
                    }
                    finally
                    {
                        ProviderDataClient.semaphore.Release();
                        telemetryContext.AddContext(responses);
                        telemetryContext.AddContext(nameof(attempts), attempts);
                    }
                }).ConfigureDefaults();
            }
        }

        /// <summary>
        /// Makes an API request to update items in context/metadata instance, it will add if not exist by id.
        /// </summary>
        /// <typeparam name="TState">The data type of the state object to save.</typeparam>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="key">The key/name of the state object in the experiment context.</param>
        /// <param name="state">The state object to save in the experiment context.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="stateId">Optionally defines the specific ID of the state/context object to retrieve. When not defined, returns the global context for the experiment.</param>
        public async Task UpdateStateItemsAsync<TState>(string experimentId, string key, IEnumerable<TState> state, CancellationToken cancellationToken, string stateId = null)
            where TState : IIdentifiable
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            key.ThrowIfNullOrWhiteSpace(nameof(key));
            state.ThrowIfNull(nameof(state));

            if (!cancellationToken.IsCancellationRequested)
            {
                EventContext telemetryContext = EventContext.Persisted()
                   .AddContext(EventProperty.ExperimentId, experimentId)
                   .AddContext(nameof(key), key)
                   .AddContext(nameof(state), state);

                await this.Logger.LogTelemetryAsync($"{nameof(ProviderDataClient)}.UpdateStateItems", telemetryContext, async () =>
                {
                    int attempts = 0;
                    List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                    await ProviderDataClient.semaphore.WaitAsync().ConfigureDefaults();

                    try
                    {
                        await this.RetryPolicy.ExecuteAsync(async () =>
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                // 1) Get the existing experiment context. Context objects are created when the experiment is initially
                                //    created.
                                attempts++;
                                ExperimentMetadataInstance experimentContext = await this.GetOrCreateExperimentStateAsync(experimentId, telemetryContext, cancellationToken, stateId)
                                    .ConfigureDefaults();

                                IEnumerable<TState> existingItems = null;

                                if (experimentContext.Extensions.ContainsKey(key))
                                {
                                    existingItems = experimentContext.Extensions[key].ToObject<IEnumerable<TState>>();
                                }
                                else
                                {
                                    existingItems = new List<TState>();
                                }

                                experimentContext.Extensions[key] = JToken.FromObject(existingItems.UpdateOrAdd(state));

                                // 3) Save the experiment context with the new state changes.
                                HttpResponseMessage updateResponse = await this.UpdateExperimentStateAsync(experimentId, experimentContext, cancellationToken, stateId)
                                    .ConfigureDefaults();

                                responses.Add(updateResponse);

                                ProviderDataClient.ThrowIfErrored(updateResponse);
                            }
                        }).ConfigureDefaults();
                    }
                    finally
                    {
                        ProviderDataClient.semaphore.Release();
                        telemetryContext.AddContext(responses);
                        telemetryContext.AddContext(nameof(attempts), attempts);
                    }
                }).ConfigureDefaults();
            }
        }

        /// <summary>
        /// Disposes of resources used by the instance.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    ProviderDataClient.semaphore.Dispose();
                }

                this.disposed = true;
            }
        }

        private static TData GetResponseData<TData>(HttpResponseMessage response)
        {
            string jsonContent = response.Content.ReadAsStringAsync()
                .GetAwaiter().GetResult();

            return jsonContent.FromJson<TData>();
        }

        private static void ThrowIfErrored(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new ProviderException(
                        $"API Request Error (status code = {response.StatusCode}): {response.Content?.ReadAsStringAsync().GetAwaiter().GetResult()}",
                        ErrorReason.Unauthorized);
                }
                else if (!response.IsJsonContent())
                {
                    throw new ProviderException(
                        $"API Request Error (status code = {response.StatusCode}): {response.Content?.ReadAsStringAsync().GetAwaiter().GetResult()}");
                }
                else
                {
                    // We need to ensure we handle cases where the content is not a structured
                    // JSON payload. All Juno APIs provided structured errors; however, the ASP.NET Core
                    // framework or Azure Web App resources might return a text or HTML response.

                    try
                    {
                        ProblemDetails problemDetails = ProviderDataClient.GetResponseData<ProblemDetails>(response);

                        ErrorReason errorReason = ErrorReason.Undefined;
                        switch (response.StatusCode)
                        {
                            case HttpStatusCode.Conflict:
                                errorReason = ErrorReason.DataAlreadyExists;
                                break;

                            case HttpStatusCode.NotFound:
                                errorReason = ErrorReason.DataNotFound;
                                break;

                            case HttpStatusCode.PreconditionFailed:
                                errorReason = ErrorReason.DataETagMismatch;
                                break;
                        }

                        List<string> errorMessageParts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(problemDetails.Title))
                        {
                            errorMessageParts.Add(problemDetails.Title);
                        }

                        if (!string.IsNullOrWhiteSpace(problemDetails.Detail))
                        {
                            errorMessageParts.Add(problemDetails.Detail);
                        }

                        throw new ProviderException(
                            $"API Request Error (status code = {response.StatusCode}): " +
                            $"{string.Join(" ", errorMessageParts.Select(part => part.EndsWith(".", StringComparison.OrdinalIgnoreCase) ? part : $"{part}."))}",
                            errorReason);
                    }
                    catch
                    {
                        throw new ProviderException($"API Request Error (status code = {response.StatusCode}): {response.Content?.ReadAsStringAsync().GetAwaiter().GetResult()}");
                    }
                }
            }
        }

        private async Task<ExperimentMetadataInstance> GetOrCreateExperimentStateAsync(string experimentId, EventContext telemetryContext, CancellationToken cancellationToken, string stateId = null)
        {
            ExperimentMetadataInstance contextInstance = null;
            List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

            try
            {
                if (this.ExecutionApiClient != null)
                {
                    HttpResponseMessage getResponse = await this.ExecutionApiClient.GetExperimentContextAsync(experimentId, cancellationToken, stateId)
                        .ConfigureAwait(false);

                    responses.Add(getResponse);

                    // We will NOT attempt to create any state/context object if the ID of the object is not
                    // defined. We are in the middle of an incremental set of refactorings/changes to modify the way that
                    // state objects are preserved in the system. There will still be a global state/context object for a given
                    // experiment. However, individual providers will save their state in separate state/context objects.
                    if (getResponse.StatusCode == HttpStatusCode.NotFound && !string.IsNullOrWhiteSpace(stateId))
                    {
                        HttpResponseMessage createResponse = await this.ExecutionApiClient.CreateExperimentContextAsync(
                            experimentId,
                            new ExperimentMetadata(experimentId),
                            cancellationToken,
                            stateId).ConfigureAwait(false);

                        responses.Add(createResponse);

                        ProviderDataClient.ThrowIfErrored(createResponse);
                        contextInstance = ProviderDataClient.GetResponseData<ExperimentMetadataInstance>(createResponse);
                    }
                    else
                    {
                        ProviderDataClient.ThrowIfErrored(getResponse);
                        contextInstance = ProviderDataClient.GetResponseData<ExperimentMetadataInstance>(getResponse);
                    }
                }
                else
                {
                    HttpResponseMessage getResponse = await this.AgentApiClient.GetExperimentContextAsync(experimentId, cancellationToken, stateId)
                        .ConfigureDefaults();

                    responses.Add(getResponse);

                    // We will NOT attempt to create any state/context object if the ID of the object is not
                    // defined. We are in the middle of an incremental set of refactorings/changes to modify the way that
                    // state objects are preserved in the system. There will still be a global state/context object for a given
                    // experiment. However, individual providers will save their state in separate state/context objects.
                    if (getResponse.StatusCode == HttpStatusCode.NotFound && !string.IsNullOrWhiteSpace(stateId))
                    {
                        HttpResponseMessage createResponse = await this.AgentApiClient.CreateExperimentContextAsync(
                            experimentId,
                            new ExperimentMetadata(experimentId),
                            cancellationToken,
                            stateId).ConfigureDefaults();

                        responses.Add(createResponse);

                        ProviderDataClient.ThrowIfErrored(createResponse);
                        contextInstance = ProviderDataClient.GetResponseData<ExperimentMetadataInstance>(createResponse);
                    }
                    else
                    {
                        ProviderDataClient.ThrowIfErrored(getResponse);
                        contextInstance = ProviderDataClient.GetResponseData<ExperimentMetadataInstance>(getResponse);
                    }
                }
            }
            finally
            {
                telemetryContext.AddContext(responses);
            }

            return contextInstance;
        }

        private async Task<HttpResponseMessage> UpdateExperimentStateAsync(string experimentId, ExperimentMetadataInstance experimentContext, CancellationToken cancellationToken, string stateId = null)
        {
            HttpResponseMessage updateResponse = null;
            if (this.ExecutionApiClient != null)
            {
                updateResponse = await this.ExecutionApiClient.UpdateExperimentContextAsync(experimentId, experimentContext, cancellationToken, stateId)
                    .ConfigureDefaults();
            }
            else
            {
                updateResponse = await this.AgentApiClient.UpdateExperimentContextAsync(experimentId, experimentContext, cancellationToken, stateId)
                   .ConfigureDefaults();
            }

            return updateResponse;
        }
    }
}