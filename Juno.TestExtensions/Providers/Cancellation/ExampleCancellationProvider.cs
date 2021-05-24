namespace Juno.Providers.Cancellation
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A mock <see cref="IExperimentProvider"/> implementation of an experiment
    /// environment setup provider.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Cancellation, SupportedStepTarget.ExecuteRemotely)]
    public class ExampleCancellationProvider : MockExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleCancellationProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleCancellationProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc />
        public override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ExecutionResult(ExecutionStatus.Cancelled));
        }
    }
}
