namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class AutoTriageQueryFactoryTests
    {
        private const string NodeId = "663a8b56-981a-4f0d-8c61-8fa6d8f8c753";
        private const string TipSessionId = "663a8b56-981a-4f0d-8c61-8fa6d8f8c753";
        private const string RGName = "rg-77fa1ec8543";
        private static readonly DateTime TimeRangeBegin = DateTime.Parse("2020-06-21 03:00:00Z").ToUniversalTime();
        private static readonly DateTime TimeRangeEnd = DateTime.Parse("2020-06-21 04:00:00Z").ToUniversalTime();

        private ExperimentFixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ExperimentFixture();
            this.mockFixture.SetupExperimentMocks();
        }

        [Test]
        [TestCaseSource(typeof(TimeRangeData), nameof(TimeRangeData.InvalidDateRanges))]
        public void FactoryValidatesRequiredParametersWhenCreatingAMicrocodeUpdateEventQuery(string nodeId, DateTime timeRangeBegin, DateTime timeRangeEnd)
        {
            Assert.Throws<ArgumentException>(() => AutoTriageQueryFactory.GetMicrocodeUpdateEventQuery(nodeId, timeRangeBegin, timeRangeEnd));
        }

        [Test]
        public void FactoryContructsTheExpectedMicrocodeUpdateEventQueryWhenValidParametersAreProvided()
        {
            string query = AutoTriageQueryFactory.GetMicrocodeUpdateEventQuery(
                AutoTriageQueryFactoryTests.NodeId,
                AutoTriageQueryFactoryTests.TimeRangeBegin,
                AutoTriageQueryFactoryTests.TimeRangeEnd);

            Assert.That(query.Contains("dynamic('663a8b56-981a-4f0d-8c61-8fa6d8f8c753')"), Is.True);
            Assert.That(query.Contains("dynamic('2020-06-21T03:00:00')"), Is.True);
            Assert.That(query.Contains("dynamic('2020-06-21T04:00:00')"), Is.True);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void FactoryValidatesRequiredParametersWhenCreatingATipSessionStatusEventsQuery(string tipSessionId)
        {
            Assert.Throws<ArgumentException>(() => AutoTriageQueryFactory.GetTipSessionStatusEventsQuery(tipSessionId));
        }

        [Test]
        public void FactoryConstructsTheExpectedTipSessionStatusEventsQueryWhenValidParametersAreProvided()
        {
            string query = AutoTriageQueryFactory.GetTipSessionStatusEventsQuery(AutoTriageQueryFactoryTests.TipSessionId);

            Assert.That(query.Contains("dynamic('663a8b56-981a-4f0d-8c61-8fa6d8f8c753')"), Is.True);
        }

        [Test]
        [TestCaseSource(typeof(TimeRangeData), nameof(TimeRangeData.InvalidDateRanges))]
        public void FactoryValidatesRequiredParametersWhenCreatingAnArmDeploymentEventQuery(string rGName, DateTime timeRangeBegin, DateTime timeRangeEnd)
        {
            Assert.Throws<ArgumentException>(() => AutoTriageQueryFactory.GetArmDeploymentOperationQuery(rGName, timeRangeBegin, timeRangeEnd));
        }

        [Test]
        public void FactoryConstructsTheExpectedArmDeploymentEventQueryWhenValidParametersAreProvided()
        {
            string query = AutoTriageQueryFactory.GetArmDeploymentOperationQuery(
                AutoTriageQueryFactoryTests.RGName,
                AutoTriageQueryFactoryTests.TimeRangeBegin,
                AutoTriageQueryFactoryTests.TimeRangeEnd);

            Assert.That(query.Contains("dynamic('rg-77fa1ec8543')"), Is.True);
            Assert.That(query.Contains("dynamic('2020-06-21T03:00:00')"), Is.True);
            Assert.That(query.Contains("dynamic('2020-06-21T04:00:00')"), Is.True);
        }

        [Test]
        [TestCaseSource(typeof(TimeRangeData), nameof(TimeRangeData.InvalidDateRanges))]
        public void FactoryValidatesRequiredParametersWhenCreatingAnArmAzCrpEventQuery(string rGName, DateTime timeRangeBegin, DateTime timeRangeEnd)
        {
            Assert.Throws<ArgumentException>(() => AutoTriageQueryFactory.GetAzCrpVmApiQosEventQuery(rGName, timeRangeBegin, timeRangeEnd));
        }

        [Test]
        public void FactoryConstructsTheExpectedArmAzCrpEventQueryWhenValidParametersAreProvided()
        {
            string query = AutoTriageQueryFactory.GetAzCrpVmApiQosEventQuery(
                AutoTriageQueryFactoryTests.RGName,
                AutoTriageQueryFactoryTests.TimeRangeBegin,
                AutoTriageQueryFactoryTests.TimeRangeEnd);

            Assert.That(query.Contains("dynamic('rg-77fa1ec8543')"), Is.True);
            Assert.That(query.Contains("dynamic('2020-06-21T03:00:00')"), Is.True);
            Assert.That(query.Contains("dynamic('2020-06-21T04:00:00')"), Is.True);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void FactoryValidatesRequiredParametersWhenCreatingAnArmAzureCMEventQuery(string tipSessionId)
        {
            Assert.Throws<ArgumentException>(() => AutoTriageQueryFactory.GetLogNodeSnapshotQuery(tipSessionId));
        }

        [Test]
        public void FactoryConstructsTheExpectedArmAzureCMEventQueryWhenValidParametersAreProvided()
        {
            string query = AutoTriageQueryFactory.GetLogNodeSnapshotQuery(AutoTriageQueryFactoryTests.TipSessionId);

            Assert.That(query.Contains("dynamic('663a8b56-981a-4f0d-8c61-8fa6d8f8c753')"), Is.True);
        }

        internal class TimeRangeData
        {
            internal static IEnumerable<TestCaseData> InvalidDateRanges
            {
                get
                {
                    // Node id is null.
                    yield return new TestCaseData(null, DateTime.UtcNow, DateTime.UtcNow);
                    // Node id is empty.
                    yield return new TestCaseData(string.Empty, DateTime.UtcNow, DateTime.UtcNow);
                    // Node id is with blank spaces.
                    yield return new TestCaseData("  ", DateTime.UtcNow, DateTime.UtcNow);
                    // Time range begin is MinValue
                    yield return new TestCaseData(AutoTriageQueryFactoryTests.NodeId, DateTime.MinValue, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                    // Time range begin is MaxValue
                    yield return new TestCaseData(AutoTriageQueryFactoryTests.NodeId, DateTime.MaxValue, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                    // Time range end is MinValue
                    yield return new TestCaseData(AutoTriageQueryFactoryTests.NodeId, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.MinValue);
                    // Time range end is MaxValue
                    yield return new TestCaseData(AutoTriageQueryFactoryTests.NodeId, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.MaxValue);
                    // Time range begin is local date time
                    yield return new TestCaseData(AutoTriageQueryFactoryTests.NodeId, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Local), new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                    // Time range end is local date time
                    yield return new TestCaseData(AutoTriageQueryFactoryTests.NodeId, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Local));
                    // Time range begin is greater than Time range end
                    yield return new TestCaseData(AutoTriageQueryFactoryTests.NodeId, new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                }
            }
        }
    }
}