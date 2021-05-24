namespace Juno.EnvironmentSelection
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class KustoFilterProviderExtensionTests
    {
        private TestKustoProvider provider;
        private IServiceCollection mockServices;
        private IConfiguration mockConfiguration;
        private Mock<ICachingFunctions> mockFunctions;
        private ILogger mockLogger;
        private Mock<IKustoQueryIssuer> mockQueryIssuer;
        private string mockQuery;

        [SetUp]
        public void Setup()
        {
            this.mockConfiguration = new ConfigurationBuilder().SetBasePath(Path.Combine(
                 Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                 @"Configuration"))
                .AddJsonFile($"juno-dev01.environmentsettings.json")
                .Build();
            this.mockServices = new ServiceCollection();
            this.mockLogger = NullLogger.Instance;
            this.mockQueryIssuer = new Mock<IKustoQueryIssuer>();
            this.mockServices.AddSingleton<IKustoQueryIssuer>(this.mockQueryIssuer.Object);
            this.mockQuery = "mock query";
            this.mockFunctions = new Mock<ICachingFunctions>();

            this.provider = new TestKustoProvider(this.mockServices, this.mockConfiguration, this.mockLogger, this.mockQuery);

            this.SetupMockFunctions();
        }

        public void SetupMockFunctions()
        {
            this.mockFunctions.Setup(cache => cache.GenerateCacheKey(It.IsAny<DataRow>(), It.IsAny<IEnumerable<KustoColumnAttribute>>()))
                .Returns(new ProviderCacheKey());

            this.mockFunctions.Setup(cache => cache.GenerateCacheValue(It.IsAny<EnvironmentCandidate>()))
                .Returns(Guid.NewGuid().ToString());
        }

        [Test]
        public void ToEnvironmentCandidateValidatesParameters()
        {
            DataTable table = new DataTable();
            DataTable nullTable = null;
            IEnumerable<KustoColumnAttribute> columns = new List<KustoColumnAttribute>();

            Assert.Throws<ArgumentException>(() => nullTable.ToEnvironmentCandidate(this.mockFunctions.Object, columns, ProviderConstants.NodeId, out IDictionary<ProviderCacheKey, IList<string>> cacheEntries));
            Assert.Throws<ArgumentException>(() => table.ToEnvironmentCandidate(this.mockFunctions.Object, null, ProviderConstants.NodeId, out IDictionary<ProviderCacheKey, IList<string>> cacheEntries));
        }

        [Test]
        public void ToEnvironmentCandidateReturnsExpectedResultWhenGivenFullyPopulatedTable()
        {
            DataTable table = this.CreateValidDataTable();
            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>()
            {
                { "node", this.CreateValidEnvironmentCandidate() }
            };

            IEnumerable<KustoColumnAttribute> columns = this.provider.GetType().GetCustomAttributes<KustoColumnAttribute>(true);
            IDictionary<string, EnvironmentCandidate> actualResult = table.ToEnvironmentCandidate(this.mockFunctions.Object, columns, ProviderConstants.NodeId, out IDictionary<ProviderCacheKey, IList<string>> cacheEntries);
            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void ToEnvironmentCandidateReturnsExpectedResultWhenGivenTableWithOnlyNode()
        {
            DataTable table = new DataTable();
            table.Columns.Add(ProviderConstants.NodeId);
            DataRow row = table.NewRow();
            row[ProviderConstants.NodeId] = "node";
            table.Rows.Add(row);

            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>()
            {
                { "node", new EnvironmentCandidate(null, null, null, null, null, "node", null, null) }
            };
            IEnumerable<KustoColumnAttribute> columns = this.provider.GetType().GetCustomAttributes<KustoColumnAttribute>(true);
            IDictionary<string, EnvironmentCandidate> actualResult = table.ToEnvironmentCandidate(this.mockFunctions.Object, columns, ProviderConstants.NodeId, out IDictionary<ProviderCacheKey, IList<string>> cacheEntries);

            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void ToEnvironmentCandidateReturnsExpectedResultWithAdditionalInfoAppended()
        {
            DataTable table = this.CreateValidDataTable();
            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>()
            {
                { "node", this.CreateValidEnvironmentCandidate(true) }
            };

            KustoFilterProvider provider = new TestKustoProviderAdditonalInfo(this.mockServices, this.mockConfiguration, this.mockLogger, this.mockQuery);

            IEnumerable<KustoColumnAttribute> columns = this.provider.GetType().GetCustomAttributes<KustoColumnAttribute>(true);
            IDictionary<string, EnvironmentCandidate> actualResult = table.ToEnvironmentCandidate(this.mockFunctions.Object, columns, ProviderConstants.NodeId, out IDictionary<ProviderCacheKey, IList<string>> cacheEntries);
            Assert.IsNotNull(actualResult);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void ValidateResultReturnsExpectedResultWhenRequiredColumnIsMissing()
        {
            DataTable table = new DataTable();
            table.Columns.Add("not a required column");

            ValidationResult result = this.provider.ValidateResult(table);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.ValidationErrors.Count());
            Assert.AreEqual("Expected column: NodeId in filter type: TestKustoProvider", result.ValidationErrors.First());
        }

        [Test]
        public void ValidateResultReturnsExpectedResultWhenAllRequiredColumnsArePresent()
        {
            DataTable table = this.CreateValidDataTable();

            ValidationResult result = this.provider.ValidateResult(table);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsFalse(result.ValidationErrors.Any());
        }

        private EnvironmentCandidate CreateValidEnvironmentCandidate(bool includeAdditonalInfo = false)
        {
            return new EnvironmentCandidate(
                "subscription", 
                "cluster",
                "region",
                "machinePool",
                "rack",
                "node",
                new List<string>() { "vmsku" },
                "cpuId",
                includeAdditonalInfo ? new Dictionary<string, string>() { { "Column", "additionalInfo" } } : null);
        }

        private DataTable CreateValidDataTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add(new DataColumn(ProviderConstants.NodeId));
            table.Columns.Add(new DataColumn(ProviderConstants.Subscription));
            table.Columns.Add(new DataColumn(ProviderConstants.ClusterId));
            table.Columns.Add(new DataColumn(ProviderConstants.MachinePoolName));
            table.Columns.Add(new DataColumn(ProviderConstants.Rack));
            table.Columns.Add(new DataColumn(ProviderConstants.Region));
            table.Columns.Add(new DataColumn(ProviderConstants.VmSku));
            table.Columns.Add(new DataColumn(ProviderConstants.CpuId));
            table.Columns.Add(new DataColumn("Column"));

            DataRow row = table.NewRow();
            row[ProviderConstants.NodeId] = "node";
            row[ProviderConstants.Subscription] = "subscription";
            row[ProviderConstants.ClusterId] = "cluster";
            row[ProviderConstants.MachinePoolName] = "machinePool";
            row[ProviderConstants.Rack] = "rack";
            row[ProviderConstants.Region] = "region";
            row[ProviderConstants.VmSku] = JsonConvert.SerializeObject(new List<string>() { "vmsku" });
            row[ProviderConstants.CpuId] = "cpuId";
            row["Column"] = "additionInfo";

            table.Rows.Add(row);
            return table;
        }

        [KustoColumn(Name = ProviderConstants.NodeId, AdditionalInfo = false)]
        private class TestKustoProvider : KustoFilterProvider
        {
            public TestKustoProvider(IServiceCollection services, IConfiguration configuration, ILogger logger, string query)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger, query)
            {
            }
        }

        [KustoColumn(Name = "Column", AdditionalInfo = true)]
        private class TestKustoProviderAdditonalInfo : KustoFilterProvider
        {
            public TestKustoProviderAdditonalInfo(IServiceCollection services, IConfiguration configuration, ILogger logger, string query)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger, query)
            {
            }
        }
    }
}
