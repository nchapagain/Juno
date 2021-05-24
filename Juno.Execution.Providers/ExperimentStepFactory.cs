namespace Juno.Execution
{
    using System;
    using System.Collections.Generic;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Execution.Providers;
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Execution.Providers.Certification;
    using Juno.Execution.Providers.Environment;
    using Juno.Execution.Providers.Payloads;
    using Juno.Execution.Providers.Watchdog;
    using Juno.Execution.Providers.Workloads;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Provides a factory for building experiment execution steps.
    /// </summary>
    public class ExperimentStepFactory : IExperimentStepFactory
    {
        private const int DefaultSequenceIncrement = 100;

        /// <summary>
        /// Creates a set of one or more experiment agent steps from the experiment
        /// component/definition.
        /// </summary>
        /// <param name="component">The experiment component.</param>
        /// <param name="agentId">The unique ID of the agent for which the step is targeted.</param>
        /// <param name="parentStepId">The unique ID of the parent step of the agent step.</param>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="sequence">
        /// Defines the initial/first sequence number to use when creating the steps. This is used to create steps that
        /// need to be sequenced after a set of pre-existing steps (e.g. steps already in the Juno system).
        /// </param>
        public IEnumerable<ExperimentStepInstance> CreateAgentSteps(ExperimentComponent component, string agentId, string parentStepId, string experimentId, int sequence)
        {
            component.ThrowIfNull(nameof(component));
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));
            parentStepId.ThrowIfNullOrWhiteSpace(nameof(parentStepId));
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();
            IEnumerable<ExperimentComponent> componentsInSequence = ExperimentStepFactory.GetComponentsInSequence(component);

            foreach (ExperimentComponent componentInSequence in componentsInSequence)
            {
                SupportedStepTarget stepTarget = componentInSequence.GetSupportedStepTarget();

                if (stepTarget == SupportedStepTarget.ExecuteRemotely)
                {
                    throw new ArgumentException(
                        $"Invalid component provider definition. The provider for component '{componentInSequence.Name}' is defined to indicate it must execute remotely. " +
                        "A step targeted for an agent cannot execute remotely.");
                }

                steps.Add(new ExperimentStepInstance(
                   Guid.NewGuid().ToString(),
                   experimentId,
                   componentInSequence.Group,
                   componentInSequence.GetSupportedStepType(),
                   ExecutionStatus.Pending,
                   sequence,
                   0,
                   componentInSequence,
                   agentId: agentId,
                   parentStepId: parentStepId));
            }

            return steps;
        }

        /// <summary>
        /// Creates a set of one or more experiment agent steps from the experiment
        /// components/definitions.
        /// </summary>
        /// <param name="components">The experiment components.</param>
        /// <param name="agentId">The unique ID of the agent for which the step is targeted.</param>
        /// <param name="parentStepId">The unique ID of the parent step of the agent step.</param>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="sequence">
        /// Optional parameter defines the initial/first sequence number to use when creating the steps. This is used to create steps that
        /// need to be sequenced after a set of pre-existing steps (e.g. steps already in the Juno system).
        /// </param>
        public IEnumerable<ExperimentStepInstance> CreateAgentSteps(IEnumerable<ExperimentComponent> components, string agentId, string parentStepId, string experimentId, int? sequence = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));
            parentStepId.ThrowIfNullOrWhiteSpace(nameof(parentStepId));
            components.ThrowIfNullOrEmpty(nameof(components));

            List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();

            int effectiveSequence = sequence ?? ExperimentStepFactory.DefaultSequenceIncrement;
            foreach (ExperimentComponent component in components)
            {
                steps.AddRange(this.CreateAgentSteps(component, agentId, parentStepId, experimentId, effectiveSequence));
                effectiveSequence += ExperimentStepFactory.DefaultSequenceIncrement;
            }

            return steps;
        }

        /// <summary>
        /// Creates a set of one or more experiment steps from the experiment
        /// component/definition.
        /// </summary>
        /// <param name="component">The experiment component.</param>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="sequence">
        /// Defines the initial/first sequence number to use when creating the steps. This is used to create steps that
        /// need to be sequenced after a set of pre-existing steps (e.g. steps already in the Juno system).
        /// </param>
        public IEnumerable<ExperimentStepInstance> CreateOrchestrationSteps(ExperimentComponent component, string experimentId, int sequence)
        {
            component.ThrowIfNull(nameof(component));
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();
            IEnumerable<ExperimentComponent> componentsForSequence = ExperimentStepFactory.GetComponentsInSequence(component);

            foreach (ExperimentComponent componentInSequence in componentsForSequence)
            {
                SupportedStepTarget stepTarget = componentInSequence.GetSupportedStepTarget();
                SupportedStepType stepType = componentInSequence.GetSupportedStepType();

                switch (stepTarget)
                {
                    case SupportedStepTarget.ExecuteRemotely:

                        steps.Add(new ExperimentStepInstance(
                            Guid.NewGuid().ToString(),
                            experimentId,
                            componentInSequence.Group,
                            stepType,
                            ExecutionStatus.Pending,
                            sequence,
                            0,
                            componentInSequence));

                        break;

                    case SupportedStepTarget.ExecuteOnNode:
                    case SupportedStepTarget.ExecuteOnVirtualMachine:
                        // All steps that execute on a physical node or on a virtual machine (e.g. host or guest agent)
                        // will be monitored.  The monitoring step is responsible for creating agent steps that run the
                        // actual/original step/component provider and for monitoring the status of those agent steps thereafter.
                        Type monitoringComponentType = null;

                        switch (stepType)
                        {
                            case SupportedStepType.EnvironmentCleanup:
                                monitoringComponentType = typeof(MonitorAgentEnvironmentCleanup);
                                break;

                            case SupportedStepType.EnvironmentSetup:
                                monitoringComponentType = typeof(MonitorAgentEnvironmentSetup);
                                break;

                            case SupportedStepType.Payload:
                                monitoringComponentType = typeof(MonitorAgentPayload);
                                break;

                            case SupportedStepType.Certification:
                                monitoringComponentType = typeof(MonitorAgentCertification);
                                break;

                            case SupportedStepType.Workload:
                                monitoringComponentType = typeof(MonitorAgentWorkload);
                                break;

                            case SupportedStepType.Watchdog:
                                monitoringComponentType = typeof(MonitorAgentWatchdog);
                                break;

                            default:
                                throw new NotSupportedException($"Unsupported step type for execution on a host or VM agent: {stepType.ToString()}");
                        }

                        ExperimentComponent monitoringComponent = new ExperimentComponent(
                            monitoringComponentType.FullName,
                            componentInSequence.Name,
                            componentInSequence.Description,
                            componentInSequence.Group,
                            parameters: componentInSequence.Parameters,
                            tags: componentInSequence.Tags);
                        monitoringComponent.Extensions.AddRange(componentInSequence.Extensions);

                        monitoringComponent.AddOrReplaceChildSteps(componentInSequence);

                        steps.Add(new ExperimentStepInstance(
                            Guid.NewGuid().ToString(),
                            experimentId,
                            componentInSequence.Group,
                            stepType,
                            ExecutionStatus.Pending,
                            sequence,
                            0,
                            monitoringComponent));

                        break;

                    default:
                        throw new ExperimentException($"Unexpected step target '{stepTarget.ToString()}' defined for component/provider '{component.ComponentType}'");
                }
            }

            return steps;
        }

        /// <summary>
        /// Creates a set of one or more experiment steps from the experiment
        /// components/definitions.
        /// </summary>
        /// <param name="components">The experiment components.</param>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="sequence">
        /// Optional parameter defines the initial/first sequence number to use when creating the steps. This is used to create steps that
        /// need to be sequenced after a set of pre-existing steps (e.g. steps already in the Juno system).
        /// </param>
        /// <param name="enableDiagnostics">True to add the auto-triage diagnostics step the the set of experiment steps created.</param>
        public IEnumerable<ExperimentStepInstance> CreateOrchestrationSteps(IEnumerable<ExperimentComponent> components, string experimentId, int? sequence = null, bool enableDiagnostics = false)
        {
            components.ThrowIfNullOrEmpty(nameof(components));
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();

            int effectiveSequence = sequence ?? ExperimentStepFactory.DefaultSequenceIncrement;
            foreach (ExperimentComponent component in components)
            {
                steps.AddRange(this.CreateOrchestrationSteps(component, experimentId, effectiveSequence));
                effectiveSequence += ExperimentStepFactory.DefaultSequenceIncrement;
            }

            if (enableDiagnostics)
            {
                ExperimentComponent autoTriageDiagnostics = new ExperimentComponent(
                    typeof(AutoTriageProvider).FullName,
                    "Perform Auto-Triage Diagnostics",
                    "Perform auto-triage diagnostics on the experiment if any steps fail causing the experiment as a whole to fail.",
                    ExperimentComponent.AllGroups);

                steps.AddRange(this.CreateOrchestrationSteps(autoTriageDiagnostics, experimentId, effectiveSequence));
            }

            return steps;
        }

        private static IEnumerable<ExperimentComponent> GetComponentsInSequence(ExperimentComponent component)
        {
            List<ExperimentComponent> components = new List<ExperimentComponent>();
            if (component.IsParallelExecution())
            {
                components.AddRange(component.GetChildSteps());
            }
            else
            {
                components.Add(component);
            }

            return components;
        }
    }
}
