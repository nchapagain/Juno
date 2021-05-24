namespace Juno.Execution.Providers.Verification
{
    using System;
    using System.Threading;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime;
    using Juno.Providers;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Integration/Live")]
    public class SsdFirmwareVerificationProviderTests
    {
        private ProviderFixture fixture;

        [OneTimeSetUp]
        public void SetupTests()
        {
            this.fixture = new ProviderFixture(typeof(SsdVerificationProvider));
        }

        [Test]
        public void ExecuteAsyncReturnsSuccessStatusCodeUnderExpectedConditions()
        {
            IExperimentProvider provider = new SsdVerificationProvider(this.fixture.Services);
            provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component).GetAwaiter().GetResult();

            // This integration test is highly dependent on the machine that is executing the provider.
            // Before running integration test run the following command in a command prompt (Not PS it doesnt like commas :/)
            // wmic diskdrive GET model, firmwarerevision
            this.fixture.Component.Parameters["targetModel"] = "WDC PC SN730 SDBQNTY-512g-1001";
            this.fixture.Component.Parameters["firmwareVersion"] = "11170101";
            
            ExecutionResult result = provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status, $"Failed with error: {result.Error}");
        }
    }
}
