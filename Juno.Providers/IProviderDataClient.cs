namespace Juno.Providers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;

    /// <summary>
    /// Provides base methods required by providers to access data in the
    /// Juno system.
    /// </summary>
    public interface IProviderDataClient
    {
        /// <summary>
        /// Makes an API request to create an agent/child step for the parent step provided and 
        /// that targets a specific agent.
        /// </summary>
        /// <param name="parentStep">The parent step of the agent/child step.</param>
        /// <param name="definition">The agent/child component definition.</param>
        /// <param name="agentId">The unique ID of the agent.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentStepInstance"/> created.
        /// </returns>
        Task<IEnumerable<ExperimentStepInstance>> CreateAgentStepsAsync(ExperimentStepInstance parentStep, ExperimentComponent definition, string agentId, CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to get agent heartbeat
        /// </summary>
        /// <param name="agentId">The unique ID of the agent.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// Latest <see cref="AgentHeartbeatInstance"/> for given agent.
        /// objects.
        /// </returns>
        Task<AgentHeartbeatInstance> GetAgentHeartbeatAsync(string agentId, CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to get all agent/child steps associated with the parent step.
        /// </summary>
        /// <param name="parentStep">The parent step of the agent/child step.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A set of agent/child <see cref="ExperimentStepInstance"/> objects related to the parent step.
        /// objects.
        /// </returns>
        Task<IEnumerable<ExperimentStepInstance>> GetAgentStepsAsync(ExperimentStepInstance parentStep, CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to get all steps associated with the experiment.
        /// </summary>
        /// <param name="experiment">The experiemnt.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A set of agent/child <see cref="ExperimentStepInstance"/> objects related to the experiment.
        /// </returns>
        Task<IEnumerable<ExperimentStepInstance>> GetExperimentStepsAsync(ExperimentInstance experiment, CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to get the context/metadata instance for an experiment.
        /// </summary>
        /// <typeparam name="TState">The data type of the state object to retrieve.</typeparam>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="key">The key/name of the state object in the experiment context.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="stateId">Optionally defines the specific ID of the state/context object to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// The state object if defined or null if it is not.
        /// </returns>
        Task<TState> GetOrCreateStateAsync<TState>(string experimentId, string key, CancellationToken cancellationToken, string stateId = null);

        /// <summary>
        /// Makes an API request to update an existing experiment context/metadata instance.
        /// </summary>
        /// <typeparam name="TState">The data type of the state object to save.</typeparam>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="key">The key/name of the state object in the experiment context.</param>
        /// <param name="state">The state object to save in the experiment context.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="stateId">Optionally defines the specific ID of the state/context object to retrieve. When not defined, returns the global context for the experiment.</param>
        Task SaveStateAsync<TState>(string experimentId, string key, TState state, CancellationToken cancellationToken, string stateId = null);

        /// <summary>
        /// Makes an API request to update items in context/metadata instance, it will add if not exist by id.
        /// </summary>
        /// <typeparam name="TState">The data type of the state object to save.</typeparam>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="key">The key/name of the state object in the experiment context.</param>
        /// <param name="state">The state object to save in the experiment context.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="stateId">Optionally defines the specific ID of the state/context object to retrieve. When not defined, returns the global context for the experiment.</param>
        Task UpdateStateItemsAsync<TState>(string experimentId, string key, IEnumerable<TState> state, CancellationToken cancellationToken, string stateId = null)
            where TState : IIdentifiable;

        /// <summary>
        /// Makes an API request to remove items in context/metadata instance based on id.
        /// </summary>
        /// <typeparam name="TState">The data type of the state object to save.</typeparam>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="key">The key/name of the state object in the experiment context.</param>
        /// <param name="state">The state object to save in the experiment context.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="stateId">Optionally defines the specific ID of the state/context object to retrieve. When not defined, returns the global context for the experiment.</param>
        Task RemoveStateItemsAsync<TState>(string experimentId, string key, IEnumerable<TState> state, CancellationToken cancellationToken, string stateId = null)
            where TState : IIdentifiable;
    }
}
