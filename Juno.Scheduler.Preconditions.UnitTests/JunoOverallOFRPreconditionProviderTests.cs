namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Data;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Providers;
    using Juno.Scheduler.Preconditions.Manager;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class JunoOverallOFRPreconditionProviderTests
    {
        private static string ofrThreholdKey = "overallOFRThreshold";
        private Mock<IKustoManager> kustoMgr;
        private IConfiguration configuration;
        private DataTable kustoDataTable;
        private Fixture mockFixture;
        private ScheduleContext mockContext;
        private IServiceCollection services;

        [SetUp]
        public void SetupTest()
        {
            this.kustoMgr = new Mock<IKustoManager>();
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();

            this.services = new ServiceCollection();
            this.services.AddSingleton<IKustoManager>(this.kustoMgr.Object);
            this.configuration = new ConfigurationBuilder().SetBasePath(Path.Combine(
                 Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                 @"Configuration"))
                .AddJsonFile($"juno-dev01.environmentsettings.json")
                .Build();

            this.mockContext = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), this.configuration);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenTheOFRThresholdPreconditionIsMet()
        {
            int ofrThreshold = 4;
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoOverallOFRPreconditionProviderTests.ofrThreholdKey, ofrThreshold);

            // creating Datatable with 3 ofr nodes
            this.kustoDataTable = JunoOverallOFRPreconditionProviderTests.GetValidDataTable();
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(this.kustoDataTable);

            PreconditionProvider provider = new JunoOverallOFRPreconditionProvider(this.services);
            var response = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(response);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenTheOFRThresholdPreconditionIsNotMet()
        {
            int ofrThreshold = 2;

            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoOverallOFRPreconditionProviderTests.ofrThreholdKey, ofrThreshold);

            // creating Datatable with 3 ofr nodes
            this.kustoDataTable = JunoOverallOFRPreconditionProviderTests.GetValidDataTable();
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(this.kustoDataTable);

            PreconditionProvider provider = new JunoOverallOFRPreconditionProvider(this.services);
            var response = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(response);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenResultSetsAreEmpty()
        {
            int ofrThreshold = 2;
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoOverallOFRPreconditionProviderTests.ofrThreholdKey, ofrThreshold);

            // Kusto Manager will return empty result set
            DataTable emptyKustoDataTable = null;
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(emptyKustoDataTable);

            PreconditionProvider provider = new JunoOverallOFRPreconditionProvider(this.services);
            var response = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(response);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenComponentsDateTimeFieldIsInvalid()
        {
            int ofrThreshold = 2;
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoOverallOFRPreconditionProviderTests.ofrThreholdKey, ofrThreshold);

            // creating Datatable with 3 ofr nodes
            this.kustoDataTable = JunoOverallOFRPreconditionProviderTests.GetDataTableWithInvalidDateTimeField();
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(this.kustoDataTable);

            PreconditionProvider provider = new JunoOverallOFRPreconditionProvider(this.services);
            Assert.ThrowsAsync<FormatException>(() => provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None));
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenComponentSchemaIsInvalid()
        {
            int ofrThreshold = 2;
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoOverallOFRPreconditionProviderTests.ofrThreholdKey, ofrThreshold);

            // creating Datatable with 3 ofr nodes
            this.kustoDataTable = JunoOverallOFRPreconditionProviderTests.GetInvalidDataTable();
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(this.kustoDataTable);

            PreconditionProvider provider = new JunoOverallOFRPreconditionProvider(this.services);
            Assert.ThrowsAsync<ArgumentException>(() => provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None));
        }

        internal static DataTable GetDataTableWithInvalidDateTimeField()
        {
            // Here we create a DataTable with wrong TIMESTAMP field since month 13 and date 35 doesnot exist.
            DataTable table = new DataTable();

            DataColumn timestampColumn = new DataColumn("TIMESTAMP", typeof(string));
            DataColumn nodeIdColumn = new DataColumn("nodeId", typeof(string));
            DataColumn tipSessionColumn = new DataColumn("tipNodeSessionId", typeof(string));

            table.Columns.Add(timestampColumn);
            table.Columns.Add(nodeIdColumn);
            table.Columns.Add(tipSessionColumn);

            for (int i = 0; i < 3; i++)
            {
                DataRow newRow = table.NewRow();
                newRow["TIMESTAMP"] = "2020-13-35";
                newRow["nodeId"] = Guid.NewGuid();
                newRow["tipNodeSessionId"] = Guid.NewGuid();
                table.Rows.Add(newRow);
            }

            return table;
        }

        internal static DataTable GetInvalidDataTable()
        {
            // Here we create a DataTable with two columns and two rows.
            DataTable table = new DataTable();

            DataColumn idColumn = new DataColumn("Id", typeof(string));
            DataColumn nameColumn = new DataColumn("Name", typeof(string));

            table.Columns.Add(idColumn);
            table.Columns.Add(nameColumn);

            for (int i = 0; i < 2; i++)
            {
                DataRow newRow = table.NewRow();
                newRow["Id"] = i;
                newRow["Name"] = "JohnDoe";
                table.Rows.Add(newRow);
            }

            return table;
        }

        private static DataTable GetValidDataTable()
        {
            // Here we create a DataTable with three columns and three rows.
            DataTable table = new DataTable();

            DataColumn timestampColumn = new DataColumn("TIMESTAMP", typeof(string));
            DataColumn nodeIdColumn = new DataColumn("nodeId", typeof(string));
            DataColumn tipSessionColumn = new DataColumn("tipNodeSessionId", typeof(string));

            table.Columns.Add(timestampColumn);
            table.Columns.Add(nodeIdColumn);
            table.Columns.Add(tipSessionColumn);

            for (int i = 0; i < 3; i++)
            {
                DataRow newRow = table.NewRow();
                newRow["TIMESTAMP"] = DateTime.UtcNow.AddDays(-2).ToString();
                newRow["nodeId"] = Guid.NewGuid();
                newRow["tipNodeSessionId"] = Guid.NewGuid();
                table.Rows.Add(newRow);
            }

            return table;
        }
    }
}
