namespace Juno.CRCBladeCertificationAgent.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using Juno.CRCTipBladeCertification;
    using Juno.CRCTipBladeCertification.Contracts;
    using Juno.CRCTipBladeCertification.Providers;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Win32;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class BSCBladeCertificationProviderTests
    {
        [Test]
        public void ProviderCollectsBaselineOnBaselineRequest()
        {
            var mockBscReader = new Mock<IBscReader>();
            var bscCertificationProvider = new BscBladeCertificationProvider(mockBscReader.Object);
            bscCertificationProvider.CollectBaseline();
            mockBscReader.Verify(s => s.Read(), Times.Once);
        }

        [Test]
        public void ProviderWritesBeforeFileOnCollectBaseline()
        {
            File.Delete($"{nameof(BscBladeCertificationProvider)}_before.xml");
            var mockBscReader = new Mock<IBscReader>();
            mockBscReader.Setup(s => s.Read()).Returns("<foo>bar</foo>");
            var bscCertificationProvider = new BscBladeCertificationProvider(mockBscReader.Object);
            bscCertificationProvider.CollectBaseline();
            mockBscReader.Verify(s => s.Read(), Times.Once);
            Assert.IsTrue(File.Exists($"{nameof(BscBladeCertificationProvider)}_before.xml"));
        }

        [Test]
        public void ProviderFailsCertifyIfBeforeFileNotPresent()
        {
            File.Delete($"{nameof(BscBladeCertificationProvider)}_before.xml");
            var mockBscReader = new Mock<IBscReader>();
            mockBscReader.Setup(s => s.Read()).Returns("<foo>bar</foo>");
            var bscCertificationProvider = new BscBladeCertificationProvider(mockBscReader.Object);
            var result = bscCertificationProvider.Certify();
            Assert.IsFalse(result.CertificationPassed);
        }

        [Test]
        public void ProviderFailsCertifyIfBeforeAndAfterFilesAreDifferent()
        {
            File.Delete($"{nameof(BscBladeCertificationProvider)}_before.xml");
            var mockBscReader = new Mock<IBscReader>();
            mockBscReader.Setup(s => s.Read()).Returns("<xml><foo>bar</foo></xml>");
            var bscCertificationProvider = new BscBladeCertificationProvider(mockBscReader.Object);
            var mockBscReader2 = new Mock<IBscReader>();
            bscCertificationProvider.CollectBaseline();
            mockBscReader2.Setup(s => s.Read()).Returns("<xml><foo>baz</foo></xml>");
            var bscCertificationProvider2 = new BscBladeCertificationProvider(mockBscReader2.Object);
            var result = bscCertificationProvider2.Certify();
            Assert.IsFalse(result.CertificationPassed);
            Assert.IsTrue(result.Error.Contains("baz"));
        }
    }
}