namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Management.Automation.Host;
    using System.Net.Http;
    using System.Threading;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Will dispose in teardown")]
    public class UpdateTemplateCmdletTests
    {
        private IAsyncPolicy retryPolicy;
        private int currentRetries;
        private TestUpdateExecutionGoalTemplateCmdlet cmdlet;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private Item<GoalBasedSchedule> mockItemFromFile;
        private Item<GoalBasedSchedule> mockItemFromServer;
        private Item<GoalBasedSchedule> mockItemUpdated;
        private HttpResponseMessage mockGetResponse;
        private HttpResponseMessage mockUpdateResponse;
        private string expectedFilePath;
        private bool exceptionThrown;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockDependencies = new FixtureDependencies();
            this.mockFixture.SetupExperimentMocks();

            this.mockItemFromFile = new Item<GoalBasedSchedule>(
                "An Id",
                FixtureExtensions.CreateExecutionGoalFromTemplate(description: "from file"));
            this.mockItemFromFile.SetETag("etag from file");
            this.mockItemFromServer = new Item<GoalBasedSchedule>(
                "An Id",
                FixtureExtensions.CreateExecutionGoalFromTemplate(description: "from server"));
            this.mockItemFromServer.SetETag("etag from get");
            this.mockItemUpdated = new Item<GoalBasedSchedule>(
                this.mockItemFromServer.Id,
                this.mockItemFromFile.Definition);
            this.mockItemUpdated.SetETag(this.mockItemFromServer.GetETag());

            this.mockGetResponse = this.mockFixture.CreateHttpResponse(System.Net.HttpStatusCode.OK, this.mockItemFromServer);
            this.mockUpdateResponse = this.mockFixture.CreateHttpResponse(System.Net.HttpStatusCode.OK, this.mockItemUpdated);

            this.retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                4,
                (retries) => TimeSpan.Zero,
                onRetry: (response, delay, retryCount, context) =>
                {
                    this.currentRetries = retryCount;
                });
            this.cmdlet = new TestUpdateExecutionGoalTemplateCmdlet(this.retryPolicy);
            this.cmdlet.ExperimentsClient = this.mockDependencies.ExperimentClient.Object;
            this.expectedFilePath = "a file path";
            this.cmdlet.FilePath = "a file path";
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
            this.expectedFilePath = string.Empty;
            this.cmdlet.FilePath = string.Empty;

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
            Assert.AreEqual(this.expectedFilePath, this.cmdlet.FilePath);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
        }

        [Test]
        public void CmdletThrowsExpectedExceptionWhenClientThrowException()
        {
            Exception expectedException = new Exception("Expected Exception Message");
            this.mockDependencies.ExperimentClient.Setup(m => m.UpdateExecutionGoalTemplateAsync(It.IsAny<Item<GoalBasedSchedule>>(), It.IsAny<CancellationToken>())).ThrowsAsync(expectedException);

            // set up the ExecutionGoal item from the designated file
            this.cmdlet.GoalBasedScheduleFromFile = this.mockItemFromFile;

            // set up the execution goal item being retrieved from the data store prior to updating
            this.mockDependencies.ExperimentClient.Setup(m => m.GetTemplatesAsync(It.IsAny<CancellationToken>(), It.IsAny<string>(), It.IsAny<View>(), It.IsAny<string>())).Callback((CancellationToken token, string teamName, View view, string executionId) =>
            {
                Assert.AreEqual(teamName, this.mockItemFromFile.Definition.TeamName);
                Assert.AreEqual(executionId, this.mockItemFromFile.Definition.ExecutionGoalId);
                Assert.IsFalse(this.cmdlet.AsJson);
                Assert.AreEqual(view, ExecutionGoalView.Full);
            }).ReturnsAsync(this.mockGetResponse);

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
            Assert.AreEqual(this.expectedFilePath, this.cmdlet.FilePath);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
            Assert.Greater(this.currentRetries, 1);
        }

        [Test]
        public void CmdletUpdatesAndWritesExpectedObject()
        {
            Item<GoalBasedSchedule> actualGoalBasedSchedule = null;
            this.cmdlet.AsJson = true;

            // set up the ExecutionGoal item from the designated file
            this.cmdlet.GoalBasedScheduleFromFile = this.mockItemFromFile;

            // set up the execution goal item being retrieved from the data store prior to updating
            this.mockDependencies.ExperimentClient.Setup(m => m.GetTemplatesAsync(It.IsAny<CancellationToken>(), It.IsAny<string>(), It.IsAny<View>(), It.IsAny<string>())).ReturnsAsync(this.mockGetResponse);

            // return an updated ExecutionGoal item with new id and info from file ExecutionGoal item
            this.mockDependencies.ExperimentClient.Setup(m => m.UpdateExecutionGoalTemplateAsync(It.IsAny<Item<GoalBasedSchedule>>(), It.IsAny<CancellationToken>())).Callback((Item<GoalBasedSchedule> itemBeingUpdated, CancellationToken token) =>
            {
                // ensure the ExecutionGoal item passed in has the latest ExecutionGoal item Id and the file ExecutionGoal items' information
                Assert.AreEqual(itemBeingUpdated.Definition, this.mockItemUpdated.Definition);
                Assert.IsTrue(this.cmdlet.AsJson);
                actualGoalBasedSchedule = itemBeingUpdated;
            }).ReturnsAsync(this.mockUpdateResponse);

            this.cmdlet.ProcessInternal();

            Assert.NotNull(actualGoalBasedSchedule);
            ////Assert.AreEqual(actualGoalBasedSchedule.GetETag(), this.mockItemFromServer.GetETag());
            Assert.AreEqual(actualGoalBasedSchedule.Definition.Description, this.mockItemFromFile.Definition.Description);
            Assert.AreEqual(this.expectedFilePath, this.cmdlet.FilePath);
            Assert.IsTrue(this.cmdlet.AsJson);
            Assert.IsTrue(this.cmdlet.IsJsonObject);
            Assert.IsNotNull(this.cmdlet.Results);
            Assert.IsInstanceOf<Item<GoalBasedSchedule>>(this.cmdlet.Results);
            Assert.AreEqual(actualGoalBasedSchedule.Definition, (this.cmdlet.Results as Item<GoalBasedSchedule>).Definition);
        }

        private class TestUpdateExecutionGoalTemplateCmdlet : UpdateTemplateCmdlet
        {
            public TestUpdateExecutionGoalTemplateCmdlet(IAsyncPolicy retryPolicy)
                : base(retryPolicy)
            {
            }

            public Item<GoalBasedSchedule> GoalBasedScheduleFromFile { get; set; }

            public bool IsJsonObject { get; set; }

            public object Results { get; set; }

            public void ProcessInternal()
            {
                this.ProcessRecord();
            }

            protected override Item<GoalBasedSchedule> GetTemplateFromFile(string filePath)
            {
                return this.GoalBasedScheduleFromFile;
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
