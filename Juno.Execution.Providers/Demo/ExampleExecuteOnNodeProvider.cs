namespace Juno.Execution.Providers.Demo
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Example provider to illustrate a provider/step that runs on a physical blade
    /// in the Juno Host Agent process.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    public class ExampleExecuteOnNodeProvider : ExperimentProvider
    {
        private Random random;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleExecuteOnNodeProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleExecuteOnNodeProvider(IServiceCollection services)
            : base(services)
        {
            this.random = new Random();
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            State state = await this.GetStateAsync<State>(context, cancellationToken).ConfigureDefaults()
                ?? new State
                {
                    MaxAttempts = this.random.Next(2, 5)
                };

            if (state.MaxAttempts > context.ExperimentStep.Attempts)
            {
                result = new ExecutionResult(ExecutionStatus.Succeeded);
            }

            return result;
        }

        internal class State
        {
            public int MaxAttempts { get; set; }
        }
    }
}
