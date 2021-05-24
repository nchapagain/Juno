namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using AutoFixture;
    using Juno.Providers.Environment;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentContextTests
    {
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private Experiment mockExperiment;
        private ExperimentStepInstance mockExperimentStep;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockDependencies = new FixtureDependencies();
            this.mockFixture.SetupExperimentMocks();

            this.mockExperiment = this.mockFixture.Create<Experiment>();
            this.mockExperimentStep = this.mockFixture.Create<ExperimentStepInstance>();
        }

        [Test]
        public void ExperimentContextDiscoversTheCorrectExperimentGroupsAssociatedWithAn_A_Experiment()
        {
            Experiment experiment = new Experiment(
                this.mockExperiment.Name,
                this.mockExperiment.Description,
                this.mockExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A")
                });

            ExperimentInstance experimentInstance = new ExperimentInstance(Guid.NewGuid().ToString(), experiment);
            ExperimentContext context = new ExperimentContext(experimentInstance, this.mockExperimentStep, this.mockDependencies.Configuration);

            IEnumerable<string> expectedGroups = new List<string> { "Group A" };
            IEnumerable<string> actualGroups = context.GetExperimentGroups();

            CollectionAssert.AreEquivalent(expectedGroups, actualGroups);
        }

        [Test]
        public void ExperimentContextDiscoversTheCorrectExperimentGroupsAssociatedWithAn_A_Experiment_WithWildcardGroup()
        {
            Experiment experiment = new Experiment(
                this.mockExperiment.Name,
                this.mockExperiment.Description,
                this.mockExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: ExperimentComponent.AllGroups),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A")
                });

            ExperimentInstance experimentInstance = new ExperimentInstance(Guid.NewGuid().ToString(), experiment);
            ExperimentContext context = new ExperimentContext(experimentInstance, this.mockExperimentStep, this.mockDependencies.Configuration);

            IEnumerable<string> expectedGroups = new List<string> { "Group A" };
            IEnumerable<string> actualGroups = context.GetExperimentGroups();

            CollectionAssert.AreEquivalent(expectedGroups, actualGroups);
        }

        [Test]
        public void ExperimentContextDiscoversTheCorrectExperimentGroupsAssociatedWithAn_A_Experiment_GivenParallelExecutionStepsExist()
        {
            Experiment experiment = new Experiment(
                this.mockExperiment.Name,
                this.mockExperiment.Description,
                this.mockExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    // Parallel execution components do not need to supply a 'group'. Even if the do, the 'group' is
                    // not used.
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A")),

                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A")
                });

            ExperimentInstance experimentInstance = new ExperimentInstance(Guid.NewGuid().ToString(), experiment);
            ExperimentContext context = new ExperimentContext(experimentInstance, this.mockExperimentStep, this.mockDependencies.Configuration);

            IEnumerable<string> expectedGroups = new List<string> { "Group A" };
            IEnumerable<string> actualGroups = context.GetExperimentGroups();

            CollectionAssert.AreEquivalent(expectedGroups, actualGroups);
        }

        [Test]
        public void ExperimentContextDiscoversTheCorrectExperimentGroupsAssociatedWithAn_AB_Experiment()
        {
            Experiment experiment = new Experiment(
                this.mockExperiment.Name,
                this.mockExperiment.Description,
                this.mockExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B")
                });

            ExperimentInstance experimentInstance = new ExperimentInstance(Guid.NewGuid().ToString(), experiment);
            ExperimentContext context = new ExperimentContext(experimentInstance, this.mockExperimentStep, this.mockDependencies.Configuration);

            IEnumerable<string> expectedGroups = new List<string> { "Group A", "Group B" };
            IEnumerable<string> actualGroups = context.GetExperimentGroups();

            CollectionAssert.AreEquivalent(expectedGroups, actualGroups);
        }

        [Test]
        public void ExperimentContextDiscoversTheCorrectExperimentGroupsAssociatedWithAn_AB_Experiment_WithWildcardGroup()
        {
            Experiment experiment = new Experiment(
                this.mockExperiment.Name,
                this.mockExperiment.Description,
                this.mockExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: ExperimentComponent.AllGroups),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B")
                });

            ExperimentInstance experimentInstance = new ExperimentInstance(Guid.NewGuid().ToString(), experiment);
            ExperimentContext context = new ExperimentContext(experimentInstance, this.mockExperimentStep, this.mockDependencies.Configuration);

            IEnumerable<string> expectedGroups = new List<string> { "Group A", "Group B" };
            IEnumerable<string> actualGroups = context.GetExperimentGroups();

            CollectionAssert.AreEquivalent(expectedGroups, actualGroups);
        }

        [Test]
        public void ExperimentContextDiscoversTheCorrectExperimentGroupsAssociatedWithAn_AB_Experiment_GivenParallelExecutionStepsExist()
        {
            Experiment experiment = new Experiment(
                this.mockExperiment.Name,
                this.mockExperiment.Description,
                this.mockExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    // Parallel execution components do not need to supply a 'group'. Even if the do, the 'group' is
                    // not used.
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B")),

                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B")
                });

            ExperimentInstance experimentInstance = new ExperimentInstance(Guid.NewGuid().ToString(), experiment);
            ExperimentContext context = new ExperimentContext(experimentInstance, this.mockExperimentStep, this.mockDependencies.Configuration);

            IEnumerable<string> expectedGroups = new List<string> { "Group A", "Group B" };
            IEnumerable<string> actualGroups = context.GetExperimentGroups();

            CollectionAssert.AreEquivalent(expectedGroups, actualGroups);
        }

        [Test]
        public void ExperimentContextDiscoversTheCorrectExperimentGroupsAssociatedWithAn_ABC_Experiment()
        {
            Experiment experiment = new Experiment(
                this.mockExperiment.Name,
                this.mockExperiment.Description,
                this.mockExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group C"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group C"),
                });

            ExperimentInstance experimentInstance = new ExperimentInstance(Guid.NewGuid().ToString(), experiment);
            ExperimentContext context = new ExperimentContext(experimentInstance, this.mockExperimentStep, this.mockDependencies.Configuration);

            IEnumerable<string> expectedGroups = new List<string> { "Group A", "Group B", "Group C" };
            IEnumerable<string> actualGroups = context.GetExperimentGroups();

            CollectionAssert.AreEquivalent(expectedGroups, actualGroups);
        }

        [Test]
        public void ExperimentContextDiscoversTheCorrectExperimentGroupsAssociatedWithAn_ABC_Experiment_WithWildcardGroup()
        {
            Experiment experiment = new Experiment(
                this.mockExperiment.Name,
                this.mockExperiment.Description,
                this.mockExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: ExperimentComponent.AllGroups),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group C"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group C"),
                });

            ExperimentInstance experimentInstance = new ExperimentInstance(Guid.NewGuid().ToString(), experiment);
            ExperimentContext context = new ExperimentContext(experimentInstance, this.mockExperimentStep, this.mockDependencies.Configuration);

            IEnumerable<string> expectedGroups = new List<string> { "Group A", "Group B", "Group C" };
            IEnumerable<string> actualGroups = context.GetExperimentGroups();

            CollectionAssert.AreEquivalent(expectedGroups, actualGroups);
        }

        [Test]
        public void ExperimentContextDiscoversTheCorrectExperimentGroupsAssociatedWithAn_ABC_Experiment_GivenParallelExecutionStepsExist()
        {
            Experiment experiment = new Experiment(
                this.mockExperiment.Name,
                this.mockExperiment.Description,
                this.mockExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    // Parallel execution components do not need to supply a 'group'. Even if the do, the 'group' is
                    // not used.
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group C")),

                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group C")
                });

            ExperimentInstance experimentInstance = new ExperimentInstance(Guid.NewGuid().ToString(), experiment);
            ExperimentContext context = new ExperimentContext(experimentInstance, this.mockExperimentStep, this.mockDependencies.Configuration);

            IEnumerable<string> expectedGroups = new List<string> { "Group A", "Group B", "Group C" };
            IEnumerable<string> actualGroups = context.GetExperimentGroups();

            CollectionAssert.AreEquivalent(expectedGroups, actualGroups);
        }

        [Test]
        public void ExperimentContextDiscoversTheCorrectExperimentGroupsAssociatedWithAn_ABC_Experiment_WithWildcardGroup_GivenParallelExecutionStepsExist()
        {
            Experiment experiment = new Experiment(
                this.mockExperiment.Name,
                this.mockExperiment.Description,
                this.mockExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    // Parallel execution components do not need to supply a 'group'. Even if the do, the 'group' is
                    // not used.
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: ExperimentComponent.AllGroups),
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group C")),

                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: ExperimentComponent.AllGroups),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group C")
                });

            ExperimentInstance experimentInstance = new ExperimentInstance(Guid.NewGuid().ToString(), experiment);
            ExperimentContext context = new ExperimentContext(experimentInstance, this.mockExperimentStep, this.mockDependencies.Configuration);

            IEnumerable<string> expectedGroups = new List<string> { "Group A", "Group B", "Group C" };
            IEnumerable<string> actualGroups = context.GetExperimentGroups();

            CollectionAssert.AreEquivalent(expectedGroups, actualGroups);
        }

        [Test]
        public void ExperimentContextDiscoversTheCorrectExperimentGroupsAssociatedWithAn_ABC_Experiment_GivenParallelExecutionStepsExist_WithUnusualNesting()
        {
            Experiment experiment = new Experiment(
                this.mockExperiment.Name,
                this.mockExperiment.Description,
                this.mockExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    // Parallel execution components do not need to supply a 'group'. Even if the do, the 'group' is
                    // not used.
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B")),

                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group C")),

                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group D")
                });

            ExperimentInstance experimentInstance = new ExperimentInstance(Guid.NewGuid().ToString(), experiment);
            ExperimentContext context = new ExperimentContext(experimentInstance, this.mockExperimentStep, this.mockDependencies.Configuration);

            IEnumerable<string> expectedGroups = new List<string> { "Group A", "Group B", "Group C", "Group D" };
            IEnumerable<string> actualGroups = context.GetExperimentGroups();

            CollectionAssert.AreEquivalent(expectedGroups, actualGroups);
        }
    }
}
