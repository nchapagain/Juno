namespace Juno.Execution.Management
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Polly;

    /// <summary>
    /// Provides methods used to manage work notices and notice queues.
    /// </summary>
    public class ExperimentNoticeManager : IExperimentNoticeManager
    {
        private static readonly TimeSpan MessageVisibilityTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentNoticeManager"/> class.
        /// </summary>
        /// <param name="executionClient">A client for communications with the Execution API.</param>
        /// <param name="workQueue">The name of the work queue.</param>
        /// <param name="logger">A logger for capturing telemetry events.</param>
        /// <param name="retryPolicy">A retry policy to apply to communications/operations against the work queue.</param>
        public ExperimentNoticeManager(ExecutionClient executionClient, string workQueue, ILogger logger = null, IAsyncPolicy retryPolicy = null)
        {
            executionClient.ThrowIfNull(nameof(executionClient));
            workQueue.ThrowIfNullOrWhiteSpace(nameof(workQueue));

            this.Client = executionClient;
            this.Logger = logger ?? NullLogger.Instance;
            this.WorkQueue = workQueue;

            // It is important that we do the best that we can to prevent duplicate messages on the queue
            // as part of the notice management workflow. Because of this, we will retry quite few times purposefully
            // to try and ensure we can survive transient issues.
            this.RetryPolicy = retryPolicy ?? Policy.Handle<Exception>()
               .WaitAndRetryAsync(10, (retries) => TimeSpan.FromSeconds(retries + 1));
        }

        /// <summary>
        /// The client for communications with the Execution API.
        /// </summary>
        public ExecutionClient Client { get; }

        /// <summary>
        /// The logger for capturing telemetry events.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Gets the retry policy to apply to API calls to handle transient failures.
        /// </summary>
        public IAsyncPolicy RetryPolicy { get; }

        /// <summary>
        /// The main work queue on which notices are managed.
        /// </summary>
        public string WorkQueue { get; }

        /// <summary>
        /// Deletes the work notice from the pool.
        /// </summary>
        /// <param name="workNotice">The work notice to delete/remove from the pool.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public async Task DeleteWorkNoticeAsync(ExperimentMetadataInstance workNotice, CancellationToken cancellationToken)
        {
            workNotice.ThrowIfNull(nameof(workNotice));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(EventProperty.ExperimentId, workNotice.Definition?.ExperimentId)
                .AddContext(EventProperty.QueueName, this.WorkQueue)
                .AddContext(EventProperty.Notice, workNotice);

            await this.Logger.LogTelemetryAsync($"{nameof(ExperimentNoticeManager)}.DeleteWorkNotice", telemetryContext, async () =>
            {
                string messageId = workNotice.MessageId();
                string popReceipt = workNotice.PopReceipt();
                telemetryContext.AddContext(EventProperty.MessageId, messageId);
                telemetryContext.AddContext(EventProperty.PopReceipt, popReceipt);

                int attempts = 0;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        attempts++;
                        HttpResponseMessage httpResponse = await this.Client.DeleteNoticeAsync(this.WorkQueue, messageId, popReceipt, cancellationToken)
                            .ConfigureDefaults();

                        responses.Add(httpResponse);

                        if (!httpResponse.IsSuccessStatusCode && httpResponse.StatusCode != HttpStatusCode.NotFound)
                        {
                            httpResponse.ThrowOnError<ExperimentException>();
                        }
                    }).ConfigureDefaults();
                }
                finally
                {
                    telemetryContext.AddContext(responses);
                    telemetryContext.AddContext(nameof(attempts), attempts);
                }
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Gets a notice of work for experiments in progress.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public async Task<ExperimentMetadataInstance> GetWorkNoticeAsync(CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(EventProperty.WorkQueue, this.WorkQueue);

            return await this.Logger.LogTelemetryAsync($"{nameof(ExperimentNoticeManager)}.GetWorkNotice", telemetryContext, async () =>
            {
                ExperimentMetadataInstance notice = null;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();
                int attempts = 0;

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        attempts++;
                        HttpResponseMessage httpResponse = await this.Client.GetNoticeAsync(
                            this.WorkQueue,
                            cancellationToken,
                            ExperimentNoticeManager.MessageVisibilityTimeout).ConfigureDefaults();

                        responses.Add(httpResponse);
                        httpResponse.ThrowOnError<ExperimentException>();

                        if (httpResponse.Content != null)
                        {
                            notice = await httpResponse.Content.ReadAsJsonAsync<ExperimentMetadataInstance>()
                                .ConfigureDefaults();
                        }
                    }).ConfigureDefaults();
                }
                finally
                {
                    telemetryContext.AddContext(EventProperty.ExperimentId, notice?.Definition?.ExperimentId);
                    telemetryContext.AddContext(EventProperty.MessageId, notice?.MessageId());
                    telemetryContext.AddContext(EventProperty.PopReceipt, notice?.PopReceipt());
                    telemetryContext.AddContext(notice);
                    telemetryContext.AddContext(responses);
                    telemetryContext.AddContext(nameof(attempts), attempts);
                }

                return notice;

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Creates a new notice based on the original notice.
        /// </summary>
        /// <param name="workNotice">The original work notice retrieved from the Juno system.</param>
        /// <param name="visibilityDelay">
        /// A time span that represents the amount of time the notice should remain hidden before it can be picked up again.
        /// </param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public async Task SetWorkNoticeVisibilityAsync(ExperimentMetadataInstance workNotice, TimeSpan visibilityDelay, CancellationToken cancellationToken)
        {
            workNotice.ThrowIfNull(nameof(workNotice));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(EventProperty.ExperimentId, workNotice.Definition?.ExperimentId)
                .AddContext(EventProperty.WorkQueue, this.WorkQueue)
                .AddContext(EventProperty.Notice, workNotice)
                .AddContext(EventProperty.VisibilityDelay, visibilityDelay);

            await this.Logger.LogTelemetryAsync($"{nameof(ExperimentNoticeManager)}.SetWorkNoticeVisibility", telemetryContext, async () =>
            {
                int attempts = 0;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                try
                {
                    string messageId = workNotice.MessageId();
                    string popReceipt = workNotice.PopReceipt();
                    telemetryContext.AddContext(EventProperty.MessageId, workNotice?.MessageId());
                    telemetryContext.AddContext(EventProperty.PopReceipt, workNotice?.PopReceipt());

                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        attempts++;
                        HttpResponseMessage httpResponse = await this.Client.SetNoticeVisibilityAsync(
                            this.WorkQueue,
                            messageId,
                            popReceipt,
                            visibilityDelay,
                            cancellationToken).ConfigureDefaults();

                        responses.Add(httpResponse);
                        httpResponse.ThrowOnError<ExperimentException>();
                    }).ConfigureDefaults();
                }
                finally
                {
                    telemetryContext.AddContext(responses);
                    telemetryContext.AddContext(nameof(attempts), attempts);
                }
            }).ConfigureDefaults();
        }
    }
}
