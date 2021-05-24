namespace Juno.EnvironmentSelection.ClusterSelectionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ClusterSkuFilterProviderTests
    {
        private TestClusterSkuFilterProvider provider;
        private FixtureDependencies mockFixture;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new FixtureDependencies();
            this.provider = new TestClusterSkuFilterProvider(this.mockFixture.Services, this.mockFixture.Configuration, this.mockFixture.Logger.Object);
        }

        [Test]
        public void ValidateParametersDoesNotThrowExceptionWhenFilterIsValid()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(ClusterSkuFilterProvider).FullName, new Dictionary<string, IConvertible>()
                    { { "includeRegion", "region1" }, { "includeClusterSku", "clusterSku1" }, { "excludeClusterSku", "clusterSku2" } });
            this.provider.OnValidateParameters(filter);

            filter = new EnvironmentFilter(typeof(ClusterSkuFilterProvider).FullName, new Dictionary<string, IConvertible>()
                    { { "includeRegion", "region1" },  { "includeClusterSku", "cluster1" } });
            this.provider.OnValidateParameters(filter);
        }

        private class TestClusterSkuFilterProvider : ClusterSkuFilterProvider
        {
            /// <inheritdoc/>
            public TestClusterSkuFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, configuration, logger)
            {
            }

            public Action<EnvironmentFilter> OnValidateParameters => this.ValidateParameters;
        }
    }
}
