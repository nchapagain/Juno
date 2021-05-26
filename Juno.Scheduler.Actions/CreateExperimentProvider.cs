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
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provides a base implementation of Control Action Launch Experiment Instance
    /// </summary>
    [SupportedParameter(Name = Parameters.ExperimentTemplateFile, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.WorkQueue, Type = typeof(string), Required = false)]
    public class CreateExperimentProvider : ScheduleActionProvider
    {
        /// <summary>
        /// Initializes a new instance of the<see cref="CreateExperimentProvider" />
        /// </summary>
        /// <param name="services">A list of services that can be used for depndency injection.</param>
        public CreateExperimentProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <summary>
        /// Posts a request to create a new experiment.
        /// </summary>
        protected override async Task ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (!cancellationToken.IsCancellationRequested)
            {
                IExperimentTemplateDataManager experimentTemplateDataManager = this.Services.GetService<IExperimentTemplateDataManager>();
                IExperimentClient experimentClient = this.Services.GetService<IExperimentClient>();
                TargetGoalTrigger targetGoal = scheduleContext.TargetGoalTrigger;

                ExperimentTemplate experiment = await this.GetExperimentAsync(component, scheduleContext, experimentTemplateDataManager, targetGoal, cancellationToken)
                    .ConfigureDefaults();

                experiment.Experiment.Metadata.Add(SchedulerEventProperty.TargetGoal, targetGoal.TargetGoal);
                experiment.Experiment.Metadata.Add(SchedulerEventProperty.ExecutionGoal, targetGoal.ExecutionGoal);

                string workQueue = component.Parameters.GetValue<string>(Parameters.WorkQueue, string.Empty);

                HttpResponseMessage response = await experimentClient.CreateExperimentFromTemplateAsync(experiment, cancellationToken, workQueue)
                    .ConfigureDefaults();
                response.ThrowOnError<SchedulerException>();

                ExperimentItem experimentResponse = await response.Content.ReadAsJsonAsync<ExperimentItem>().ConfigureDefaults();
                telemetryContext.AddContext(EventProperty.ExperimentId, experimentResponse.Id);
                telemetryContext.AddContext(EventProperty.ExperimentName, experimentResponse.Definition.Name);
            }
        }

        private static string ReplaceParameters(ScheduleAction component)
        {
            var experimentParameters = new TemplateOverride(component.Parameters);
            return experimentParameters.ToJson();
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

                var templateName = component.Parameters.GetValue<string>(Parameters.ExperimentTemplateFile);
                templateName.ThrowIfNullOrWhiteSpace(Parameters.ExperimentTemplateFile);

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

        private class Parameters
        {
            public const string ExperimentTemplateFile = nameof(Parameters.ExperimentTemplateFile);
            public const string WorkQueue = nameof(Parameters.WorkQueue);
        }
    }
}