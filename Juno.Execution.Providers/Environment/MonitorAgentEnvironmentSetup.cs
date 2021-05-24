namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provider is used to monitor the status of Juno agent environment setup steps from within
    /// the execution orchestration system.
    /// </summary>
    /// <remarks>
    /// A monitoring provider is responsible for monitoring the state of other steps/providers
    /// executing in the Juno system. Agent monitoring providers monitor the status of steps that
    /// run on agents in the Juno system (e.g. steps that run workloads on agents). Agent monitoring 
    /// providers always execute in the Juno execution orchestration system and not on the actual agents
    /// themselves.
    /// Internal-only provider
    /// </remarks>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    public class MonitorAgentEnvironmentSetup : ExperimentProvider, IExperimentStepMonitoringProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorAgentEnvironmentSetup"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public MonitorAgentEnvironmentSetup(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc />
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            return this.ExecuteMonitoringStepsAsync(
                context,
                component,
                telemetryContext,
                cancellationToken,
                timeout: component.Timeout());
        }
    }
}
