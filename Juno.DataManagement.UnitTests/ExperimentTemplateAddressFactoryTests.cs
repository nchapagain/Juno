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
    public class ExperimentTemplateAddressFactoryTests
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
        public void ExperimentTemplateAddressFactoryCreatesTheExpectedCosmosAddressFromAnExperimentTemplateId()
        {
            string experimentTemplateId = Guid.NewGuid().ToString();
            string teamName = "someTeamName";
            CosmosAddress address = ExperimentTemplateAddressFactory.CreateExperimentTemplateAddress(teamName, experimentTemplateId);
            Assert.AreEqual("experimentDefinitions", address.DatabaseId, $"{nameof(address.DatabaseId)} property is incorrect.");
            Assert.AreEqual("experiments", address.ContainerId, $"{nameof(address.ContainerId)} property is incorrect.");
            Assert.AreEqual(teamName, address.PartitionKey, $"{nameof(address.PartitionKey)} property is incorrect.");
            Assert.AreEqual("/definition/metadata/teamName", address.PartitionKeyPath, $"{nameof(address.PartitionKeyPath)} property is incorrect.");
            Assert.AreEqual(experimentTemplateId, address.DocumentId, $"{nameof(address.DocumentId)} property is incorrect.");
        }

        [Test]
        public void ExperimentTemplateAddressFactoryThrowsIfNullTeamNameIsProvided()
        {
            string experimentTemplateId = Guid.NewGuid().ToString();
            string teamName = null;
            Assert.Throws<ArgumentException>(() => ExperimentTemplateAddressFactory.CreateExperimentTemplateAddress(teamName, experimentTemplateId));
        }

        [Test]
        public void ExperimentTemplateAddressFactoryDoesNotThrowsIfNullTemplateIdIsProvided()
        {
            string experimentTemplateId = null;
            string teamName = "Any Team Name";
            Assert.DoesNotThrow(() => ExperimentTemplateAddressFactory.CreateExperimentTemplateAddress(teamName, experimentTemplateId));
        }
    }
}
