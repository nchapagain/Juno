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
    /// Example provider to demo PilotFish (PF) service application deployment behavior. Payloads will often
    /// be deployed to the physical nodes via PilotFish.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Parameters.MicrocodeProvider, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.MicrocodeVersion, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.PFServiceName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.PFServicePath, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.RequestTimeout, Type = typeof(TimeSpan))]
    [SupportedParameter(Name = Parameters.VerificationTimeout, Type = typeof(TimeSpan))]
    public class ExampleMicrocodeUpdateProvider : ExperimentProvider
    {
        private Random random;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleMicrocodeUpdateProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleMicrocodeUpdateProvider(IServiceCollection services)
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

            MicrocodeUpdateProviderState state = await this.GetStateAsync<MicrocodeUpdateProviderState>(context, cancellationToken).ConfigureDefaults()
                ?? new MicrocodeUpdateProviderState
                {
                    MaxAttempts = this.random.Next(10, 15)
                };

            if (!state.DeploymentRequested)
            {
                // Imagine the first step begin a request made to the TiP Service to deploy a microcode update
                // to the node/blade in the experiment Group B. The TiP Service in turn communicates with the PilotFish
                // service running on the node/blade itself.
                await this.RequestMicrocodeUpdateDeploymentAsync().ConfigureDefaults();
                state.DeploymentRequested = true;
                await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
            }
            else if (!state.DeploymentConfirmed)
            {
                // After we've asked the TiP Service to deploy the microcode update, we need to confirm
                // that it successfully handed off the request to the PilotFish service.
                if (await this.ConfirmMicrocodeUpdateDeploymentAsync(context).ConfigureDefaults())
                {
                    state.DeploymentConfirmed = true;
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }
            }
            else if (!state.AgentStepsCreated)
            {
                // After we've confirmed that the microcode update deployment was successfully handed off
                // to the PilotFish service, we create agent steps that will run on the node/blade. The Juno
                // host agent running on the node/blade is polling for work/agent steps. We HAVE to have an actual
                // step run on the physical node/blade in order to definitively confirm a microcode update is ACTUALLY
                // activated.
                await this.CreateAgentStepsAsync().ConfigureDefaults();
                state.AgentStepsCreated = true;
                await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
            }
            else if (!state.ActivationConfirmed)
            {
                // Then we check the status of the agent step to determine whether the microcode upate was actually
                // applied. The agent steps create just above are running on the node/blade (via the Host Agent) and are
                // polling the system to confirm when the expected microcode update is applied. The agent steps update their
                // status as they go so that the Juno execution system will know.
                if (await this.ConfirmMicrocodeUpdateActivatedAsync(context).ConfigureDefaults())
                {
                    state.ActivationConfirmed = true;
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }
            }
            else if (context.ExperimentStep.Attempts > state.MaxAttempts)
            {
                result = new ExecutionResult(ExecutionStatus.Succeeded);
            }

            result.Extension = TimeSpan.FromSeconds(30);

            return result;
        }

        private Task<bool> ConfirmMicrocodeUpdateDeploymentAsync(ExperimentContext context)
        {
            bool confirmed = false;
            if (context.ExperimentStep.Attempts >= 3)
            {
                confirmed = true;
            }

            return Task.FromResult(confirmed);
        }

        private Task<bool> ConfirmMicrocodeUpdateActivatedAsync(ExperimentContext context)
        {
            bool confirmed = false;
            if (context.ExperimentStep.Attempts >= 5)
            {
                confirmed = true;
            }

            return Task.FromResult(confirmed);
        }

        private Task CreateAgentStepsAsync()
        {
            return Task.Delay(1000);
        }

        private Task RequestMicrocodeUpdateDeploymentAsync()
        {
            return Task.Delay(1000);
        }

        internal class MicrocodeUpdateProviderState
        {
            public bool ActivationConfirmed { get; set; }

            public bool AgentStepsCreated { get; set; }

            public bool DeploymentRequested { get; set; }

            public bool DeploymentConfirmed { get; set; }

            public int MaxAttempts { get; set; }
        }

        private static class Parameters
        {
            public const string MicrocodeProvider = "microcodeProvider";
            public const string MicrocodeVersion = "microcodeVersion";
            public const string PFServiceName = "pfServiceName";
            public const string PFServicePath = "pfServicePath";
            public const string RequestTimeout = "requestTimeout";
            public const string VerificationTimeout = "verificationTimeout";
        }
    }
}
