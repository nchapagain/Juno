namespace Juno.DataManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Cosmos;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class AnalysisCacheManagerTests
    {
        private AnalysisCacheManager analysisCacheManager;
        private Mock<IDocumentStore<CosmosAddress>> mockCacheStore;

        [SetUp]
        public void SetupTest()
        {
            this.mockCacheStore = new Mock<IDocumentStore<CosmosAddress>>();
            this.analysisCacheManager = new AnalysisCacheManager(this.mockCacheStore.Object, NullLogger.Instance);
        }

        [Test]
        public void AnalysisCacheManagerValidatesParameters()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => new AnalysisCacheManager(null, NullLogger.Instance));
            Assert.AreEqual("analysisCacheStore", ex.ParamName);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void GetBusinessSignalsAsyncValidatesStringParameters(string invalidParameter)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => this.analysisCacheManager.GetBusinessSignalsAsync(invalidParameter, CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual("query", ex.ParamName);
        }

        [Test]
        public void AnalysisCacheManagerReturnsExpectedBusinessSignals()
        {
            List<BusinessSignal> signals = new List<BusinessSignal>()
            {
                new BusinessSignal("x", "x", "x", "y", "y", 200, 100, 100, 50, 50, 50, 50, "Red", new DateTime(2020, 07, 20)),
                new BusinessSignal("y", "y", "y", "y", "y", 100, 50, 50, 0, 100, 0, 0, "Green", new DateTime(2020, 07, 22))
            };

            this.mockCacheStore.Setup(store => store.QueryDocumentsAsync<BusinessSignal>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<QueryFilter>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((signals.AsEnumerable())));

            IEnumerable<BusinessSignal> businessSignals = this.analysisCacheManager.GetBusinessSignalsAsync(
                "some query",
                CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(businessSignals);
            Assert.AreEqual(signals.Count, businessSignals.Count());
            Assert.AreEqual(
                signals.Where(x => x.ExperimentName == "y").Count(),
                businessSignals.Where(x => x.ExperimentName == "y").Count());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void GetExperimentsProgressAsyncValidatesStringParameters(string invalidParameter)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => this.analysisCacheManager.GetExperimentsProgressAsync(invalidParameter, CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual("query", ex.ParamName);
        }

        [Test]
        public void AnalysisCacheManagerReturnsExpectedProgress()
        {
            List<ExperimentProgress> progresses = new List<ExperimentProgress>()
            {
                new ExperimentProgress("x", "x", 50),
                new ExperimentProgress("y", "y", 100)
            };

            this.mockCacheStore.Setup(store => store.QueryDocumentsAsync<ExperimentProgress>(
                It.IsAny<CosmosAddress>(),
                It.IsAny<QueryFilter>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((progresses.AsEnumerable())));

            IEnumerable<ExperimentProgress> experimentProgresses = this.analysisCacheManager.GetExperimentsProgressAsync(
                "some query",
                CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(experimentProgresses);
            Assert.AreEqual(progresses.Count, experimentProgresses.Count());
            Assert.AreEqual(
                progresses.Where(x => x.ExperimentName == "y").Select(y => y.Progress).FirstOrDefault(),
                experimentProgresses.Where(x => x.ExperimentName == "y").Select(y => y.Progress).FirstOrDefault());
        }
    }
}