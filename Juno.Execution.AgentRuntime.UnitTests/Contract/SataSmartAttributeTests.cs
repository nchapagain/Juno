namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using AutoFixture;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SataSmartAttributeTests : RuntimeContractsTests<SataSmartAttribute>
    {
        [Test]
        [TestCase(null)]
        public void ConstructorValidatesStringParameters(string invalidParameter)
        {
            SataSmartAttribute validComponent = this.MockFixture.Create<SataSmartAttribute>();
            Assert.Throws<ArgumentException>(() => new SataSmartAttribute(invalidParameter, validComponent.Name, validComponent.Value, validComponent.Worst, validComponent.Threshold, validComponent.FailureTime));
            Assert.Throws<ArgumentException>(() => new SataSmartAttribute(validComponent.Id, invalidParameter, validComponent.Value, validComponent.Worst, validComponent.Threshold, validComponent.FailureTime));
            Assert.Throws<ArgumentException>(() => new SataSmartAttribute(validComponent.Id, validComponent.Name, invalidParameter, validComponent.Worst, validComponent.Threshold, validComponent.FailureTime));
            Assert.Throws<ArgumentException>(() => new SataSmartAttribute(validComponent.Id, validComponent.Name, validComponent.Value, invalidParameter, validComponent.Threshold, validComponent.FailureTime));
            Assert.Throws<ArgumentException>(() => new SataSmartAttribute(validComponent.Id, validComponent.Name, validComponent.Value, validComponent.Worst, invalidParameter, validComponent.FailureTime));
            Assert.Throws<ArgumentException>(() => new SataSmartAttribute(validComponent.Id, validComponent.Name, validComponent.Value, validComponent.Worst, validComponent.Threshold, invalidParameter));
        }
    }
}
