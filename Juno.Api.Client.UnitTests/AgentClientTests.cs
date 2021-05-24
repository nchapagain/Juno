namespace Juno.Api.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Rest;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class AgentClientTests
    {
        private Fixture mockFixture;
        private AgentClient agentClient;
        private HttpResponseMessage mockResponse;
        private Mock<IRestClient> mockRestClient;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();

            this.mockRestClient = new Mock<IRestClient>();
            this.agentClient = new AgentClient(
                this.mockRestClient.Object,
                new Uri("https://anyjunoenvironment.agentapi"),
                Policy.NoOpAsync());

            this.mockResponse = AgentClientTests.CreateResponseMessage(HttpStatusCode.OK);
            this.mockRestClient.SetReturnsDefault(this.mockResponse);
        }

        [TearDown]
        public void CleanupTest()
        {
            this.mockResponse.Dispose();
        }

        [Test]
        public void AgentClientCallsTheExpectedApiToCreateAHeartbeat()
        {
            this.agentClient.CreateHeartbeatAsync(this.mockFixture.Create<AgentHeartbeat>(), CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockRestClient.Verify(client => client.PostAsync(
                It.Is<Uri>(uri => uri.AbsolutePath.Equals("/api/heartbeats")),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        [Test]
        public void AgentClientCallsTheExpectedApiToGetAgentSteps()
        {
            string agentId = "AnyAgent";
            this.agentClient.GetAgentStepsAsync(agentId, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockRestClient.Verify(client => client.GetAsync(
                It.Is<Uri>(uri => uri.PathAndQuery.Equals($"/api/experiments/agent-steps?agentId={agentId}")),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        [Test]
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters should start on line after declaration", Justification = "Not desirable in this case.")]
        public void AgentClientCallsTheExpectedApiToGetAgentStepsByStatus()
        {
            string agentId = "AnyAgent";
            List<ExecutionStatus> statuses = new List<ExecutionStatus>(Enum.GetValues(typeof(ExecutionStatus)) as IEnumerable<ExecutionStatus>);

            this.mockRestClient.Setup(client => client.GetAsync(
               It.IsAny<Uri>(),
               It.IsAny<CancellationToken>(),
               It.IsAny<HttpCompletionOption>()))
                .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, option) =>
                {
                    string encodedFilter = HttpUtility.UrlEncode(string.Join(" or ", statuses.ToList().Select(status => $"(Status eq '{status.ToString()}')")));
                    Assert.IsTrue(uri.PathAndQuery.Equals($"/api/experiments/agent-steps?agentId={agentId}&filter={encodedFilter}"));
                })
                .Returns(Task.FromResult(this.mockResponse));

            this.agentClient.GetAgentStepsAsync(agentId, CancellationToken.None, status: statuses)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void AgentClientCallsTheExpectedApiToGetAnAgentHeartbeat()
        {
            string agentId = "AnyAgent";
            this.agentClient.GetHeartbeatAsync(agentId, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockRestClient.Verify(client => client.GetAsync(
                It.Is<Uri>(uri => uri.PathAndQuery.Equals($"/api/heartbeats?agentId={agentId}")),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        [Test]
        public void AgentClientCallsTheExpectedApiToGetTheExperimentForAGivenAgent()
        {
            string agentId = Guid.NewGuid().ToString();
            this.agentClient.GetAgentExperimentAsync(agentId, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockRestClient.Verify(client => client.GetAsync(
                It.Is<Uri>(uri => uri.PathAndQuery.Equals($"/api/experiments?agentId={agentId}")),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        [Test]
        public void AgentClientCallsTheExpectedApiToGetAnExperiment()
        {
            string experimentId = Guid.NewGuid().ToString();
            this.agentClient.GetExperimentAsync(experimentId, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockRestClient.Verify(client => client.GetAsync(
                It.Is<Uri>(uri => uri.AbsolutePath.Equals($"/api/experiments/{experimentId}")),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        [Test]
        public void AgentClientCallsTheExpectedApiToGetTheContextForAnExperiment()
        {
            string experimentId = Guid.NewGuid().ToString();
            this.agentClient.GetExperimentContextAsync(experimentId, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockRestClient.Verify(client => client.GetAsync(
                It.Is<Uri>(uri => uri.AbsolutePath.Equals($"/api/experiments/{experimentId}/context")),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        [Test]
        public void AgentClientCallsTheExpectedApiUploadAFile()
        {
            string experimentId = Guid.NewGuid().ToString();
            string agentType = "AnyAgent";
            string agentId = "AnyAgentId";
            string fileName = "AnyFile";
            string contentType = "text/plain";
            Encoding contentEncoding = Encoding.UTF8;
            DateTime timestamp = DateTime.UtcNow;

            using (Stream fileStream = new MemoryStream(Encoding.UTF8.GetBytes("Any file content")))
            {
                using (HttpResponseMessage response = AgentClientTests.CreateResponseMessage(HttpStatusCode.OK))
                {
                    this.mockRestClient.Setup(client => client.PostAsync(
                            It.IsAny<Uri>(),
                            It.IsAny<HttpContent>(),
                            It.IsAny<CancellationToken>()))
                        .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                        {
                            Assert.IsTrue(uri.PathAndQuery.Equals(
                                $"/api/experiments/agent-files?experimentId={experimentId}&agentType={agentType}&agentId={agentId}&fileName={fileName}&timestamp={timestamp.ToString("o")}"));
                        })
                        .Returns(Task.FromResult(response));

                    this.agentClient.UploadFileAsync(experimentId, agentType, agentId, fileName, contentType, contentEncoding, fileStream, timestamp, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
            }
        }

        [Test]
        public void AgentClientAddsTheExpectedFileStreamToTheRequestBodyToUploadAFile()
        {
            string experimentId = Guid.NewGuid().ToString();
            string agentType = "AnyAgent";
            string agentId = "AnyAgentId";
            string fileName = "AnyFile";
            string contentType = "text/plain";
            Encoding contentEncoding = Encoding.UTF8;
            DateTime timestamp = DateTime.UtcNow;

            using (Stream fileStream = new MemoryStream(Encoding.UTF8.GetBytes("Any file content")))
            {
                using (HttpResponseMessage response = AgentClientTests.CreateResponseMessage(HttpStatusCode.OK))
                {
                    this.mockRestClient.Setup(client => client.PostAsync(
                            It.IsAny<Uri>(),
                            It.IsAny<HttpContent>(),
                            It.IsAny<CancellationToken>()))
                        .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                        {
                            StreamContent fileContent = content as StreamContent;
                            Assert.IsNotNull(fileContent);
                            Assert.AreEqual("Any file content", fileContent.ReadAsStringAsync().GetAwaiter().GetResult());
                        })
                        .Returns(Task.FromResult(response));

                    this.agentClient.UploadFileAsync(experimentId, agentType, agentId, fileName, contentType, contentEncoding, fileStream, timestamp, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
            }
        }

        [Test]
        public void AgentClientCallsTheExpectedApiToUpdateAnAgentStep()
        {
            ExperimentStepInstance agentStep = this.mockFixture.Create<ExperimentStepInstance>();

            this.agentClient.UpdateAgentStepAsync(agentStep, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockRestClient.Verify(client => client.PutAsync(
                It.Is<Uri>(uri => uri.AbsolutePath.Equals($"/api/experiments/agent-steps/{agentStep.Id}")),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        [Test]
        public void AgentClientCallsTheExpectedApiToUpdateTheContextForAnExperiment()
        {
            string experimentId = Guid.NewGuid().ToString();
            ExperimentMetadataInstance context = this.mockFixture.Create<ExperimentMetadataInstance>();

            this.agentClient.UpdateExperimentContextAsync(experimentId, context, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockRestClient.Verify(client => client.PutAsync(
                It.Is<Uri>(uri => uri.AbsolutePath.Equals($"/api/experiments/{experimentId}/context")),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
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
