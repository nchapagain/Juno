namespace Juno.DataManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.DataManagement.Cosmos;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Contracts.OData;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Provides methods for managing Juno experiment data operations.
    /// </summary>
    // [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed of")]
    public class ExperimentDataManager : IExperimentDataManager
    {
        /// <summary>
        /// Initializes and instance of the <see cref="ExperimentDataManager"/> class.
        /// </summary>
        /// <param name="documentStore">Provides methods to manage experiment JSON documents/instances.</param>
        /// <param name="tableStore">Provides methods to manage experiment steps.</param>
        /// <param name="stepFactory">Provides a factory for the creation of steps from an experiment definition.</param>
        /// <param name="logger">A logger to use for capturing telemetry data.</param>
        public ExperimentDataManager(IDocumentStore<CosmosAddress> documentStore, ITableStore<CosmosTableAddress> tableStore, IExperimentStepFactory stepFactory, ILogger logger = null)
        {
            documentStore.ThrowIfNull(nameof(documentStore));
            tableStore.ThrowIfNull(nameof(tableStore));
            stepFactory.ThrowIfNull(nameof(stepFactory));

            this.DocumentStore = documentStore;
            this.TableStore = tableStore;
            this.StepFactory = stepFactory;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the experiment document store data provider.
        /// </summary>
        protected IDocumentStore<CosmosAddress> DocumentStore { get; }

        /// <summary>
        /// Gets the logger for capturing operation telemetry.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the step factory to use to create execution steps associated with an
        /// experiment definition.
        /// </summary>
        protected IExperimentStepFactory StepFactory { get; }

        /// <summary>
        /// Gets the experiment step table store data provider.
        /// </summary>
        protected ITableStore<CosmosTableAddress> TableStore { get; }

        /// <summary>
        /// Creates a step targeted for a specific agent running as part of an experiment.
        /// </summary>
        /// <param name="parentStep">The parent step.</param>
        /// <param name="agentId">The unique ID of the agent.</param>
        /// <param name="definition">The definition of the agent step.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="ExperimentStepInstance"/> instance created for the agent step.
        /// </returns>
        public async Task<IEnumerable<ExperimentStepInstance>> CreateAgentStepsAsync(ExperimentStepInstance parentStep, ExperimentComponent definition, string agentId, CancellationToken cancellationToken)
        {
            definition.ThrowIfNull(nameof(definition));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(parentStep, nameof(parentStep))
                .AddContext(definition)
                .AddContext(nameof(agentId), agentId);

            return await this.Logger.LogTelemetryAsync(EventNames.CreateAgentSteps, telemetryContext, async () =>
            {
                // To enable certain data manager (and API method) possibilities, we need to be able to associate the experiment
                // with its agents. Individual agents do not know about the experiment when they initially startup. They will need to
                // have the ability to get their experiment steps using just the ID of the agent itself. We use a mapping table to enable the
                // ability to associate an agent to an experiment. The ID of the experiment is then the partition key for the agent experiment 
                // steps table.
                ExperimentAgentTableEntity experimentAgent = null;
                CosmosTableAddress experimentAgentAddress = ExperimentAddressFactory.CreateExperimentAgentAddress(agentId, parentStep.ExperimentId);

                bool agentRegistered = false;

                try
                {
                    await this.TableStore.GetEntityAsync<ExperimentAgentTableEntity>(experimentAgentAddress, cancellationToken)
                    .ConfigureDefaults();

                    agentRegistered = true;
                }
                catch (DataStoreException exc) when (exc.Reason == DataErrorReason.DataNotFound)
                {
                    // The agent/experiment data does not exist.
                }

                if (!agentRegistered)
                {
                    // 1) Save the agent-to-experiment mapping data
                    experimentAgent = new ExperimentAgentTableEntity
                    {
                        AgentId = agentId,
                        ExperimentId = parentStep.ExperimentId,
                        Timestamp = DateTime.UtcNow
                    };

                    await this.TableStore.SaveEntityAsync(experimentAgentAddress, experimentAgent, cancellationToken)
                       .ConfigureDefaults();
                }

                // The sequence of the agent/child step will be the same as for the parent step unless
                // an override is provided.
                IEnumerable<ExperimentStepInstance> agentSteps = this.StepFactory.CreateAgentSteps(
                        definition,
                        agentId,
                        parentStep.Id,
                        parentStep.ExperimentId,
                        parentStep.Sequence);

                telemetryContext.AddContext(agentSteps, nameof(agentSteps));

                // 2) Create the agent steps
                foreach (ExperimentStepInstance agentStep in agentSteps)
                {
                    CosmosTableAddress address = ExperimentAddressFactory.CreateAgentStepAddress(parentStep.ExperimentId, agentStep.Id);
                    ExperimentStepTableEntity stepEntity = agentStep.ToTableEntity();

                    await this.TableStore.SaveEntityAsync(address, stepEntity, cancellationToken)
                        .ConfigureDefaults();

                    agentStep.SetETag(stepEntity.ETag);
                    telemetryContext.AddContext(agentStep);
                }

                return agentSteps;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Creates a new experiment instance in the data store.
        /// </summary>
        /// <param name="experiment">An experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentInstance"/> created in the backing data store.
        /// </returns>
        public async Task<ExperimentInstance> CreateExperimentAsync(Experiment experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(experiment);

            return await this.Logger.LogTelemetryAsync(EventNames.CreateExperiment, telemetryContext, async () =>
            {
                Experiment inlinedExperiment = experiment.Inlined();

                string experimentId = ExperimentAddressFactory.CreateExperimentId();
                ExperimentInstance experimentInstance = new ExperimentInstance(experimentId, inlinedExperiment);
                CosmosAddress experimentAddress = ExperimentAddressFactory.CreateExperimentAddress(experimentId);

                await this.DocumentStore.SaveDocumentAsync(experimentAddress, experimentInstance, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(experimentInstance);
                return experimentInstance;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Creates a new experiment context/metadata instance in the data store.
        /// </summary>
        /// <param name="context">The experiment context/metadata definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// The <see cref="ExperimentMetadataInstance"/> created in the backing data store.
        /// </returns>
        public async Task<ExperimentMetadataInstance> CreateExperimentContextAsync(ExperimentMetadata context, CancellationToken cancellationToken, string contextId = null)
        {
            context.ThrowIfNull(nameof(context));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(context)
                .AddContext(nameof(contextId), contextId);

            return await this.Logger.LogTelemetryAsync(EventNames.CreateExperimentContext, telemetryContext, async () =>
            {
                CosmosAddress contextAddress = ExperimentAddressFactory.CreateExperimentContextAddress(context.ExperimentId, contextId);
                ExperimentMetadataInstance contextInstance = new ExperimentMetadataInstance(contextAddress.DocumentId, context);

                await this.DocumentStore.SaveDocumentAsync(contextAddress, contextInstance, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(contextInstance);
                return contextInstance;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Creates a set of steps for the experiment in the data store.
        /// </summary>
        /// <param name="experiment">The experiment instance that describe the steps required to execute the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A set of <see cref="ExperimentStepInstance"/> instances each describing a step within the
        /// experiment (in order).
        /// </returns>
        public async Task<IEnumerable<ExperimentStepInstance>> CreateExperimentStepsAsync(ExperimentInstance experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(experiment);

            return await this.Logger.LogTelemetryAsync(EventNames.CreateExperimentSteps, telemetryContext, async () =>
            {
                List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();

                try
                {
                    steps.AddRange(this.StepFactory.CreateOrchestrationSteps(
                        experiment.Definition.Workflow,
                        experiment.Id,
                        enableDiagnostics: experiment.IsDiagnosticsEnabled()));
                }
                catch (TypeLoadException exc)
                {
                    throw new SchemaException(exc.Message, exc);
                }

                telemetryContext.AddContext(steps);

                foreach (ExperimentStepInstance step in steps)
                {
                    CosmosTableAddress address = ExperimentAddressFactory.CreateExperimentStepAddress(step.ExperimentId, step.Id);
                    ExperimentStepTableEntity stepEntity = step.ToTableEntity();

                    await this.TableStore.SaveEntityAsync(address, stepEntity, cancellationToken)
                        .ConfigureDefaults();

                    step.SetETag(stepEntity.ETag);
                }

                return steps;
            }).ConfigureDefaults();
        }

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
        public async Task<IEnumerable<ExperimentStepInstance>> CreateExperimentStepsAsync(string experimentId, int sequence, ExperimentComponent definition, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            definition.ThrowIfNull(nameof(definition));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(experimentId), experimentId)
                .AddContext(nameof(sequence), sequence)
                .AddContext(definition);

            return await this.Logger.LogTelemetryAsync(EventNames.CreateExperimentStep, telemetryContext, async () =>
            {
                List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();

                try
                {
                    steps.AddRange(this.StepFactory.CreateOrchestrationSteps(definition, experimentId, sequence));
                }
                catch (TypeLoadException exc)
                {
                    throw new SchemaException(exc.Message, exc);
                }

                foreach (ExperimentStepInstance step in steps)
                {
                    CosmosTableAddress address = ExperimentAddressFactory.CreateExperimentStepAddress(step.ExperimentId, step.Id);
                    ExperimentStepTableEntity stepEntity = step.ToTableEntity();

                    await this.TableStore.SaveEntityAsync(address, stepEntity, cancellationToken)
                        .ConfigureDefaults();

                    step.SetETag(stepEntity.ETag);
                }

                return steps;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Deletes all agent steps for the experiment from the backing data store.
        /// </summary>
        /// <param name="experimentId">Defines the unique ID of the experiment for which the agents are associated.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public async Task DeleteAgentStepsAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(experimentId), experimentId);

            await this.Logger.LogTelemetryAsync(EventNames.DeleteAgentSteps, telemetryContext, async () =>
            {
                try
                {
                    CosmosTableAddress stepsAddress = ExperimentAddressFactory.CreateAgentStepAddress(experimentId);
                    IEnumerable<ExperimentStepTableEntity> stepsToDelete = await this.TableStore.GetEntitiesAsync<ExperimentStepTableEntity>(stepsAddress, cancellationToken)
                        .ConfigureDefaults();

                    if (stepsToDelete?.Any() == true)
                    {
                        // 2) Delete all steps for each individual agent.
                        List<Task> deleteTasks = new List<Task>();
                        stepsToDelete.ToList().ForEach(step =>
                        {
                            CosmosTableAddress address = ExperimentAddressFactory.CreateAgentStepAddress(experimentId, step.Id);
                            deleteTasks.Add(this.TableStore.DeleteEntityAsync<ExperimentStepTableEntity>(address, cancellationToken));
                        });

                        await Task.WhenAll(deleteTasks).ConfigureDefaults();
                    }
                }
                catch (DataStoreException exc) when (exc.Reason == DataErrorReason.DataNotFound)
                {
                    // No need to attempt to delete the entities if no mappings exist. The entities do not
                    // exist.
                }
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Deletes the experiment from the backing data store.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment instance to delete.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public async Task DeleteExperimentAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(nameof(experimentId), experimentId);

            await this.Logger.LogTelemetryAsync(EventNames.DeleteExperiment, telemetryContext, async () =>
            {
                CosmosAddress address = ExperimentAddressFactory.CreateExperimentAddress(experimentId);
                await this.DocumentStore.DeleteDocumentAsync(address, cancellationToken)
                    .ConfigureDefaults();

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Deletes the experiment context/metdata instance from the backing data store.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment instance to delete.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        public async Task DeleteExperimentContextAsync(string experimentId, CancellationToken cancellationToken, string contextId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(nameof(experimentId), experimentId);

            await this.Logger.LogTelemetryAsync(EventNames.DeleteExperimentContext, telemetryContext, async () =>
            {
                CosmosAddress contextAddress = ExperimentAddressFactory.CreateExperimentContextAddress(experimentId, contextId);
                await this.DocumentStore.DeleteDocumentAsync(contextAddress, cancellationToken)
                    .ConfigureDefaults();

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Deletes all of the experiment steps from the data store for the 
        /// experiment defined.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment whose steps will be deleted.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        public async Task DeleteExperimentStepsAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(nameof(experimentId), experimentId);

            await this.Logger.LogTelemetryAsync(EventNames.DeleteExperimentSteps, telemetryContext, async () =>
            {
                try
                {
                    // 1) Get the set of existing steps
                    CosmosTableAddress stepsAddress = ExperimentAddressFactory.CreateExperimentStepAddress(experimentId);
                    IEnumerable<ExperimentAgentTableEntity> existingSteps = await this.TableStore.GetEntitiesAsync<ExperimentAgentTableEntity>(stepsAddress, cancellationToken)
                        .ConfigureDefaults();

                    // 2) Delete each of the steps individually
                    List<Task> deleteTasks = new List<Task>();
                    existingSteps.ToList().ForEach(step =>
                    {
                        CosmosTableAddress address = ExperimentAddressFactory.CreateExperimentStepAddress(experimentId, step.Id);
                        deleteTasks.Add(this.TableStore.DeleteEntityAsync<ExperimentStepTableEntity>(address, cancellationToken));
                    });

                    await Task.WhenAll(deleteTasks).ConfigureDefaults();
                }
                catch (DataStoreException exc) when (exc.Reason == DataErrorReason.DataNotFound)
                {
                    // If there aren't any steps for the experiment, then we are already in the
                    // desired state.
                }
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Returns the experiment from the data store for which the agent is associated.
        /// </summary>
        /// <param name="agentId">The unique ID of an experiment agent.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentInstance"/> instance for which the agent is associated.
        /// </returns>
        public async Task<ExperimentInstance> GetAgentExperimentAsync(string agentId, CancellationToken cancellationToken)
        {
            agentId.ThrowIfNull(nameof(agentId));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(nameof(agentId), agentId.ToString());

            return await this.Logger.LogTelemetryAsync(EventNames.GetAgentSteps, telemetryContext, async () =>
            {
                // To enable certain data manager (and API method) possibilities, we need to be able to associate the experiment
                // with its agents. Individual agents do not know about the experiment when they initially startup. They will need to
                // have the ability to get their experiment steps using just the ID of the agent itself. We use a mapping table to enable the
                // ability to associate an agent to an experiment. The ID of the experiment is then the partition key for the agent experiment 
                // steps table.
                CosmosTableAddress experimentAgentAddress = ExperimentAddressFactory.CreateExperimentAgentAddress(agentId);

                IEnumerable<ExperimentAgentTableEntity> agentExperiments = await this.TableStore.GetEntitiesAsync<ExperimentAgentTableEntity>(
                    experimentAgentAddress,
                    cancellationToken).ConfigureDefaults();

                ExperimentInstance experiment = null;
                if (agentExperiments?.Any() == true)
                {
                    string experimentId = agentExperiments.OrderByDescending(entity => entity.Timestamp).First().ExperimentId;
                    telemetryContext.AddContext(nameof(experimentId), experimentId);

                    experiment = await this.GetExperimentAsync(experimentId, cancellationToken).ConfigureDefaults();
                }

                return experiment;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Returns the experiment steps from the data store for the given agent.
        /// </summary>
        /// <param name="agentId">The unique ID of an experiment agent.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="filter">An optional filter expression to apply to the step search.</param>
        /// <returns>
        /// A set of one or more <see cref="ExperimentStepInstance"/> instances associated with the agent.
        /// </returns>
        public async Task<IEnumerable<ExperimentStepInstance>> GetAgentStepsAsync(string agentId, CancellationToken cancellationToken, IQueryFilter filter = null)
        {
            agentId.ThrowIfNull(nameof(agentId));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(nameof(agentId), agentId.ToString())
               .AddContext(nameof(filter), filter);

            return await this.Logger.LogTelemetryAsync(EventNames.GetAgentSteps, telemetryContext, async () =>
            {
                List<ExperimentStepInstance> matchingSteps = new List<ExperimentStepInstance>();

                // To enable certain data manager (and API method) possibilities, we need to be able to associate the experiment
                // with its agents. Individual agents do not know about the experiment when they initially startup. They will need to
                // have the ability to get their experiment steps using just the ID of the agent itself. We use a mapping table to enable the
                // ability to associate an agent to an experiment. The ID of the experiment is then the partition key for the agent experiment 
                // steps table.
                CosmosTableAddress experimentAgentAddress = ExperimentAddressFactory.CreateExperimentAgentAddress(agentId);

                IEnumerable<ExperimentAgentTableEntity> agentExperiments = await this.TableStore.GetEntitiesAsync<ExperimentAgentTableEntity>(
                    experimentAgentAddress,
                    cancellationToken).ConfigureDefaults();

                if (agentExperiments?.Any() == true)
                {
                    string latestExperimentId = agentExperiments.OrderByDescending(entity => entity.Timestamp).First().ExperimentId;

                    CosmosTableAddress address = ExperimentAddressFactory.CreateAgentStepAddress(latestExperimentId);
                    IEnumerable<ExperimentStepTableEntity> stepEntities = null;

                    QueryFilter queryFilter = new QueryFilter();

                    if (filter != null)
                    {
                        queryFilter.And(filter).Enclose();
                    }

                    queryFilter.And("AgentId", ComparisonType.Equal, agentId);

                    stepEntities = await this.TableStore.GetEntitiesAsync<ExperimentStepTableEntity>(address, queryFilter, cancellationToken)
                        .ConfigureDefaults();

                    if (stepEntities?.Any() == true)
                    {
                        matchingSteps.AddRange(stepEntities.Select(entity => entity.ToStep()).OrderBy(step => step.Sequence));
                    }
                }

                telemetryContext.AddContext(matchingSteps);

                return matchingSteps;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Returns the experiment with the ID provided from the data store.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment instance to get.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentInstance"/> for the ID provided.
        /// </returns>
        public async Task<ExperimentInstance> GetExperimentAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(nameof(experimentId), experimentId);

            return await this.Logger.LogTelemetryAsync(EventNames.GetExperiment, telemetryContext, async () =>
            {
                CosmosAddress address = ExperimentAddressFactory.CreateExperimentAddress(experimentId);
                ExperimentInstance instance = await this.DocumentStore.GetDocumentAsync<ExperimentInstance>(address, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(instance);
                return instance;
            }).ConfigureDefaults();
        }

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
        public async Task<ExperimentMetadataInstance> GetExperimentContextAsync(string experimentId, CancellationToken cancellationToken, string contextId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(nameof(experimentId), experimentId)
               .AddContext(nameof(contextId), contextId);

            return await this.Logger.LogTelemetryAsync(EventNames.GetExperimentContext, telemetryContext, async () =>
            {
                CosmosAddress address = ExperimentAddressFactory.CreateExperimentContextAddress(experimentId, contextId);
                ExperimentMetadataInstance instance = await this.DocumentStore.GetDocumentAsync<ExperimentMetadataInstance>(address, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(instance);
                return instance;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Returns an experiment step from the data store.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="stepId">The unique ID of the agent experiment step.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="ExperimentStepInstance"/> instances associated with the experiment.
        /// </returns>
        public async Task<ExperimentStepInstance> GetExperimentAgentStepAsync(string experimentId, string stepId, CancellationToken cancellationToken)
        {
            stepId.ThrowIfNullOrWhiteSpace(nameof(stepId));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(stepId), stepId);

            return await this.Logger.LogTelemetryAsync(EventNames.GetAgentSteps, telemetryContext, async () =>
            {
                CosmosTableAddress address = ExperimentAddressFactory.CreateAgentStepAddress(experimentId, stepId);
                ExperimentStepTableEntity matchingEntity = await this.TableStore.GetEntityAsync<ExperimentStepTableEntity>(address, cancellationToken)
                    .ConfigureDefaults();

                ExperimentStepInstance step = matchingEntity.ToStep();
                telemetryContext.AddContext(step);

                return step;
            }).ConfigureDefaults();
        }

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
        public async Task<IEnumerable<ExperimentStepInstance>> GetExperimentAgentStepsAsync(string experimentId, CancellationToken cancellationToken, IQueryFilter filter = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(experimentId);

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(nameof(experimentId), experimentId)
               .AddContext(nameof(filter), filter);

            return await this.Logger.LogTelemetryAsync(EventNames.GetAgentSteps, telemetryContext, async () =>
            {
                List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();
                List<Task> parallelTasks = new List<Task>();
                CosmosTableAddress address = ExperimentAddressFactory.CreateAgentStepAddress(experimentId);

                if (filter != null)
                {
                    parallelTasks.Add(this.TableStore.GetEntitiesAsync<ExperimentStepTableEntity>(address, filter, cancellationToken));
                }
                else
                {
                    parallelTasks.Add(this.TableStore.GetEntitiesAsync<ExperimentStepTableEntity>(address, cancellationToken));
                }

                await Task.WhenAll(parallelTasks).ConfigureDefaults();

                foreach (Task<IEnumerable<ExperimentStepTableEntity>> task in parallelTasks)
                {
                    steps.AddRange(task.Result.Select(result => result.ToStep()));
                }

                telemetryContext.AddContext(steps);

                return steps;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Returns the experiment step with the ID provided from the data store.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="stepId">The unique ID of the experiment step.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentStepInstance"/> for the ID provided.
        /// </returns>
        public async Task<ExperimentStepInstance> GetExperimentStepAsync(string experimentId, string stepId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            stepId.ThrowIfNullOrWhiteSpace(nameof(stepId));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(nameof(experimentId), experimentId)
               .AddContext(nameof(stepId), stepId);

            return await this.Logger.LogTelemetryAsync(EventNames.GetExperimentStep, telemetryContext, async () =>
            {
                CosmosTableAddress address = ExperimentAddressFactory.CreateExperimentStepAddress(experimentId, stepId);
                ExperimentStepTableEntity entity = await this.TableStore.GetEntityAsync<ExperimentStepTableEntity>(address, cancellationToken)
                    .ConfigureDefaults();

                ExperimentStepInstance instance = entity?.ToStep();
                telemetryContext.AddContext(instance);

                return instance;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Returns the experiment steps from the data store for the experiment defined.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="filter">An optional filter expression to apply to the step search.</param>
        /// <returns>
        /// A set of one or more <see cref="ExperimentStepInstance"/> instances associated with the experiment.
        /// </returns>
        public async Task<IEnumerable<ExperimentStepInstance>> GetExperimentStepsAsync(string experimentId, CancellationToken cancellationToken, IQueryFilter filter = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(nameof(experimentId), experimentId)
               .AddContext(nameof(filter), filter);

            return await this.Logger.LogTelemetryAsync(EventNames.GetExperimentSteps, telemetryContext, async () =>
            {
                List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();
                CosmosTableAddress address = ExperimentAddressFactory.CreateExperimentStepAddress(experimentId);
                IEnumerable<ExperimentStepTableEntity> stepEntities = null;

                if (filter == null)
                {
                    stepEntities = await this.TableStore.GetEntitiesAsync<ExperimentStepTableEntity>(address, cancellationToken)
                        .ConfigureDefaults();
                }
                else
                {
                    stepEntities = await this.TableStore.GetEntitiesAsync<ExperimentStepTableEntity>(address, filter, cancellationToken)
                        .ConfigureDefaults();
                }

                if (stepEntities?.Any() == true)
                {
                    steps.AddRange(stepEntities.Select(entity => entity.ToStep()).OrderBy(step => step.Sequence));
                }

                telemetryContext.AddContext(steps);

                return steps;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an existing agent step instance in the data store.
        /// </summary>
        /// <param name="updatedStep">The agent step definition containing the updates.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentStepInstance"/> having the updates.
        /// </returns>
        public async Task<ExperimentStepInstance> UpdateAgentStepAsync(ExperimentStepInstance updatedStep, CancellationToken cancellationToken)
        {
            updatedStep.ThrowIfNull(nameof(updatedStep));

            updatedStep.ThrowIfInvalid(
                nameof(updatedStep),
                step => !string.IsNullOrWhiteSpace(updatedStep.AgentId),
                $"The '{nameof(updatedStep.AgentId)}' property must be defined.");

            updatedStep.ThrowIfInvalid(
                nameof(updatedStep),
                step => !string.IsNullOrWhiteSpace(updatedStep.ParentStepId),
                $"The '{nameof(updatedStep.ParentStepId)}' property must be defined.");

            EventContext telemetryContext = EventContext.Persisted()
              .AddContext(updatedStep);

            return await this.Logger.LogTelemetryAsync(EventNames.UpdateAgentStep, telemetryContext, async () =>
            {
                CosmosTableAddress address = ExperimentAddressFactory.CreateAgentStepAddress(updatedStep.ExperimentId, updatedStep.Id);
                ExperimentStepTableEntity stepEntity = updatedStep.ToTableEntity();

                await this.TableStore.SaveEntityAsync(address, stepEntity, cancellationToken, replaceIfExists: true)
                    .ConfigureDefaults();

                updatedStep.SetETag(stepEntity.ETag);
                telemetryContext.AddContext("newETag", stepEntity.ETag);

                return updatedStep;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an existing experiment instance in the data store.
        /// </summary>
        /// <param name="updatedExperiment">The updated experiment instance definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentInstance"/> that defines the updates.
        /// </returns>
        public async Task<ExperimentInstance> UpdateExperimentAsync(ExperimentInstance updatedExperiment, CancellationToken cancellationToken)
        {
            updatedExperiment.ThrowIfNull(nameof(updatedExperiment));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(updatedExperiment);

            return await this.Logger.LogTelemetryAsync(EventNames.UpdateExperiment, telemetryContext, async () =>
            {
                updatedExperiment.LastModified = DateTime.UtcNow;
                CosmosAddress address = ExperimentAddressFactory.CreateExperimentAddress(updatedExperiment.Id);

                await this.DocumentStore.SaveDocumentAsync(address, updatedExperiment, cancellationToken, replaceIfExists: true)
                    .ConfigureDefaults();

                telemetryContext.AddContext("newETag", updatedExperiment.GetETag());

                return updatedExperiment;
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an existing experiment instance in the data store.
        /// </summary>
        /// <param name="updatedContext">The updated experiment context/metadata instance definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// The <see cref="ExperimentMetadataInstance"/> having the updates.
        /// </returns>
        public async Task<ExperimentMetadataInstance> UpdateExperimentContextAsync(ExperimentMetadataInstance updatedContext, CancellationToken cancellationToken, string contextId = null)
        {
            updatedContext.ThrowIfNull(nameof(updatedContext));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(updatedContext)
               .AddContext(nameof(contextId), contextId);

            return await this.Logger.LogTelemetryAsync(EventNames.UpdateExperimentContext, telemetryContext, async () =>
            {
                updatedContext.LastModified = DateTime.UtcNow;
                CosmosAddress address = ExperimentAddressFactory.CreateExperimentContextAddress(updatedContext.Definition.ExperimentId, contextId);

                await this.DocumentStore.SaveDocumentAsync(address, updatedContext, cancellationToken, replaceIfExists: true)
                    .ConfigureDefaults();

                telemetryContext.AddContext("newETag", updatedContext.GetETag());

                return updatedContext;

            }).ConfigureDefaults();
        }

        /// <summary>
        /// Updates an existing experiment step instance in the data store.
        /// </summary>
        /// <param name="updatedStep">The experiment step definition containing the updates.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="ExperimentStepInstance"/> that defines the updates.
        /// </returns>
        public async Task<ExperimentStepInstance> UpdateExperimentStepAsync(ExperimentStepInstance updatedStep, CancellationToken cancellationToken)
        {
            updatedStep.ThrowIfNull(nameof(updatedStep));

            updatedStep.ThrowIfInvalid(
                nameof(updatedStep),
                step => string.IsNullOrWhiteSpace(updatedStep.AgentId),
                $"The '{nameof(updatedStep.AgentId)}' property cannot be defined on general experiment steps.");

            updatedStep.ThrowIfInvalid(
                nameof(updatedStep),
                step => string.IsNullOrWhiteSpace(updatedStep.ParentStepId),
                $"The '{nameof(updatedStep.ParentStepId)}'  property cannot be defined on general experiment steps.");

            EventContext telemetryContext = EventContext.Persisted()
              .AddContext(updatedStep);

            return await this.Logger.LogTelemetryAsync(EventNames.UpdateExperimentStep, telemetryContext, async () =>
            {
                CosmosTableAddress address = ExperimentAddressFactory.CreateExperimentStepAddress(updatedStep.ExperimentId, updatedStep.Id);
                ExperimentStepTableEntity stepEntity = updatedStep.ToTableEntity();

                await this.TableStore.SaveEntityAsync(address, stepEntity, cancellationToken, replaceIfExists: true)
                    .ConfigureDefaults();

                updatedStep.SetETag(stepEntity.ETag);
                telemetryContext.AddContext("newETag", stepEntity.ETag);

                return updatedStep;

            }).ConfigureDefaults();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<JObject>> QueryExperimentsAsync(string query, CancellationToken cancellationToken)
        {
            query.ThrowIfNullOrWhiteSpace(nameof(query));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(query), query);

            return await this.Logger.LogTelemetryAsync(EventNames.QueryExperiments, telemetryContext, async () =>
            {
                CosmosAddress cosmosAddress = ExperimentAddressFactory.CreateExperimentAddress();
                return await this.DocumentStore.QueryDocumentsAsync<JObject>(cosmosAddress, new QueryFilter(query), cancellationToken).ConfigureDefaults();
            }).ConfigureDefaults();            
        }

        /// <summary>
        /// Provides the names of telemetry events emitted by the data manager.
        /// </summary>
        private static class EventNames
        {
            public static readonly string CreateAgentSteps = EventContext.GetEventName(nameof(ExperimentDataManager), "CreateAgentSteps");
            public static readonly string CreateExperiment = EventContext.GetEventName(nameof(ExperimentDataManager), "CreateExperiment");
            public static readonly string CreateExperimentContext = EventContext.GetEventName(nameof(ExperimentDataManager), "CreateExperimentContext");
            public static readonly string CreateExperimentStep = EventContext.GetEventName(nameof(ExperimentDataManager), "CreateExperimentStep");
            public static readonly string CreateExperimentSteps = EventContext.GetEventName(nameof(ExperimentDataManager), "CreateExperimentSteps");

            public static readonly string DeleteAgentSteps = EventContext.GetEventName(nameof(ExperimentDataManager), "DeleteAgentSteps");
            public static readonly string DeleteAgentStepsForExperiment = EventContext.GetEventName(nameof(ExperimentDataManager), "DeleteAgentStepsForExperiment");
            public static readonly string DeleteExperiment = EventContext.GetEventName(nameof(ExperimentDataManager), "DeleteExperiment");
            public static readonly string DeleteExperimentContext = EventContext.GetEventName(nameof(ExperimentDataManager), "DeleteExperimentContext");
            public static readonly string DeleteExperimentSteps = EventContext.GetEventName(nameof(ExperimentDataManager), "DeleteExperimentSteps");

            public static readonly string GetAgentStep = EventContext.GetEventName(nameof(ExperimentDataManager), "GetAgentStep");
            public static readonly string GetAgentSteps = EventContext.GetEventName(nameof(ExperimentDataManager), "GetAgentSteps");
            public static readonly string GetExperiment = EventContext.GetEventName(nameof(ExperimentDataManager), "GetExperiment");
            public static readonly string GetExperimentAgents = EventContext.GetEventName(nameof(ExperimentDataManager), "GetExperimentAgents");
            public static readonly string GetExperimentContext = EventContext.GetEventName(nameof(ExperimentDataManager), "GetExperimentContext");
            public static readonly string GetExperimentStep = EventContext.GetEventName(nameof(ExperimentDataManager), "GetExperimentStep");
            public static readonly string GetExperimentSteps = EventContext.GetEventName(nameof(ExperimentDataManager), "GetExperimentSteps");

            public static readonly string UpdateAgentStep = EventContext.GetEventName(nameof(ExperimentDataManager), "UpdateAgentStep");
            public static readonly string UpdateExperiment = EventContext.GetEventName(nameof(ExperimentDataManager), "UpdateExperiment");
            public static readonly string UpdateExperimentContext = EventContext.GetEventName(nameof(ExperimentDataManager), "UpdateExperimentContext");
            public static readonly string UpdateExperimentStep = EventContext.GetEventName(nameof(ExperimentDataManager), "UpdateExperimentStep");

            public static readonly string QueryExperiments = EventContext.GetEventName(nameof(ExperimentDataManager), "QueryExperiments");
        }
    }
}
