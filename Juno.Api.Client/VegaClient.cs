namespace Juno.Api.Client
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Azure.CRC.Rest;
    using Polly;

    /// <summary>
    /// VegaClient.
    /// </summary>
    public class VegaClient : IRestClient
    {
        /// <summary>
        /// Defines the default retry policy for Api operations on timeout only
        /// </summary>
        private static IAsyncPolicy defaultRetryPolicy = Policy.Handle<WebException>((exc) =>
        {
            return exc.Status == WebExceptionStatus.Timeout;
        })
        .WaitAndRetryAsync(1, (retries) => TimeSpan.FromMilliseconds(retries * 100));

        private readonly Lazy<HttpClient> httpClient;

        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="VegaClient"/> class.
        /// </summary>
        /// <param name="certManager"></param>
        /// <param name="retryPolicy"></param>
        /// <param name="certThumbprint"></param>
        public VegaClient(string certThumbprint, ICertificateManager certManager = null, IAsyncPolicy retryPolicy = null)
        {
            certThumbprint.ThrowIfNull(nameof(certThumbprint));
            this.RetryPolicy = retryPolicy ?? VegaClient.defaultRetryPolicy;

            this.httpClient = new Lazy<HttpClient>(() =>
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                ICertificateManager certificateManager = certManager ?? new CertificateManager();
                X509Certificate2 certificate = certificateManager.GetCertificateFromStoreAsync(certThumbprint).GetAwaiter().GetResult();

                var handler = new HttpClientHandler();
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ClientCertificates.Add(certificate);
                handler.UseProxy = false;

                return new HttpClient(handler);
            });
        }

        internal VegaClient(HttpClient httpClient)
        {
            httpClient.ThrowIfNull(nameof(httpClient));

            this.httpClient = new Lazy<HttpClient>(() => httpClient);
        }

        /// <summary>
        /// Gets or sets the authentication provider that adds auth header to the rest client. Not used for VegaApiClient.
        /// </summary>
        public IHttpAuthentication AuthenticationProvider
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the retry policy to apply when experiencing transient issues.
        /// </summary>
        protected IAsyncPolicy RetryPolicy { get; }

        /// <summary>
        /// Method to Delete.
        /// </summary>
        /// <param name="requestUri">requestUri</param>
        /// <param name="cancellationToken">cancellationToken</param>
        /// <returns>A <see cref="Task{HttpResponseMessage}"/> representing the result of the asynchronous operation.</returns>
        public async Task<HttpResponseMessage> DeleteAsync(Uri requestUri, CancellationToken cancellationToken)
        {
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.httpClient.Value.DeleteAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> DeleteAsync(Uri requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                HttpRequestMessage request = new HttpRequestMessage
                {
                    Content = content,
                    Method = HttpMethod.Delete,
                    RequestUri = requestUri
                };
                HttpResponseMessage responseMessage = await this.httpClient.Value.SendAsync(request, cancellationToken).ConfigureAwait(false);
                return responseMessage;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Method to Get.
        /// </summary>
        /// <param name="requestUri">requestUri</param>
        /// <param name="cancellationToken">cancellationToken</param>
        /// <param name="completionOption">completionOption</param>
        /// <returns>A <see cref="Task{HttpResponseMessage}"/> representing the result of the asynchronous operation.</returns>
        public async Task<HttpResponseMessage> GetAsync(Uri requestUri, CancellationToken cancellationToken, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.httpClient.Value.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// HeadAsync.
        /// </summary>
        /// <param name="requestUri">requestUri</param>
        /// <param name="cancellationToken">cancellationToken</param>
        /// <returns>A <see cref="Task{HttpResponseMessage}"/> representing the result of the asynchronous operation.</returns>
        public async Task<HttpResponseMessage> HeadAsync(Uri requestUri, CancellationToken cancellationToken)
        {
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                using (var message = new HttpRequestMessage(HttpMethod.Head, requestUri))
                {
                    return await this.httpClient.Value.SendAsync(message, cancellationToken)
                    .ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Method to Post.
        /// </summary>
        /// <param name="requestUri">requestUri</param>
        /// <param name="cancellationToken">cancellationToken</param>
        /// <returns>A <see cref="Task{HttpResponseMessage}"/> representing the result of the asynchronous operation.</returns>
        public async Task<HttpResponseMessage> PostAsync(Uri requestUri, CancellationToken cancellationToken)
        {
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.httpClient.Value.PostAsync(requestUri, null, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Method to Post.
        /// </summary>
        /// <param name="requestUri">requestUri</param>
        /// <param name="content">content</param>
        /// <param name="cancellationToken">cancellationToken</param>
        /// <returns>A <see cref="Task{HttpResponseMessage}"/> representing the result of the asynchronous operation.</returns>
        public async Task<HttpResponseMessage> PostAsync(Uri requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.httpClient.Value.PostAsync(requestUri, content, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Method to Put.
        /// </summary>
        /// <param name="requestUri">requestUri</param>
        /// <param name="content">content</param>
        /// <param name="cancellationToken">cancellationToken</param>
        /// <returns>A <see cref="Task{HttpResponseMessage}"/> representing the result of the asynchronous operation.</returns>
        public async Task<HttpResponseMessage> PutAsync(Uri requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.httpClient.Value.PutAsync(requestUri, content, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Method to Dispose.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            // Suppress finalization.
            #pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
            GC.SuppressFinalize(this);
            #pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        }

        /// <summary>
        /// Method to Dispose.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.httpClient.Value.Dispose();
                }

                this.disposed = true;
            }
        }
    }
}
