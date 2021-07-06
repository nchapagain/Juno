namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentSummaryExtensionsTests
    {
        private static IEnumerable<BusinessSignal> businessSignals = new List<BusinessSignal>()
        {
            new BusinessSignal("X", "X", "semaphoreX1", "semaphoreX1", "semaphoreX1", 100, 50, 50, 25, 25, 25, 25, "Red", new DateTime(2020, 07, 21)),
            new BusinessSignal("X", "X", "semaphoreX2", "semaphoreX2", "semaphoreX2", 200, 100, 100, 50, 50, 50, 50, "Red", new DateTime(2020, 07, 20)),

            new BusinessSignal("Y", "Y", "semaphoreY1", "semaphoreY1", "semaphoreY1", 100, 50, 50, 0, 100, 0, 0, "Green", new DateTime(2020, 07, 22)),

            new BusinessSignal("Z", "Z", "semaphoreZ1", "semaphoreZ1", "semaphoreZ1", 100, 50, 50, 0, 100, 0, 0, "Grey", new DateTime(2020, 07, 23)),
            new BusinessSignal("Z", "Z", "semaphoreZ2", "semaphoreZ2", "semaphoreZ2", 100, 50, 50, 0, 100, 0, 0, "Grey", new DateTime(2020, 07, 22))
        };

        private static IEnumerable<ExperimentProgress> progresses = new List<ExperimentProgress>()
        {
            new ExperimentProgress("X", "X", 50),
            new ExperimentProgress("Z", "Z", 100),
        };

        [Test]
        public void DeriveExperimentSummaryValidatesParameters()
        {
            Assert.Throws<ArgumentException>(() => ExperimentSummaryExtensions.DeriveExperimentSummary(
                null, ExperimentSummaryExtensionsTests.progresses));

            Assert.Throws<ArgumentException>(() => ExperimentSummaryExtensions.DeriveExperimentSummary(
                new List<BusinessSignal>(), ExperimentSummaryExtensionsTests.progresses));
        }

        [Test]
        public void DeriveExperimentSummaryMaintainsExpectedUniqueness()
        {
            int expectedSummariesCount = ExperimentSummaryExtensionsTests.businessSignals
                    // Subsequent releases will have TenantId added to the identifiers.
                    .GroupBy(businessSignal => new { businessSignal.ExperimentName, businessSignal.Revision })
                    .Select(disintctExperiment => disintctExperiment.Key)
                    .Count();

            var summaries = ExperimentSummaryExtensions.DeriveExperimentSummary(
                ExperimentSummaryExtensionsTests.businessSignals, ExperimentSummaryExtensionsTests.progresses);

            Assert.AreEqual(
                expectedSummariesCount,
                summaries.Count());
        }

        [Test]
        public void DeriveExperimentSummaryReturnsExpectedStartDateForEachExperiment()
        {
            string expectedStartDateX = ExperimentSummaryExtensionsTests.GetBusinessSignalsForExperiment("X", "X").
                Min(signal => signal.ExperimentDateUtc).ToShortDateString();

            string expectedStartDateY = ExperimentSummaryExtensionsTests.GetBusinessSignalsForExperiment("Y", "Y").
                Min(signal => signal.ExperimentDateUtc).ToShortDateString();

            string expectedStartDateZ = ExperimentSummaryExtensionsTests.GetBusinessSignalsForExperiment("Z", "Z").
                Min(signal => signal.ExperimentDateUtc).ToShortDateString();

            var summaries = ExperimentSummaryExtensions.DeriveExperimentSummary(
                ExperimentSummaryExtensionsTests.businessSignals, ExperimentSummaryExtensionsTests.progresses);

            string actualStartDateX = ExperimentSummaryExtensionsTests.GetSummaryForExperiment(summaries, "X", "X").ExperimentDateUtc;
            string actualStartDateY = ExperimentSummaryExtensionsTests.GetSummaryForExperiment(summaries, "Y", "Y").ExperimentDateUtc;
            string actualStartDateZ = ExperimentSummaryExtensionsTests.GetSummaryForExperiment(summaries, "Z", "Z").ExperimentDateUtc;

            Assert.AreEqual(expectedStartDateX, actualStartDateX);
            Assert.AreEqual(expectedStartDateY, actualStartDateY);
            Assert.AreEqual(expectedStartDateZ, actualStartDateZ);
        }

        [Test]
        public void DeriveExperimentSummaryReturnsExpectedProgressWhenAvailable()
        {
            int expectedProgresX = ExperimentSummaryExtensionsTests.GetProgressForExperiment("X", "X").FirstOrDefault().Progress;
            int expectedProgresZ = ExperimentSummaryExtensionsTests.GetProgressForExperiment("Z", "Z").FirstOrDefault().Progress;

            var summaries = ExperimentSummaryExtensions.DeriveExperimentSummary(
                ExperimentSummaryExtensionsTests.businessSignals, ExperimentSummaryExtensionsTests.progresses);

            int actualProgresX = ExperimentSummaryExtensionsTests.GetSummaryForExperiment(summaries, "X", "X").Progress;
            int actualProgresZ = ExperimentSummaryExtensionsTests.GetSummaryForExperiment(summaries, "Z", "Z").Progress;

            Assert.AreEqual(expectedProgresX, actualProgresX);
            Assert.AreEqual(expectedProgresZ, actualProgresZ);
        }

        [Test]
        public void DeriveExperimentSummaryReturnsExpectedProgressWhenNotAvailable()
        {
            var summaries = ExperimentSummaryExtensions.DeriveExperimentSummary(
                ExperimentSummaryExtensionsTests.businessSignals, ExperimentSummaryExtensionsTests.progresses);

            int actualProgresY = ExperimentSummaryExtensionsTests.GetSummaryForExperiment(summaries, "Y", "Y").Progress;

            Assert.AreEqual(0, actualProgresY);
        }

        [Test]
        public void DeriveExperimentSummaryReturnsExpectedSemaphores()
        {
            IEnumerable<BusinessSignalKPI> expectedBusinessSignalKPIsX = ExperimentSummaryExtensionsTests.GetBusinessSignalsForExperiment("X", "X").
                Select(signal => new BusinessSignalKPI(signal));

            IEnumerable<BusinessSignalKPI> expectedBusinessSignalKPIsY = ExperimentSummaryExtensionsTests.GetBusinessSignalsForExperiment("Y", "Y").
                Select(signal => new BusinessSignalKPI(signal));

            IEnumerable<BusinessSignalKPI> expectedBusinessSignalKPIsZ = ExperimentSummaryExtensionsTests.GetBusinessSignalsForExperiment("Z", "Z").
                Select(signal => new BusinessSignalKPI(signal));

            var summaries = ExperimentSummaryExtensions.DeriveExperimentSummary(
                ExperimentSummaryExtensionsTests.businessSignals, ExperimentSummaryExtensionsTests.progresses);

            var actualBusinessSignalKPIsX = ExperimentSummaryExtensionsTests.GetSummaryForExperiment(summaries, "X", "X").BusinessSignalKPIs;
            var actualBusinessSignalKPIsY = ExperimentSummaryExtensionsTests.GetSummaryForExperiment(summaries, "Y", "Y").BusinessSignalKPIs;
            var actualBusinessSignalKPIsZ = ExperimentSummaryExtensionsTests.GetSummaryForExperiment(summaries, "Z", "Z").BusinessSignalKPIs;

            Assert.IsTrue(expectedBusinessSignalKPIsX.ElementAt(0) == actualBusinessSignalKPIsX.ElementAt(0));
            Assert.IsTrue(expectedBusinessSignalKPIsX.ElementAt(1) == actualBusinessSignalKPIsX.ElementAt(1));
            Assert.IsTrue(expectedBusinessSignalKPIsY.ElementAt(0) == actualBusinessSignalKPIsY.ElementAt(0));
            Assert.IsTrue(expectedBusinessSignalKPIsZ.ElementAt(0) == actualBusinessSignalKPIsZ.ElementAt(0));
            Assert.IsTrue(expectedBusinessSignalKPIsZ.ElementAt(1) == actualBusinessSignalKPIsZ.ElementAt(1));
        }

        [Test]
        public void DeriveExperimentSummaryReturnsLatestExperimentsOnTop()
        {
            string expectedStartDateOnTop = new[]
            {
                ExperimentSummaryExtensionsTests.GetBusinessSignalsForExperiment("X", "X").Min(signal => signal.ExperimentDateUtc),
                ExperimentSummaryExtensionsTests.GetBusinessSignalsForExperiment("Y", "Y").Min(signal => signal.ExperimentDateUtc),
                ExperimentSummaryExtensionsTests.GetBusinessSignalsForExperiment("Z", "Z").Min(signal => signal.ExperimentDateUtc)
            }.Max().ToShortDateString();

            var summaries = ExperimentSummaryExtensions.DeriveExperimentSummary(
                ExperimentSummaryExtensionsTests.businessSignals, ExperimentSummaryExtensionsTests.progresses);

            string actualStartDateOnTop = summaries.First().ExperimentDateUtc;

            Assert.AreEqual(expectedStartDateOnTop, actualStartDateOnTop);
        }

        private static IEnumerable<BusinessSignal> GetBusinessSignalsForExperiment(string experimentName, string revision)
        {
            return ExperimentSummaryExtensionsTests.businessSignals.Where(
                businessSignal => businessSignal.ExperimentName == experimentName && businessSignal.Revision == revision);
        }

        private static IEnumerable<ExperimentProgress> GetProgressForExperiment(string experimentName, string revision)
        {
            return ExperimentSummaryExtensionsTests.progresses.Where(
                businessSignal => businessSignal.ExperimentName == experimentName && businessSignal.Revision == revision);
        }

        private static ExperimentSummary GetSummaryForExperiment(IEnumerable<ExperimentSummary> experimentSummaries, string experimentName, string revision)
        {
            return experimentSummaries.Where(summary => summary.ExperimentName == experimentName && summary.Revision == revision).FirstOrDefault();
        }
    }
}