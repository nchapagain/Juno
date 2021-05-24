namespace Juno.CRCBladeCertificationAgent.Tests
{
    using System.Collections.Generic;
    using Juno.CRCTipBladeCertification;
    using Juno.CRCTipBladeCertification.Contracts;
    using Juno.CRCTipBladeCertification.Providers;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Win32;
    using Moq;
    using NUnit.Framework;
    using CertificationManager = Juno.CRCTipBladeCertification.Providers.CertificationManager;

    [TestFixture]
    [Category("Unit")]
    public class CertificationManagerTests
    {
        [Test]
        public void ManagerCollectsBaselineFromExplicitProviders()
        {
            var mockProvider = new Mock<IBladeCertificationProvider>();
            var certMgr = new CertificationManager(new List<IBladeCertificationProvider>() 
            {
                mockProvider.Object 
            });
            certMgr.CollectBaseline();
            mockProvider.Verify(s => s.CollectBaseline(), Times.Once);
        }

        [Test]
        public void ManagerReportsFailureOnComparisonFailure()
        {
            var mockProvider = new Mock<IBladeCertificationProvider>();
            mockProvider.Setup(s => s.Certify()).Returns(new CertificationResult()
            {
                CertificationPassed = false,
                Error = "mock provider has diff baseline",
                ProviderName = "mock provider"
            });
            var certMgr = new CertificationManager(new List<IBladeCertificationProvider>()
            {
                mockProvider.Object
            });
            bool passed = certMgr.CompareWithBaseline(out var errors);
            Assert.IsFalse(passed);
            Assert.IsTrue(errors.Contains("mock provider has diff baseline"));
            mockProvider.Verify(s => s.Certify(), Times.Once);
            mockProvider.Verify(s => s.CollectBaseline(), Times.Never);
        }

        [Test]
        public void ManagerReportsSuccessForComparisonSuccess()
        {
            var mockProvider = new Mock<IBladeCertificationProvider>();
            mockProvider.Setup(s => s.Certify()).Returns(new CertificationResult()
            {
                CertificationPassed = true,
                ProviderName = "mock provider"
            });
            var certMgr = new CertificationManager(new List<IBladeCertificationProvider>()
            {
                mockProvider.Object
            });
            bool passed = certMgr.CompareWithBaseline(out var errors);
            Assert.IsTrue(passed);
            mockProvider.Verify(s => s.Certify(), Times.Once);
            mockProvider.Verify(s => s.CollectBaseline(), Times.Never);
        }

        [Test]
        public void ManagerHandlesFailuresFromMultipleProviders()
        {
            var mockProvider1 = new Mock<IBladeCertificationProvider>();
            mockProvider1.Setup(s => s.Certify()).Returns(new CertificationResult()
            {
                CertificationPassed = true,
                ProviderName = "mock provider1"
            });
            var mockProvider2 = new Mock<IBladeCertificationProvider>();
            mockProvider2.Setup(s => s.Certify()).Returns(new CertificationResult()
            {
                CertificationPassed = false,
                Error = "provider2 doesn't like it",
                ProviderName = "mock provider2"
            });
            var certMgr = new CertificationManager(new List<IBladeCertificationProvider>()
            {
                mockProvider1.Object, mockProvider2.Object
            });
            bool passed = certMgr.CompareWithBaseline(out var errors);
            Assert.IsFalse(passed);
            Assert.IsTrue(errors.Contains("provider2"));
            mockProvider1.Verify(s => s.Certify(), Times.Once);
            mockProvider1.Verify(s => s.CollectBaseline(), Times.Never);
            mockProvider2.Verify(s => s.Certify(), Times.Once);
            mockProvider2.Verify(s => s.CollectBaseline(), Times.Never);
        }
    }
}