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
    public class GetTemplateCmdletTests
    {
        private IAsyncPolicy retryPolicy;
        private int currentRetries;
        private TestGetTemplateCmdlet cmdlet;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private Item<GoalBasedSchedule> mockTemplate;
        private HttpResponseMessage mockGetResponse;
        private bool exceptionThrown;
        private string expectedTeamName;
        private string expectedTemplateId;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockDependencies = new FixtureDependencies();
            this.mockFixture.SetupExperimentMocks();
            this.mockTemplate = new Item<GoalBasedSchedule>("an id", FixtureExtensions.CreateExecutionGoalTemplate());
            this.mockGetResponse = this.mockFixture.CreateHttpResponse(System.Net.HttpStatusCode.OK, this.mockTemplate);

            this.retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                4,
                (retries) => TimeSpan.Zero,
                onRetry: (response, delay, retryCount, context) =>
                {
                    this.currentRetries = retryCount;
                });
            this.expectedTeamName = "a team";
            this.expectedTemplateId = "an id";
            this.cmdlet = new TestGetTemplateCmdlet(this.retryPolicy);
            this.cmdlet.TeamName = "a team";
            this.cmdlet.TemplateId = "an id";
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
            this.cmdlet.TemplateId = "aTemplateId";

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
            Assert.AreNotEqual(string.Empty, this.cmdlet.TemplateId);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
        }

        [Test]
        public void CmdletThrowsExpectedExceptionWhenClientThrowException()
        {
            Exception expectedException = new Exception("Expected Exception Message");
            this.mockDependencies.ExperimentClient.Setup(m => m.GetTemplatesAsync(It.IsAny<CancellationToken>(), It.IsAny<string>(), It.IsAny<Contracts.View>(), It.IsAny<string>()))
                .Callback((CancellationToken token, string teamName, Contracts.View view, string definitionId) =>
            {
                Assert.AreEqual(this.cmdlet.TeamName, teamName);
                Assert.AreEqual(this.cmdlet.TemplateId, definitionId);
                Assert.IsFalse(this.cmdlet.AsJson);
                Assert.AreEqual(View.Full, view);
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
            Assert.AreEqual(this.expectedTemplateId, this.cmdlet.TemplateId);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
            Assert.Greater(this.currentRetries, 1);
        }

        [Test]
        public void CmdletWritesExpectedObject()
        {
            this.cmdlet.AsJson = true;
            this.mockDependencies.ExperimentClient.Setup(m => m.GetTemplatesAsync(It.IsAny<CancellationToken>(), It.IsAny<string>(), It.IsAny<View>(), It.IsAny<string>()))
                .Callback((CancellationToken token, string teamName, View view, string definitionId) =>
            {
                Assert.AreEqual(this.cmdlet.TeamName, teamName);
                Assert.AreEqual(this.cmdlet.TemplateId, definitionId);
                Assert.IsTrue(this.cmdlet.AsJson);
                Assert.AreEqual(View.Full, view);
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
            Assert.AreEqual(this.expectedTemplateId, this.cmdlet.TemplateId);
            Assert.IsTrue(this.cmdlet.AsJson);
            Assert.IsTrue(this.cmdlet.IsJsonObject);
            Assert.AreEqual(this.mockTemplate, this.cmdlet.Results);
        }

        private class TestGetTemplateCmdlet : GetTemplateCmdlet
        {
            public TestGetTemplateCmdlet(IAsyncPolicy retryPolicy)
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
