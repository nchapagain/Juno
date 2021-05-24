namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using AutoFixture;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SsdDriveTests : RuntimeContractsTests<SsdDrive>
    {
        [Test]
        [TestCase(null)]
        public void RuntimeContractConstructorValidatesStringParameters(string invalidParam)
        {
            Assert.Throws<ArgumentException>(() => new SsdDrive(invalidParam, "string1", "string2"));
            Assert.Throws<ArgumentException>(() => new SsdDrive("string1", invalidParam, "string2"));
            Assert.Throws<ArgumentException>(() => new SsdDrive("string1", "string2", invalidParam));
        }
    }
}
