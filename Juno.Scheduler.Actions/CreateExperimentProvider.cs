namespace Juno.Scheduler.Actions
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;

    /// <summary>
    /// Provides a base implementation of Control Action Launch Experiment Instance
    /// </summary>
    [SupportedParameter(Name = Parameters.ExperimentTemplateFileName, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.WorkQueueName, Type = typeof(string), Required = false)]
    public class CreateExperimentProvider : ScheduleActionProvider
    {
        /// <summary>
        /// Initializes a new instance of the<see cref="CreateExperimentProvider" />
        /// </summary>
        /// <param name="services"><see cref="IServiceCollection"/></param>
        public CreateExperimentProvider(IServiceCollection services)
            : base(services)
        {
        }

        private static string ReplaceParameters(ScheduleAction component)
        {
            var experimentParameters = new TemplateOverride(component.Parameters);
            return experimentParameters.ToJson();
        }

        /// <summary>
        /// Triggers Experiment Instance
        /// </summary>
        /// <param name="component">The schedule action that providers parameters for a successful execution</param>
        /// <param name="scheduleContext">Offers execution context to the provider</param>
        /// <param name="telemetryContext">Object to capture telemetry</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        protected override async Task<ExecutionResult> ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            scheduleContext.ThrowIfInvalid(nameof(scheduleContext), (sc) =>
            {
                return scheduleContext.TargetGoalTrigger != null;
            });

            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            ExecutionStatus status = ExecutionStatus.InProgress;

            if (!cancellationToken.IsCancellationRequested)
            {
                IExperimentTemplateDataManager experimentTemplateDataManager = this.Services.GetService<IExperimentTemplateDataManager>();
                experimentTemplateDataManager.ThrowIfNull(nameof(experimentTemplateDataManager));

                IExperimentClient experimentClient = this.Services.GetService<IExperimentClient>();
                experimentClient.ThrowIfNull(nameof(experimentClient));

                // Getting an entity from Execution Goal Triggers
                TargetGoalTrigger targetGoal = scheduleContext.TargetGoalTrigger;
                this.ProviderContext.Add("enabled", targetGoal.Enabled);

                if (!targetGoal.Enabled)
                {
                    return new ExecutionResult(ExecutionStatus.Succeeded);
                }

                // Getting experiment definition from cosmos
                ExperimentTemplate experiment = await this.GetExperimentAsync(component, scheduleContext, experimentTemplateDataManager, targetGoal, cancellationToken)
                    .ConfigureDefaults();

                // Add Target Goal and Execution Goal name for traceability in Kusto.
                experiment.Experiment.Metadata.Add(SchedulerEventProperty.TargetGoal, targetGoal.TargetGoal);
                experiment.Experiment.Metadata.Add(SchedulerEventProperty.ExecutionGoal, targetGoal.ExecutionGoal);

                this.ProviderContext.Add(SchedulerEventProperty.ExecutionGoal, targetGoal.ExecutionGoal);
                this.ProviderContext.Add(SchedulerEventProperty.TargetGoal, targetGoal.TargetGoal);
                this.ProviderContext.Add(SchedulerEventProperty.ExperimentParameters, experiment.Experiment.Parameters);

                string workQueue = component.Parameters.GetValue<string>(Parameters.WorkQueueName, string.Empty);

                // Making API call to create experiment
                HttpResponseMessage response = await experimentClient.CreateExperimentFromTemplateAsync(experiment, cancellationToken, workQueue)
                    .ConfigureDefaults();

                if (!response.IsSuccessStatusCode)
                {
                    string details;
                    try
                    {
                        ProblemDetails problemDetails = await response.Content.ReadAsJsonAsync<ProblemDetails>().ConfigureDefaults();
                        telemetryContext.AddContext(nameof(problemDetails), problemDetails);
                        details = problemDetails.Detail;
                    }
                    catch (ArgumentException)
                    {
                        details = $"{nameof(HttpResponseMessage)} had no content";
                    }
                    catch (JsonReaderException)
                    {
                        details = $"Unable to parse {nameof(ProblemDetails)}";
                    }

                    throw new SchedulerException($"Call to {nameof(IExperimentClient.CreateExperimentFromTemplateAsync)} was {response.StatusCode} with details: {details}");
                }

                var experimentResponse = await response.Content.ReadAsJsonAsync<ExperimentItem>().ConfigureDefaults();
                this.ProviderContext.Add(EventProperty.ExperimentId, experimentResponse.Id);
                this.ProviderContext.Add(EventProperty.ExperimentName, experimentResponse.Definition.Name);
                return new ExecutionResult(ExecutionStatus.Succeeded);
            }

            return new ExecutionResult(status);
        }

        private async Task<ExperimentTemplate> GetExperimentAsync(
            ScheduleAction component,
            ScheduleContext scheduleContext,
            IExperimentTemplateDataManager templateDataManager,
            TargetGoalTrigger targetGoal,
            CancellationToken token)
        {
            EventContext telemetryContext = EventContext.Persisted();
            return await this.Logger.LogTelemetryAsync($"{nameof(CreateExperimentProvider)}.GetExperiment", telemetryContext, async () =>
            {
                if (scheduleContext.ExecutionGoal.Experiment != null)
                {
                    return new ExperimentTemplate()
                    {
                        Experiment = new Experiment(scheduleContext.ExecutionGoal.Experiment),
                        Override = CreateExperimentProvider.ReplaceParameters(component)
                    };
                }

                var templateName = component.Parameters.GetValue<string>(Parameters.ExperimentTemplateFileName);
                templateName.ThrowIfNullOrWhiteSpace(Parameters.ExperimentTemplateFileName);

                // Get experiment definition from cosmos
                ExperimentTemplate payload = new ExperimentTemplate()
                {
                    Experiment = await this.GetPayloadAsync(templateName, templateDataManager, targetGoal, token).ConfigureDefaults(),
                    Override = CreateExperimentProvider.ReplaceParameters(component)
                };
                return payload;
            }).ConfigureDefaults();
        }

        private async Task<Experiment> GetPayloadAsync(
            string templateName,
            IExperimentTemplateDataManager templateDataManager,
            TargetGoalTrigger targetGoal,
            CancellationToken token)
        {
            EventContext telemetryContext = EventContext.Persisted();

            return await this.Logger.LogTelemetryAsync($"{nameof(CreateExperimentProvider)}.GetPayload", telemetryContext, async () =>
            {
                var experimentTemplate = await templateDataManager.GetExperimentTemplateAsync(
                    templateName,
                    targetGoal.TeamName,
                    token,
                    experimentName: targetGoal.ExperimentName).ConfigureDefaults();

                return experimentTemplate.Definition;
            }).ConfigureDefaults();
        }

        internal class Parameters
        {
            internal const string ExperimentTemplateFileName = "experimentTemplateFile";
            internal const string WorkQueueName = "workQueue";
        }
    }
}