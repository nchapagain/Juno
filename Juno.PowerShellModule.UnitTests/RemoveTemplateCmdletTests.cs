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
    public class RemoveTemplateCmdletTests
    {
        private IAsyncPolicy retryPolicy;
        private int currentRetries;
        private TestRemoveTemplateCmdlet cmdlet;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private HttpResponseMessage mockGetResponse;
        private bool exceptionThrown;
        private string expectedTeamName;
        private string expectedId;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockDependencies = new FixtureDependencies();
            this.mockFixture.SetupExperimentMocks();
            this.mockGetResponse = this.mockFixture.CreateHttpResponse(System.Net.HttpStatusCode.OK);

            this.retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                4,
                (retries) => TimeSpan.Zero,
                onRetry: (response, delay, retryCount, context) =>
                {
                    this.currentRetries = retryCount;
                });
            this.expectedTeamName = "a team";
            this.expectedId = "an id";
            this.cmdlet = new TestRemoveTemplateCmdlet(this.retryPolicy);
            this.cmdlet.TeamName = this.expectedTeamName;
            this.cmdlet.TemplateId = this.expectedId;
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
        [TestCase(null, null)]
        [TestCase(null, "anExecutionGoalTemplateId")]
        [TestCase("aTeamName", null)]
        public void CmdletThrowsExpectedExceptionWhenInvalidParametersAreGiven(string teamName, string executionId)
        {
            this.cmdlet.TeamName = teamName;
            this.cmdlet.TemplateId = executionId;

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
            Assert.AreEqual(teamName, this.cmdlet.TeamName);
            Assert.AreEqual(executionId, this.cmdlet.TemplateId);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
        }

        [Test]
        public void CmdletThrowsExpectedExceptionWhenClientThrowException()
        {
            Exception expectedException = new Exception("Expected Exception Message");
            this.mockDependencies.ExperimentClient.Setup(m => m.DeleteExecutionGoalTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string executionGoalId, string teamName, CancellationToken token) =>
                {
                    Assert.AreEqual(this.cmdlet.TemplateId, executionGoalId);
                    Assert.AreEqual(this.cmdlet.TeamName, teamName);
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
            Assert.AreEqual(this.expectedTeamName, this.cmdlet.TeamName);
            Assert.AreEqual(this.expectedId, this.cmdlet.TemplateId);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
            Assert.Greater(this.currentRetries, 1);
        }

        [Test]
        public void CmdletWritesExpectedObject()
        {
            this.cmdlet.AsJson = true;
            this.mockDependencies.ExperimentClient.Setup(m => m.DeleteExecutionGoalTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string executionGoalId, string teamName, CancellationToken token) =>
                {
                    Assert.AreEqual(this.cmdlet.TemplateId, executionGoalId);
                    Assert.AreEqual(this.cmdlet.TeamName, teamName);
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

            Assert.AreEqual(this.expectedTeamName, this.cmdlet.TeamName);
            Assert.AreEqual(this.expectedId, this.cmdlet.TemplateId);
            Assert.IsTrue(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
        }

        private class TestRemoveTemplateCmdlet : RemoveTemplateCmdlet
        {
            public TestRemoveTemplateCmdlet(IAsyncPolicy retryPolicy)
                : base(retryPolicy)
            {
            }

            public bool IsJsonObject { get; set; }

            public object Results { get; set; }

            public void ProcessInternal()
            {
                this.ProcessRecord();
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
