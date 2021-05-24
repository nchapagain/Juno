namespace Juno.Execution.Providers.Verification
{
    using System.Threading;
    using Juno.Contracts;
    using NUnit.Framework;

    /// <summary>
    /// Integration test to check that VerifyBmcVersion provider works when the required tools are present in C:\\BladeFX\\BladeFX
    /// </summary>
    [TestFixture]
    [Category("Integration/Live")]
    public class RegistryVerificationProviderTests
    {
        private ProviderFixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(RegistryVerificationProvider));
        }

        /// <summary>
        /// Live integration test to check that BmcFirmwareVerification provider works when the required tools are present in C:\\BladeFX\\BladeFXs
        /// </summary>
        [Test]
        public void ProviderCanVerifyRegistryWhenToolsPresent()
        {
            this.mockFixture.Component.Parameters.Add("keyName", "HKEY_LOCAL_MACHINE\\HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0");
            this.mockFixture.Component.Parameters.Add("valueName", "VendorIdentifier");
            this.mockFixture.Component.Parameters.Add("type", "System.String");
            this.mockFixture.Component.Parameters.Add("expectedValue", "GenuineIntel");

            var selectionProvider = new RegistryVerificationProvider(this.mockFixture.Services);
            selectionProvider.ConfigureServicesAsync(this.mockFixture.Context, this.mockFixture.Component).GetAwaiter().GetResult();
            var result = selectionProvider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }
    }
}
