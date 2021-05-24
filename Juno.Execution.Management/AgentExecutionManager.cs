namespace Juno.Execution.Management
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
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
    /// Provides agent core functionality to run steps and send heartbeat
    /// </summary>
    public class AgentExecutionManager : StepExecution
    {
        /// <summary>
        /// Create new instance of <see cref="AgentExecutionManager"/>
        /// </summary>
        public AgentExecutionManager(IServiceCollection services, IConfiguration configuration, IAsyncPolicy retryPolicy = null)
            : base(services, configuration)
        {
            this.ValidateRequiredServicesProvided(services);
            this.AgentId = this.Services.GetService<AgentIdentification>();
            this.AgentClient = this.Services.GetService<AgentClient>();
            this.RetryPolicy = retryPolicy ?? Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(10, (retries) => TimeSpan.FromMilliseconds(retries * 1000));
        }

        /// <summary>
        /// The Juno Agent API client.
        /// </summary>
        protected AgentClient AgentClient { get; }

        /// <summary>
        /// The agent identification.
        /// </summary>
        protected AgentIdentification AgentId { get; }

        /// <summary>
        /// Gets the retry policy to apply to API calls to handle transient failures.
        /// </summary>
        protected IAsyncPolicy RetryPolicy { get; }

        /// <summary>
        /// Execute experiment steps on agent
        /// Workflows . 
        /// 1. Get all steps for agents with status: InProgress, InProgressContinue or pending
        /// 2. Select steps to execute.
        ///     2.1 Get all steps which is inprogress or inprogresscontinue, and add them to selected steps collection
        ///     2.2 Check if selected steps contains any step with status=inprogress, if no add the next pending step to collection based on sequence ranking.
        /// 3  Execute all the selected states in parallel
        /// </summary>
        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            this.HostCancellationToken = cancellationToken;

            // Persist the activity ID so that it can be used down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid());

            try
            {
                using (CancellationTokenSource stepCancellation = new CancellationTokenSource())
                {
                    // 1) Get all agent steps for the current experiment (i.e. the latest experiment).
                    IEnumerable<ExperimentStepInstance> allSteps = await this.GetAgentStepsAsync(telemetryContext, stepCancellation.Token)
                        .ConfigureDefaults();

                    if (allSteps?.Any() == true)
                    {
                        string experimentId = allSteps.First().ExperimentId;
                        telemetryContext.AddContext(nameof(experimentId), experimentId);

                        // We need to capture the steps in a separate telemetry event to ensure we do not lose telemetry data
                        // because of the size of the context/event properties.
                        await this.Logger.LogTelemetryAsync($"{nameof(AgentExecutionManager)}.Steps", telemetryContext, allSteps)
                            .ConfigureDefaults();

                        if (allSteps?.Any() == true)
                        {
                            // 2) Get experiment related to the agent and steps.
                            ExperimentInstance experiment = await this.GetExperimentAsync(allSteps.First().ExperimentId, telemetryContext, stepCancellation.Token)
                                .ConfigureDefaults();

                            if (experiment != null && !this.HostCancellationToken.IsCancellationRequested)
                            {
                                // 3) Get the next steps in-sequence to execute.
                                IEnumerable<ExperimentStepInstance> nextSteps = StepExecution.GetNextExperimentSteps(allSteps);

                                await this.Logger.LogTelemetryAsync($"{nameof(AgentExecutionManager)}.NextSteps", telemetryContext, nextSteps)
                                    .ConfigureDefaults();

                                if (nextSteps?.Any() == true)
                                {
                                    // 4) Process/execute the next steps.
                                    await this.ProcessStepsAsync(nextSteps, experiment, telemetryContext, stepCancellation.Token)
                                        .ConfigureDefaults();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                telemetryContext.AddError(exc, withCallStack: true);
                await this.Logger.LogTelemetryAsync($"{nameof(AgentExecutionManager)}Error", LogLevel.Error, telemetryContext)
                    .ConfigureDefaults();
            }
        }

        private async Task<IEnumerable<ExperimentStepInstance>> GetAgentStepsAsync(EventContext telemetryContext, CancellationToken cancellationToken)
        {
            // Clone with properties ensures we have the experiment ID in the telemetry
            // which is the core identifier to enable aggregation of telemetry events for
            // a given experiment.
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);

            return await this.Logger.LogTelemetryAsync($"{nameof(AgentExecutionManager)}.GetAgentSteps", relatedContext, async () =>
            {
                var statusFilters = new List<ExecutionStatus>()
                {
                    ExecutionStatus.InProgress,
                    ExecutionStatus.InProgressContinue,
                    ExecutionStatus.Pending
                };

                int attempts = 0;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();
                IEnumerable<ExperimentStepInstance> experimentSteps = null;

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        attempts++;
                        HttpResponseMessage httpResponse = await this.AgentClient.GetAgentStepsAsync(this.AgentId.ToString(), cancellationToken, statusFilters)
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

        private async Task<ExperimentInstance> GetExperimentAsync(string experimentId, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            // Clone with properties ensures we have the experiment ID in the telemetry
            // which is the core identifier to enable aggregation of telemetry events for
            // a given experiment.
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);

            return await this.Logger.LogTelemetryAsync($"{nameof(AgentExecutionManager)}.GetExperiment", relatedContext, async () =>
            {
                int attempts = 0;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();
                ExperimentInstance experiment = null;

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        attempts++;
                        HttpResponseMessage httpResponse = await this.AgentClient.GetExperimentAsync(experimentId, cancellationToken)
                            .ConfigureDefaults();

                        httpResponse.Handle(response =>
                        {
                            responses.Add(response);
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

        private async Task ProcessStepsAsync(IEnumerable<ExperimentStepInstance> nextSteps, ExperimentInstance experiment, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            List<Task> stepProcessingTasks = new List<Task>();
            nextSteps.ToList().ForEach(step => stepProcessingTasks.Add(Task.Run(async () =>
            {
                // We had an unusual incident at some point where we received a Success/OK response back from
                // one of the API calls but the data/objects were not complete. If any of the data whatsoever is
                // not complete, we do not want to go any further.
                if (step != null && step?.Definition != null && experiment != null && experiment?.Id != null)
                {
                    ExecutionResult result = null;
                    EventContext relatedContext = telemetryContext.Clone(withProperties: true)
                        .AddContext(step);

                    try
                    {
                        await this.Logger.LogTelemetryAsync($"{nameof(AgentExecutionManager)}.ExecuteStep", relatedContext, async () =>
                        {
                            if (!this.HostCancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    step.Attempts++;
                                    if (step.StartTime == null)
                                    {
                                        step.StartTime = DateTime.UtcNow;
                                        await this.Logger.LogTelemetryAsync($"{nameof(AgentExecutionManager)}.StepBegin", LogLevel.Information,
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
                                        step.Status = result.Status;

                                        if (result.Error != null)
                                        {
                                            step.SetError(result.Error);
                                        }

                                        if (result.IsCompleted())
                                        {
                                            step.EndTime = DateTime.UtcNow;
                                            await this.Logger.LogTelemetryAsync($"{nameof(AgentExecutionManager)}.StepEnd", LogLevel.Information,
                                                telemetryContext.Clone().AddContext(step).AddContext(result)).ConfigureDefaults();
                                        }

                                        await this.UpdateAgentStepAsync(step, telemetryContext, cancellationToken)
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
                }
            })));

            await Task.WhenAll(stepProcessingTasks).ConfigureDefaults();
        }

        private async Task<ExperimentStepInstance> UpdateAgentStepAsync(ExperimentStepInstance step, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            // Clone with properties ensures we have the experiment ID in the telemetry
            // which is the core identifier to enable aggregation of telemetry events for
            // a given experiment.
            EventContext relatedContext = telemetryContext.Clone(withProperties: true)
                .AddContext(step);

            return await this.Logger.LogTelemetryAsync($"{nameof(AgentExecutionManager)}.UpdateExperimentStep", relatedContext, async () =>
            {
                int attempts = 0;
                List<HttpResponseMessage> responses = new List<HttpResponseMessage>();
                ExperimentStepInstance updatedStep = null;

                try
                {
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        attempts++;
                        HttpResponseMessage httpResponse = await this.AgentClient.UpdateAgentStepAsync(step, cancellationToken)
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

            if (!services.HasService<AgentIdentification>())
            {
                missingDependencies.Add(typeof(AgentIdentification));
            }

            if (!services.HasService<AgentClient>())
            {
                missingDependencies.Add(typeof(AgentClient));
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
