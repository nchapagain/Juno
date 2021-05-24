namespace Juno.Scheduler.Preconditions.Manager
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text;
    using System.Threading.Tasks;
    using Juno.Contracts.Configuration;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Extensions.Configuration;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class KustoManagerTests
    {
        private KustoSettings mockSettings;

        [SetUp]
        public void SetupTests()
        {
            this.mockSettings = new KustoSettings()
            {
                ClusterDatabase = "clusterDatabase",
                ClusterUri = new Uri("https://thisisamockURI.com")
            };
        }

        [Test]
        public void KustoManagerInstanceAreSameOnDifferentInvocations()
        { 
            IKustoManager component1 = KustoManager.Instance;
            IKustoManager component2 = KustoManager.Instance;
            Assert.AreEqual(component1, component2);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void GetKustoResponseValidatesStringParameters(string invalidParameter)
        {
            IKustoManager component = KustoManager.Instance;
            string validParameter = "keyOrQuery";
            Assert.ThrowsAsync<ArgumentException>(async () => await component.GetKustoResponseAsync(validParameter, this.mockSettings, invalidParameter));
            Assert.ThrowsAsync<ArgumentException>(async () => await component.GetKustoResponseAsync(invalidParameter, this.mockSettings, validParameter));
        }

        [Test]
        public void GetKustoResponseValidatesParameters() 
        {
            IKustoManager component = KustoManager.Instance;
            KustoSettings invalidClusterDatabase = new KustoSettings()
            {
                ClusterDatabase = null,
                ClusterUri = new Uri("https://validURI.com")
            };
            KustoSettings invalidClusterUri = new KustoSettings()
            {
                ClusterDatabase = "clusterDatabase",
                ClusterUri = null
            };

            Assert.ThrowsAsync<ArgumentException>(async () => await component.GetKustoResponseAsync("cachekey", invalidClusterUri, "query"));
            Assert.ThrowsAsync<ArgumentException>(async () => await component.GetKustoResponseAsync("cachekey", invalidClusterDatabase, "query"));
        }

        [Test]
        public void SeUpWithConfigurationValidatesParameters()
        {
            IKustoManager component = KustoManager.Instance;
            Assert.Throws<ArgumentException>(() => component.SetUp((IConfiguration)null));
        }

        [Test]
        public void SetUpWithIssuerValidatesParamaters()
        {
            IKustoManager component = KustoManager.Instance;
            Assert.Throws<ArgumentException>(() => component.SetUp((IKustoQueryIssuer)null));
        }

        [Test]
        public async Task GetKustoResponseReturnsExpectedDataTable()
        {
            Mock<IKustoQueryIssuer> issuer = new Mock<IKustoQueryIssuer>();
            DataTable expectedResult = new DataTable();
            issuer.Setup(mock => mock.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(expectedResult));

            IKustoManager component = KustoManager.Instance;
            component.SetUp(issuer.Object);

            DataTable actualResult = await component.GetKustoResponseAsync("cacheKey", this.mockSettings, "query").ConfigureAwait(false);

            Assert.AreEqual(expectedResult, actualResult);
        }
    }
}
