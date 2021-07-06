namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Management.Automation.Host;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
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
    public class UpdateExecutionGoalFromTemplateCmdletTests
    {
        private IAsyncPolicy retryPolicy;
        private int currentRetries;
        private TestUpdateExecutionGoalFromTemplateCmdlet cmdlet;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private ExecutionGoalParameter mockItemFromFile;
        private Item<GoalBasedSchedule> mockItemUpdated;
        private HttpResponseMessage mockUpdateResponse;
        private string expectedFilePath;
        private string expectedTemplateId;
        private string expectedExecutionGoalId;
        private string expectedTeamName;
        private bool exceptionThrown;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockDependencies = new FixtureDependencies();
            this.mockFixture.SetupExperimentMocks();
            this.mockItemFromFile = GoalBasedScheduleExtensions.GetParametersFromTemplate(FixtureExtensions.CreateExecutionGoalTemplate());
            this.mockItemUpdated = new Item<GoalBasedSchedule>(
                "An Id",
                FixtureExtensions.CreateExecutionGoalFromTemplate(description: "From Update"));
            this.mockItemUpdated.SetETag("some etag");

            this.mockUpdateResponse = this.mockFixture.CreateHttpResponse(System.Net.HttpStatusCode.OK, this.mockItemUpdated);

            this.retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                4,
                (retries) => TimeSpan.Zero,
                onRetry: (response, delay, retryCount, context) =>
                {
                    this.currentRetries = retryCount;
                });
            this.cmdlet = new TestUpdateExecutionGoalFromTemplateCmdlet(this.retryPolicy);
            this.cmdlet.ExperimentsClient = this.mockDependencies.ExperimentClient.Object;
            this.expectedFilePath = "a file path";
            this.cmdlet.FilePath = "a file path";
            this.expectedTemplateId = "templateId";
            this.cmdlet.TemplateId = "templateId";
            this.expectedExecutionGoalId = "executionGoalId";
            this.cmdlet.ExecutionGoalId = "executionGoalId";
            this.expectedTeamName = "teamName";
            this.cmdlet.TeamName = "teamName";
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
            this.mockDependencies.ExperimentClient.Setup(m => m.UpdateExecutionGoalFromTemplateAsync(It.IsAny<ExecutionGoalParameter>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // set up the ExecutionGoal parameter file
            this.cmdlet.ExecutionGoalParameterFile = this.mockItemFromFile;

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
            bool updateCalled = false;
            this.cmdlet.AsJson = true;

            // set up the ExecutionGoal item from the designated file
            this.cmdlet.ExecutionGoalParameterFile = this.mockItemFromFile;

            // return an updated ExecutionGoal item with new id and info from file ExecutionGoal item
            this.mockDependencies.ExperimentClient.Setup(m => m.UpdateExecutionGoalFromTemplateAsync(It.IsAny<ExecutionGoalParameter>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((ExecutionGoalParameter parameters, string templateId, string executionGoalId, string teamName, CancellationToken token) =>
            {
                Assert.AreEqual(parameters, this.mockItemFromFile);
                Assert.AreEqual(templateId, this.expectedTemplateId);
                Assert.AreEqual(executionGoalId, this.expectedExecutionGoalId);
                Assert.AreEqual(teamName, this.expectedTeamName);
                Assert.IsTrue(this.cmdlet.AsJson);
                updateCalled = true;
            }).ReturnsAsync(this.mockUpdateResponse);

            this.cmdlet.ProcessInternal();

            Assert.IsTrue(updateCalled);
            Assert.AreEqual(this.expectedFilePath, this.cmdlet.FilePath);
            Assert.IsTrue(this.cmdlet.AsJson);
            Assert.IsTrue(this.cmdlet.IsJsonObject);
            Assert.IsNotNull(this.cmdlet.Results);
            Assert.IsInstanceOf<Item<GoalBasedSchedule>>(this.cmdlet.Results);

            Assert.IsNull((this.cmdlet.Results as Item<GoalBasedSchedule>).GetETag());
            Assert.IsNotNull(this.mockItemUpdated.GetETag());
            Assert.AreNotEqual(this.mockItemUpdated.ToJson(), this.cmdlet.Results.ToJson());
            this.cmdlet.RemoveTags(this.mockItemUpdated);
            Assert.AreEqual(this.mockItemUpdated.ToJson(), this.cmdlet.Results.ToJson());
        }

        private class TestUpdateExecutionGoalFromTemplateCmdlet : UpdateExecutionGoalFromTemplateCmdlet
        {
            public TestUpdateExecutionGoalFromTemplateCmdlet(IAsyncPolicy retryPolicy)
                : base(retryPolicy)
            {
            }

            public ExecutionGoalParameter ExecutionGoalParameterFile { get; set; }

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

            protected override Task<ExecutionGoalParameter> GetExecutionGoalParameterFromFileAsync(string filePath)
            {
                return Task.FromResult(this.ExecutionGoalParameterFile);
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
