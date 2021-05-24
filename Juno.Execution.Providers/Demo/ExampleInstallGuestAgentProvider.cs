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
    /// Example provider to illustrate installing the Juno Guest agent. Note that this provider
    /// is for example only and does not actually install the agent on any VMs.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    public class ExampleInstallGuestAgentProvider : ExperimentProvider
    {
        private Random random;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleInstallGuestAgentProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleInstallGuestAgentProvider(IServiceCollection services)
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

            InstallGuestAgentProviderState state = await this.GetStateAsync<InstallGuestAgentProviderState>(context, cancellationToken, sharedState: false).ConfigureDefaults()
                ?? new InstallGuestAgentProviderState
                {
                    MaxAttempts = this.random.Next(5, 10)
                };

            if (!state.InstallationRequested)
            {
                // Imagine we are calling the ARM API service to request that the Guest Agent be installed on 
                // VMs in the experiment group.
                await this.RequestAgentInstallationAsync().ConfigureDefaults();
                state.InstallationRequested = true;
                await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
            }
            else if (!state.InstallationConfirmed)
            {
                // The request to install the agent is not atomic. Imagine polling the ARM API service
                // for status of the installation.
                if (await this.ConfirmInstallationAsync(context).ConfigureDefaults())
                {
                    state.InstallationConfirmed = true;
                    await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
                }
            }
            else if (!state.HeartbeatConfirmed)
            {
                // Once we've confirmed that the agent is installed, we need to ensure that it actually
                // starts up. The agent will begin publishing heartbeats on a consistent interval when it successfully
                // startus up. Imagine polling the Juno Agents API to check for heartbeats from the agent.
                if (await this.ConfirmHeartbeatAsync(context).ConfigureDefaults())
                {
                    state.HeartbeatConfirmed = true;
                    await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
                }
            }
            else if (context.ExperimentStep.Attempts > state.MaxAttempts)
            {
                result = new ExecutionResult(ExecutionStatus.Succeeded);
            }

            result.Extension = TimeSpan.FromSeconds(30);

            return result;
        }

        private Task<bool> ConfirmHeartbeatAsync(ExperimentContext context)
        {
            bool confirmed = false;
            if (context.ExperimentStep.Attempts >= 5)
            {
                confirmed = true;
            }

            return Task.FromResult(confirmed);
        }

        private Task<bool> ConfirmInstallationAsync(ExperimentContext context)
        {
            bool confirmed = false;
            if (context.ExperimentStep.Attempts >= 3)
            {
                confirmed = true;
            }

            return Task.FromResult(confirmed);
        }

        private Task RequestAgentInstallationAsync()
        {
            return Task.Delay(1000);
        }

        internal class InstallGuestAgentProviderState
        {
            public bool HeartbeatConfirmed { get; set; }

            public bool InstallationRequested { get; set; }

            public bool InstallationConfirmed { get; set; }

            public int MaxAttempts { get; set; }
        }
    }
}
