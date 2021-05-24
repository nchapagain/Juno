namespace Juno.Api.Client
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Rest;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentsClientTests
    {
        private Fixture mockFixture;
        private ExperimentsClient experimentsClient;
        private Mock<IRestClient> mockRestClient;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockFixture.SetupEnvironmentSelectionMocks();

            this.mockRestClient = new Mock<IRestClient>();
            this.experimentsClient = new ExperimentsClient(
                this.mockRestClient.Object,
                new Uri("https://experiments/api"),
                Policy.NoOpAsync());
        }

        [Test]
        public void ExperimentsClientCallsTheExpectedApiToCancelAnExperiment()
        {
            using (HttpResponseMessage response = ExperimentsClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                string experimentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.PutAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/experiments/{experimentId}?cancel=true"));
                    })
                    .Returns(Task.FromResult(response));

                this.experimentsClient.CancelExperimentAsync(experimentId, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExperimentsClientCallsTheExpectedApiToCreateAnExperiment()
        {
            using (HttpResponseMessage response = ExperimentsClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals("/api/experiments"));
                    })
                    .Returns(Task.FromResult(response));

                this.experimentsClient.CreateExperimentAsync(this.mockFixture.Create<Experiment>(), CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExperimentClientCallsTheExpectedApiToCreateAnExperimentFromTemplate()
        {
            using (HttpResponseMessage response = ExperimentsClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals("/api/experiments/template"));
                    })
                    .Returns(Task.FromResult(response));

                this.experimentsClient.CreateExperimentFromTemplateAsync(this.mockFixture.Create<ExperimentTemplate>(), CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExperimentsClientCallsTheExpectedApiToCreateAnExperimentOnASpecificWorkQueue()
        {
            using (HttpResponseMessage response = ExperimentsClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string expectedWorkQueue = "anySpecifiedQueueName";

                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/experiments?workQueue={expectedWorkQueue}"));
                    })
                    .Returns(Task.FromResult(response));

                this.experimentsClient.CreateExperimentAsync(this.mockFixture.Create<Experiment>(), CancellationToken.None, workQueue: expectedWorkQueue)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExperimentsClientCallsTheExpectedApiToValidateAnExperiment()
        {
            using (HttpResponseMessage response = ExperimentsClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals("/api/experiments?validate=true"));
                    })
                    .Returns(Task.FromResult(response));

                this.experimentsClient.ValidateExperimentAsync(this.mockFixture.Create<Experiment>(), CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public async Task CreateExecutionGoalFromTemplateCallsExpectedApi()
        {
            string executionGoalTemplateId = Guid.NewGuid().ToString();
            string teamName = "teamName";
            this.mockRestClient.Setup(client => client.PostAsync(
                It.IsAny<Uri>(),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()))
                .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoals/{executionGoalTemplateId}?teamName={teamName}"));
                });

            dynamic targetGoal = new List<TargetGoalParameter>();
            targetGoal.Add(new TargetGoalParameter(executionGoalTemplateId, "workload", null));
            dynamic experimentParameters = new ExecutionGoalParameter(executionGoalTemplateId, "experimentName", "owner", true, targetGoal);
            _ = await this.experimentsClient.CreateExecutionGoalFromTemplateAsync(experimentParameters, executionGoalTemplateId, teamName, CancellationToken.None);
        }

        [Test]
        public async Task GetExecutionGoalsAsyncCallsExpectedApi()
        {
            string teamName = "teamName";
            ExecutionGoalView view = ExecutionGoalView.Full;
            this.mockRestClient.Setup(client => client.GetAsync(
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()))
                .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, options) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoals?teamName={teamName}&view={view}"));
                });

            _ = await this.experimentsClient.GetExecutionGoalsAsync(CancellationToken.None, teamName);
        }

        [Test]
        public async Task GetExecutionGoalAsyncCallsExpectedApi()
        {
            string teamName = "teamName";
            string executionGoalId = Guid.NewGuid().ToString();
            ExecutionGoalView view = ExecutionGoalView.Full;
            this.mockRestClient.Setup(client => client.GetAsync(
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()))
                .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, options) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoals?teamName={teamName}&executionGoalId={executionGoalId}&view={view}"));
                });

            _ = await this.experimentsClient.GetExecutionGoalsAsync(CancellationToken.None, teamName, executionGoalId, view);
        }

        [Test]
        public async Task GetExecutionGoalTemplateSummaryCallsExpectedApi()
        {
            string teamName = "teamName";
            this.mockRestClient.Setup(client => client.GetAsync(
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()))
                .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, options) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoalTemplates?teamName={teamName}&view=Summary"));
                });

            _ = await this.experimentsClient.GetTemplatesAsync(CancellationToken.None, teamName, View.Summary);
        }

        [Test]
        public async Task GetExecutionGoalTemplatesSummaryCallsexpectedApiWithId()
        {
            string teamName = "teamName";
            string id = Guid.NewGuid().ToString();
            this.mockRestClient.Setup(client => client.GetAsync(
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()))
                .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, options) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoalTemplates?teamName={teamName}&view=Summary&templateId={id}"));
                });

            _ = await this.experimentsClient.GetTemplatesAsync(CancellationToken.None, teamName, View.Summary, id);
        }

        [Test]
        public async Task GetExecutionGoalTemplatesCallsExpectedApi()
        {
            string teamName = "teamName";
            this.mockRestClient.Setup(client => client.GetAsync(
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()))
                .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, options) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoalTemplates?teamName={teamName}&view=Full"));
                });

            _ = await this.experimentsClient.GetTemplatesAsync(CancellationToken.None, teamName);
        }

        [Test]
        public async Task GetExecutionGoalTemplatesCallsExpectedApiWithId()
        {
            string teamName = "teamName";
            string id = Guid.NewGuid().ToString();
            this.mockRestClient.Setup(client => client.GetAsync(
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()))
                .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, options) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoalTemplates?teamName={teamName}&view=Full&templateId={id}"));
                });

            _ = await this.experimentsClient.GetTemplatesAsync(CancellationToken.None, teamName, templateId: id);
        }

        [Test]
        public async Task GetEnvironmentsAsyncCallsExpectedApi()
        {
            EnvironmentQuery query = this.mockFixture.Create<EnvironmentQuery>();
            this.mockRestClient.Setup(client => client.PostAsync(
                It.IsAny<Uri>(),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()))
                .Callback<Uri, HttpContent, CancellationToken>((uri, token, options) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Equals($"/api/environments"));
                });

            _ = await this.experimentsClient.ReserveEnvironmentsAsync(query, CancellationToken.None);
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExperimentStatuses()
        {
            using (HttpResponseMessage response = ExperimentsClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string experimentName = "Any_Experiment";

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, option) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experimentstatus/{experimentName}"));
                    })
                    .Returns(Task.FromResult(response));

                this.experimentsClient.GetExperimentInstanceStatusesAsync(experimentName, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        private static HttpResponseMessage CreateResponseMessage(HttpStatusCode expectedStatusCode, object expectedContent = null)
        {
            HttpResponseMessage mockResponse = new HttpResponseMessage(expectedStatusCode);

            if (expectedContent != null)
            {
                mockResponse.Content = new StringContent(expectedContent.ToJson());
            }

            return mockResponse;
        }
    }
}
