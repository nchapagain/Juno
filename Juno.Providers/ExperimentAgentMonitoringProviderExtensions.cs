namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;

    /// <summary>
    /// Extension methods for <see cref="IExperimentStepMonitoringProvider"/> instances.
    /// </summary>
    public static class ExperimentAgentMonitoringProviderExtensions
    {
        /// <summary>
        /// Creates an agent/child step for the parent step provided that targets a specific agent.
        /// </summary>
        /// <param name="provider">The experiment provider itself.</param>
        /// <param name="context">Provides the context for the specific experiment for which the provider is related.</param>
        /// <param name="definition">The agent/child component definition.</param>
        /// <param name="agentId">The unique ID of the agent.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="ExperimentStepInstance"/> representing the agent step created.
        /// </returns>
        public static async Task<IEnumerable<ExperimentStepInstance>> CreateAgentStepsAsync(
            this IExperimentProvider provider,
            ExperimentContext context,
            ExperimentComponent definition,
            string agentId,
            CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));
            definition.ThrowIfNull(nameof(definition));
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));

            IEnumerable<ExperimentStepInstance> agentSteps = null;
            if (!cancellationToken.IsCancellationRequested)
            {
                IProviderDataClient apiClient = provider.Services.GetService<IProviderDataClient>();

                agentSteps = await apiClient.CreateAgentStepsAsync(context.ExperimentStep, definition, agentId, cancellationToken)
                    .ConfigureAwait(false);
            }

            return agentSteps;
        }

        /// <summary>
        /// Creates a set of one or more agent steps for the child steps defined in the parent component.
        /// </summary>
        /// <param name="provider">The experiment provider itself.</param>
        /// <param name="context">Provides context specific to the experiment.</param>
        /// <param name="parentComponent">
        /// The parent component that contains the child steps each representing work to be performed on Guest or Host agents that are part
        /// of the experiment.
        /// </param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns></returns>
        public static async Task CreateAgentStepsAsync(this IExperimentProvider provider, ExperimentContext context, ExperimentComponent parentComponent, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));
            parentComponent.ThrowIfNull(nameof(parentComponent));

            IEnumerable<EnvironmentEntity> entitiesProvisioned = await provider.GetEntitiesProvisionedAsync(context, cancellationToken)
                .ConfigureDefaults();

            IEnumerable<ExperimentComponent> childSteps = parentComponent.GetChildSteps();

            if (childSteps == null)
            {
                throw new ProviderException(
                    $"Unexpected parent step definition. There are no child 'steps' are not defined for the parent component.",
                    ErrorReason.SchemaInvalid);
            }

            foreach (ExperimentComponent agentStep in childSteps)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    SupportedStepTarget stepTarget = agentStep.GetSupportedStepTarget();
                    IEnumerable<EnvironmentEntity> targetAgents = null;

                    switch (stepTarget)
                    {
                        case SupportedStepTarget.ExecuteOnNode:
                            targetAgents = entitiesProvisioned?.GetEntities(EntityType.Node, context.ExperimentStep.ExperimentGroup);
                            break;

                        case SupportedStepTarget.ExecuteOnVirtualMachine:
                            targetAgents = entitiesProvisioned?.GetEntities(EntityType.VirtualMachine, context.ExperimentStep.ExperimentGroup);
                            break;

                        default:
                            throw new ProviderException(
                                $"The provider definition for the component '{agentStep.Name}' is invalid. Providers that target agents in the system " +
                                $"must be attributed with one of the following valid step targets: {nameof(SupportedStepTarget.ExecuteOnNode)}, {nameof(SupportedStepTarget.ExecuteOnVirtualMachine)}",
                                ErrorReason.ProviderDefinitionInvalid);
                    }

                    if (targetAgents?.Any() != true)
                    {
                        throw new ProviderException(
                            $"There are no environment entities (e.g. nodes, VMs) defined/provisioned for the experiment that can host the target agent/agent steps.",
                            ErrorReason.TargetAgentsNotFound);
                    }

                    foreach (EnvironmentEntity agent in targetAgents)
                    {
                        await provider.CreateAgentStepsAsync(context, agentStep, agent.AgentId(), cancellationToken)
                            .ConfigureDefaults();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the set of all agent/child steps for the parent step provided.
        /// </summary>
        /// <param name="provider">The experiment provider itself.</param>
        /// <param name="parentStep">The parent experiment step.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A set of one or more <see cref="ExperimentStepInstance"/> objects each representing a child/agent 
        /// step for the parent step.
        /// </returns>
        public static async Task<IEnumerable<ExperimentStepInstance>> GetAgentStepsAsync(this IExperimentProvider provider, ExperimentStepInstance parentStep, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            parentStep.ThrowIfNull(nameof(parentStep));

            IEnumerable<ExperimentStepInstance> agentSteps = null;
            if (!cancellationToken.IsCancellationRequested)
            {
                IProviderDataClient apiClient = provider.Services.GetService<IProviderDataClient>();

                agentSteps = await apiClient.GetAgentStepsAsync(parentStep, cancellationToken)
                    .ConfigureAwait(false);
            }

            return agentSteps;
        }

        /// <summary>
        /// Gets the set of all agent/child steps for the parent step provided.
        /// </summary>
        /// <param name="provider">The experiment provider itself.</param>
        /// <param name="experiment">The experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A set of one or more <see cref="ExperimentStepInstance"/> objects each representing a child/agent 
        /// step for the parent step.
        /// </returns>
        public static async Task<IEnumerable<ExperimentStepInstance>> GetExperimentStepsAsync(this IExperimentProvider provider, ExperimentInstance experiment, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            experiment.ThrowIfNull(nameof(experiment));

            IEnumerable<ExperimentStepInstance> steps = null;
            if (!cancellationToken.IsCancellationRequested)
            {
                IProviderDataClient apiClient = provider.Services.GetService<IProviderDataClient>();

                steps = await apiClient.GetExperimentStepsAsync(experiment, cancellationToken)
                    .ConfigureAwait(false);
            }

            return steps;
        }

        /// <summary>
        /// Executes the logic required to monitor the status of a set of one or more agent steps.
        /// </summary>
        /// <param name="monitoringProvider">The provider responsible for monitoring the status of the child steps.</param>
        /// <param name="context">
        /// Provides context information about the experiment within which the provider is running.
        /// </param>
        /// <param name="component">The experiment component definition that provides the details on the agent steps to monitor.</param>
        /// <param name="telemetryContext">Provides telemetry event context properties.</param>
        /// <param name="cancellationToken">A token that can be used to request the provider cancel its operations.</param>
        /// <param name="defaultStatus">Optional parameter defines the default execution status to return when the agent steps are not all completed.</param>
        /// <param name="timeout"></param>
        /// <returns>
        /// A task that can be used to execute the provider logic asynchronously and that contains the
        /// result of the provider execution.
        /// </returns>
        public static async Task<ExecutionResult> ExecuteMonitoringStepsAsync(
            this IExperimentStepMonitoringProvider monitoringProvider,
            ExperimentContext context,
            ExperimentComponent component,
            EventContext telemetryContext,
            CancellationToken cancellationToken,
            ExecutionStatus defaultStatus = ExecutionStatus.InProgress,
            TimeSpan? timeout = null)
        {
            monitoringProvider.ThrowIfNull(nameof(monitoringProvider));
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = null;

            if (!cancellationToken.IsCancellationRequested)
            {
                AgentStepMonitorState state = await monitoringProvider.GetStateAsync<AgentStepMonitorState>(context, cancellationToken).ConfigureDefaults()
                    ?? new AgentStepMonitorState
                    {
                        // Leave an hour buffer between the timeout for the step and the time the monitor times out the experiment
                        // to allow for a graceful finish/exit of the step itself.
                        AgentStepsTimeout = (timeout != null) ? DateTime.UtcNow.Add(timeout.Value.Add(TimeSpan.FromHours(1))) : null as DateTime?
                    };

                EventContext relatedContext = telemetryContext.Clone();
                if (!state.AgentStepsCreated)
                {
                    await monitoringProvider.Logger.LogTelemetryAsync($"{monitoringProvider.GetType().Name}.CreateAgentSteps", relatedContext, async () =>
                    {
                        await monitoringProvider.CreateAgentStepsAsync(context, component, cancellationToken)
                            .ConfigureDefaults();

                        result = new ExecutionResult(defaultStatus);
                        relatedContext.AddContext(result);
                    }).ConfigureDefaults();

                    state.AgentStepsCreated = true;
                    await monitoringProvider.SaveStateAsync(context, state, cancellationToken)
                        .ConfigureDefaults();
                }
                else
                {
                    await monitoringProvider.Logger.LogTelemetryAsync($"{monitoringProvider.GetType().Name}.VerifyAgentStepStatus", relatedContext, async () =>
                    {
                        IEnumerable<ExperimentStepInstance> agentSteps = await monitoringProvider.GetAgentStepsAsync(context.ExperimentStep, cancellationToken)
                            .ConfigureDefaults();

                        ExecutionStatus overallStatus = agentSteps.GetExecutionStatus();
                        if (overallStatus == ExecutionStatus.Pending)
                        {
                            overallStatus = ExecutionStatus.InProgress;
                        }

                        if (overallStatus != ExecutionStatus.Succeeded && state.AgentStepsTimeout != null && DateTime.UtcNow > state.AgentStepsTimeout)
                        {
                            // The experiment step honors the timeout that was defined. If a timeout was defined in the step definition
                            // for which we are monitoring, it is applied.
                            bool timeoutIndicatesFailure = true;
                            int minStepsSucceededRequired = component.Parameters.GetValue<int>(StepParameters.TimeoutMinStepsSucceeded, -1);
                            if (minStepsSucceededRequired > 0)
                            {
                                // If the number of steps succeeded is at least as many as the timeout minimum # steps completed,
                                // AND there are no steps marked as Pending (i.e. never ran), then the overall status is considered to be 
                                // Succeeded.
                                //
                                // An example of the purpose of this is a set of steps running a KStress workload on one or more VMs as part
                                // of the experiment. Sometimes, the KStress workload crashes one of the VMs. In this scenario, the workload was
                                // running successfully on all VMs for a period of time. The case where the workload crashed the VM does not indicate
                                // that the experiment itself failed. In fact, it indicates the opposite...the experiment successfully found an issue.
                                int agentStepsSucceeded = agentSteps.Count(step => step.Status == ExecutionStatus.Succeeded);
                                int agentStepsPending = agentSteps.Count(step => step.Status == ExecutionStatus.Pending);
                                timeoutIndicatesFailure = agentStepsPending > 0 || agentStepsSucceeded < minStepsSucceededRequired;
                            }

                            if (timeoutIndicatesFailure)
                            {
                                throw new TimeoutException($"Experiment step timeout. The agent steps have timed out before completion (timeout = '{timeout?.ToString()}')");
                            }
                            else
                            {
                                overallStatus = ExecutionStatus.Succeeded;
                            }
                        }

                        IEnumerable<Exception> stepErrors = agentSteps.GetErrors();

                        result = new ExecutionResult(overallStatus, error: stepErrors?.Any() == true ? new AggregateException(stepErrors) : null);
                        relatedContext.AddContext(result);

                    }).ConfigureDefaults();
                }
            }

            return result;
        }

        internal class AgentStepMonitorState
        {
            public bool AgentStepsCreated { get; set; }

            public DateTime? AgentStepsTimeout { get; set; }
        }
    }
}
