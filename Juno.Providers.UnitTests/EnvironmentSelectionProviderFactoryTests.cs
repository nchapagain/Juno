namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
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
    public class EnvironmentSelectionProviderFactoryTests
    {
        private Fixture mockFixture;
        private IServiceCollection mockServices;
        private IConfiguration mockConfiguration;
        private ILogger mockLogger;
        private EnvironmentFilter mockFilter;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupEnvironmentSelectionMocks();
            this.mockServices = new ServiceCollection();
            this.mockConfiguration = new Mock<IConfiguration>().Object;
            this.mockLogger = NullLogger.Instance;
            this.mockFilter = FixtureExtensions.CreateEnvironmentFilterFromType(typeof(TestProvider));
        }

        [Test]
        public void CreateEnvironmentFilterProviderValidatesParameters()
        {
            Assert.Throws<ArgumentException>(() => EnvironmentSelectionProviderFactory.CreateEnvironmentFilterProvider(null, this.mockServices, this.mockConfiguration, this.mockLogger));
            Assert.Throws<ArgumentException>(() => EnvironmentSelectionProviderFactory.CreateEnvironmentFilterProvider(this.mockFilter, null, this.mockConfiguration, this.mockLogger));
            Assert.Throws<ArgumentException>(() => EnvironmentSelectionProviderFactory.CreateEnvironmentFilterProvider(this.mockFilter, this.mockServices, null, this.mockLogger));
        }

        [Test]
        public void FactoryCreatesTheExpectedEnvironmentFilterProvider()
        {
            IEnvironmentSelectionProvider provider = EnvironmentSelectionProviderFactory.CreateEnvironmentFilterProvider(this.mockFilter, this.mockServices, this.mockConfiguration, this.mockLogger);
            Assert.IsNotNull(provider);
            Assert.IsInstanceOf<TestProvider>(provider);
        }

        [Test]
        public void FactoryThrowsExceptionWhenEnvironmentProviderDoesNotExist()
        {
            EnvironmentFilter filter = new EnvironmentFilter("I am not a type of filter");
            ProviderException exc = Assert.Throws<ProviderException>(() => EnvironmentSelectionProviderFactory.CreateEnvironmentFilterProvider(filter, this.mockServices, this.mockConfiguration, this.mockLogger));

            Assert.AreEqual(ErrorReason.ProviderNotFound, exc.Reason);
            Assert.IsNotNull(exc.InnerException);
            Assert.IsInstanceOf<TypeLoadException>(exc.InnerException);
        }

        private class TestProvider : EnvironmentSelectionProvider
        {
            public TestProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger)
            { 
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                return (Task<IDictionary<string, EnvironmentCandidate>>)Task.CompletedTask;
            }
        }
    }
}
