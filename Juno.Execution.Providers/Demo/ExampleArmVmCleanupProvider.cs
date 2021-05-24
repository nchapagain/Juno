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
    /// Example provider to illustrate the deletion of virtual machines in the experiment environment
    /// group using the Azure Resource Manager (ARM) service. Note that this provider is for example only
    /// and does not actually interface with the ARM service nor does it delete real VMs.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    public class ExampleArmVmCleanupProvider : ExperimentProvider
    {
        private Random random;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleArmVmCleanupProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleArmVmCleanupProvider(IServiceCollection services)
            : base(services)
        {
            this.random = new Random();
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            ArmVmCleanupProviderState state = await this.GetStateAsync<ArmVmCleanupProviderState>(context, cancellationToken).ConfigureDefaults()
                ?? new ArmVmCleanupProviderState
                {
                    MaxAttempts = this.random.Next(5, 10)
                };

            if (!state.CleanupRequested)
            {
                // Imagine logic in this method that calls the ARM API service to verify if the VMs associated with the
                // experiment group have been deleted as well as the resource group itself.
                await this.RequestCleanupAsync().ConfigureDefaults();
                state.CleanupRequested = true;
                await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
            }
            else if (!state.CleanupConfirmed)
            {
                // Imagine logic in this method that calls the ARM API service to request the deletion of the VMs associated
                // with the resource group and the resource group itself.
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

        internal class ArmVmCleanupProviderState
        {
            public bool CleanupRequested { get; set; }

            public bool CleanupConfirmed { get; set; }

            public int MaxAttempts { get; set; }
        }
    }
}
