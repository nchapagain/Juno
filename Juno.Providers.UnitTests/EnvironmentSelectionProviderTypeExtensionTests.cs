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
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentSelectionProviderTypeExtensionTests
    {
        [Test]
        public void GetProviderTypeValidatesParameters()
        {
            EnvironmentFilter filter = null;
            Assert.Throws<ArgumentException>(() => filter.GetProviderType());
        }

        [Test]
        public void GetProviderReturnsExpectedProviderType()
        {
            EnvironmentFilter filter = FixtureExtensions.CreateEnvironmentFilterFromType(typeof(TestProvider));
            Type actualType = filter.GetProviderType();
            Assert.AreEqual(typeof(TestProvider), actualType);
        }

        [Test]
        public void GetProviderThrowsExceptionWhenTypeDoesNotExist()
        {
            EnvironmentFilter filter = new EnvironmentFilter("Not a type");
            TypeLoadException exc = Assert.Throws<TypeLoadException>(() => filter.GetProviderType());

            Assert.AreEqual("A component provider of type 'Not a type' does not exist in the app domain. ", exc.Message);
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
