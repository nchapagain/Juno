namespace Juno.DataManagement
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using AutoFixture;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Azure.CRC.Repository.Storage;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentAddressFactoryTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockFixture.SetupAgentMocks();
        }

        [Test]
        public void ExperimentAddressFactoryCreateTheExpectedAddressForAgentHeartbeats()
        {
            var agentId = "Cluster01,Node01,VM01,Tip01";
            IFormatProvider culture = CultureInfo.CurrentCulture;
            Dictionary<DateTime, string> expectedPartitions = new Dictionary<DateTime, string>
            {
                [DateTime.Parse("2020-08-08T00:03:24.123Z", culture, DateTimeStyles.AdjustToUniversal)] = $"2020-08-08T00:00-{agentId.ToLowerInvariant()}",
                [DateTime.Parse("2020-08-08T00:13:24.123Z", culture, DateTimeStyles.AdjustToUniversal)] = $"2020-08-08T00:10-{agentId.ToLowerInvariant()}",
                [DateTime.Parse("2020-08-08T00:23:24.123Z", culture, DateTimeStyles.AdjustToUniversal)] = $"2020-08-08T00:20-{agentId.ToLowerInvariant()}",
                [DateTime.Parse("2020-08-08T00:33:24.123Z", culture, DateTimeStyles.AdjustToUniversal)] = $"2020-08-08T00:30-{agentId.ToLowerInvariant()}",
                [DateTime.Parse("2020-08-08T00:43:24.123Z", culture, DateTimeStyles.AdjustToUniversal)] = $"2020-08-08T00:40-{agentId.ToLowerInvariant()}",
                [DateTime.Parse("2020-08-08T00:53:24.123Z", culture, DateTimeStyles.AdjustToUniversal)] = $"2020-08-08T00:50-{agentId.ToLowerInvariant()}",
                [DateTime.Parse("2020-08-08T10:23:24.123Z", culture, DateTimeStyles.AdjustToUniversal)] = $"2020-08-08T10:20-{agentId.ToLowerInvariant()}",
                [DateTime.Parse("2020-08-08T15:33:24.123Z", culture, DateTimeStyles.AdjustToUniversal)] = $"2020-08-08T15:30-{agentId.ToLowerInvariant()}",
                [DateTime.Parse("2020-08-08T20:43:24.123Z", culture, DateTimeStyles.AdjustToUniversal)] = $"2020-08-08T20:40-{agentId.ToLowerInvariant()}",
                [DateTime.Parse("2020-08-08T23:53:24.123Z", culture, DateTimeStyles.AdjustToUniversal)] = $"2020-08-08T23:50-{agentId.ToLowerInvariant()}",
            };

            foreach (var entry in expectedPartitions)
            {
                var address = ExperimentAddressFactory.CreateAgentHeartbeatAddress(entry.Key, agentId);

                Assert.AreEqual("agentHeartbeats", address.TableName, $"{nameof(address.TableName)} property is incorrect.");
                Assert.AreEqual(entry.Value, address.PartitionKey, $"{nameof(address.PartitionKey)} property is incorrect.");
                Assert.IsNull(address.RowKey);
            }
        }

        [Test]
        public void ExperimentAddressFactoryCreateTheExpectedAddressForAgentHeartbeatsWhenARowKeyIsProvided()
        {
            string agentId = "Cluster01,Node01,VM01,Tip01";
            string heartbeatInstanceId = Guid.NewGuid().ToString();
            DateTime heartbeatTimestamp = DateTime.Parse("2020-08-08T10:23:24.123Z", CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal);

            var address = ExperimentAddressFactory.CreateAgentHeartbeatAddress(heartbeatTimestamp, agentId, heartbeatInstanceId);

            Assert.AreEqual("agentHeartbeats", address.TableName, $"{nameof(address.TableName)} property is incorrect.");
            Assert.AreEqual($"2020-08-08T10:20-{agentId.ToLowerInvariant()}", address.PartitionKey, $"{nameof(address.PartitionKey)} property is incorrect.");
            Assert.AreEqual(heartbeatInstanceId.ToLowerInvariant(), address.RowKey);
        }

        [Test]
        public void ExperimentAddressFactoryProducesAValidGuidAsTheIdOfAnExperiment()
        {
            string experimentId = ExperimentAddressFactory.CreateExperimentId();
            Assert.DoesNotThrow(() => Guid.Parse(experimentId));
        }

        [Test]
        public void ExperimentAddressFactoryProducesARandomIdForAGivenExperiment()
        {
            string experimentId1 = ExperimentAddressFactory.CreateExperimentId();
            string experimentId2 = ExperimentAddressFactory.CreateExperimentId();
            string experimentId3 = ExperimentAddressFactory.CreateExperimentId();

            Assert.AreNotEqual(experimentId1, experimentId2);
            Assert.AreNotEqual(experimentId2, experimentId3);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void ExperimentAddressFactoryThrowIfInvalidParametersAreProvidedWhenCreatingAnAddressForAnExperiment(string invalidValue)
        {
            Assert.Throws<ArgumentException>(() => ExperimentAddressFactory.CreateExperimentAddress(invalidValue));
        }

        [Test]
        public void ExperimentAddressFactoryCreatesTheExpectedCosmosAddressFromAnExperimentId()
        {
            string experimentId = Guid.NewGuid().ToString();
            CosmosAddress address = ExperimentAddressFactory.CreateExperimentAddress(experimentId);

            Assert.AreEqual("experiments", address.DatabaseId, $"{nameof(address.DatabaseId)} property is incorrect.");
            Assert.AreEqual("instances", address.ContainerId, $"{nameof(address.ContainerId)} property is incorrect.");
            Assert.AreEqual(experimentId.Substring(0, 4), address.PartitionKey, $"{nameof(address.PartitionKey)} property is incorrect.");
            Assert.AreEqual("/_partition", address.PartitionKeyPath, $"{nameof(address.PartitionKeyPath)} property is incorrect.");
            Assert.AreEqual(experimentId, address.DocumentId, $"{nameof(address.DocumentId)} property is incorrect.");
        }

        [Test]
        public void ExperimentAddressFactoryCreatesTheExpectedCosmosAddressForExperimentSharedContext()
        {
            string experimentId = Guid.NewGuid().ToString();
            CosmosAddress address = ExperimentAddressFactory.CreateExperimentContextAddress(experimentId);

            Assert.AreEqual("experimentState", address.DatabaseId, $"{nameof(address.DatabaseId)} property is incorrect.");
            Assert.AreEqual("instances", address.ContainerId, $"{nameof(address.ContainerId)} property is incorrect.");
            Assert.AreEqual(experimentId.Substring(0, 4), address.PartitionKey, $"{nameof(address.PartitionKey)} property is incorrect.");
            Assert.AreEqual("/_partition", address.PartitionKeyPath, $"{nameof(address.PartitionKeyPath)} property is incorrect.");
            Assert.AreEqual($"{experimentId}-context", address.DocumentId, $"{nameof(address.DocumentId)} property is incorrect.");
        }

        [Test]
        public void ExperimentAddressFactoryCreatesTheExpectedCosmosAddressForExperimentStepContext()
        {
            string experimentId = Guid.NewGuid().ToString();
            CosmosAddress address = ExperimentAddressFactory.CreateExperimentContextAddress(experimentId, "SomeStep");

            Assert.AreEqual("experimentState", address.DatabaseId, $"{nameof(address.DatabaseId)} property is incorrect.");
            Assert.AreEqual("instances", address.ContainerId, $"{nameof(address.ContainerId)} property is incorrect.");
            Assert.AreEqual(experimentId.Substring(0, 4), address.PartitionKey, $"{nameof(address.PartitionKey)} property is incorrect.");
            Assert.AreEqual("/_partition", address.PartitionKeyPath, $"{nameof(address.PartitionKeyPath)} property is incorrect.");
            Assert.AreEqual($"{experimentId}-context-SomeStep", address.DocumentId, $"{nameof(address.DocumentId)} property is incorrect.");
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void ExperimentAddressFactoryThrowsIfInvalidParametersAreProvidedWhenCreatingAnAddressForAnExperimentStep(string invalidValue)
        {
            string validExperimentId = Guid.NewGuid().ToString();
            string validStepId = Guid.NewGuid().ToString();

            Assert.Throws<ArgumentException>(() => ExperimentAddressFactory.CreateExperimentStepAddress(invalidValue));
        }

        [Test]
        public void ExperimentAddressFactoryCreatesTheExpectedCosmosTableAddressForExperiments()
        {
            // Test for case-sensitivity
            string experimentId = Guid.NewGuid().ToString().ToUpperInvariant();
            CosmosTableAddress address = ExperimentAddressFactory.CreateExperimentStepAddress(experimentId);

            Assert.AreEqual("experimentSteps", address.TableName, $"{nameof(address.TableName)} property is incorrect.");
            Assert.AreEqual(experimentId.ToLowerInvariant(), address.PartitionKey, $"{nameof(address.PartitionKey)} property is incorrect.");
            Assert.IsNull(address.RowKey, $"{nameof(address.RowKey)} property should not be defined.");
        }

        [Test]
        public void ExperimentAddressFactoryCreatesTheExpectedCosmosTableAddressForExperimentSteps()
        {
            // Test for case-sensitivity
            string experimentId = Guid.NewGuid().ToString().ToUpperInvariant();
            string stepId = Guid.NewGuid().ToString().ToUpperInvariant();
            CosmosTableAddress address = ExperimentAddressFactory.CreateExperimentStepAddress(experimentId, stepId: stepId);

            Assert.AreEqual("experimentSteps", address.TableName, $"{nameof(address.TableName)} property is incorrect.");
            Assert.AreEqual(experimentId.ToLowerInvariant(), address.PartitionKey, $"{nameof(address.PartitionKey)} property is incorrect.");
            Assert.AreEqual(stepId.ToLowerInvariant(), address.RowKey, $"{nameof(address.RowKey)} property is incorrect.");
        }

        [Test]
        public void ExperimentAddressFactoryCreatesTheExpectedCosmosTableAddressForAgentSteps()
        {
            // Test for case-sensitivity
            string experimentId = Guid.NewGuid().ToString().ToUpperInvariant();
            CosmosTableAddress address = ExperimentAddressFactory.CreateAgentStepAddress(experimentId);

            Assert.AreEqual("experimentAgentSteps", address.TableName, $"{nameof(address.TableName)} property is incorrect.");
            Assert.AreEqual(experimentId.ToLowerInvariant(), address.PartitionKey, $"{nameof(address.PartitionKey)} property is incorrect.");
            Assert.IsNull(address.RowKey);
        }

        [Test]
        public void ExperimentAddressFactoryCreatesTheExpectedCosmosTableAddressForAgentStepsById()
        {
            // Test for case-sensitivity
            string experimentId = Guid.NewGuid().ToString().ToUpperInvariant();
            string stepId = Guid.NewGuid().ToString().ToUpperInvariant();
            CosmosTableAddress address = ExperimentAddressFactory.CreateAgentStepAddress(experimentId, stepId);

            Assert.AreEqual("experimentAgentSteps", address.TableName, $"{nameof(address.TableName)} property is incorrect.");
            Assert.AreEqual(experimentId.ToLowerInvariant(), address.PartitionKey, $"{nameof(address.PartitionKey)} property is incorrect.");
            Assert.AreEqual(stepId.ToLowerInvariant(), address.RowKey, $"{nameof(address.RowKey)} property is incorrect.");
        }

        [Test]
        public void ExperimentAddressFactoryReturnsTheExpectedPartitionForAGivenExperiment()
        {
            string experimentId = Guid.NewGuid().ToString();
            string partition = ExperimentAddressFactory.GetExperimentPartition(experimentId);

            Assert.AreEqual(experimentId.Substring(0, 4), partition);
        }

        [Test]
        public void ExperimentAddressFactoryCreatesTheExpectedBlobAddressForAFile()
        {
            string experimentId = Guid.NewGuid().ToString();
            string fileName = "AnyFile";
            DateTime timestamp = DateTime.UtcNow;

            BlobAddress address = ExperimentAddressFactory.CreateExperimentFileAddress(experimentId, fileName, timestamp);

            Assert.AreEqual(
                experimentId.ToLowerInvariant(),
                address.ContainerName,
                $"{nameof(address.ContainerName)} property is incorrect.");

            Assert.AreEqual(
                $"{fileName}_{timestamp.ToString("o")}".ToLowerInvariant(),
                address.BlobName,
                $"{nameof(address.BlobName)} property is incorrect.");
        }

        [Test]
        public void ExperimentAddressFactoryCreatesTheExpectedBlobAddressForAFileWhenAgentInfoIsProvided()
        {
            string experimentId = Guid.NewGuid().ToString();
            string fileName = "AnyFile";
            string agentType = "Host";
            string agentId = "Cluster01,Node01";
            DateTime timestamp = DateTime.UtcNow;

            BlobAddress address = ExperimentAddressFactory.CreateExperimentFileAddress(experimentId, fileName, timestamp, agentType, agentId);

            Assert.AreEqual(
                experimentId.ToLowerInvariant(),
                address.ContainerName,
                $"{nameof(address.ContainerName)} property is incorrect.");

            Assert.AreEqual(
                $"{agentType}/{agentId}/{fileName}_{timestamp.ToString("o")}".ToLowerInvariant(),
                address.BlobName,
                $"{nameof(address.BlobName)} property is incorrect.");
        }

        [Test]
        public void ExperimentAddressFactoryCreatesTheExpectedQueueAddress()
        {
            // Azure Queue names MUST be lower-cased.
            string expectedQueueName = "anyqueue";
            QueueAddress address = ExperimentAddressFactory.CreateNoticeAddress(expectedQueueName.ToUpperInvariant());

            Assert.IsNotNull(address);
            Assert.AreEqual(expectedQueueName, address.QueueName);

            address = ExperimentAddressFactory.CreateNoticeAddress(expectedQueueName.ToLowerInvariant());

            Assert.IsNotNull(address);
            Assert.AreEqual(expectedQueueName, address.QueueName);
        }
    }
}
