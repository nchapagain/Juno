namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension methods for
    /// </summary>
    public static class ExperimentProviderExtensions
    {
        /// <summary>
        /// Extension returns a result that indicates the provider operation was cancelled.
        /// </summary>
        /// <param name="provider">The experiment provider itself.</param>
        /// <param name="error">An optional parameter that provides an error that caused the cancellation.</param>
        /// <returns>
        /// An <see cref="ExecutionResult"/> having a canceled status.
        /// </returns>
        public static ExecutionResult Cancelled(this IExperimentProvider provider, Exception error = null)
        {
            return new ExecutionResult(ExecutionStatus.Cancelled, error: error);
        }

        /// <summary>
        /// Returns the net/rollup execution status for the set of agent steps.
        /// </summary>
        /// <param name="executionResults">The steps for which to check for a net status.</param>
        /// <returns>
        /// The net/rollup status for the set of agent steps.
        /// </returns>
        public static ExecutionResult GetExecutionResult(this IEnumerable<ExecutionResult> executionResults)
        {
            executionResults.ThrowIfNullOrEmpty(nameof(executionResults));

            // The net/rollup status for a set of steps has a priority order.  Any steps that are failed,
            // indicates the entire experiment workflow must be set as failed. It is the same thing for cancelled
            // steps. The following set of conditionals are in priority order to represent the "best-case" current
            // status of the set of steps.
            ExecutionResult executionResult = null;
            ExecutionStatus status = ExecutionStatus.Pending;
            if (executionResults.Any(result => result.Status == ExecutionStatus.Failed))
            {
                status = ExecutionStatus.Failed;
            }
            else if (executionResults.Any(result => result.Status == ExecutionStatus.Cancelled))
            {
                status = ExecutionStatus.Cancelled;
            }
            else if (executionResults.Any(result => result.Status == ExecutionStatus.InProgress))
            {
                // Any InProgress steps take priority over InProgressContinue when evaluating the
                // net status of a set of steps. Essentially, if there are steps marked InProgress, the
                // overall workflow is expected to wait for those steps to complete before continuing.
                status = ExecutionStatus.InProgress;
            }
            else if (executionResults.Any(result => result.Status == ExecutionStatus.InProgressContinue))
            {
                status = ExecutionStatus.InProgressContinue;
            }
            else if (executionResults.All(result => result.Status == ExecutionStatus.Succeeded))
            {
                status = ExecutionStatus.Succeeded;
            }

            if (executionResults.Any(result => result.Error != null))
            {
                List<Exception> errors = new List<Exception>();
                executionResults.ToList().ForEach(result =>
                {
                    if (result.Error != null)
                    {
                        errors.Add(result.Error);
                    }
                });

                executionResult = new ExecutionResult(status, error: new AggregateException(errors));
            }
            else
            {
                executionResult = new ExecutionResult(status);
            }

            return executionResult;
        }

        /// <summary>
        /// Returns the net/rollup execution status for the set of agent steps.
        /// </summary>
        /// <param name="agentSteps">The steps for which to check for a net status.</param>
        /// <returns>
        /// The net/rollup status for the set of agent steps.
        /// </returns>
        public static ExecutionStatus GetExecutionStatus(this IEnumerable<ExperimentStepInstance> agentSteps)
        {
            agentSteps.ThrowIfNullOrEmpty(nameof(agentSteps));

            // The net/rollup status for a set of steps has a priority order.  Any steps that are failed,
            // indicates the entire experiment workflow must be set as failed. It is the same thing for cancelled
            // steps. The following set of conditionals are in priority order to represent the "best-case" current
            // status of the set of steps.
            ExecutionStatus status = ExecutionStatus.Pending;
            if (agentSteps.Any(step => step.Status == ExecutionStatus.Failed))
            {
                status = ExecutionStatus.Failed;
            }
            else if (agentSteps.Any(step => step.Status == ExecutionStatus.Cancelled))
            {
                status = ExecutionStatus.Cancelled;
            }
            else if (agentSteps.Any(step => step.Status == ExecutionStatus.InProgress))
            {
                // Any InProgress steps take priority over InProgressContinue when evaluating the
                // net status of a set of steps. Essentially, if there are steps marked InProgress, the
                // overall workflow is expected to wait for those steps to complete before continuing.
                status = ExecutionStatus.InProgress;
            }
            else if (agentSteps.Any(step => step.Status == ExecutionStatus.InProgressContinue))
            {
                status = ExecutionStatus.InProgressContinue;
            }
            else if (agentSteps.All(step => step.Status == ExecutionStatus.Succeeded))
            {
                status = ExecutionStatus.Succeeded;
            }

            return status;
        }

        /// <summary>
        /// Extension gets the current entity pool definition from the experiment state/context.
        /// </summary>
        /// <param name="provider">The experiment provider instance.</param>
        /// <param name="context">Provides the context for the experiment itself (e.g. the ID).</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public static Task<IEnumerable<EnvironmentEntity>> GetEntityPoolAsync(this IExperimentProvider provider, ExperimentContext context, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));

            return provider.GetStateAsync<IEnumerable<EnvironmentEntity>>(
                context,
                ContractExtension.EntityPool,
                cancellationToken);
        }

        /// <summary>
        /// Extension gets the set of entities that have been setup/provisioned for the experiment
        /// from the experiment state/context.
        /// </summary>
        /// <param name="provider">The experiment provider instance.</param>
        /// <param name="context">Provides the context for the experiment itself (e.g. the ID).</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public static Task<IEnumerable<EnvironmentEntity>> GetEntitiesProvisionedAsync(this IExperimentProvider provider, ExperimentContext context, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));

            return provider.GetStateAsync<IEnumerable<EnvironmentEntity>>(
                context,
                ContractExtension.EntitiesProvisioned,
                cancellationToken);
        }

        /// <summary>
        /// Gets the state object for the provider.
        /// </summary>
        /// <param name="provider">The experiment provider itself.</param>
        /// <param name="context">Provides the context for the specific experiment for which the provider is related.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="sharedState">
        /// True to look for the state definition in the shared/global state/context object. False to look for the state definition in a provider-specific
        /// state/context object.
        /// </param>
        /// <returns>
        /// A task that can be used to save the provider state object asynchronously.
        /// </returns>
        public static Task<TState> GetStateAsync<TState>(this IExperimentProvider provider, ExperimentContext context, CancellationToken cancellationToken, bool sharedState = true)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));

            string key = ExperimentProviderExtensions.GetStateKey(context);
            string stateId = null;

            if (!sharedState)
            {
                stateId = ExperimentProviderExtensions.GetStateId(context);
            }

            return ExperimentProviderExtensions.GetStateAsync<TState>(provider, context, key, cancellationToken, stateId);
        }

        /// <summary>
        /// Gets the state object for the provider.
        /// </summary>
        /// <param name="provider">The experiment provider itself.</param>
        /// <param name="context">Provides the context for the specific experiment for which the provider is related.</param>
        /// <param name="key">The key within the context object.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="stateId">The ID of the context object in the backing data store.</param>
        /// <returns>
        /// A task that can be used to save the provider state object asynchronously.
        /// </returns>
        public static async Task<TState> GetStateAsync<TState>(this IExperimentProvider provider, ExperimentContext context, string key, CancellationToken cancellationToken, string stateId = null)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));

            TState state = default(TState);
            if (!cancellationToken.IsCancellationRequested)
            {
                IProviderDataClient apiClient = provider.Services.GetService<IProviderDataClient>();
                state = await apiClient.GetOrCreateStateAsync<TState>(context.Experiment.Id, key, cancellationToken, stateId)
                    .ConfigureAwait(false);
            }

            return state;
        }

        /// <summary>
        /// Gets agent identifications
        /// </summary>
        /// <returns>
        /// Agent identifications
        /// </returns>
        public static AgentIdentification GetAgentIdentifications(this IExperimentProvider provider)
        {
            provider.ThrowIfNull(nameof(provider));
            provider.Services.TryGetService<AgentIdentification>(out AgentIdentification identification);
            return identification;
        }

        /// <summary>
        /// Extension returns true/false whether the specified feature flag is defined in the experiment component
        /// definition.
        /// </summary>
        /// <param name="provider">The provider itself.</param>
        /// <param name="component">The experiment component/step definition containing feature flag parameters.</param>
        /// <param name="flag">The feature flag to look for.</param>
        /// <returns>
        /// True if the experiment component parameters contains the feature flag specified, false if not.
        /// </returns>
        public static bool HasFeatureFlag(this IExperimentProvider provider, ExperimentComponent component, string flag)
        {
            component.ThrowIfNull(nameof(component));
            flag.ThrowIfNullOrWhiteSpace(nameof(flag));

            bool hasFlag = false;
            IConvertible flags;
            if (component.Parameters.TryGetValue(StepParameters.FeatureFlag, out flags))
            {
                string[] individualFlags = flags?.ToString().Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (individualFlags?.Any() == true)
                {
                    hasFlag = individualFlags.Contains(flag, StringComparer.OrdinalIgnoreCase);
                }
            }

            return hasFlag;
        }

        /// <summary>
        /// Extension returns true/false whether diagnostics is enabled in the experiment component.
        /// </summary>
        /// <param name="provider">The provider itself.</param>
        /// <param name="component">The experiment component/step definition containing enable diagnostics parameter.</param>
        /// <returns>
        /// True if the experiment component parameters contains the enable diagnostics with true value, false otherwise.
        /// </returns>
        public static bool IsDiagnosticsEnabled(this IExperimentProvider provider, ExperimentComponent component)
        {
            component.ThrowIfNull(nameof(component));

            bool enableDiagnostics = false;
            if (component.Parameters.TryGetValue(StepParameters.EnableDiagnostics, out IConvertible enableDiagnosticsFlag))
            {
                enableDiagnostics = Convert.ToBoolean(enableDiagnosticsFlag);
            }

            return enableDiagnostics;
        }

        /// <summary>
        /// Initializes/installs any dependencies for the given experiment component/step.
        /// </summary>
        /// <param name="provider">The experiment provider instance.</param>
        /// <param name="context">Provides context for the experiment.</param>
        /// <param name="component">The experiment component/step that defines the dependencies.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="ExecutionResult"/> that represents the outcome of the dependency installation.
        /// </returns>
        public static async Task<ExecutionResult> InstallDependenciesAsync(this IExperimentProvider provider, ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            component.ThrowIfNull(nameof(component));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.Succeeded);
            if (component.Dependencies?.Any() == true)
            {
                List<Task<ExecutionResult>> installationTasks = new List<Task<ExecutionResult>>();
                foreach (ExperimentComponent dependency in component.Dependencies)
                {
                    IExperimentProvider dependencyProvider = ExperimentProviderFactory.CreateProvider(dependency,  provider.Services, SupportedStepType.Dependency);
                    await dependencyProvider.ConfigureServicesAsync(context, component).ConfigureDefaults();
                    installationTasks.Add(dependencyProvider.ExecuteAsync(context, dependency, cancellationToken));
                }

                await Task.WhenAll(installationTasks).ConfigureDefaults();
                result = installationTasks.Select(task => task.GetAwaiter().GetResult()).GetExecutionResult();
            }

            return result;
        }

        /// <summary>
        /// Extension saves the entity pool definition to the experiment state/context.
        /// </summary>
        /// <param name="provider">The experiment provider instance.</param>
        /// <param name="context">Provides the context for the experiment itself (e.g. the ID).</param>
        /// <param name="entityPool">The entity pool definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public static Task SaveEntityPoolAsync(this IExperimentProvider provider, ExperimentContext context, IEnumerable<EnvironmentEntity> entityPool, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));

            return provider.SaveStateAsync(context, ContractExtension.EntityPool, entityPool, cancellationToken);
        }

        /// <summary>
        /// Extension saves the entity pool definition to the experiment state/context.
        /// </summary>
        /// <param name="provider">The experiment provider instance.</param>
        /// <param name="context">Provides the context for the experiment itself (e.g. the ID).</param>
        /// <param name="entities">The set of entities provisioned definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public static Task SaveEntitiesProvisionedAsync(this IExperimentProvider provider, ExperimentContext context, IEnumerable<EnvironmentEntity> entities, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));

            return provider.SaveStateAsync(context, ContractExtension.EntitiesProvisioned, entities, cancellationToken);
        }

        /// <summary>
        /// Extension to add or update provisioned entities to the entity pool definition to the experiment state/context.
        /// </summary>
        /// <param name="provider">The experiment provider instance.</param>
        /// <param name="context">Provides the context for the experiment itself (e.g. the ID).</param>
        /// <param name="entities">The set of entities provisioned to be added.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public static async Task UpdateEntitiesProvisionedAsync(this IExperimentProvider provider, ExperimentContext context, IEnumerable<EnvironmentEntity> entities, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));

            if (!cancellationToken.IsCancellationRequested)
            {
                IProviderDataClient apiClient = provider.Services.GetService<IProviderDataClient>();

                await apiClient.UpdateStateItemsAsync(context.Experiment.Id, ContractExtension.EntitiesProvisioned, entities, cancellationToken)
                    .ConfigureDefaults();
            }
        }

        /// <summary>
        /// Extension to remove provisioned entities from the entity pool definition to the experiment state/context.
        /// </summary>
        /// <param name="provider">The experiment provider instance.</param>
        /// <param name="context">Provides the context for the experiment itself (e.g. the ID).</param>
        /// <param name="entities">The set of entities provisioned to be added.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public static async Task RemoveFromEntitiesProvisionedAsync(this IExperimentProvider provider, ExperimentContext context, IEnumerable<EnvironmentEntity> entities, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));

            if (!cancellationToken.IsCancellationRequested)
            {
                IProviderDataClient apiClient = provider.Services.GetService<IProviderDataClient>();

                await apiClient.RemoveStateItemsAsync(context.Experiment.Id, ContractExtension.EntitiesProvisioned, entities, cancellationToken)
                    .ConfigureDefaults();
            }
        }

        /// <summary>
        /// Extension to remove provisioned entities from the entity pool definition to the experiment state/context.
        /// </summary>
        /// <param name="provider">The experiment provider instance.</param>
        /// <param name="context">Provides the context for the experiment itself (e.g. the ID).</param>
        /// <param name="entities">The set of entities provisioned to be added.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public static async Task RemoveFromEntityPoolAsync(this IExperimentProvider provider, ExperimentContext context, IEnumerable<EnvironmentEntity> entities, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));

            if (!cancellationToken.IsCancellationRequested)
            {
                IProviderDataClient apiClient = provider.Services.GetService<IProviderDataClient>();

                await apiClient.RemoveStateItemsAsync(context.Experiment.Id, ContractExtension.EntityPool, entities, cancellationToken)
                    .ConfigureDefaults();
            }
        }

        /// <summary>
        /// Extension to add or update provisioned entities to the entity pool definition to the experiment state/context.
        /// </summary>
        /// <param name="provider">The experiment provider instance.</param>
        /// <param name="context">Provides the context for the experiment itself (e.g. the ID).</param>
        /// <param name="entities">The set of entities provisioned to be added.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public static async Task UpdateEntityPoolAsync(this IExperimentProvider provider, ExperimentContext context, IEnumerable<EnvironmentEntity> entities, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));

            if (!cancellationToken.IsCancellationRequested)
            {
                IProviderDataClient apiClient = provider.Services.GetService<IProviderDataClient>();

                await apiClient.UpdateStateItemsAsync(context.Experiment.Id, ContractExtension.EntityPool, entities, cancellationToken)
                    .ConfigureDefaults();
            }
        }

        /// <summary>
        /// Saves the state object for the provider.
        /// </summary>
        /// <param name="provider">The experiment provider itself.</param>
        /// <param name="context">Provides the context for the specific experiment for which the provider is related.</param>
        /// <param name="state">The state object itself.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="sharedState">
        /// True to look for the state definition in the shared/global state/context object. False to look for the state definition in a provider-specific
        /// state/context object.
        /// </param>
        /// <returns>
        /// A task that can be used to save the provider state object asynchronously.
        /// </returns>
        public static Task SaveStateAsync<TState>(this IExperimentProvider provider, ExperimentContext context, TState state, CancellationToken cancellationToken, bool sharedState = true)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));
            state.ThrowIfNull(nameof(state));

            string stateKey = ExperimentProviderExtensions.GetStateKey(context);
            string stateId = null;

            if (!sharedState)
            {
                stateId = ExperimentProviderExtensions.GetStateId(context);
            }

            return provider.SaveStateAsync(context, stateKey, state, cancellationToken, stateId);
        }

        /// <summary>
        /// Saves the state object for the provider.
        /// </summary>
        /// <param name="provider">The experiment provider itself.</param>
        /// <param name="context">Provides the context for the specific experiment for which the provider is related.</param>
        /// <param name="key">The key to which the state will be saved.</param>
        /// <param name="state">The state object itself.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// A task that can be used to save the provider state object asynchronously.
        /// </returns>
        public static async Task SaveStateAsync<TState>(this IExperimentProvider provider, ExperimentContext context, string key, TState state, CancellationToken cancellationToken, string contextId = null)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));
            key.ThrowIfNullOrWhiteSpace(nameof(key));
            state.ThrowIfNull(nameof(state));

            if (!cancellationToken.IsCancellationRequested)
            {
                IProviderDataClient apiClient = provider.Services.GetService<IProviderDataClient>();
                await apiClient.SaveStateAsync(context.Experiment.Id, key, state, cancellationToken, contextId)
                    .ConfigureDefaults();
            }
        }

        /// <summary>
        /// Gets the diagnostics request details to the backend store.
        /// </summary>
        /// <param name="provider">Specifies the experiment provider instance.</param>
        /// <param name="context">Specifies the context for the experiment.</param>
        /// <param name="cancellationToken">Specifies a token that can be used to cancel the operation.</param>
        public static Task<IEnumerable<DiagnosticsRequest>> GetDiagnosticsRequestsAsync(this IExperimentProvider provider, ExperimentContext context, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));

            return provider.GetStateAsync<IEnumerable<DiagnosticsRequest>>(
                context,
                ContractExtension.DiagnosticsRequests,
                cancellationToken);
        }

        /// <summary>
        /// Stores the diagnostics request details to the backend store.
        /// </summary>
        /// <param name="provider">Specifies the experiment provider instance.</param>
        /// <param name="context">Specifies the context for the experiment.</param>
        /// <param name="diagnosticsRequests">Specifies the list of diagnostics request item.</param>
        /// <param name="cancellationToken">Specifies a token that can be used to cancel the operation.</param>
        /// <returns></returns>
        public static async Task AddDiagnosticsRequestAsync(this IExperimentProvider provider, ExperimentContext context, IEnumerable<DiagnosticsRequest> diagnosticsRequests, CancellationToken cancellationToken)
        {
            provider.ThrowIfNull(nameof(provider));
            context.ThrowIfNull(nameof(context));
            diagnosticsRequests.ThrowIfNull(nameof(diagnosticsRequests));

            if (!cancellationToken.IsCancellationRequested)
            {
                IProviderDataClient apiClient = provider.Services.GetService<IProviderDataClient>();

                await apiClient.UpdateStateItemsAsync(context.Experiment.Id, ContractExtension.DiagnosticsRequests, diagnosticsRequests, cancellationToken)
                    .ConfigureDefaults();
            }
        }

        internal static string GetStateId(ExperimentContext context)
        {
            context.ThrowIfNull(nameof(context));
            return context.ExperimentStep.Id;
        }

        internal static string GetStateKey(ExperimentContext context)
        {
            context.ThrowIfNull(nameof(context));
            return $"state-{context.ExperimentStep.Id}";
        }
    }
}
