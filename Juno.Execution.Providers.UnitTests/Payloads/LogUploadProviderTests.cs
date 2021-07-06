namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class LogUploadProviderTests
    {
        private IServiceCollection providerServices;
        private ProviderFixture mockFixture;
        private Mock<IFileSystem> mockFileSystem;
        private Mock<IRestClient> mockRestClient;

        [SetUp]
        public void Setup()
        {
            this.mockFileSystem = new Mock<IFileSystem>();
            this.mockRestClient = new Mock<IRestClient>();

            var agentId = new AgentIdentification("fakecluster,fakenodeid,faketipsessionid");
            AgentClient apiClient = new AgentClient(
                this.mockRestClient.Object,
                new Uri("https://anyjunoenvironment.agent"),
                Policy.NoOpAsync());
            ClientPool<AgentClient> agentApiClientPool = new ClientPool<AgentClient>
            {
                [ApiClientType.AgentFileUploadApi] = apiClient,
            };

            this.providerServices = new ServiceCollection()
                .AddSingleton<AgentIdentification>(agentId)
                .AddSingleton<IFileSystem>(this.mockFileSystem.Object)
                .AddSingleton<ClientPool<AgentClient>>(agentApiClientPool);

            this.mockFixture = new ProviderFixture(typeof(LogUploadProvider));
        }

        [Test]
        public async Task LogUploadProviderUploadsTheLogFileWhenTheLogFileIsFound()
        {
            this.mockFileSystem.Setup(f => f.Directory.GetFiles(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchOption>()))
            .Returns(new string[] { @"Drive\FakePayload\File.log" });
            this.mockFileSystem.Setup(f => f.File.Open(
                It.IsAny<string>(),
                It.IsAny<FileMode>()))
            .Returns(new MemoryStream());
            this.mockRestClient.Setup(r => r.PostAsync(
                It.IsAny<Uri>(),
                It.IsAny<StreamContent>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))
            .Verifiable();
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "payload", "FakePayload" },
                { "fileName", "File.log" }
            });
            var provider = new TestLogUploadProvider(this.providerServices);

            ExecutionResult result = await provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            this.mockRestClient.Verify(r => r.PostAsync(
                It.IsAny<Uri>(),
                It.IsAny<StreamContent>(),
                It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task LogUploadProviderAddsTheContentToTelemetryWhenTheLogFileWhenTheLogFileIsFound()
        {
            this.mockFileSystem.Setup(f => f.Directory.GetFiles(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchOption>()))
            .Returns(new string[] { @"Drive\FakePayload\File.log" });
            string logFileContents = "logfilecontents";
            this.mockFileSystem.Setup(f => f.File.ReadAllTextAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(logFileContents));
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "payload", "FakePayload" },
                { "fileName", "File.log" },
                { "uploadToKusto", "true" }
            });
            var provider = new TestLogUploadProvider(this.providerServices);

            ExecutionResult result = await provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            Assert.AreEqual(logFileContents, provider.TelemetryContext.Properties[nameof(logFileContents)]);
        }

        [Test]
        public async Task LogUploadProviderSucceedsWhenTheLogFileIsNotFound()
        {
            this.mockFileSystem.Setup(f => f.Directory.GetFiles(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchOption>()))
            .Throws(new Exception("File not found"));
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "payload", "FakePayload" },
                { "fileName", "File.log" },
                { "uploadToKusto", "true" }
            });
            var provider = new TestLogUploadProvider(this.providerServices);

            ExecutionResult result = await provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            Assert.IsNotEmpty(provider.TelemetryContext.Properties[nameof(Exception)].ToString());
        }

        [Test]
        public async Task LogUploadProviderSucceedsWhenTheApiThrowsAnError()
        {
            this.mockFileSystem.Setup(f => f.Directory.GetFiles(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchOption>()))
            .Returns(new string[] { @"Drive\FakePayload\File.log" });
            this.mockFileSystem.Setup(f => f.File.Open(
                It.IsAny<string>(),
                It.IsAny<FileMode>()))
            .Returns(new MemoryStream());
            this.mockRestClient.Setup(r => r.PostAsync(
                It.IsAny<Uri>(),
                It.IsAny<StreamContent>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "payload", "FakePayload" },
                { "fileName", "File.log" }
            });
            var provider = new TestLogUploadProvider(this.providerServices);

            ExecutionResult result = await provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
            Assert.IsNotEmpty(provider.TelemetryContext.Properties[nameof(Exception)].ToString());
        }

        private class TestLogUploadProvider : LogUploadProvider
        {
            public TestLogUploadProvider(IServiceCollection services)
                : base(services)
            {
            }

            public EventContext TelemetryContext { get; private set; }

            public Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component)
            {
                this.TelemetryContext = EventContext.Persisted();
                return this.ExecuteAsync(context, component, this.TelemetryContext, CancellationToken.None);
            }
        }
    }
}
