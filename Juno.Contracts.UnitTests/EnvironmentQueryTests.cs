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
    public class EnvironmentQueryTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupEnvironmentSelectionMocks();
        }

        [Test]
        public void EnvironmentQueryIsJsonSerializable()
        {
            EnvironmentQuery component = this.mockFixture.Create<EnvironmentQuery>();

            SerializationAssert.IsJsonSerializable(component);
        }

        [Test]
        public void EnvironmentQueryIsJsonSerializableusingExpectedSerializerSettings()
        {
            EnvironmentQuery component = this.mockFixture.Create<EnvironmentQuery>();

            SerializationAssert.IsJsonSerializable(component, ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void EnvironmentQueryValidatesStringParameters(string invalidParam)
        {
            EnvironmentQuery component = this.mockFixture.Create<EnvironmentQuery>();
            Assert.Throws<ArgumentException>(() => new EnvironmentQuery(invalidParam, 6, component.Filters, parameters: component.Parameters));
        }

        [Test]
        public void EnvironmentQueryValidatesParameters()
        {
            EnvironmentQuery component = this.mockFixture.Create<EnvironmentQuery>();
            Assert.Throws<ArgumentNullException>(() => new EnvironmentQuery(component.Name, 6, null, parameters: component.Parameters));
        }

        [Test]
        public void EnvrionmentQueryCorrectlyImplementsEqualitySemantics()
        {
            EnvironmentQuery instance1 = this.mockFixture.Create<EnvironmentQuery>();
            EnvironmentQuery instance2 = new EnvironmentQuery("differentname", 6, instance1.Filters, parameters: instance1.Parameters);

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }
    }
}
