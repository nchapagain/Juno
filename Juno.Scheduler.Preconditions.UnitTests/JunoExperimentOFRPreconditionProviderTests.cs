﻿namespace Juno.Scheduler.Preconditions
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
    public class JunoExperimentOFRPreconditionProviderTests
    {
        private static string ofrThreholdKey = "experimentOFRThreshold";
        private static string experimentNameKey = "experimentName";
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
            int experimentOFRThreshold = 3;
            string experimentName = "MCU2019.2 Perf/CPU Patrol Scrubber";
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoExperimentOFRPreconditionProviderTests.ofrThreholdKey, experimentOFRThreshold);
            component.Parameters.Add(JunoExperimentOFRPreconditionProviderTests.experimentNameKey, experimentName);

            // creating Datatable with 3 overall ofr nodes
            this.kustoDataTable = JunoExperimentOFRPreconditionProviderTests.GetValidDataTable();
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(this.kustoDataTable);

            PreconditionProvider provider = new JunoExperimentGoalOFRPreconditionProvider(this.services);
            var response = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(response.Status, ExecutionStatus.Succeeded);
            Assert.IsTrue(response.Satisfied);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenTheOFRThresholdPreconditionIsNotMet()
        {
            int experimentOFRThreshold = 5;
            string experimentName = "MCU2019.2 Perf/CPU Patrol Scrubber";
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoExperimentOFRPreconditionProviderTests.ofrThreholdKey, experimentOFRThreshold);
            component.Parameters.Add(JunoExperimentOFRPreconditionProviderTests.experimentNameKey, experimentName);

            // creating Datatable with 3 overall ofr nodes for one experimentId
            this.kustoDataTable = JunoExperimentOFRPreconditionProviderTests.GetValidDataTable();
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(this.kustoDataTable);

            PreconditionProvider provider = new JunoExperimentGoalOFRPreconditionProvider(this.services);
            var response = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(response.Status, ExecutionStatus.Succeeded);
            Assert.IsFalse(response.Satisfied);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenResultSetsAreEmpty()
        {
            int experimentOFRThreshold = 3;
            string experimentName = "MCU2019.2 Perf/CPU Patrol Scrubber";
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoExperimentOFRPreconditionProviderTests.ofrThreholdKey, experimentOFRThreshold);
            component.Parameters.Add(JunoExperimentOFRPreconditionProviderTests.experimentNameKey, experimentName);

            // Kusto Manager will return empty result set
            DataTable emptyKustoDataTable = null;
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(emptyKustoDataTable);

            PreconditionProvider provider = new JunoExperimentGoalOFRPreconditionProvider(this.services);
            var response = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(response.Status, ExecutionStatus.Succeeded);
            Assert.IsFalse(response.Satisfied);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenComponentsDateTimeFieldIsInvalid()
        {
            int experimentOFRThreshold = 2;
            string experimentName = "MCU2019.2 Perf/CPU Patrol Scrubber";
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoExperimentOFRPreconditionProviderTests.ofrThreholdKey, experimentOFRThreshold);
            component.Parameters.Add(JunoExperimentOFRPreconditionProviderTests.experimentNameKey, experimentName);

            // creating Datatable with 2 ofr nodes
            this.kustoDataTable = JunoOverallOFRPreconditionProviderTests.GetDataTableWithInvalidDateTimeField();
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(this.kustoDataTable);

            PreconditionProvider provider = new JunoExperimentGoalOFRPreconditionProvider(this.services);
            var response = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(response.Status, ExecutionStatus.Failed);
            Assert.IsFalse(response.Satisfied);
        }

        [Test]
        public void ProviderReturnsTheExpectedResponseWhenComponentSchemaIsInvalid()
        {
            int experimentOFRThreshold = 2;
            string experimentName = "MCU2019.2 Perf/CPU Patrol Scrubber";
            Precondition component = this.mockFixture.Create<Precondition>();
            component.Parameters.Add(JunoExperimentOFRPreconditionProviderTests.ofrThreholdKey, experimentOFRThreshold);
            component.Parameters.Add(JunoExperimentOFRPreconditionProviderTests.experimentNameKey, experimentName);

            // creating Datatable with 2 ofr nodes
            this.kustoDataTable = JunoOverallOFRPreconditionProviderTests.GetInvalidDataTable();
            this.kustoMgr.Setup(x => x.GetKustoResponseAsync(It.IsAny<string>(), It.IsAny<KustoSettings>(), It.IsAny<string>(), It.IsAny<double?>())).ReturnsAsync(this.kustoDataTable);

            PreconditionProvider provider = new JunoExperimentGoalOFRPreconditionProvider(this.services);
            var response = provider.IsConditionSatisfiedAsync(component, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(response.Status, ExecutionStatus.Failed);
            Assert.IsFalse(response.Satisfied);
        }

        private static DataTable GetValidDataTable()
        {
            // Here we create a DataTable with three columns and three rows.
            DataTable table = new DataTable();

            DataColumn timestampColumn = new DataColumn("TIMESTAMP", typeof(string));
            DataColumn nodeIdColumn = new DataColumn("nodeId", typeof(string));
            DataColumn tipSessionColumn = new DataColumn("tipNodeSessionId", typeof(string));
            DataColumn experimentNameColumn = new DataColumn("experimentName", typeof(string));
            DataColumn experimentIdColumn = new DataColumn("experimentId", typeof(string));

            table.Columns.Add(timestampColumn);
            table.Columns.Add(nodeIdColumn);
            table.Columns.Add(tipSessionColumn);
            table.Columns.Add(experimentNameColumn);
            table.Columns.Add(experimentIdColumn);

            for (int i = 0; i < 3; i++)
            {
                DataRow newRow = table.NewRow();
                newRow["TIMESTAMP"] = DateTime.UtcNow.AddDays(-1).ToString();
                newRow["nodeId"] = Guid.NewGuid();
                newRow["tipNodeSessionId"] = Guid.NewGuid();
                newRow["experimentName"] = "Patrol Scrubber";
                newRow["experimentId"] = Guid.NewGuid();
                table.Rows.Add(newRow);
            }

            return table;
        }
    }
}
