namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
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
    public class NewExecutionGoalCmdletTests
    {
        private IAsyncPolicy retryPolicy;
        private int currentRetries;
        private TestNewExecutionGoalCmdlet cmdlet;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private ExecutionGoalParameter mockParameter;
        private HttpResponseMessage mockGetResponse;
        private bool exceptionThrown;
        private string expectedTeamName;
        private string expectedTemplateId;
        private string expectedExperimentName;
        private string expectedParameterFile;
        private object expectedOutput;

        [SetUp]
        public void SetupTest()
        {
            this.expectedTeamName = "a team";
            this.expectedTemplateId = "an id";
            this.expectedExperimentName = "an experimentName";
            this.expectedParameterFile = "a parameterFile";
            this.mockFixture = new Fixture();
            this.mockDependencies = new FixtureDependencies();
            this.mockFixture.SetupExperimentMocks();
            this.mockParameter = FixtureExtensions.CreateExecutionGoalParameter();
            Item<GoalBasedSchedule> mockResponseContent = new Item<GoalBasedSchedule>(
                this.expectedTemplateId, 
                FixtureExtensions.CreateExecutionGoalFromTemplate());
            // no need to inline this goalbasedschedule, the output only barfs id/created/lastmodified/status
            this.mockGetResponse = this.mockFixture.CreateHttpResponse(System.Net.HttpStatusCode.Created, mockResponseContent);

            this.retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                4,
                (retries) => TimeSpan.Zero,
                onRetry: (response, delay, retryCount, context) =>
                {
                    this.currentRetries = retryCount;
                });
            this.cmdlet = new TestNewExecutionGoalCmdlet(this.retryPolicy);
            this.cmdlet.TeamName = this.expectedTeamName;
            this.cmdlet.TemplateId = this.expectedTemplateId;
            this.cmdlet.ExperimentName = this.expectedExperimentName;
            this.cmdlet.ParameterFilePath = this.expectedParameterFile;
            this.cmdlet.ExperimentsClient = this.mockDependencies.ExperimentClient.Object;
            this.cmdlet.AsJson = false;
            this.cmdlet.ExecutionGoalParameter = this.mockParameter;
            this.exceptionThrown = false;
            this.expectedOutput = this.cmdlet.CreateOutput(this.mockGetResponse.Content);
        }

        [TearDown]
        public void TearDown()
        {
            this.cmdlet.Dispose();
        }

        [Test]
        [TestCase(null, null, null, null)]
        [TestCase("templateId", null, null, null)]
        [TestCase(null, "teamName", null, null)]
        [TestCase(null, null, "experimentName", null)]
        [TestCase(null, null, null, "parameterFile")]
        [TestCase(null, "teamName", "experimentName", null)]
        [TestCase("templateId", null, null, "parameterFile")]
        public void CmdletThrowsExpectedExceptionWhenInvalidParametersAreGiven(string templateId, string teamName, string experimentName, string parameterFile)
        {
            this.cmdlet.TeamName = teamName;
            this.cmdlet.TemplateId = templateId;
            this.cmdlet.ExperimentName = experimentName;
            this.cmdlet.ParameterFilePath = parameterFile;

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
            Assert.AreEqual(templateId, this.cmdlet.TemplateId);
            Assert.AreEqual(experimentName, this.cmdlet.ExperimentName);
            Assert.AreEqual(parameterFile, this.cmdlet.ParameterFilePath);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
        }

        [Test]
        public void CmdletThrowsExpectedExceptionWhenClientThrowException()
        {
            Exception expectedException = new Exception("Expected Exception Message");
            this.mockDependencies.ExperimentClient.Setup(m => m.CreateExecutionGoalFromTemplateAsync(It.IsAny<ExecutionGoalParameter>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((ExecutionGoalParameter parameters, string templateId, string teamName, CancellationToken token) =>
            {
                Assert.AreEqual(this.cmdlet.TeamName, teamName);
                Assert.AreEqual(this.cmdlet.TemplateId, templateId);
                Assert.AreEqual(this.cmdlet.ExecutionGoalParameter, parameters);
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
            this.mockDependencies.ExperimentClient.Setup(m => m.CreateExecutionGoalFromTemplateAsync(It.IsAny<ExecutionGoalParameter>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((ExecutionGoalParameter parameters, string templateId, string teamName, CancellationToken token) => 
            {
                Assert.AreEqual(this.cmdlet.TeamName, teamName);
                Assert.AreEqual(this.cmdlet.TemplateId, templateId);
                Assert.AreEqual(this.cmdlet.ExecutionGoalParameter, parameters);
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
            Assert.AreEqual(this.expectedTeamName, this.cmdlet.TeamName);
            Assert.AreEqual(this.expectedTemplateId, this.cmdlet.TemplateId);
            Assert.IsTrue(this.cmdlet.AsJson);
            Assert.IsTrue(this.cmdlet.IsJsonObject);
            Assert.AreEqual(this.expectedOutput, this.cmdlet.Results);
        }

        private class TestNewExecutionGoalCmdlet : NewExecutionGoalCmdlet
        {
            public TestNewExecutionGoalCmdlet(IAsyncPolicy retryPolicy)
                : base(retryPolicy)
            {
            }

            public bool IsJsonObject { get; set; }

            public object Results { get; set; }

            public ExecutionGoalParameter ExecutionGoalParameter { get; set; }

            public void ProcessInternal()
            {
                this.ProcessRecord();
            }

            public object CreateOutput(HttpContent content)
            {
                return this.FormatOutput(content);
            }

            protected override Task<ExecutionGoalParameter> CreateExecutionGoalParametersAsync()
            {
                return Task.FromResult(this.ExecutionGoalParameter);
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
