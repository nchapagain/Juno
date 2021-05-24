namespace Juno.CRCBladeCertificationAgent.Tests
{
    using System.IO;
    using Juno.CRCTipBladeCertification.Providers;
    using Juno.Execution.AgentRuntime.Contract;
    using Juno.Execution.AgentRuntime.Windows;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SPSBladeCertificationProviderTests
    {
        private Mock<IFirmwareReader<BiosInfo>> spsVersion;
        private SPSBladeCertificationProvider provider;

        [SetUp]
        public void SetupTest()
        {
            this.spsVersion = new Mock<IFirmwareReader<BiosInfo>>();
            this.provider = new SPSBladeCertificationProvider(this.spsVersion.Object);
            this.spsVersion.Setup(s => s.Read()).Returns(new BiosInfo(string.Empty, string.Empty, "<foo>bar</foo>"));    
        }

        [Test]
        public void ProviderCollectsBaselineOnBaselineRequest()
        {
            this.provider.CollectBaseline();
            this.spsVersion.Verify(s => s.Read(), Times.Once);
        }

        [Test]
        public void ProviderWritesBeforeFileOnCollectBaseline()
        {
            File.Delete($"{nameof(SPSBladeCertificationProvider)}_before.log");
            this.provider.CollectBaseline();
            this.spsVersion.Verify(s => s.Read(), Times.Once);
            Assert.IsTrue(File.Exists($"{nameof(SPSBladeCertificationProvider)}_before.log"));
        }

        [Test]
        public void ProviderFailsCertifyIfBeforeFileNotPresent()
        {
            File.Delete($"{nameof(SPSBladeCertificationProvider)}_before.log");
            var result = this.provider.Certify();
            Assert.IsFalse(result.CertificationPassed);
        }

        [Test]
        public void ProviderFailsCertifyIfBeforeAndAfterFilesAreDifferent()
        {
            File.Delete($"{nameof(SPSBladeCertificationProvider)}_before.log");
            var mockSpsReader1 = this.spsVersion;
            mockSpsReader1.Setup(s => s.Read()).Returns(new BiosInfo(string.Empty, string.Empty, "<foo>bar</foo>"));
            var spsCertificationProvider = new SPSBladeCertificationProvider(mockSpsReader1.Object);
            var mockSpsReader2 = this.spsVersion;
            spsCertificationProvider.CollectBaseline();
            mockSpsReader2.Setup(s => s.Read()).Returns(new BiosInfo(string.Empty, string.Empty, "<foo>baz</foo>"));
            var spsCertificationProvider2 = new SPSBladeCertificationProvider(mockSpsReader2.Object);
            var result = spsCertificationProvider2.Certify();
            Assert.IsFalse(result.CertificationPassed);
            Assert.IsTrue(result.Error.Contains("baz"));
        }
    }
}