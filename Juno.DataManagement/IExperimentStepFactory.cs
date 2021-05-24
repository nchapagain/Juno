namespace Juno.DataManagement
{
    using System.Collections.Generic;
    using Juno.Contracts;

    /// <summary>
    /// Provides a factory for building experiment execution steps.
    /// </summary>
    public interface IExperimentStepFactory
    {
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
        IEnumerable<ExperimentStepInstance> CreateAgentSteps(ExperimentComponent component, string agentId, string parentStepId, string experimentId, int sequence);

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
        IEnumerable<ExperimentStepInstance> CreateAgentSteps(IEnumerable<ExperimentComponent> components, string agentId, string parentStepId, string experimentId, int? sequence = null);

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
        IEnumerable<ExperimentStepInstance> CreateOrchestrationSteps(ExperimentComponent component, string experimentId, int sequence);

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
        IEnumerable<ExperimentStepInstance> CreateOrchestrationSteps(IEnumerable<ExperimentComponent> components, string experimentId, int? sequence = null, bool enableDiagnostics = false);
    }
}
