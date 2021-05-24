namespace Juno.DataManagement
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Provides methods for managing Juno experiment data operations.
    /// </summary>
    public interface IExperimentDataManager
    {
        /// <summary>
        /// Creates one or more experment steps from the definition that are targeted for a specific agent
        /// running as part of an experiment.
        /// </summary>
        /// <param name="parentStep">The parent step.</param>
        /// <param name="agentId">The unique ID of the agent.</param>
        /// <param name="definition">The definition of the agent step.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A set of one or more <see cref="ExperimentStepInstance"/> instances created for the agent.
        /// </returns>
        Task<IEnumerable<ExperimentStepInstance>> CreateAgentStepsAsync(ExperimentStepInstance parentStep, ExperimentComponent definition, string agentId, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a new experiment instance in the data store.
        /// </summary>
        /// <param name="experiment">An experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentInstance"/> created in the backing data store.
        /// </returns>
        Task<ExperimentInstance> CreateExperimentAsync(Experiment experiment, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a new experiment context/metadata instance in the data store.
        /// </summary>
        /// <param name="context">The experiment context/metadata definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>s
        /// <returns>
        /// The <see cref="ExperimentMetadataInstance"/> created in the backing data store.
        /// </returns>
        Task<ExperimentMetadataInstance> CreateExperimentContextAsync(ExperimentMetadata context, CancellationToken cancellationToken, string contextId = null);

        /// <summary>
        /// Creates a set of steps for the experiment in the data store.
        /// </summary>
        /// <param name="experiment">The experiment instance.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A set of <see cref="ExperimentStepInstance"/> instances each describing a step within the
        /// experiment (in order).
        /// </returns>
        Task<IEnumerable<ExperimentStepInstance>> CreateExperimentStepsAsync(ExperimentInstance experiment, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a set of steps for an existing experiment in the data store given a definition.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment for which the step is related.</param>
        /// <param name="sequence">The sequence in which the step should be added.</param>
        /// <param name="definition">The step definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A set of <see cref="ExperimentStepInstance"/> instances each describing a step within the
        /// experiment (in order).
        /// </returns>
        /// <remarks>
        /// Certain experiment component definitions (e.g. ParallelExecution) can have more than one child step. This method supports the scenario
        /// where a single experiment component definition can result in multiple experiment steps created.
        /// </remarks>
        Task<IEnumerable<ExperimentStepInstance>> CreateExperimentStepsAsync(string experimentId, int sequence, ExperimentComponent definition, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes all agent steps for the experiment from the backing data store.
        /// </summary>
        /// <param name="experimentId">Defines the unique ID of the experiment for which the agents are associated.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        Task DeleteAgentStepsAsync(string experimentId, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the experiment instance from the backing data store.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment instance to delete.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        Task DeleteExperimentAsync(string experimentId, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the experiment context/metadata instance from the backing data store.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment instance to delete.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        Task DeleteExperimentContextAsync(string experimentId, CancellationToken cancellationToken, string contextId = null);

        /// <summary>
        /// Deletes all of the experiment steps from the data store for the 
        /// experiment defined.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment whose steps will be deleted.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        Task DeleteExperimentStepsAsync(string experimentId, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the experiment from the data store for which the agent is associated.
        /// </summary>
        /// <param name="agentId">The unique ID of an experiment agent.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentInstance"/> instance for which the agent is associated.
        /// </returns>
        Task<ExperimentInstance> GetAgentExperimentAsync(string agentId, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the experiment steps from the data store for the given agent.
        /// </summary>
        /// <param name="agentId">The unique ID of the agent.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="filter">An optional filter expression to apply to the step search.</param>
        /// <returns>
        /// A set of one or more <see cref="ExperimentStepInstance"/> instances associated with the agent.
        /// </returns>
        Task<IEnumerable<ExperimentStepInstance>> GetAgentStepsAsync(string agentId, CancellationToken cancellationToken, IQueryFilter filter = null);

        /// <summary>
        /// Returns the experiment with the ID provided from the data store.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment instance to get.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentInstance"/> for the ID provided.
        /// </returns>
        Task<ExperimentInstance> GetExperimentAsync(string experimentId, CancellationToken cancellationToken);

        /// <summary>
        /// Performs a query against the experiments data store.
        /// </summary>
        /// <param name="query">SQL query string</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A set of one or more <see cref="JObject"/>.
        /// </returns>
        Task<IEnumerable<JObject>> QueryExperimentsAsync(string query, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the context/metadata instance for the experiment with the ID provided
        /// from the data store.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment whose context/metadata will be retrieved.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// The <see cref="ExperimentMetadataInstance"/> for the experiment ID provided.
        /// </returns>
        Task<ExperimentMetadataInstance> GetExperimentContextAsync(string experimentId, CancellationToken cancellationToken, string contextId = null);

        /// <summary>
        /// Returns an experiment step from the data store.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="stepId">The unique ID of the agent experiment step.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A set of one or more <see cref="ExperimentStepInstance"/> instances associated with the agent.
        /// </returns>
        Task<ExperimentStepInstance> GetExperimentAgentStepAsync(string experimentId, string stepId, CancellationToken cancellationToken);

        /// <summary>
        /// Returns all agent steps from the data store for the given experiment.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="filter">An optional filter expression to apply to the step search.</param>
        /// <returns>
        /// A set of one or more <see cref="ExperimentStepInstance"/> instances associated with all
        /// experiment agents.
        /// </returns>
        Task<IEnumerable<ExperimentStepInstance>> GetExperimentAgentStepsAsync(string experimentId, CancellationToken cancellationToken, IQueryFilter filter = null);

        /// <summary>
        /// Returns the experiment step with the ID provided from the data store.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="stepId">The unique ID of the experiment step.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentStepInstance"/> for the ID provided.
        /// </returns>
        Task<ExperimentStepInstance> GetExperimentStepAsync(string experimentId, string stepId, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the experiment steps from the data store for the experiment defined.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="filter">An optional filter expression to apply to the step search.</param>
        /// <returns>
        /// A set of one or more <see cref="ExperimentStepInstance"/> instances associated with the experiment.
        /// </returns>
        Task<IEnumerable<ExperimentStepInstance>> GetExperimentStepsAsync(string experimentId, CancellationToken cancellationToken, IQueryFilter filter = null);

        /// <summary>
        /// Updates an existing agent step instance in the data store.
        /// </summary>
        /// <param name="updatedStep">The agent step definition containing the updates.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentStepInstance"/> having the updates.
        /// </returns>
        Task<ExperimentStepInstance> UpdateAgentStepAsync(ExperimentStepInstance updatedStep, CancellationToken cancellationToken);

        /// <summary>
        /// Updates an existing experiment instance in the data store.
        /// </summary>
        /// <param name="updatedExperiment">The updated experiment instance definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentInstance"/> having the updates.
        /// </returns>
        Task<ExperimentInstance> UpdateExperimentAsync(ExperimentInstance updatedExperiment, CancellationToken cancellationToken);

        /// <summary>
        /// Updates an existing experiment instance in the data store.
        /// </summary>
        /// <param name="updatedContext">The updated experiment context/metadata instance definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// The <see cref="ExperimentMetadataInstance"/> having the updates.
        /// </returns>
        Task<ExperimentMetadataInstance> UpdateExperimentContextAsync(ExperimentMetadataInstance updatedContext, CancellationToken cancellationToken, string contextId = null);

        /// <summary>
        /// Updates an existing experiment step instance in the data store.
        /// </summary>
        /// <param name="updatedStep">The experiment step definition containing the updates.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentStepInstance"/> having the updates.
        /// </returns>
        Task<ExperimentStepInstance> UpdateExperimentStepAsync(ExperimentStepInstance updatedStep, CancellationToken cancellationToken);
    }
}
