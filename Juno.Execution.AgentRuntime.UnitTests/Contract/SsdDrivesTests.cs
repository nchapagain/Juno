namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Collections.Generic;
    using AutoFixture;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SsdDrivesTests : RuntimeContractsTests<SsdDrives>
    {
        [Test]
        public void RuntimeContractConstructorValidatesNonStringParameters()
        {
            Assert.Throws<ArgumentException>(() => new SsdDrives(null));
        }
    }
}
