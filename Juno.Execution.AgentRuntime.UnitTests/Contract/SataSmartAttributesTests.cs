namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SataSmartAttributesTests : RuntimeContractsTests<SataSmartAttributes>
    {
        [Test]
        public void ConstructorValidatesNonStringParameters()
        {
            Assert.Throws<ArgumentException>(() => new SataSmartAttributes(null));
        }
    }
}
