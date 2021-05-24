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
    public class CpuDescriptionFilterProviderTests
    {
        private TestCpuDescriptionFilterProvider provider;
        private FixtureDependencies mockFixture;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new FixtureDependencies();
            this.provider = new TestCpuDescriptionFilterProvider(this.mockFixture.Services, this.mockFixture.Configuration, this.mockFixture.Logger.Object);
        }

        [Test]
        public void ValidateParametersThrowsExceptionWhenBothParametersAreMissing()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(CpuDescriptionFilterProvider).FullName, new Dictionary<string, IConvertible>() 
            { { "includeCluster", "cluster1" } });
            Assert.Throws<SchemaException>(() => this.provider.OnValidateParameters(filter));
        }

        [Test]
        public void ValidateParametersThrowsExceptionWhenBothparametersArePresent()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(CpuDescriptionFilterProvider).FullName, new Dictionary<string, IConvertible>() 
            { { "includeCluster", "cluster1" }, { "includeCpuDescription", "cpu1" }, { "excludeCpuDescription", "cpu2" } });
            Assert.Throws<SchemaException>(() => this.provider.OnValidateParameters(filter));
        }

        [Test]
        public void ValidateParametersThrowsExceptionWhenParamaterIsImplicitList()
        {
            EnvironmentFilter filter = new EnvironmentFilter(typeof(CpuDescriptionFilterProvider).FullName, new Dictionary<string, IConvertible>()
            { { "includeCluster", "cluster1" }, { "includeCpuDescription", "cpu1,cpu2" } });
            Assert.Throws<SchemaException>(() => this.provider.OnValidateParameters(filter));
        }

        [Test]
        public void ValidateParametersDoesNotThrowExceptionWhenFilterIsValid()
        {
            try
            {
                EnvironmentFilter filter = new EnvironmentFilter(typeof(CpuDescriptionFilterProvider).FullName, new Dictionary<string, IConvertible>()
                    { { "includeCluster", "cluster1" }, { "includeCpuDescription", "cpu1" } });
                this.provider.OnValidateParameters(filter);

                filter = new EnvironmentFilter(typeof(CpuDescriptionFilterProvider).FullName, new Dictionary<string, IConvertible>()
                    { { "includeCluster", "cluster1" }, { "excludeCpuDescription", "cpu1" } });
                this.provider.OnValidateParameters(filter);
            }
            catch (SchemaException)
            {
                Assert.Fail();
            }

            Assert.Pass();
        }

        private class TestCpuDescriptionFilterProvider : CpuDescriptionFilterProvider
        {
            /// <inheritdoc/>
            public TestCpuDescriptionFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, configuration, logger)
            {
            }

            public Action<EnvironmentFilter> OnValidateParameters => this.ValidateParameters;
        }
    }
}
