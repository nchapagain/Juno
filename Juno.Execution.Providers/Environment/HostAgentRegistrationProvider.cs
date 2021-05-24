namespace Juno.Execution.Providers.Environment
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provider is used to cause the Juno system to put identifiers in the data stores that connect the 
    /// Host Agent (and its agent ID) with the experiment. By registering the agent with the experiment,
    /// this enables operations that run in the process of the Host Agent to determine with which experiment
    /// the agent is associated. This is especially important for operations that are not experiment steps but
    /// need to post data from the system associated with the experiment.
    /// Internal-only provider
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteOnNode)]
    public class HostAgentRegistrationProvider : ExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HostAgentRegistrationProvider"/>
        /// </summary>
        /// <param name="services">The set of services/dependencies for the provider.</param>
        public HostAgentRegistrationProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <summary>
        /// Does nothing. The provider itself is not meant to do anything. The provider is used to force the Juno
        /// system to register an agent ID-to-experiment mapping during agent step creation.
        /// </summary>
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
        }
    }
}