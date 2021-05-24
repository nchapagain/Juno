namespace Juno.Execution.Providers.Cancellation
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provider can be used to cancel an experiment.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Cancellation, SupportedStepTarget.ExecuteRemotely)]
    public class CancellationProvider : ExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CancellationProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public CancellationProvider(IServiceCollection services)
           : base(services)
        {
        }

        /// <inheritdoc />
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            return Task.FromResult(new ExecutionResult(ExecutionStatus.Cancelled));
        }
    }
}
