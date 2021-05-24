namespace Juno.Execution.Management
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Polly;

    /// <summary>
    /// Manage execution of the experiment
    /// </summary>
    public class ExecutionManager : StepExecution
    {
        /// <summary>
        /// Create new instance of <see cref="ExecutionManager"/>
        /// </summary>
        /// <param name="services">The trace/telemetry logger for the controller.</param>
        /// <param name="configuration">The configuration for execution managements and providers.</param>
        /// <param name="workQueue">Optional parameter allows the target work queue to be supplied as an override to the default queue.</param>
        /// <param name="retryPolicy">A retry policy to apply to API calls.</param>
        public ExecutionManager(IServiceCollection services, IConfiguration configuration, string workQueue = null, IAsyncPolicy retryPolicy = null)
            : base(services, configuration)
        {
            services.ThrowIfNull(nameof(services));
            configuration.ThrowIfNull(nameof(configuration));

            this.ValidateRequiredServicesProvided(services);
            this.ExecutionClient = this.Services.GetService<ExecutionClient>();
            this.OverrideWorkQueue = workQueue;

            if (services.TryGetService<IExperimentNoticeManager>(out IExperimentNoticeManager noticeManager))
            {
                this.NoticeManager = noticeManager;
            }
            else
            {
                EnvironmentSettings settings = EnvironmentSettings.Initialize(this.Configuration);
                this.NoticeManager = new ExperimentNoticeManager(
                    this.ExecutionClient,
                    this.OverrideWorkQueue ?? settings.ExecutionSettings.WorkQueueName,
                    this.Logger);
            }

            this.RetryPolicy = retryPolicy ?? Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(10, (retries) => TimeSpan.FromMilliseconds(retries * 1000));
        }

        /// <summary>
        /// The wait handle to use to control execution lifetime to allow the execution
        /// workflow to exit gracefully.
        /// </summary>
        public static ManualResetEventSlim WaitHandle { get; } = new ManualResetEventSlim(true);

        /// <summary>
        /// The number of experiments processed during the round.
        /// </summary>
        public int ExperimentsProcessedCount { get; private set; }

        /// <summary>
        /// The Juno Execution API client.
        /// </summary>
        protected ExecutionClient ExecutionClient { get; }

        /// <summary>
        /// The name of a work/notice queue to use as an override to the default one defined
        /// in the configuration settings.
        /// </summary>
        protected string OverrideWorkQueue { get; }

        /// <summary>
        /// Manages interactions with the Juno system to get notices
        /// of work.
        /// </summary>
        protected IExperimentNoticeManager NoticeManager { get; }

        /// <summary>
        /// Gets the retry policy to apply to API calls to handle transient failures.
        /// </summary>
        protected IAsyncPolicy RetryPolicy { get; }

        /// <summary>
        /// Execute experiment steps workflow.
        /// </summary>
        /// <returns>True if succeeded or false if there is an exception</returns>
        /// <remarks>
        /// Workflow:
        /// 1) Get/check notification of work.
        /// 2) If a notification exists then continue.
        /// 3) If the notice is flagged for audit, then perform an audit (e.g. check for duplicates).
        /// 4) Get the experiment instance itself noted in the notification.
        /// 5) Get the next experiment steps slated for execution (note: the default behavior here is
        ///    the state of the steps at the very beginning of the experiment execution where all steps
        ///    are in a 'Pending' state.
        /// 6) Execute each step and update the step (e.g. status) after execution.
        /// 7) Update the experiment instance (e.g. status) after all steps are executed.
        /// 8a) If experiment is not completed, then set the notice visible on the work queue.
        /// 8b) If the experiment is completed, then delete the notice from the work queue.
        /// </remarks>
        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            this.HostCancellationToken = cancellationToken;

            try
            {
                ExecutionManager.WaitHandle.Reset();
                this.ExperimentsProcessedCount = 0;

                // Persist the activity ID so that it can be used down the callstack.
                EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid());

                ExperimentMetadataInstance notice = null;
                TimeSpan noticeVisibilityDelay = TimeSpan.FromSeconds(1);
                bool experimentIsCompleted = false;
                bool hasAnyStepsToProcess = false;

                try
                {
                    if (!this.HostCancellationToken.IsCancellationRequested)
                    {
                        using (CancellationTokenSource stepCancellation = new CancellationTokenSource())
                        {
                            // 1) Get notice of work.
                            //    Notices are queued references to a specific experiment. When we receive a notice, it is an indication
                            //    that we need to process next steps for that specific experiment. This enables the ability to support
                            //    long-running experiment step scenarios in an asynchronous way. Steps are processed for a given experiment
                            //    then a notice to process the next steps is created. This may be picked up by the same instance of the Execution
                            //    Service or by another enabling scale-out processing capacity.
                            notice = await this.NoticeManager.GetWorkNoticeAsync(CancellationToken.None).ConfigureDefaults();
                            telemetryContext.AddContext(notice, nameof(notice));

                            if (notice?.Definition != null)
                            {
                                // 3) Get experiment referenced in the notice.
                                ExperimentInstance experiment = await this.GetExperimentAsync(notice.Definition.ExperimentId, telemetryContext, stepCancellation.Token)
                                    .ConfigureDefaults();

                                if (experiment != null)
                                {
                                    this.ExperimentsProcessedCount++;
                                    telemetryContext.AddContext(EventProperty.ExperimentId, experiment.Id);

                                    // 4) Get all steps for the experiment.
                                    IEnumerable<ExperimentStepInstance> allSteps = await this.GetExperimentStepsAsync(experiment.Id, telemetryContext, stepCancellation.Token)
                                        .ConfigureDefaults();

                                    // We need to capture the steps in a separate telemetry event to ensure we do not lose telemetry data
                                    // because of the size of the context/event properties.
                                    await this.Logger.LogTelemetryAsync($"{nameof(ExecutionManager)}.Steps", telemetryContext, allSteps)
                                        .ConfigureDefaults();

                                    if (!this.HostCancellationToken.IsCancellationRequested)
                                    {
                                        ExperimentStatus experimentStatus;
                                        if (StepExecution.IsExperimentCompleted(allSteps, out experimentStatus))
                                        {
                                            // If any steps are in a terminal state (e.g. Succeeded, Failed, Cancelled, SystemCancelled), then the experiment
                                            // is finished. We update the experiment status and go no further.  Additionally, no further
                                            // notices of work will be created for the experiment.
                                            experimentIsCompleted = true;
                                            experiment.Status = experimentStatus;
                                            await this.UpdateExperimentAsync(experiment, telemetryContext, stepCancellation.Token)
                                                .ConfigureDefaults();

                                            // The steps for a given experiment will be processed over the course of any number of times
                                            // through the execution manager workflow.  To make it easier to identify when an experiment is
                                            // actually at its end in telemetry, we capture a special/single event to mark that point.
                                            telemetryContext.AddContext(nameof(experimentStatus), experiment.Status.ToString());
                                            await this.Logger.LogTelemetryAsync($"{nameof(ExecutionManager)}.ExperimentEnd", LogLevel.Information, telemetryContext)
                                                .ConfigureDefaults();
                                        }
                                        else
                                        {
                                            if (experiment.Status != experimentStatus)
                                            {
                                                experiment.Status = experimentStatus;
                                                await this.UpdateExperimentAsync(experiment, telemetryContext, stepCancellation.Token)
                                                    .ConfigureDefaults();

                                                if (experimentStatus == ExperimentStatus.InProgress)
                                                {
                                                    // The steps for a given experiment will be processed over the course of any number of times
                                                    // through the execution manager workflow.  To make it easier to identify when an experiment
                                                    // actually began in telemetry, we capture a special/single event to mark that point.
                                                    telemetryContext.AddContext(nameof(experimentStatus), experimentStatus);
                                                    await this.Logger.LogTelemetryAsync($"{nameof(ExecutionManager)}.ExperimentBegin", LogLevel.Information, telemetryContext)
                                                        .ConfigureDefaults();
                                                }
                                            }

                                            // 5) Filter all steps down to the next steps for execution.
                                            IEnumerable<ExperimentStepInstance> nextSteps = StepExecution.GetNextExperimentSteps(allSteps);

                                            // We need to capture the steps in a separate telemetry event to ensure we do not lose telemetry data
                                            // because of the size of the context/event properties.
                                            await this.Logger.LogTelemetryAsync($"{nameof(ExecutionManager)}.NextSteps", telemetryContext, nextSteps)
                                                .ConfigureDefaults();

                                            hasAnyStepsToProcess = nextSteps.Any();
                                            if (hasAnyStepsToProcess)
                                            {
                                                if (!this.HostCancellationToken.IsCancellationRequested)
                                                {
                                                    // 5) Process/execute the next steps. The logic returns a "rollup" visibility delay which allows individual providers
                                                    //    to provide a request to be invisible on the queue for an extended period of time. This allows other notices
                                                    //    to get processed more quickly.
                                                    noticeVisibilityDelay = await this.ProcessStepsAsync(nextSteps, experiment, telemetryContext, stepCancellation.Token)
                                                        .ConfigureDefaults();
                                                }
                                            }
                                        }

                                        if ((experimentIsCompleted || !hasAnyStepsToProcess) && allSteps.Any(step => ExecutionResult.NonCompletedStatuses.Contains(step.Status)))
                                        {
                                            // If there are no next steps or the experiment is completed but there are pending steps, the scheduler determined there are no steps to run.
                                            // We then set the status of all the pending steps to system cancelled.
                                            foreach (ExperimentStepInstance step in allSteps.Where(step => ExecutionResult.NonCompletedStatuses.Contains(step.Status)))
                                            {
                                                step.Status = ExecutionStatus.SystemCancelled;
                                                await this.UpdateExperimentStepAsync(step, telemetryContext, cancellationToken)
                                                    .ConfigureDefaults();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (notice != null)
                    {
                        if (!experimentIsCompleted)
                        {
                            // 6) If the experiment is not complete, set the notice to be visible so that
                            //    it can be picked up by the Execution Service for the next round of work processing steps.
                            //    This includes the scenario where any steps are failed.  The experiment will be completed
                            //    (in failed status) on the next round of processing.
                            await this.NoticeManager.SetWorkNoticeVisibilityAsync(notice, noticeVisibilityDelay, cancellationToken)
                                .ConfigureDefaults();

                            // When we are running in Dev, the code cycles around too fast and ends up missing notices that are hidden for 1 sec.
                            // This in turn causes the ExecutionFunction to delay the next round of execution for a minute. This is not the intended 
                            // behavior of the throttle control in the ExecutionFunction.This time will not affect the processing rate of EOS in any meaningful way, 
                            // but will ensure we are not waiting unnecessarily in Dev environment scenarios.
                            await Task.Delay(1500).ConfigureDefaults();
                        }
                        else
                        {
                            // 7) Delete the notice from the queue if it's finished.
                            await this.NoticeManager.DeleteWorkNoticeAsync(notice, cancellationToken)
                                .ConfigureDefaults();
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                EventContext telemetryContext = EventContext.Persisted().AddError(exc, withCallStack: true);
                await this.Logger.LogTelemetryAsync($"{nameof(ExecutionManager)}Error", LogLevel.Error, telemetryContext)
                    .ConfigureDefaults();
            }
            finally
            {
                ExecutionManager.WaitHandle.Set();
            }
        }

        private async Task<ExperimentInstance> GetExperimentAsync(string experimentId, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            // Clone with properties ensures we have the experiment ID in the telemetry
            // which is the core identifier to enable aggregation of telemetry events for
            // a given experiment.
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);

            return await this.Logger.LogTelemetryAsync($"{nameof(ExecutionManager)}.GetExperiment", relatedContext, async () =>
            {
                int attempts = 0;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();
                ExperimentInstance experiment = null;

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        attempts++;
                        HttpResponseMessage httpResponse = await this.ExecutionClient.GetExperimentAsync(experimentId, cancellationToken)
                            .ConfigureDefaults();

                        httpResponse.Handle(response =>
                        {
                            responses.Add(httpResponse);
                            response.ThrowOnError<ExperimentException>();
                        });

                        if (httpResponse.Content != null)
                        {
                            experiment = await httpResponse.Content.ReadAsJsonAsync<ExperimentInstance>()
                                .ConfigureDefaults();
                        }
                    }).ConfigureDefaults();
                }
                finally
                {
                    relatedContext.AddContext(experiment);
                    relatedContext.AddContext(responses);
                    relatedContext.AddContext(nameof(attempts), attempts);
                }

                return experiment;

            }).ConfigureDefaults();
        }

        private async Task<IEnumerable<ExperimentStepInstance>> GetExperimentStepsAsync(string experimentId, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            // Clone with properties ensures we have the experiment ID in the telemetry
            // which is the core identifier to enable aggregation of telemetry events for
            // a given experiment.
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);

            return await this.Logger.LogTelemetryAsync($"{nameof(ExecutionManager)}.GetExperimentSteps", relatedContext, async () =>
            {
                int attempts = 0;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();
                IEnumerable<ExperimentStepInstance> experimentSteps = null;

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        attempts++;
                        HttpResponseMessage httpResponse = await this.ExecutionClient.GetExperimentStepsAsync(experimentId, cancellationToken)
                            .ConfigureDefaults();

                        httpResponse.Handle(response =>
                        {
                            responses.Add(httpResponse);
                            response.ThrowOnError<ExperimentException>();
                        });

                        if (httpResponse.Content != null)
                        {
                            experimentSteps = await httpResponse.Content.ReadAsJsonAsync<IEnumerable<ExperimentStepInstance>>()
                                .ConfigureDefaults();
                        }
                    }).ConfigureDefaults();
                }
                finally
                {
                    relatedContext.AddContext(experimentSteps);
                    relatedContext.AddContext(responses);
                    relatedContext.AddContext(nameof(attempts), attempts);
                }

                return experimentSteps;
            }).ConfigureDefaults();
        }

        private async Task<TimeSpan> ProcessStepsAsync(IEnumerable<ExperimentStepInstance> nextSteps, ExperimentInstance experiment, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            List<Task> stepProcessingTasks = new List<Task>();
            ConcurrentBag<ExecutionResult> results = new ConcurrentBag<ExecutionResult>();

            nextSteps.ToList().ForEach(step => stepProcessingTasks.Add(Task.Run(async () =>
            {
                ExecutionResult result = null;
                EventContext relatedContext = telemetryContext.Clone()
                    .AddContext(step);

                try
                {
                    await this.Logger.LogTelemetryAsync($"{nameof(ExecutionManager)}.ExecuteStep", relatedContext, async () =>
                    {
                        if (!this.HostCancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                step.Attempts++;
                                if (step.StartTime == null)
                                {
                                    step.StartTime = DateTime.UtcNow;
                                    await this.Logger.LogTelemetryAsync($"{nameof(ExecutionManager)}.StepBegin", LogLevel.Information,
                                        telemetryContext.Clone().AddContext(step)).ConfigureDefaults();
                                }

                                // Create the provider from the experiment step/component definition.
                                ExperimentContext experimentContext = new ExperimentContext(experiment, step, this.Configuration);
                                IExperimentProvider provider = ExperimentProviderFactory.CreateProvider(step.Definition, this.Services);

                                // Execute the provider logic/operation to process the requirements of the experiment
                                // step.
                                result = await provider.ExecuteAsync(experimentContext, step.Definition, cancellationToken)
                                    .ConfigureDefaults();

                                relatedContext.AddContext(result);
                            }
                            catch (Exception exc)
                            {
                                result = new ExecutionResult(ExecutionStatus.Failed);

                                step.Status = ExecutionStatus.Failed;
                                step.EndTime = DateTime.UtcNow;
                                step.SetError(exc);

                                // The telemetry pipeline will naturally capture the error. The logic handles
                                // the exception below to ensure we don't crash the service.
                                throw;
                            }
                            finally
                            {
                                // Update the steps status
                                if (result != null)
                                {
                                    results.Add(result);
                                    step.Status = result.Status;

                                    if (result.Error != null)
                                    {
                                        step.SetError(result.Error);
                                    }

                                    if (result.IsCompleted())
                                    {
                                        step.EndTime = DateTime.UtcNow;
                                        await this.Logger.LogTelemetryAsync($"{nameof(ExecutionManager)}.StepEnd", LogLevel.Information,
                                            telemetryContext.Clone().AddContext(step).AddContext(result)).ConfigureDefaults();
                                    }

                                    await this.UpdateExperimentStepAsync(step, telemetryContext, cancellationToken)
                                        .ConfigureDefaults();
                                }
                            }
                        }
                    }).ConfigureDefaults();
                }
                catch
                {
                    // We do not want to allow exceptions that happen during the execution of steps/step providers
                    // to potentially crash the Execution Service.  We capture telemetry for the errors by default.
                }
            })));

            await Task.WhenAll(stepProcessingTasks).ConfigureDefaults();

            return ExecutionResult.GetRelativeTimeExtension(results);
        }

        private async Task<ExperimentInstance> UpdateExperimentAsync(ExperimentInstance experiment, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            // Clone with properties ensures we have the experiment ID in the telemetry
            // which is the core identifier to enable aggregation of telemetry events for
            // a given experiment.
            EventContext relatedContext = telemetryContext.Clone(withProperties: true)
                .AddContext(experiment);

            return await this.Logger.LogTelemetryAsync($"{nameof(ExecutionManager)}.UpdateExperiment", relatedContext, async () =>
            {
                int attempts = 0;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();
                ExperimentInstance updatedExperiment = null;

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        attempts++;
                        HttpResponseMessage httpResponse = await this.ExecutionClient.UpdateExperimentAsync(experiment, cancellationToken)
                            .ConfigureDefaults();

                        httpResponse.Handle(response =>
                        {
                            responses.Add(httpResponse);
                            response.ThrowOnError<ExperimentException>();
                        });

                        if (httpResponse.Content != null)
                        {
                            updatedExperiment = await httpResponse.Content.ReadAsJsonAsync<ExperimentInstance>()
                                .ConfigureDefaults();
                        }
                    }).ConfigureDefaults();
                }
                finally
                {
                    relatedContext.AddContext(updatedExperiment);
                    relatedContext.AddContext(responses);
                    relatedContext.AddContext(nameof(attempts), attempts);
                }

                return updatedExperiment;

            }).ConfigureDefaults();
        }

        private async Task<ExperimentStepInstance> UpdateExperimentStepAsync(ExperimentStepInstance step, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            // Clone with properties ensures we have the experiment ID in the telemetry
            // which is the core identifier to enable aggregation of telemetry events for
            // a given experiment.
            EventContext relatedContext = telemetryContext.Clone(withProperties: true)
                .AddContext(step);

            return await this.Logger.LogTelemetryAsync($"{nameof(ExecutionManager)}.UpdateExperimentStep", relatedContext, async () =>
            {
                int attempts = 0;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();
                ExperimentStepInstance updatedStep = null;

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        attempts++;
                        HttpResponseMessage httpResponse = await this.ExecutionClient.UpdateExperimentStepAsync(step, cancellationToken)
                            .ConfigureDefaults();

                        httpResponse.Handle(response =>
                        {
                            responses.Add(httpResponse);
                            response.ThrowOnError<ExperimentException>();
                        });

                        if (httpResponse.Content != null)
                        {
                            updatedStep = await httpResponse.Content.ReadAsJsonAsync<ExperimentStepInstance>()
                                .ConfigureDefaults();
                        }
                    }).ConfigureDefaults();
                }
                finally
                {
                    relatedContext.AddContext(updatedStep);
                    relatedContext.AddContext(responses);
                    relatedContext.AddContext(nameof(attempts), attempts);
                }

                return updatedStep;
            }).ConfigureDefaults();
        }

        private void ValidateRequiredServicesProvided(IServiceCollection services)
        {
            List<Type> missingDependencies = new List<Type>();

            if (!services.HasService<ExecutionClient>())
            {
                missingDependencies.Add(typeof(ExecutionClient));
            }

            if (!services.HasService<IProviderDataClient>())
            {
                missingDependencies.Add(typeof(IProviderDataClient));
            }

            if (!services.HasService<IAzureKeyVault>())
            {
                missingDependencies.Add(typeof(IAzureKeyVault));
            }

            if (missingDependencies.Any())
            {
                throw new ExecutionException(
                    $"Required dependencies missing. The execution manager requires the following dependencies that were " +
                    $"not provided: {string.Join(", ", missingDependencies.Select(d => d.Name))}");
            }
        }
    }
}