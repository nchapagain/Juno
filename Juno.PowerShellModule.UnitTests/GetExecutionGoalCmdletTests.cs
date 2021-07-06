namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Rest;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Will dispose in teardown")]
    public class GetExecutionGoalCmdletTests
    {
        private IAsyncPolicy retryPolicy;
        private int currentRetries;
        private TestGetExecutionGoalCmdlet cmdlet;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private Item<GoalBasedSchedule> mockExecutionGoal;
        private HttpResponseMessage mockGetResponse;
        private bool exceptionThrown;
        private string expectedTeamName;
        private string expectedExecutionGoalId;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockDependencies = new FixtureDependencies();
            this.mockFixture.SetupExperimentMocks();
            this.mockExecutionGoal = new Item<GoalBasedSchedule>("an id", FixtureExtensions.CreateExecutionGoalFromTemplate());
            this.mockExecutionGoal.SetETag("some value");
            this.mockGetResponse = this.mockFixture.CreateHttpResponse(System.Net.HttpStatusCode.OK, this.mockExecutionGoal);

            this.retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                4,
                (retries) => TimeSpan.Zero,
                onRetry: (response, delay, retryCount, context) =>
                {
                    this.currentRetries = retryCount;
                });
            this.expectedTeamName = "a team";
            this.expectedExecutionGoalId = "an id";
            this.cmdlet = new TestGetExecutionGoalCmdlet(this.retryPolicy);
            this.cmdlet.TeamName = "a team";
            this.cmdlet.ExecutionGoalId = "an id";
            this.cmdlet.ExperimentsClient = this.mockDependencies.ExperimentClient.Object;
            this.cmdlet.AsJson = false;
            this.exceptionThrown = false;
        }

        [TearDown]
        public void TearDown()
        {
            this.cmdlet.Dispose();
        }

        [Test]
        public void CmdletThrowsExpectedExceptionWhenInvalidParametersAreGiven()
        {
            this.cmdlet.TeamName = string.Empty;
            this.cmdlet.ExecutionGoalId = string.Empty;

            try
            {
                this.cmdlet.ProcessInternal();
            }
            catch (Exception e)
            {
                this.exceptionThrown = true;
                Assert.AreEqual(typeof(ArgumentException), e.GetType());
            }

            Assert.IsTrue(this.exceptionThrown);
            Assert.AreEqual(string.Empty, this.cmdlet.TeamName);
            Assert.AreEqual(string.Empty, this.cmdlet.ExecutionGoalId);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
        }

        [Test]
        public void CmdletThrowsExpectedExceptionWhenClientThrowException()
        {
            Exception expectedException = new Exception("Expected Exception Message");
            this.mockDependencies.ExperimentClient.Setup(m => m.GetExecutionGoalsAsync(It.IsAny<CancellationToken>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ExecutionGoalView>())).Callback((CancellationToken token, string teamName, string definitionId, ExecutionGoalView view) =>
            {
                Assert.AreEqual(this.cmdlet.TeamName, teamName);
                Assert.AreEqual(this.cmdlet.ExecutionGoalId, definitionId);
                Assert.IsFalse(this.cmdlet.AsJson);
                Assert.AreEqual(ExecutionGoalView.Full, view);
            }).ThrowsAsync(expectedException);

            try
            {
                this.cmdlet.ProcessInternal();
            }
            catch (Exception e)
            {
                this.exceptionThrown = true;
                Assert.AreEqual(expectedException.GetType(), e.GetType());
                Assert.AreEqual(expectedException.Message, e.Message);
                Assert.AreEqual(expectedException, e);
            }

            Assert.IsTrue(this.exceptionThrown);
            Assert.AreEqual(this.expectedTeamName, this.cmdlet.TeamName);
            Assert.AreEqual(this.expectedExecutionGoalId, this.cmdlet.ExecutionGoalId);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
            Assert.Greater(this.currentRetries, 1);
        }

        [Test]
        public void CmdletWritesExpectedObject()
        {
            this.cmdlet.AsJson = true;
            this.mockDependencies.ExperimentClient.Setup(m => m.GetExecutionGoalsAsync(It.IsAny<CancellationToken>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ExecutionGoalView>())).Callback((CancellationToken token, string teamName, string definitionId, ExecutionGoalView view) =>
            {
                Assert.AreEqual(this.cmdlet.TeamName, teamName);
                Assert.AreEqual(this.cmdlet.ExecutionGoalId, definitionId);
                Assert.IsTrue(this.cmdlet.AsJson);
                Assert.AreEqual(ExecutionGoalView.Full, view);
            }).ReturnsAsync(this.mockGetResponse);

            try
            {
                this.cmdlet.ProcessInternal();
            }
            catch (Exception)
            {
                Assert.Fail();
            }

            Assert.IsNotNull(this.cmdlet.Results);
            Assert.AreEqual(this.expectedTeamName, this.cmdlet.TeamName);
            Assert.AreEqual(this.expectedExecutionGoalId, this.cmdlet.ExecutionGoalId);
            Assert.IsTrue(this.cmdlet.AsJson);
            Assert.IsTrue(this.cmdlet.IsJsonObject);
            Assert.IsNull((this.cmdlet.Results as Item<GoalBasedSchedule>).GetETag());
            Assert.IsNotNull(this.mockExecutionGoal.GetETag());
            Assert.AreNotEqual(this.mockExecutionGoal, this.cmdlet.Results);
            this.cmdlet.RemoveTags(this.mockExecutionGoal);
            Assert.AreEqual(this.mockExecutionGoal.ToJson(), this.cmdlet.Results.ToJson());
        }

        private class TestGetExecutionGoalCmdlet : GetExecutionGoalsCmdlet
        {
            public TestGetExecutionGoalCmdlet(IAsyncPolicy retryPolicy)
                : base(retryPolicy)
            {
            }

            public bool IsJsonObject { get; set; }

            public object Results { get; set; }

            public void ProcessInternal()
            {
                this.ProcessRecord();
            }

            public void RemoveTags(ItemBase item)
            {
                this.RemoveServerSideDataTags(item);
            }

            protected override void WriteResults(object results)
            {
                this.Results = results;
                this.IsJsonObject = false;
            }

            protected override void WriteResultsAsJson(object results)
            {
                this.Results = results;
                this.IsJsonObject = true;
            }
        }
    }
}
