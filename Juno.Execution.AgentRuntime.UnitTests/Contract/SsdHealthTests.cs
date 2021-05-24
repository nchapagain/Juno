namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SsdHealthTests : RuntimeContractsTests<SsdHealth>
    {
        [Test]
        [TestCase(null)]
        public void ConstructorValidatesStringParmaeters(string invalidParameter)
        {
            Assert.Throws<ArgumentException>(() => new SsdHealth(invalidParameter));
        }
    }
}
