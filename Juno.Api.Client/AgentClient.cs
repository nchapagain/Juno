namespace Juno.Api.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Contracts.OData;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Polly;

    /// <summary>
    /// Juno Agent REST API client.
    /// </summary>
    public class AgentClient
    {
        private const string AgentHeartbeatsApiRoute = "/api/heartbeats";
        private const string AgentExperimentsApiRoute = "/api/experiments";

        /// <summary>
        /// Defines the default retry policy for Api operations on timeout only
        /// </summary>
        private static IAsyncPolicy defaultRetryPolicy = Policy.Handle<WebException>((exc) =>
        {
            return exc.Status == WebExceptionStatus.Timeout;
        })
        .WaitAndRetryAsync(1, (retries) => TimeSpan.FromMilliseconds(retries * 100));

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentClient"/> class.
        /// </summary>
        /// <param name="restClient">
        /// The REST client that handles HTTP communications with the API
        /// service.
        /// </param>
        /// <param name="baseUri">
        /// The base URI to the server hosting the API (e.g. https://juno-prod01.westUS2.webapps.net).
        /// </param>
        /// <param name="retryPolicy">A retry policy to apply to api call operations that experience transient issues.</param>
        public AgentClient(IRestClient restClient, Uri baseUri, IAsyncPolicy retryPolicy = null)
        {
            restClient.ThrowIfNull(nameof(restClient));
            baseUri.ThrowIfNull(nameof(baseUri));

            this.RestClient = restClient;
            this.BaseUri = baseUri;
            this.RetryPolicy = retryPolicy ?? AgentClient.defaultRetryPolicy;
        }

        /// <summary>
        /// Gets the base URI to the server hosting the API.
        /// </summary>
        protected Uri BaseUri { get; }

        /// <summary>
        /// Gets the retry policy to apply when experiencing transient issues.
        /// </summary>
        protected IAsyncPolicy RetryPolicy { get; }

        /// <summary>
        /// Gets or sets the REST client that handles HTTP communications
        /// with the API service.
        /// </summary>
        protected IRestClient RestClient { get; }

        /// <summary>
        /// Makes an API request to create a context/metadata instance for an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment for which the context/metadata is related.</param>
        /// <param name="context">The experiment context/metadata instance.</param>
        /// <param name="cancellationToken">A token that can be used bo cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentMetadataInstance"/> created.
        /// </returns>
        public async Task<HttpResponseMessage> CreateExperimentContextAsync(string experimentId, ExperimentMetadata context, CancellationToken cancellationToken, string contextId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            context.ThrowIfNull(nameof(context));

            using (StringContent requestBody = AgentClient.CreateJsonContent(context.ToJson()))
            {
                // Format: /api/experiments/{experimentId}/context
                // Format: /api/experiments/{experimentId}/context?contextId={contextId}
                string route = contextId == null ?
                    $"{AgentClient.AgentExperimentsApiRoute}/{experimentId}/context" :
                    $"{AgentClient.AgentExperimentsApiRoute}/{experimentId}/context?contextId={contextId}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to create a new agent heartbeat
        /// </summary>
        /// <param name="heartbeat">The agent heatbeat.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="AgentHeartbeatInstance"/> created.
        /// </returns>
        public async Task<HttpResponseMessage> CreateHeartbeatAsync(AgentHeartbeat heartbeat, CancellationToken cancellationToken)
        {
            heartbeat.ThrowIfNull(nameof(heartbeat));
            heartbeat.AgentIdentification.ThrowIfNull(nameof(heartbeat.AgentIdentification));

            using (StringContent requestBody = AgentClient.CreateJsonContent(heartbeat.ToJson()))
            {
                // Format: /api/heartbeats
                string route = AgentClient.AgentHeartbeatsApiRoute;
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets an experiment instance from the system for which the agent is associated.
        /// </summary>
        /// <param name="agentId">The unique ID of the agent.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> GetAgentExperimentAsync(string agentId, CancellationToken cancellationToken)
        {
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));

            // Format: /api/experiments?agentId=Cluster01,Node01
            string route = $"{AgentClient.AgentExperimentsApiRoute}?agentId={agentId}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);

            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to gets experiment Steps instance for an agent
        /// </summary>
        /// <param name="agentId">The ID of the agent.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="status">Optional parameter defines a set of statuses on which to filter the results.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentStepInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> GetAgentStepsAsync(string agentId, CancellationToken cancellationToken, IEnumerable<ExecutionStatus> status = null)
        {
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));

            // Format: /api/experiments/agent-steps?agentId={agentId}
            //         /api/experiments/agent-steps?agentId={agentId}&status=InProgress&status=InProgressContinue
            string route = $"{AgentClient.AgentExperimentsApiRoute}/agent-steps?agentId={agentId}";
            QueryFilter filter = new QueryFilter();

            if (status?.Any() == true)
            {
                status.ToList().ForEach(individualStatus => filter.Or(nameof(ExperimentStepInstance.Status), ComparisonType.Equal, individualStatus.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(filter.Filter))
            {
                route += $"&filter={HttpUtility.UrlEncode(filter.CreateExpression())}";
            }

            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                .ConfigureAwait(false);

            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to gets an agentheart instance.
        /// </summary>
        /// <param name="agentId">The ID of the agent.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="AgentHeartbeatInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> GetHeartbeatAsync(string agentId, CancellationToken cancellationToken)
        {
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));

            // Format: /api/heartbeats/{agentId}
            string route = $"{AgentClient.AgentHeartbeatsApiRoute}?agentId={HttpUtility.UrlEncode(agentId)}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);

            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets an experiment instance from the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> GetExperimentAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Format: /api/experiments/{experimentId}
            string route = $"{AgentClient.AgentExperimentsApiRoute}/{experimentId}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);

            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to get the context/metadata instance for an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentMetadataInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> GetExperimentContextAsync(string experimentId, CancellationToken cancellationToken, string contextId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Format: /api/experiments/{experimentId}/context
            // Format: /api/experiments/{experimentId}/context?contextId={contextId}
            string route = contextId == null ?
                    $"{AgentClient.AgentExperimentsApiRoute}/{experimentId}/context" :
                    $"{AgentClient.AgentExperimentsApiRoute}/{experimentId}/context?contextId={contextId}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);

            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to update an existing experiment step instance for given agent.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment in which the agent and file are associated.</param>
        /// <param name="agentType">The agent type (e.g. Host, Guest).</param>
        /// <param name="agentId">The ID of the agent submitting the file.</param>
        /// <param name="fileName">The name of the file (e.g. sellog).</param>
        /// <param name="contentType">The HTTP content type (e.g. text/plain).</param>
        /// <param name="contentEncoding">The content encoding (e.g. UTF-8).</param>
        /// <param name="fileStream">A stream that contains the file contents.</param>
        /// <param name="timestamp">The timestamp when the file was produced.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the updated <see cref="ExperimentStepInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> UploadFileAsync(
            string experimentId,
            string agentType,
            string agentId,
            string fileName,
            string contentType,
            Encoding contentEncoding,
            Stream fileStream,
            DateTime timestamp,
            CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            fileName.ThrowIfNullOrWhiteSpace(nameof(fileName));
            fileStream.ThrowIfNull(nameof(fileStream));
            agentType.ThrowIfNullOrWhiteSpace(nameof(agentType));
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));
            contentType.ThrowIfNullOrWhiteSpace(nameof(contentType));
            contentEncoding.ThrowIfNull(nameof(contentEncoding));

            using (StreamContent requestBody = new StreamContent(fileStream))
            {
                requestBody.Headers.ContentType = new MediaTypeHeaderValue(contentType)
                {
                    CharSet = contentEncoding.WebName
                };

                // Format: 
                // /api/experiments/agent-files?experimentId=16EE3894-BDF5-410C-A62A-3E97F823FEA3&agentType=Host&agentId=Node01&fileName=sellog&timestamp=2020-02-28t13:45:30.0000000z

                StringBuilder routeBuilder = new StringBuilder($"{AgentClient.AgentExperimentsApiRoute}/agent-files")
                    .Append($"?experimentId={HttpUtility.UrlEncode(experimentId)}")
                    .Append($"&agentType={HttpUtility.UrlEncode(agentType)}")
                    .Append($"&agentId={HttpUtility.UrlEncode(agentId)}")
                    .Append($"&fileName={HttpUtility.UrlEncode(fileName)}")
                    .Append($"&timestamp={timestamp.ToString("o")}");

                Uri requestUri = new Uri(this.BaseUri, routeBuilder.ToString());

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to update an existing experiment step instance for given agent.
        /// </summary>
        /// <param name="step">The updated experiment step definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the updated <see cref="ExperimentStepInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> UpdateAgentStepAsync(ExperimentStepInstance step, CancellationToken cancellationToken)
        {
            step.ThrowIfNull(nameof(step));

            using (StringContent requestBody = AgentClient.CreateJsonContent(step.ToJson()))
            {
                // Format: /api/experiments/agent-steps/{stepId}
                string route = $"{AgentClient.AgentExperimentsApiRoute}/agent-steps/{step.Id}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> UpdateExperimentContextAsync(string experimentId, ExperimentMetadataInstance context, CancellationToken cancellationToken, string contextId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            context.ThrowIfNull(nameof(context));

            using (StringContent requestBody = AgentClient.CreateJsonContent(context.ToJson()))
            {
                // Format: /api/experiments/{experimentId}/context
                // Format: /api/experiments/{experimentId}/context?contextId={contextId}
                string route = contextId == null ?
                        $"{AgentClient.AgentExperimentsApiRoute}/{experimentId}/context" :
                        $"{AgentClient.AgentExperimentsApiRoute}/{experimentId}/context?contextId={contextId}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        private static StringContent CreateJsonContent(string content)
        {
            return new StringContent(content, Encoding.UTF8, "application/json");
        }
    }
}
