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
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Will dispose in teardown")]
    public class NewTemplateCmdletTests
    {
        private IAsyncPolicy retryPolicy;
        private int currentRetries;
        private TestNewTemplateCmdlet cmdlet;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private Item<GoalBasedSchedule> mockGoalBasedScheduleItem;
        private HttpResponseMessage mockGetResponse;
        private bool exceptionThrown;
        private string expectedFilePath;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockDependencies = new FixtureDependencies();
            this.mockFixture.SetupExperimentMocks();
            this.mockGoalBasedScheduleItem = new Item<GoalBasedSchedule>("an id", FixtureExtensions.CreateExecutionGoalTemplate());
            this.mockGetResponse = this.mockFixture.CreateHttpResponse(System.Net.HttpStatusCode.OK, this.mockGoalBasedScheduleItem);

            this.retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                4,
                (retries) => TimeSpan.Zero,
                onRetry: (response, delay, retryCount, context) =>
                {
                    this.currentRetries = retryCount;
                });
            this.cmdlet = new TestNewTemplateCmdlet(this.retryPolicy);
            this.cmdlet.FilePath = "a file path";
            this.cmdlet.ExperimentsClient = this.mockDependencies.ExperimentClient.Object;
            this.cmdlet.AsJson = false;
            this.exceptionThrown = false;
            this.expectedFilePath = this.cmdlet.FilePath;
        }

        [TearDown]
        public void TearDown()
        {
            this.cmdlet.Dispose();
        }

        [Test]
        public void CmdletThrowsExpectedExceptionWhenInvalidParametersAreGiven()
        {
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
            Assert.AreEqual(string.Empty, this.cmdlet.FilePath);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
        }

        [Test]
        public void CmdletThrowsExpectedExceptionWhenClientThrowException()
        {
            Exception expectedException = new Exception("Expected Exception Message");
            this.mockDependencies.ExperimentClient.Setup(m => m.CreateExecutionGoalTemplateAsync(It.IsAny<Item<GoalBasedSchedule>>(), It.IsAny<CancellationToken>()))
                .Callback((Item<GoalBasedSchedule> parameters, CancellationToken token) =>
                {
                    Assert.AreEqual(this.cmdlet.GoalBasedScheduleItem, parameters);
                    Assert.IsFalse(this.cmdlet.AsJson);
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
            Assert.AreEqual(this.expectedFilePath, this.cmdlet.FilePath);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
            Assert.Greater(this.currentRetries, 1);
        }

        [Test]
        public void CmdletWritesExpectedObject()
        {
            this.cmdlet.AsJson = true;
            this.mockDependencies.ExperimentClient.Setup(m => m.CreateExecutionGoalTemplateAsync(It.IsAny<Item<GoalBasedSchedule>>(), It.IsAny<CancellationToken>()))
                .Callback((Item<GoalBasedSchedule> parameters, CancellationToken token) =>
                {
                    Assert.AreEqual(this.cmdlet.GoalBasedScheduleItem, parameters);
                    Assert.IsTrue(this.cmdlet.AsJson);
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
            Assert.AreEqual(this.expectedFilePath, this.cmdlet.FilePath);
            Assert.IsTrue(this.cmdlet.AsJson);
            Assert.IsTrue(this.cmdlet.IsJsonObject);
            Assert.AreEqual(this.mockGoalBasedScheduleItem, this.cmdlet.Results);
        }

        private class TestNewTemplateCmdlet : NewTemplateCmdlet
        {
            public TestNewTemplateCmdlet(IAsyncPolicy retryPolicy)
                : base(retryPolicy)
            {
            }

            public bool IsJsonObject { get; set; }

            public object Results { get; set; }

            public Item<GoalBasedSchedule> GoalBasedScheduleItem { get; set; }

            public void ProcessInternal()
            {
                this.ProcessRecord();
            }

            protected override Item<GoalBasedSchedule> GetExecutionGoalFromFile(string filePath)
            {
                return this.GoalBasedScheduleItem;
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
