namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentFilterTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupEnvironmentSelectionMocks();
        }

        [Test]
        public void EnvironmentFilterIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<EnvironmentFilter>());
        }

        [Test]
        public void EnvironmentFilterIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<EnvironmentFilter>(), 
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void EnvironmentFilterConstructorValidatesRequiredParameters(string invalidParameter)
        {
            EnvironmentFilter validComponent = this.mockFixture.Create<EnvironmentFilter>();

            Assert.Throws<ArgumentException>(() => new EnvironmentFilter(invalidParameter));
        }

        [Test]
        public void EnvironmentFilterCorrectlyImplementsEqualitySemantics()
        {
            EnvironmentFilter instance1 = this.mockFixture.Create<EnvironmentFilter>();
            EnvironmentFilter instance2 = new EnvironmentFilter(instance1.Type + "other", instance1.Parameters);

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }
    }
}
