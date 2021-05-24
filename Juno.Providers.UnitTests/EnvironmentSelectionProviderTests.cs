namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentSelectionProviderTests
    {
        private TestProvider provider;
        private EnvironmentFilter mockFilter;
        private IServiceCollection mockServices;
        private IConfiguration mockConfiguration;
        private ILogger mockLogger;

        [SetUp]
        public void SetupTests()
        {
            this.mockServices = new ServiceCollection();
            this.mockConfiguration = new Mock<IConfiguration>().Object;
            this.mockLogger = NullLogger.Instance;
            this.mockFilter = FixtureExtensions.CreateEnvironmentFilterFromType(typeof(TestProvider));
            this.provider = new TestProvider(this.mockServices, this.mockConfiguration, this.mockLogger);
        }

        [Test]
        public void EnvironmentFilterProviderReturnsTheExpectedResult()
        {
            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>();
            this.provider.OnExecuteAsync = (filter, context, token) => expectedResult;

            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.ExecuteAsync(this.mockFilter, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void EnvironmentFilterProviderReturnsTheExpectedResultWhenCancellationIsRequested()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource()) 
            {
                tokenSource.Cancel();
                var actualResult = this.provider.ExecuteAsync(this.mockFilter, tokenSource.Token).GetAwaiter().GetResult();

                Assert.IsNotNull(actualResult);
                Assert.IsTrue(actualResult.Count == 0);
            }
        }

        [Test]
        public void EnvironmentFilterProviderThrowsErrorWhenExceptionOccurs()
        {
            ProviderException exc = new ProviderException(ErrorReason.ProviderDefinitionInvalid);
            this.provider.OnExecuteAsync = (filter, context, token) => throw exc;

            Assert.ThrowsAsync<ProviderException>(() => this.provider.ExecuteAsync(this.mockFilter, CancellationToken.None));
        }

        private class TestProvider : EnvironmentSelectionProvider
        {
            public TestProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, TimeSpan.FromSeconds(2), configuration, logger)
            {
            }

            public Func<EnvironmentFilter, EventContext, CancellationToken, IDictionary<string, EnvironmentCandidate>> OnExecuteAsync { get; set; }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                return (Task<IDictionary<string, EnvironmentCandidate>>)(this.OnExecuteAsync != null
                    ? Task.FromResult(this.OnExecuteAsync(filter, telemetryContext, token))
                    : Task.CompletedTask);
            }
        }
    }
}
