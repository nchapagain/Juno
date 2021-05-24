namespace Juno.DataManagement
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.DataManagement.Cosmos;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Moq;
    using Moq.Language.Flow;

    /// <summary>
    /// Extension methods to help ease the setup of common mock behaviors for tests
    /// in the project.
    /// </summary>
    internal static class MockSetupExtensions
    {
        /// <summary>
        /// Setup store mock call to get <see cref="ExperimentStepTableEntity"/> instances from the Cosmos table.
        /// </summary>
        public static ISetup<ITableStore<CosmosTableAddress>, Task<IEnumerable<ExperimentStepTableEntity>>> OnGetExperimentSteps(this Mock<ITableStore<CosmosTableAddress>> tableStore)
        {
            return tableStore.Setup(store => store.GetEntitiesAsync<ExperimentStepTableEntity>(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Setup store mock call to get an <see cref="ExperimentAgentTableEntity"/> from the Cosmos table.
        /// </summary>
        public static ISetup<ITableStore<CosmosTableAddress>, Task<ExperimentAgentTableEntity>> OnGetExperimentAgent(this Mock<ITableStore<CosmosTableAddress>> tableStore)
        {
            return tableStore.Setup(store => store.GetEntityAsync<ExperimentAgentTableEntity>(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Setup store mock call to save an <see cref="ExperimentAgentTableEntity"/> to the Cosmos table.
        /// </summary>
        public static ISetup<ITableStore<CosmosTableAddress>, Task> OnSaveExperimentAgent(this Mock<ITableStore<CosmosTableAddress>> tableStore)
        {
            return tableStore.Setup(store => store.SaveEntityAsync<ExperimentAgentTableEntity>(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<ExperimentAgentTableEntity>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()));
        }

        /// <summary>
        /// Setup store mock call to save an <see cref="ExperimentStepTableEntity"/> to the Cosmos table.
        /// </summary>
        public static ISetup<ITableStore<CosmosTableAddress>, Task> OnSaveExperimentStep(this Mock<ITableStore<CosmosTableAddress>> tableStore)
        {
            return tableStore.Setup(store => store.SaveEntityAsync<ExperimentStepTableEntity>(
                It.IsAny<CosmosTableAddress>(),
                It.IsAny<ExperimentStepTableEntity>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()));
        }
    }
}
