namespace Juno.Execution.Providers.Demo
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Example provider to illustrate the TiP session cleanup behavior. Note that this provider is
    /// for example only and does not actually delete any live/real TiP sessions.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentCleanup, SupportedStepTarget.ExecuteRemotely)]
    public class ExampleTipCleanupProvider : ExperimentProvider
    {
        private Random random;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleTipCleanupProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleTipCleanupProvider(IServiceCollection services)
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

            TipCleanupProviderState state = await this.GetStateAsync<TipCleanupProviderState>(context, cancellationToken).ConfigureDefaults()
                ?? new TipCleanupProviderState
                {
                    MaxAttempts = this.random.Next(5, 10)
                };

            if (!state.CleanupRequested)
            {
                // Imagine that we make a request to the TiP Service to cleanup the TiP session for the node/blade
                // we isolated and used as part of the experiment. The TiP service will reimage the machine and ready
                // it to return to production pool capacity.
                await this.RequestCleanupAsync().ConfigureDefaults();
                state.CleanupRequested = true;
                await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
            }
            else if (!state.CleanupConfirmed)
            {
                // It takes time to reimage and ready a node/blade for return to customer production pool
                // capacity. We poll the TiP Service to get status on the completion of the process.
                if (await this.ConfirmCleanupAsync(context).ConfigureDefaults())
                {
                    state.CleanupConfirmed = true;
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }
            }
            else if (context.ExperimentStep.Attempts > state.MaxAttempts)
            {
                result = new ExecutionResult(ExecutionStatus.Succeeded);
            }

            return result;
        }

        private Task<bool> ConfirmCleanupAsync(ExperimentContext context)
        {
            bool confirmed = false;
            if (context.ExperimentStep.Attempts >= 5)
            {
                confirmed = true;
            }

            return Task.FromResult(confirmed);
        }

        private Task RequestCleanupAsync()
        {
            return Task.Delay(1000);
        }

        internal class TipCleanupProviderState
        {
            public bool CleanupRequested { get; set; }

            public bool CleanupConfirmed { get; set; }

            public int MaxAttempts { get; set; }
        }
    }
}
