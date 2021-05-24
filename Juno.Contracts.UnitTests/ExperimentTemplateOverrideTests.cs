namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using AutoFixture;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NuGet.Protocol;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentTemplateOverrideTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetUpTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockFixture.SetupExperimentMocks();
        }

        [Test]
        public void TemplateOverrideReturnsExpectedValue()
        {
            ScheduleAction component = this.mockFixture.Create<ScheduleAction>();

            var parameter = new TemplateOverride(component.Parameters).Parameters;

            foreach (var pram in component.Parameters)
            {
                Assert.AreEqual(parameter[pram.Key], pram.Value);
            }

            Assert.AreEqual(component.Parameters.Count, parameter.Count);
        }

        [Test]
        public void TemplateOverrideReturnsExpectedValueForEmptyDictionary()
        {
            var parameter = new TemplateOverride(new Dictionary<string, IConvertible>());
            
            Assert.AreEqual(parameter.Parameters.Count, 0);
        }

        [Test]
        public void PreconditionIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<TemplateOverride>());
        }

        [Test]
        public void PreconditionIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<TemplateOverride>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }
    }
}
