namespace Juno.Api.Client
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Polly;

    /// <summary>
    /// Juno Environments REST API client.
    /// </summary>
    public class EnvironmentClient : IEnvironmentClient
    {
        private const string EnvironmentsApiRoute = "/api/environments";
        private const string ReservedNodesApiRoute = "/reservedNodes";

        /// <summary>
        /// Defines the default retry policy for Api operations on timeout only
        /// </summary>
        private static IAsyncPolicy defaultRetryPolicy = Policy.Handle<WebException>((exc) =>
        {
            return exc.Status == WebExceptionStatus.Timeout;
        })
        .WaitAndRetryAsync(1, (retries) => TimeSpan.FromMilliseconds(retries * 100));

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentClient"/> class.
        /// </summary>
        /// <param name="restClient">
        /// The REST client that handles HTTP communications with the API
        /// service.
        /// </param>
        /// <param name="baseUri">
        /// The base URI to the server hosting the API (e.g. https://juno-prod01.westUS2.webapps.net).
        /// </param>
        /// <param name="retryPolicy">A retry policy to apply to api call operations that experience transient issues.</param>
        public EnvironmentClient(IRestClient restClient, Uri baseUri, IAsyncPolicy retryPolicy = null)
        {
            restClient.ThrowIfNull(nameof(restClient));
            baseUri.ThrowIfNull(nameof(baseUri));

            this.RestClient = restClient;
            this.BaseUri = baseUri;
            this.RetryPolicy = retryPolicy ?? EnvironmentClient.defaultRetryPolicy;
        }

        /// <summary>
        /// Gets the base URI to the server hosting the API.
        /// </summary>
        protected Uri BaseUri { get; }

        /// <summary>
        /// Gets or sets the REST client that handles HTTP communications
        /// with the API service.
        /// </summary>
        protected IRestClient RestClient { get; }

        /// <summary>
        /// Gets the retry policy to apply when experiencing transient issues.
        /// </summary>
        protected IAsyncPolicy RetryPolicy { get; }

        /// <see cref="IEnvironmentClient.ReserveEnvironmentsAsync"/>
        public async Task<HttpResponseMessage> ReserveEnvironmentsAsync(EnvironmentQuery query, CancellationToken cancellationToken)
        {
            query.ThrowIfNull(nameof(query));

            // Format: /api/envrionments
            string route = $"{EnvironmentClient.EnvironmentsApiRoute}";
            Uri requestUri = new Uri(this.BaseUri, route);

            using (StringContent requestBody = new StringContent(query.ToJson(), Encoding.UTF8, "application/json"))
            {
                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> CreateReservedNodesAsync(ReservedNodes reservedNodes, CancellationToken cancellationToken)
        {
            reservedNodes.ThrowIfNull(nameof(reservedNodes));

            // Format: /api/envrionments/reservedNodes
            var route = $"{EnvironmentClient.EnvironmentsApiRoute}{EnvironmentClient.ReservedNodesApiRoute}";
            var requestUri = new Uri(this.BaseUri, route);

            using (StringContent requestBody = new StringContent(reservedNodes.ToJson(), Encoding.UTF8, "application/json"))
            {
                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> DeleteReservedNodesAsync(ReservedNodes reservedNodes, CancellationToken cancellationToken)
        {
            reservedNodes.ThrowIfNull(nameof(reservedNodes));

            // Format: /api/envrionments/reservedNodes
            var route = $"{EnvironmentClient.EnvironmentsApiRoute}{EnvironmentClient.ReservedNodesApiRoute}";
            var requestUri = new Uri(this.BaseUri, route);

            using (StringContent requestBody = new StringContent(reservedNodes.ToJson(), Encoding.UTF8, "application/json"))
            {
                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.DeleteAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }
    }
}
