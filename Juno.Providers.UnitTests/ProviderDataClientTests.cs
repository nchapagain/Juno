namespace Juno.Providers
{
    using System;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ProviderDataClientTests
    {
        private FixtureDependencies mockFixture;
        private ExecutionClient executionApiClient;
        private AgentClient agentApiClient;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new FixtureDependencies();
            this.executionApiClient = new ExecutionClient(this.mockFixture.RestClient.Object, new Uri("https://any/uri"), Policy.NoOpAsync());
            this.agentApiClient = new AgentClient(this.mockFixture.RestClient.Object, new Uri("https://any/uri"), Policy.NoOpAsync());
        }

        [Test]
        public void ProviderDataClientConstructorsSetPropertiesToExpectedValues1()
        {
            ProviderDataClient dataClient = new ProviderDataClient(this.agentApiClient);
            EqualityAssert.PropertySet(dataClient, "AgentApiClient", this.agentApiClient);
            EqualityAssert.PropertySet(dataClient, "Logger", NullLogger.Instance);

            Mock<ILogger> expectedLogger = new Mock<ILogger>();
            dataClient = new ProviderDataClient(this.agentApiClient, logger: expectedLogger.Object);
            EqualityAssert.PropertySet(dataClient, "AgentApiClient", this.agentApiClient);
            EqualityAssert.PropertySet(dataClient, "Logger", expectedLogger.Object);
        }

        [Test]
        public void ProviderDataClientConstructorsSetPropertiesToExpectedValues2()
        {
            ProviderDataClient dataClient = new ProviderDataClient(this.executionApiClient);
            EqualityAssert.PropertySet(dataClient, "ExecutionApiClient", this.executionApiClient);
            EqualityAssert.PropertySet(dataClient, "Logger", NullLogger.Instance);

            Mock<ILogger> expectedLogger = new Mock<ILogger>();
            dataClient = new ProviderDataClient(this.executionApiClient, logger: expectedLogger.Object);
            EqualityAssert.PropertySet(dataClient, "ExecutionApiClient", this.executionApiClient);
            EqualityAssert.PropertySet(dataClient, "Logger", expectedLogger.Object);
        }
    }
}
