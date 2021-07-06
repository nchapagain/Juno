namespace Juno.Scheduler.Preconditions
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.DataManagement;
    using Juno.Execution;
    using Juno.Hosting.Common;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    [TestFixture]
    [Category("Integration/Live")]
    public class SuccessfulExperimentsProviderExTests
    {
        private SuccessfulExperimentsProvider provider;
        private IExperimentDataManager dataManager;
        private FixtureDependencies dependencies;
        private Precondition component;

        [SetUp]
        public void SetUp()
        {
            this.dependencies = new FixtureDependencies();
            this.dependencies.SetUpGoalBasedScheduleMocks();
            EnvironmentSettings settings = EnvironmentSettings.Initialize(this.dependencies.Configuration);
            AadPrincipalSettings schedulerPrincipal = settings.SchedulerSettings.AadPrincipals.Get("Scheduler");
            IAzureKeyVault kv = HostDependencies.CreateKeyVaultClient(schedulerPrincipal, settings.KeyVaultSettings.Get("Default"));
            this.dataManager = HostDependencies.CreateExperimentDataManager(settings, new ExperimentStepFactory(), kv);

            this.dependencies.Services.AddSingleton<IExperimentDataManager>(this.dataManager);
            this.provider = new SuccessfulExperimentsProvider(this.dependencies.Services);
            this.component = new Precondition(typeof(SuccessfulExperimentsProvider).FullName, new Dictionary<string, IConvertible>()
            {
                { "targetExperimentInstances", 10 },
                { "daysAgo", 5 }
            });
        }

        [Test]
        public void ExecuteAsyncReturnsResultsFromCosmos()
        {
            string executionGoal = "MicroCodb5783f2f-7b25-4037-b9a6-ef2988cab856";
            string targetGoal = "PERF-CPU-GEEKBENCH-V1";
            ScheduleContext context = new ScheduleContext(
                new Item<GoalBasedSchedule>(executionGoal, this.dependencies.Create<GoalBasedSchedule>()),
                new TargetGoalTrigger(Guid.NewGuid().ToString(), executionGoal, targetGoal, "* * * * *", false, "2021", "teamname", DateTime.UtcNow, DateTime.UtcNow),
                this.dependencies.Configuration);

            Assert.DoesNotThrowAsync(() => this.provider.IsConditionSatisfiedAsync(this.component, context, CancellationToken.None));
        }
    }
}
