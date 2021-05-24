namespace Juno.DataManagement
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Storage;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentFileManagerTests
    {
        private ExperimentFileManager fileManager;
        private Mock<IBlobStore<BlobAddress, BlobStream>> mockBlobStore;
        private BlobStream mockFile;

        [SetUp]
        public void SetupTest()
        {
            this.mockBlobStore = new Mock<IBlobStore<BlobAddress, BlobStream>>();
            this.fileManager = new ExperimentFileManager(this.mockBlobStore.Object, NullLogger.Instance);
            this.mockFile = new BlobStream(new MemoryStream(Encoding.UTF8.GetBytes("Any file content")), "text/plain", Encoding.UTF8);
        }

        [TearDown]
        public void CleanupTest()
        {
            this.mockFile.Content.Dispose();
        }

        [Test]
        public void ExperimentFileManagerStoresTheExpectedFile()
        {
            this.fileManager.CreateFileAsync("AnyExperiment", "AnyFile", this.mockFile, DateTime.UtcNow, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockBlobStore.Verify(store => store.SaveBlobAsync(
                It.IsAny<BlobAddress>(),
                this.mockFile,
                It.IsAny<CancellationToken>()));
        }

        [Test]
        public void ExperimentFileManagerStoresFilesInTheExpectedBlobStoreLocation()
        {
            string experimentId = Guid.NewGuid().ToString();
            string fileName = "AnyFile";
            DateTime timestamp = DateTime.UtcNow;

            this.fileManager.CreateFileAsync(experimentId, fileName, this.mockFile, timestamp, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockBlobStore.Verify(store => store.SaveBlobAsync(
                It.Is<BlobAddress>(address => address.BlobName == $"{fileName}_{timestamp.ToString("o")}".ToLowerInvariant()
                    && address.ContainerName == experimentId.ToLowerInvariant()),
                It.IsAny<BlobStream>(),
                It.IsAny<CancellationToken>()));
        }

        [Test]
        public void ExperimentFileManagerStoresFilesInTheExpectedBlobStoreLocationForSpecificAgents()
        {
            string experimentId = Guid.NewGuid().ToString();
            string fileName = "AnyFile";
            string agentType = "Host";
            string agentId = "Cluster01,Node01";
            DateTime timestamp = DateTime.UtcNow;

            this.fileManager.CreateFileAsync(experimentId, fileName, this.mockFile, timestamp, CancellationToken.None, agentType, agentId)
                .GetAwaiter().GetResult();

            this.mockBlobStore.Verify(store => store.SaveBlobAsync(
                It.Is<BlobAddress>(address => address.BlobName == $"{agentType}/{agentId}/{fileName}_{timestamp.ToString("o")}".ToLowerInvariant()
                    && address.ContainerName == experimentId.ToLowerInvariant()),
                It.IsAny<BlobStream>(),
                It.IsAny<CancellationToken>()));
        }
    }
}
