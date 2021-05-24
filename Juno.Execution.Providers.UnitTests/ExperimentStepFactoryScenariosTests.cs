namespace Juno.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution.Providers.Watchdog;
    using Juno.Execution.Providers.Workloads;
    using Juno.Providers.Environment;
    using Juno.Providers.Payloads;
    using Juno.Providers.Workloads;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentStepFactoryScenariosTests
    {
        // Note:
        // Due to the complex and varying nature of creating experiment steps from experiment definitions,
        // we are assembling a special set of tests here to validate the behavior of the experiment step factory.
        // Each of the tests below validate a specific scenario for experiment definitions.

        private Fixture mockFixture;
        private ExperimentStepFactory stepFactory;
        private Experiment validExperiment;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.validExperiment = this.mockFixture.Create<Experiment>();
            this.stepFactory = new ExperimentStepFactory();
        }

        [Test]
        public void StepFactoryCreatesTheExpectedExperimentSteps_Simple_A_Scenario1()
        {
            // Scenario:
            // General A/B workflow with shared environment criteria

            List<ExperimentComponent> steps = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: "*"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group A")
            };

            ExperimentInstance experiment = new ExperimentInstance(Guid.NewGuid().ToString(), new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                steps));

            int sequence = 100;

            List<ExperimentStepInstance> stepsCreated = new List<ExperimentStepInstance>(
                this.stepFactory.CreateOrchestrationSteps(experiment.Definition.Workflow, experiment.Id, sequence));

            Assert.IsTrue(stepsCreated.Count == steps.Count);

            // Environment Criteria steps
            Assert.IsTrue(steps.ElementAt(0).Equals(stepsCreated.ElementAt(0).Definition));

            // Environment Setup steps
            Assert.IsTrue(steps.ElementAt(1).Equals(stepsCreated.ElementAt(1).Definition));
            Assert.IsTrue(steps.ElementAt(2).Equals(stepsCreated.ElementAt(2).Definition));

            // Payload steps
            Assert.IsTrue(steps.ElementAt(3).Equals(stepsCreated.ElementAt(3).Definition));

            // Workload steps
            // Because workload steps run on target agents, the expected behavior when the data manager
            // builds the steps is to create an 'agent step monitoring' step in which the actual workload
            // step is a child/target step.
            Assert.AreEqual(stepsCreated.ElementAt(4).Definition.ComponentType, typeof(MonitorAgentWorkload).FullName);
            IEnumerable<ExperimentComponent> childSteps = stepsCreated.ElementAt(4).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(4).Equals(childSteps.First()));

            // watchdog steps
            // Because watchdog steps run on target agents, the expected behavior when the data manager
            // builds the steps is to create an 'agent step monitoring' step in which the actual watchdog
            // step is a child/target step.
            Assert.AreEqual(stepsCreated.ElementAt(5).Definition.ComponentType, typeof(MonitorAgentWatchdog).FullName);
            IEnumerable<ExperimentComponent> childWatchdog = stepsCreated.ElementAt(5).Definition.GetChildSteps();
            Assert.IsNotNull(childWatchdog);
            Assert.IsTrue(childWatchdog.Count() == 1);
            Assert.IsTrue(steps.ElementAt(5).Equals(childWatchdog.First()));

            // Environment Cleanup steps
            Assert.IsTrue(steps.ElementAt(6).Equals(stepsCreated.ElementAt(6).Definition));

            // Validate sequencing is correct
            foreach (ExperimentStepInstance step in stepsCreated)
            {
            }
        }

        [Test]
        public void StepFactoryCreatesTheExpectedExperimentSteps_Simple_A_Scenario_WithParallelExecutionSteps()
        {
            // Scenario:
            // General A/B workflow with shared environment criteria. Validate

            ExperimentComponent parallelExecution1 = ExperimentStepFactoryScenariosTests.CreateParallelExecutionStep(new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
            });

            ExperimentComponent parallelExecution2 = ExperimentStepFactoryScenariosTests.CreateParallelExecutionStep(new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group A"),
            });

            ExperimentComponent parallelExecution3 = ExperimentStepFactoryScenariosTests.CreateParallelExecutionStep(new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group A"),
            });

            List<ExperimentComponent> steps = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: "*"),
                parallelExecution1,
                FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider), group: "Group A"),
                parallelExecution2,
                parallelExecution3,
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group A")
            };

            List<int> expectedSequences = new List<int> { 100, 200, 200, 300, 400, 400, 500, 500, 600 };

            ExperimentInstance experiment = new ExperimentInstance(Guid.NewGuid().ToString(), new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                steps));

            int sequence = 100;

            List<ExperimentStepInstance> stepsCreated = new List<ExperimentStepInstance>(
                this.stepFactory.CreateOrchestrationSteps(experiment.Definition.Workflow, experiment.Id, sequence));

            Assert.IsTrue(stepsCreated.Count == 9);
            CollectionAssert.AreEqual(expectedSequences, stepsCreated.Select(step => step.Sequence));
        }

        [Test]
        public void StepFactoryCreatesTheExpectedExperimentSteps_Simple_A_Scenario2()
        {
            // Scenario:
            // General A/B workflow with group-specific environment criteria

            List<ExperimentComponent> steps = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group A")
            };

            ExperimentInstance experiment = new ExperimentInstance(Guid.NewGuid().ToString(), new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                steps));

            int sequence = 100;

            List<ExperimentStepInstance> stepsCreated = new List<ExperimentStepInstance>(
                this.stepFactory.CreateOrchestrationSteps(experiment.Definition.Workflow, experiment.Id, sequence));

            Assert.IsTrue(stepsCreated.Count == steps.Count);

            // Environment Criteria steps
            Assert.IsTrue(steps.ElementAt(0).Equals(stepsCreated.ElementAt(0).Definition));

            // Environment Setup steps
            Assert.IsTrue(steps.ElementAt(1).Equals(stepsCreated.ElementAt(1).Definition));
            Assert.IsTrue(steps.ElementAt(2).Equals(stepsCreated.ElementAt(2).Definition));

            // Payload steps
            Assert.IsTrue(steps.ElementAt(3).Equals(stepsCreated.ElementAt(3).Definition));

            // Workload steps
            // Because workload steps run on target agents, the expected behavior when the data manager
            // builds the steps is to create an 'agent step monitoring' step in which the actual workload
            // step is a child/target step.
            Assert.AreEqual(stepsCreated.ElementAt(4).Definition.ComponentType, typeof(MonitorAgentWorkload).FullName);
            IEnumerable<ExperimentComponent> childSteps = stepsCreated.ElementAt(4).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(4).Equals(childSteps.First()));

            // Environment Cleanup steps
            Assert.IsTrue(steps.ElementAt(5).Equals(stepsCreated.ElementAt(5).Definition));
        }

        [Test]
        public void StepFactoryCreatesTheExpectedExperimentSteps_Simple_AB_Scenario1()
        {
            // Scenario:
            // General 'A/B' workflow with shared environment criteria

            List<ExperimentComponent> steps = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: "*"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group B")
            };

            ExperimentInstance experiment = new ExperimentInstance(Guid.NewGuid().ToString(), new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                steps));

            int sequence = 100;

            List<ExperimentStepInstance> stepsCreated = new List<ExperimentStepInstance>(
                this.stepFactory.CreateOrchestrationSteps(experiment.Definition.Workflow, experiment.Id, sequence));

            Assert.IsTrue(stepsCreated.Count == steps.Count);

            // Environment Criteria steps
            Assert.IsTrue(steps.ElementAt(0).Equals(stepsCreated.ElementAt(0).Definition));

            // Environment Setup steps
            Assert.IsTrue(steps.ElementAt(1).Equals(stepsCreated.ElementAt(1).Definition));
            Assert.IsTrue(steps.ElementAt(2).Equals(stepsCreated.ElementAt(2).Definition));
            Assert.IsTrue(steps.ElementAt(3).Equals(stepsCreated.ElementAt(3).Definition));
            Assert.IsTrue(steps.ElementAt(4).Equals(stepsCreated.ElementAt(4).Definition));

            // Payload steps
            Assert.IsTrue(steps.ElementAt(5).Equals(stepsCreated.ElementAt(5).Definition));

            // Workload steps
            // Because workload steps run on target agents, the expected behavior when the data manager
            // builds the steps is to create an 'agent step monitoring' step in which the actual workload
            // step is a child/target step.
            Assert.AreEqual(stepsCreated.ElementAt(6).Definition.ComponentType, typeof(MonitorAgentWorkload).FullName);
            IEnumerable<ExperimentComponent> childSteps = stepsCreated.ElementAt(6).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(6).Equals(childSteps.First()));

            Assert.AreEqual(stepsCreated.ElementAt(7).Definition.ComponentType, typeof(MonitorAgentWorkload).FullName);
            childSteps = stepsCreated.ElementAt(7).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(7).Equals(childSteps.First()));

            // Watchdog steps
            // Because watchdog steps run on target agents, the expected behavior when the data manager
            // builds the steps is to create an 'agent step monitoring' step in which the actual workload
            // step is a child/target step.
            Assert.AreEqual(stepsCreated.ElementAt(8).Definition.ComponentType, typeof(MonitorAgentWatchdog).FullName);
            childSteps = stepsCreated.ElementAt(8).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(8).Equals(childSteps.First()));

            Assert.AreEqual(stepsCreated.ElementAt(9).Definition.ComponentType, typeof(MonitorAgentWatchdog).FullName);
            childSteps = stepsCreated.ElementAt(9).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(9).Equals(childSteps.First()));

            // Environment Cleanup steps
            Assert.IsTrue(steps.ElementAt(10).Equals(stepsCreated.ElementAt(10).Definition));
            Assert.IsTrue(steps.ElementAt(11).Equals(stepsCreated.ElementAt(11).Definition));
        }

        [Test]
        public void StepFactoryCreatesTheExpectedExperimentSteps_Simple_AB_Scenario2()
        {
            // Scenario:
            // General 'A/B' workflow with group-specific environment criteria

            List<ExperimentComponent> steps = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group B")
            };

            ExperimentInstance experiment = new ExperimentInstance(Guid.NewGuid().ToString(), new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                steps));

            int sequence = 100;

            List<ExperimentStepInstance> stepsCreated = new List<ExperimentStepInstance>(
                this.stepFactory.CreateOrchestrationSteps(experiment.Definition.Workflow, experiment.Id, sequence));

            Assert.IsTrue(stepsCreated.Count == steps.Count);

            // Environment Criteria steps
            Assert.IsTrue(steps.ElementAt(0).Equals(stepsCreated.ElementAt(0).Definition));
            Assert.IsTrue(steps.ElementAt(1).Equals(stepsCreated.ElementAt(1).Definition));

            // Environment Setup steps
            Assert.IsTrue(steps.ElementAt(2).Equals(stepsCreated.ElementAt(2).Definition));
            Assert.IsTrue(steps.ElementAt(3).Equals(stepsCreated.ElementAt(3).Definition));
            Assert.IsTrue(steps.ElementAt(4).Equals(stepsCreated.ElementAt(4).Definition));
            Assert.IsTrue(steps.ElementAt(5).Equals(stepsCreated.ElementAt(5).Definition));

            // Payload steps
            Assert.IsTrue(steps.ElementAt(6).Equals(stepsCreated.ElementAt(6).Definition));

            // Workload steps
            // Because workload steps run on target agents, the expected behavior when the data manager
            // builds the steps is to create an 'agent step monitoring' step in which the actual workload
            // step is a child/target step.
            Assert.AreEqual(stepsCreated.ElementAt(7).Definition.ComponentType, typeof(MonitorAgentWorkload).FullName);
            IEnumerable<ExperimentComponent> childSteps = stepsCreated.ElementAt(7).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(7).Equals(childSteps.First()));

            Assert.AreEqual(stepsCreated.ElementAt(8).Definition.ComponentType, typeof(MonitorAgentWorkload).FullName);
            childSteps = stepsCreated.ElementAt(8).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(8).Equals(childSteps.First()));

            // Watchdog steps
            // Because workload steps run on target agents, the expected behavior when the data manager
            // builds the steps is to create an 'agent step monitoring' step in which the actual workload
            // step is a child/target step.
            Assert.AreEqual(stepsCreated.ElementAt(9).Definition.ComponentType, typeof(MonitorAgentWatchdog).FullName);
            childSteps = stepsCreated.ElementAt(9).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(9).Equals(childSteps.First()));

            Assert.AreEqual(stepsCreated.ElementAt(10).Definition.ComponentType, typeof(MonitorAgentWatchdog).FullName);
            childSteps = stepsCreated.ElementAt(10).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(10).Equals(childSteps.First()));

            // Environment Cleanup steps
            Assert.IsTrue(steps.ElementAt(11).Equals(stepsCreated.ElementAt(11).Definition));
            Assert.IsTrue(steps.ElementAt(12).Equals(stepsCreated.ElementAt(12).Definition));
        }

        [Test]
        public void StepFactoryCreatesTheExpectedExperimentSteps_Simple_AB_Scenario_WithParallelExecutionSteps()
        {
            // Scenario:
            // General A/B workflow with shared environment criteria. Validate

            ExperimentComponent parallelExecution1 = ExperimentStepFactoryScenariosTests.CreateParallelExecutionStep(new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
            });

            ExperimentComponent parallelExecution2 = ExperimentStepFactoryScenariosTests.CreateParallelExecutionStep(new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group B"),
            });

            ExperimentComponent parallelExecution3 = ExperimentStepFactoryScenariosTests.CreateParallelExecutionStep(new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group B"),
            });

            ExperimentComponent parallelExecution4 = ExperimentStepFactoryScenariosTests.CreateParallelExecutionStep(new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group B"),
            });

            List<ExperimentComponent> steps = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: "*"),
                parallelExecution1,
                FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider), group: "Group B"),
                parallelExecution2,
                parallelExecution3,
                parallelExecution4
            };

            List<int> expectedSequences = new List<int> { 100, 200, 200, 200, 200, 300, 400, 400, 400, 400, 500, 500, 500, 500, 600, 600, 600, 600 };

            ExperimentInstance experiment = new ExperimentInstance(Guid.NewGuid().ToString(), new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                steps));

            int sequence = 100;

            List<ExperimentStepInstance> stepsCreated = new List<ExperimentStepInstance>(
                this.stepFactory.CreateOrchestrationSteps(experiment.Definition.Workflow, experiment.Id, sequence));

            Assert.IsTrue(stepsCreated.Count == 18);
            CollectionAssert.AreEqual(expectedSequences, stepsCreated.Select(step => step.Sequence));
        }

        [Test]
        public void StepFactoryCreatesTheExpectedExperimentSteps_Simple_ABC_Scenario1()
        {
            // Scenario:
            // General 'A/B/C' workflow with shared environment criteria

            List<ExperimentComponent> steps = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: "*"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group C"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group C"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider), group: "Group C"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group C"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), group: "Group C"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group A"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group B"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), group: "Group C")
            };

            ExperimentInstance experiment = new ExperimentInstance(Guid.NewGuid().ToString(), new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                steps));

            int sequence = 100;

            List<ExperimentStepInstance> stepsCreated = new List<ExperimentStepInstance>(
                this.stepFactory.CreateOrchestrationSteps(experiment.Definition.Workflow, experiment.Id, sequence));

            Assert.IsTrue(stepsCreated.Count == steps.Count);

            // Environment Criteria steps
            Assert.IsTrue(steps.ElementAt(0).Equals(stepsCreated.ElementAt(0).Definition));

            // Environment Setup steps
            Assert.IsTrue(steps.ElementAt(1).Equals(stepsCreated.ElementAt(1).Definition));
            Assert.IsTrue(steps.ElementAt(2).Equals(stepsCreated.ElementAt(2).Definition));
            Assert.IsTrue(steps.ElementAt(3).Equals(stepsCreated.ElementAt(3).Definition));
            Assert.IsTrue(steps.ElementAt(4).Equals(stepsCreated.ElementAt(4).Definition));
            Assert.IsTrue(steps.ElementAt(5).Equals(stepsCreated.ElementAt(5).Definition));
            Assert.IsTrue(steps.ElementAt(6).Equals(stepsCreated.ElementAt(6).Definition));

            // Payload steps
            Assert.IsTrue(steps.ElementAt(7).Equals(stepsCreated.ElementAt(7).Definition));
            Assert.IsTrue(steps.ElementAt(8).Equals(stepsCreated.ElementAt(8).Definition));

            // Workload steps
            // Because workload steps run on target agents, the expected behavior when the data manager
            // builds the steps is to create an 'agent step monitoring' step in which the actual workload
            // step is a child/target step.
            Assert.AreEqual(stepsCreated.ElementAt(9).Definition.ComponentType, typeof(MonitorAgentWorkload).FullName);
            IEnumerable<ExperimentComponent> childSteps = stepsCreated.ElementAt(9).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(9).Equals(childSteps.First()));

            Assert.AreEqual(stepsCreated.ElementAt(10).Definition.ComponentType, typeof(MonitorAgentWorkload).FullName);
            childSteps = stepsCreated.ElementAt(10).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(10).Equals(childSteps.First()));

            Assert.AreEqual(stepsCreated.ElementAt(11).Definition.ComponentType, typeof(MonitorAgentWorkload).FullName);
            childSteps = stepsCreated.ElementAt(11).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(11).Equals(childSteps.First()));

            // Workload steps
            // Because workload steps run on target agents, the expected behavior when the data manager
            // builds the steps is to create an 'agent step monitoring' step in which the actual workload
            // step is a child/target step.
            Assert.AreEqual(stepsCreated.ElementAt(12).Definition.ComponentType, typeof(MonitorAgentWatchdog).FullName);
            childSteps = stepsCreated.ElementAt(12).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(12).Equals(childSteps.First()));

            Assert.AreEqual(stepsCreated.ElementAt(13).Definition.ComponentType, typeof(MonitorAgentWatchdog).FullName);
            childSteps = stepsCreated.ElementAt(13).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(13).Equals(childSteps.First()));

            Assert.AreEqual(stepsCreated.ElementAt(14).Definition.ComponentType, typeof(MonitorAgentWatchdog).FullName);
            childSteps = stepsCreated.ElementAt(14).Definition.GetChildSteps();
            Assert.IsNotNull(childSteps);
            Assert.IsTrue(childSteps.Count() == 1);
            Assert.IsTrue(steps.ElementAt(14).Equals(childSteps.First()));

            // Environment Cleanup steps
            Assert.IsTrue(steps.ElementAt(15).Equals(stepsCreated.ElementAt(15).Definition));
            Assert.IsTrue(steps.ElementAt(16).Equals(stepsCreated.ElementAt(16).Definition));
            Assert.IsTrue(steps.ElementAt(17).Equals(stepsCreated.ElementAt(17).Definition));
        }

        private static ExperimentComponent CreateParallelExecutionStep(IEnumerable<ExperimentComponent> childSteps)
        {
            ExperimentComponent parallelExecution = new ExperimentComponent(
                ExperimentComponent.ParallelExecutionType,
                ExperimentComponent.ParallelExecutionType,
                "Any parallel execution step");

            parallelExecution.AddOrReplaceChildSteps(childSteps.ToArray());

            return parallelExecution;
        }
    }
}
