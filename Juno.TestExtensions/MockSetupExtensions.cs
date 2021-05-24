namespace Juno
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.NuGetIntegration;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Moq;
    using Moq.Language;
    using Moq.Language.Flow;
    using NuGet.Versioning;

    /// <summary>
    /// Extension methods to ease the setup of common mock behaviors.
    /// </summary>
    public static class MockSetupExtensions
    {
        /// <summary>
        /// Setup default behavior to get experiment steps from the backing store.
        /// </summary>
        public static ISetup<IProviderDataClient, Task<IEnumerable<ExperimentStepInstance>>> OnGetExperimentSteps(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient.Setup(client => client.GetExperimentStepsAsync(
                It.IsAny<ExperimentInstance>(),
                It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Setup default behavior to get a secret from a Key Vault.
        /// </summary>
        public static ISetup<IAzureKeyVault, Task<SecureString>> OnGetSecret(this Mock<IAzureKeyVault> keyVaultStore)
        {
            keyVaultStore.ThrowIfNull(nameof(keyVaultStore));

            return keyVaultStore.Setup(store => store.GetSecretAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Setup default behavior to get state from the backing store.
        /// </summary>
        public static ISetup<IProviderDataClient, Task<T>> OnGetState<T>(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient.Setup(client => client.GetOrCreateStateAsync<T>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()));
        }

        /// <summary>
        /// Setup default behavior to save state in the backing store.
        /// </summary>
        public static ISetup<IProviderDataClient, Task> OnSaveState<T>(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient.Setup(client => client.SaveStateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<T>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()));
        }

        /// <summary>
        /// Setup default behavior to get subscription logs from activity logs
        /// </summary>
        public static ISetup<IArmClient, Task<HttpResponseMessage>> OnGetSubscriptionLogs(this Mock<IArmClient> armClient)
        {
            armClient.ThrowIfNull(nameof(armClient));

            return armClient.Setup(m =>
                m.GetSubscriptionActivityLogsAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<IEnumerable<string>>()));
        }

        /// <summary>
        /// Setup default behavior to get the entities provisioned from the backing store.
        /// </summary>
        public static ISetup<IProviderDataClient, Task<IEnumerable<EnvironmentEntity>>> OnGetEntitiesProvisioned(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient
                .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    It.IsAny<string>(),
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()));
        }

        /// <summary>
        /// Setup default behavior to get the entities provisioned from the backing store in sequence.
        /// </summary>
        public static ISetupSequentialResult<Task<IEnumerable<EnvironmentEntity>>> OnGetEntitiesProvisionedSequence(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient
                .SetupSequence(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    It.IsAny<string>(),
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()));
        }

        /// <summary>
        /// Setup default behavior to get the entity pool from the backing store.
        /// </summary>
        public static ISetup<IProviderDataClient, Task<IEnumerable<EnvironmentEntity>>> OnGetEntityPool(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient
                .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    It.IsAny<string>(),
                    ContractExtension.EntityPool,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()));
        }

        /// <summary>
        /// Setup default behavior to get the entity pool from the backing store in sequence.
        /// </summary>
        public static ISetupSequentialResult<Task<IEnumerable<EnvironmentEntity>>> OnGetEntityPoolSequence(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient
                .SetupSequence(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    It.IsAny<string>(),
                    ContractExtension.EntityPool,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()));
        }

        /// <summary>
        /// Setup the default behavior for removing entities from the entities provisioned in
        /// the backing store.
        /// </summary>
        public static ISetup<IProviderDataClient, Task> OnRemoveEntitiesProvisioned(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient.Setup(c => c.RemoveStateItemsAsync<EnvironmentEntity>(
                    It.IsAny<string>(),
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<IEnumerable<EnvironmentEntity>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()));
        }

        /// <summary>
        /// Setup the default behavior for saving the entities provisioned to the backing store.
        /// </summary>
        public static ISetup<IProviderDataClient, Task> OnSaveEntitiesProvisioned(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient
                .Setup(client => client.SaveStateAsync<IEnumerable<EnvironmentEntity>>(
                    It.IsAny<string>(),
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<IEnumerable<EnvironmentEntity>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()));
        }

        /// <summary>
        /// Setup the default behavior for saving the entity pool to the backing store.
        /// </summary>
        public static ISetup<IProviderDataClient, Task> OnSaveEntityPool(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient
                .Setup(client => client.SaveStateAsync<IEnumerable<EnvironmentEntity>>(
                    It.IsAny<string>(),
                    ContractExtension.EntityPool,
                    It.IsAny<IEnumerable<EnvironmentEntity>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()));
        }

        /// <summary>
        /// Setup the default behavior for updating the entities provisioned in the backing store.
        /// </summary>
        public static ISetup<IProviderDataClient, Task> OnUpdateEntitiesProvisioned(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient.Setup(c => c.UpdateStateItemsAsync<EnvironmentEntity>(
                    It.IsAny<string>(),
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<IEnumerable<EnvironmentEntity>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()));
        }

        /// <summary>
        /// Setup default behavior for installing a NuGet package.
        /// </summary>
        public static ISetup<INuGetPackageInstaller, Task<NuGetVersion>> OnInstallPackage(this Mock<INuGetPackageInstaller> packageInstaller)
        {
            packageInstaller.ThrowIfNull(nameof(packageInstaller));

            return packageInstaller.Setup(installer => installer.InstallPackageAsync(
                It.IsAny<NuGetPackageInfo>(),
                It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Gets the diagnostics request items from backend cosmos store.
        /// </summary>
        public static ISetup<IProviderDataClient, Task<IEnumerable<DiagnosticsRequest>>> OnGetDiagnosticsRequestsAsync(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient
                .Setup(client => client.GetOrCreateStateAsync<IEnumerable<DiagnosticsRequest>>(
                    It.IsAny<string>(),
                    ContractExtension.DiagnosticsRequests,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()));
        }

        /// <summary>
        /// Updates the diagnostics request items in backend cosmos store.
        /// </summary>
        public static ISetup<IProviderDataClient, Task> OnAddDiagnosticsRequestAsync(this Mock<IProviderDataClient> dataClient)
        {
            dataClient.ThrowIfNull(nameof(dataClient));

            return dataClient.Setup(c => c.UpdateStateItemsAsync<DiagnosticsRequest>(
                    It.IsAny<string>(),
                    ContractExtension.DiagnosticsRequests,
                    It.IsAny<IEnumerable<DiagnosticsRequest>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()));
        }
    }
}