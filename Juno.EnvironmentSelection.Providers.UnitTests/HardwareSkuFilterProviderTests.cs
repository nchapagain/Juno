namespace Juno.EnvironmentSelection.NodeSelectionFilters
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
    public class HardwareSkuFilterProviderTests
    {
        private TestHardwareSkuFilterProvider provider;
        private FixtureDependencies mockFixture;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new FixtureDependencies();
            this.provider = new TestHardwareSkuFilterProvider(this.mockFixture.Services, this.mockFixture.Configuration, this.mockFixture.Logger.Object);
        }

        [Test]
        public void ValidateParametersDoesNotThrowExceptionWhenFilterIsValid()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(HardwareSkuFilterProvider).FullName, new Dictionary<string, IConvertible>()
                    { { "includeCluster", "cluster1" }, { "excludeHwSku", "hwsku1" }, { "includeHwSku", "hwsku1" } });
            this.provider.OnValidateParameters(filter);

            filter = new EnvironmentFilter(typeof(HardwareSkuFilterProvider).FullName, new Dictionary<string, IConvertible>()
                    { { "includeCluster", "cluster1" }, { "includeHwSku", "hwsku1" } });
            this.provider.OnValidateParameters(filter);
        }

        private class TestHardwareSkuFilterProvider : HardwareSkuFilterProvider
        {
            /// <inheritdoc/>
            public TestHardwareSkuFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, configuration, logger)
            {
            }

            public Action<EnvironmentFilter> OnValidateParameters => this.ValidateParameters;
        }
    }
}
