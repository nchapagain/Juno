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
    /// Example provider to illustrate the VM workload execution behavior. Note that this is for example
    /// only and does not run any actual workloads on live VMs.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Parameters.Command, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.CommandArguments, Type = typeof(string), Required = true)]
    public class ExampleVirtualClientWorkloadProvider : ExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleVirtualClientWorkloadProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ExampleVirtualClientWorkloadProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc />
        protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgressContinue);

            if (context.ExperimentStep.Attempts >= 10)
            {
                result = new ExecutionResult(ExecutionStatus.Succeeded);
            }

            return Task.FromResult(result);
        }

        private class Parameters
        {
            internal const string Command = "command";
            internal const string CommandArguments = "commandArguments";
        }
    }
}
