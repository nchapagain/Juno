namespace Juno.Contracts
{
    using System;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentCandidateTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupEnvironmentSelectionMocks();
        }

        [Test]
        public void EnvironmentCandidateIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<EnvironmentCandidate>());
        }

        [Test]
        public void EnvironmentCandidateIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<EnvironmentCandidate>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void EnvironmentCandidatePopulatesNullFieldsWithWildCard()
        {
            const string wildCard = "*"; 
            EnvironmentCandidate component = new EnvironmentCandidate(null, null, null, null, null, null, null, null);
            
            Assert.AreEqual(wildCard, component.Subscription);
            Assert.AreEqual(wildCard, component.MachinePoolName);
            Assert.AreEqual(wildCard, component.Rack);
            Assert.AreEqual(wildCard, component.NodeId);
            Assert.AreEqual(wildCard, component.Region);
            Assert.AreEqual(wildCard, component.ClusterId);
            Assert.AreEqual(wildCard, component.CpuId);
        }

        [Test]
        public void EqualityConfirmsThatWildCardAndPopulatedValueAreEqual()
        {
            EnvironmentCandidate allWildCard = new EnvironmentCandidate(null, null, null, null, null, null, null, null);
            EnvironmentCandidate populatedComponent = this.mockFixture.Create<EnvironmentCandidate>();
            Assert.AreEqual(allWildCard, populatedComponent);
        }

        [Test]
        public void EqualityConfirmsThatTwoInstancesWhomAreDifferentArentEqual()
        { 
            EnvironmentCandidate mostlyWildCard = new EnvironmentCandidate(Guid.NewGuid().ToString(), null, null, null, null, null, null, null);
            EnvironmentCandidate component = this.mockFixture.Create<EnvironmentCandidate>();
            Assert.AreNotEqual(mostlyWildCard, component);
        }

        [Test]
        public void EnvironmentCandidateImplementsEqualityCorrectly()
        {
            EnvironmentCandidate instance1 = this.mockFixture.Create<EnvironmentCandidate>();
            EnvironmentCandidate instance2 = new EnvironmentCandidate(
                instance1.Subscription, 
                instance1.ClusterId, 
                instance1.Region, 
                instance1.MachinePoolName, 
                instance1.Rack,
                Guid.NewGuid().ToString(), 
                instance1.VmSku, 
                instance1.CpuId, 
                instance1.AdditionalInfo);
            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }
    }
}
