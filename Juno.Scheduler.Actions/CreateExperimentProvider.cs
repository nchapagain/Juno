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
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provides a base implementation of Control Action Launch Experiment Instance
    /// </summary>
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
                IExperimentClient experimentClient = this.Services.GetService<IExperimentClient>();
                TargetGoalTrigger targetGoal = scheduleContext.TargetGoalTrigger;
                if (!component.Parameters.ContainsKey(ExecutionGoalMetadata.ExperimentName))
                {
                    component.Parameters.Add(ExecutionGoalMetadata.ExperimentName, scheduleContext.ExecutionGoal.Definition.ExperimentName);
                }

                component.Parameters[ExecutionGoalMetadata.ExperimentName] = scheduleContext.ExecutionGoal.Definition.ExperimentName;

                ExperimentTemplate experiment = new ExperimentTemplate()
                {
                    Experiment = scheduleContext.ExecutionGoal.Definition.Experiment,
                    Override = CreateExperimentProvider.ReplaceParameters(component)
                };

                experiment.Experiment.Metadata.Add(SchedulerEventProperty.TargetGoal, targetGoal.Name);
                experiment.Experiment.Metadata.Add(SchedulerEventProperty.ExecutionGoal, targetGoal.ExecutionGoal);
                experiment.Experiment.Metadata.Add("executionGoalId", scheduleContext.ExecutionGoal.Id);

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

        private class Parameters
        {
            public const string WorkQueue = nameof(Parameters.WorkQueue);
        }
    }
}