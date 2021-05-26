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
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class JunoDailyOFRPreconditionProviderTests
    {
        private static string ofrThreholdKey = "dailyOFRThreshold";
        private Mock<IKustoManager> kustoMgr;
        private IConfiguration configuration;
        private DataTable kustoDataTable;
        private Fixture mockFixture;
        private IServiceCollection services;
        private ScheduleContext mockContext;

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
            int dailyOFRThreshold = 11;

            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoDailyOFRPreconditionProviderTests.ofrThreholdKey, dailyOFRThreshold);

            // creating Datatable with 10 overall ofr nodes with 5 ofr nodes today // using 
            this.kustoDataTable = JunoDailyOFRPreconditionProviderTests.GetValidDataTable();
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(this.kustoDataTable);

            PreconditionProvider provider = new JunoDailyOFRPreconditionProvider(this.services);

            var response = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(response);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenTheOFRThresholdPreconditionIsNotMet()
        {
            int dailyOFRThreshold = 5;

            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoDailyOFRPreconditionProviderTests.ofrThreholdKey, dailyOFRThreshold);

            // creating Datatable with 10 overall ofr nodes with 5 ofr nodes today
            this.kustoDataTable = JunoDailyOFRPreconditionProviderTests.GetValidDataTable();
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(this.kustoDataTable);

            PreconditionProvider provider = new JunoDailyOFRPreconditionProvider(this.services);

            var response = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(response);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenResultSetsAreEmpty()
        {
            int dailyOFRThreshold = 2;
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoDailyOFRPreconditionProviderTests.ofrThreholdKey, dailyOFRThreshold);

            // Kusto Manager will return empty result set
            DataTable emptyKustoDataTable = null;
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(emptyKustoDataTable);

            PreconditionProvider provider = new JunoDailyOFRPreconditionProvider(this.services);
            var response = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(response);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenComponentsDateTimeFieldIsInvalid()
        {
            int dailyOFRThreshold = 2;
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoDailyOFRPreconditionProviderTests.ofrThreholdKey, dailyOFRThreshold);

            // creating Datatable with 3 ofr nodes
            this.kustoDataTable = JunoOverallOFRPreconditionProviderTests.GetDataTableWithInvalidDateTimeField();
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(this.kustoDataTable);

            PreconditionProvider provider = new JunoDailyOFRPreconditionProvider(this.services);
            Assert.ThrowsAsync<FormatException>(() => provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None));
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenComponentSchemaIsInvalid()
        {
            int dailyOFRThreshold = 2;

            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoDailyOFRPreconditionProviderTests.ofrThreholdKey, dailyOFRThreshold);

            // creating Datatable with 10 overall ofr nodes with 5 ofr nodes today
            this.kustoDataTable = JunoOverallOFRPreconditionProviderTests.GetInvalidDataTable();
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(this.kustoDataTable);

            PreconditionProvider provider = new JunoDailyOFRPreconditionProvider(this.services);

            Assert.ThrowsAsync<ArgumentException>(() => provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None));
        }

        private static DataTable GetValidDataTable()
        {
            // Here we create a DataTable with three columns and ten rows.
            // 10 overall ofr nodes with 5 ofr nodes today
            DataTable table = new DataTable();

            DataColumn timestampColumn = new DataColumn("TIMESTAMP", typeof(string));
            DataColumn nodeIdColumn = new DataColumn("nodeId", typeof(string));
            DataColumn tipSessionColumn = new DataColumn("tipNodeSessionId", typeof(string));

            table.Columns.Add(timestampColumn);
            table.Columns.Add(nodeIdColumn);
            table.Columns.Add(tipSessionColumn);

            for (int i = 0; i < 10; i++)
            {
                DataRow newRow = table.NewRow();
                newRow["TIMESTAMP"] = DateTime.UtcNow.AddHours(-i * 6).ToString();
                newRow["nodeId"] = Guid.NewGuid();
                newRow["tipNodeSessionId"] = Guid.NewGuid();
                table.Rows.Add(newRow);
            }

            return table;
        }
    }
}
