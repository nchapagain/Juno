namespace Juno.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution.Providers;
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Execution.Providers.Environment;
    using Juno.Execution.Providers.Payloads;
    using Juno.Execution.Providers.Workloads;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentStepFactoryTests
    {
        private Fixture mockFixture;
        private ExperimentStepFactory stepFactory;
        private ExperimentInstance exampleExperiment;
        private ExperimentStepInstance exampleParentStep;
        private string exampleAgentId;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);

            this.stepFactory = new ExperimentStepFactory();
            this.exampleExperiment = this.mockFixture.Create<ExperimentInstance>();
            this.exampleParentStep = this.mockFixture.Create<ExperimentStepInstance>();
            this.exampleAgentId = "Cluster,Node,VM,TiPSession";
        }

        [Test]
        public void FactoryCreatesTheExpectedAgentStepForAGivenComponent()
        {
            // Note:
            // The correctness of the assertions is based on the metadata defined in the attribute of the
            // test providers below. If those provider classes are changed, then this test will be invalidated
            // and will need to be updated.
            ExperimentComponent component = FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesOnVm));

            DateTime referenceTime = DateTime.Now;
            Task.Delay(5).GetAwaiter().GetResult(); // Just to ensure a valid comparison for Created/LastModified dates

            int expectedSequence = 100;
            IEnumerable<ExperimentStepInstance> steps = this.stepFactory.CreateAgentSteps(
                component,
                this.exampleAgentId,
                this.exampleParentStep.Id,
                this.exampleExperiment.Id,
                expectedSequence);

            Assert.IsNotNull(steps);
            Assert.IsNotEmpty(steps);
            Assert.IsTrue(steps.Count() == 1);

            ExperimentStepInstance step = steps.First();
            Assert.IsTrue(component.Equals(step.Definition));
            Assert.AreEqual(this.exampleExperiment.Id, step.ExperimentId);
            Assert.AreEqual(this.exampleAgentId, step.AgentId);
            Assert.AreEqual(this.exampleParentStep.Id, step.ParentStepId);
            Assert.AreEqual(component.Group, step.ExperimentGroup);
            Assert.AreEqual(ExecutionStatus.Pending, step.Status);
            Assert.AreEqual(component.GetSupportedStepType(), step.StepType);
            Assert.AreEqual(SupportedStepTarget.ExecuteOnVirtualMachine, component.GetSupportedStepTarget());
            Assert.AreEqual(0, step.Attempts);
            Assert.AreEqual(expectedSequence, step.Sequence);
            Assert.IsTrue(step.Created > referenceTime);
            Assert.IsTrue(step.LastModified > referenceTime);
            Assert.IsNull(step.EndTime);
            Assert.IsNull(step.StartTime);
        }

        [Test]
        public void FactoryAppliesTheCorrectSequencesToASetOfAgentSteps()
        {
            // Note:
            // The correctness of the assertions is based on the metadata defined in the attribute of the
            // test providers below. If those provider classes are changed, then this test will be invalidated
            // and will need to be updated.
            List<ExperimentComponent> components = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesOnVm)),
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesOnVm)),
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesOnVm))
            };

            int expectedSequence = 100;
            IEnumerable<ExperimentStepInstance> steps = this.stepFactory.CreateAgentSteps(
                components,
                this.exampleAgentId,
                this.exampleParentStep.Id,
                this.exampleExperiment.Id,
                expectedSequence);

            Assert.IsNotNull(steps);
            Assert.IsNotEmpty(steps);

            foreach (ExperimentStepInstance step in steps)
            {
                Assert.IsTrue(step.Sequence == expectedSequence);
                expectedSequence += 100; // The default sequence increment
            }
        }

        [Test]
        public void FactoryAppliesTheCorrectSequencesToASetOfAgentStepsWhenParallelExecutionIsRequired()
        {
            List<ExperimentComponent> childSteps = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesOnVm)),
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesOnVm)),
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesOnVm))
            };

            ExperimentComponent parallelExecution = ExperimentStepFactoryTests.CreateParallelExecutionStep(childSteps);

            // Factory method overload that takes in a single experiment component
            int sequence = 100;
            IEnumerable<ExperimentStepInstance> steps = this.stepFactory.CreateAgentSteps(
                parallelExecution,
                this.exampleAgentId,
                this.exampleParentStep.Id,
                this.exampleExperiment.Id,
                sequence);

            Assert.IsNotNull(steps);
            Assert.IsNotEmpty(steps);

            foreach (ExperimentStepInstance step in steps)
            {
                Assert.IsTrue(step.Sequence == sequence);
            }
        }

        [Test]
        public void FactoryThrowsWhenCreatingAnAgentStepIfAComponentDoesNotSupportRunningOnAnAgent()
        {
            ExperimentComponent component = FixtureExtensions.CreateExperimentComponent(
                typeof(TestEnvironmentCriteriaExecutesRemotely),
                "Cannot run on an agent");

            Assert.Throws<ArgumentException>(() => this.stepFactory.CreateAgentSteps(
               component,
               this.exampleAgentId,
               this.exampleParentStep.Id,
               this.exampleExperiment.Id,
               0));
        }

        [Test]
        public void FactoryCreatesTheExpectedOrchestrationStepsForComponentsThatExecuteRemotely()
        {
            // Note:
            // The correctness of the assertions is based on the metadata defined in the attribute of the
            // test providers below. If those provider classes are changed, then this test will be invalidated
            // and will need to be updated.
            List<Type> providerTypes = new List<Type>
            {
                typeof(TestEnvironmentCleanupExecutesRemotely),
                typeof(TestEnvironmentSetupExecutesRemotely),
                typeof(TestPayloadExecutesRemotely),
                typeof(TestWorkloadExecutesRemotely)
            };

            int expectedSequence = 100;
            foreach (Type providerType in providerTypes)
            {
                ExperimentComponent component = FixtureExtensions.CreateExperimentComponent(providerType);

                DateTime referenceTime = DateTime.Now;
                Task.Delay(5).GetAwaiter().GetResult(); // Just to ensure a valid comparison for Created/LastModified dates

                IEnumerable<ExperimentStepInstance> steps = this.stepFactory.CreateOrchestrationSteps(component, this.exampleExperiment.Id, expectedSequence);
                Assert.IsNotNull(steps);
                Assert.IsNotEmpty(steps);
                Assert.IsTrue(steps.Count() == 1);

                ExperimentStepInstance step = steps.First();
                Assert.IsNotNull(step);
                Assert.IsTrue(component.Equals(step.Definition));
                Assert.AreEqual(this.exampleExperiment.Id, step.ExperimentId);
                Assert.AreEqual(component.Group, step.ExperimentGroup);
                Assert.AreEqual(ExecutionStatus.Pending, step.Status);
                Assert.AreEqual(component.GetSupportedStepType(), step.StepType);
                Assert.AreEqual(expectedSequence, step.Sequence);
                Assert.AreEqual(0, step.Attempts);
                Assert.IsTrue(step.Created > referenceTime);
                Assert.IsTrue(step.LastModified > referenceTime);
                Assert.IsNull(step.AgentId);
                Assert.IsNull(step.ParentStepId);
                Assert.IsNull(step.EndTime);
                Assert.IsNull(step.StartTime);

                Assert.AreEqual(providerType.FullName, component.ComponentType);
            }
        }

        [Test]
        public void FactoryCopiesOverTheExtensionObjectsToTheMonitorStepForComponentsThatDontExecuteRemotely()
        {
            ExperimentComponent component = FixtureExtensions.CreateExperimentComponent(typeof(TestPayloadExecutesOnVm));
            component.Extensions.Add("flow", "flowObj");

            IEnumerable<ExperimentStepInstance> steps = this.stepFactory.CreateOrchestrationSteps(component, this.exampleExperiment.Id, 100);

            Assert.IsNotNull(steps);
            Assert.IsNotEmpty(steps);
            Assert.IsTrue(steps.Count() == 1);
            Assert.IsNotNull(steps.First().Definition.Extensions);
            Assert.AreEqual("flowObj", steps.First().Definition.Extensions["flow"].ToString());
        }

        [Test]
        public void FactoryCreatesTheExpectedOrchestrationStepsForComponentsWhoseProvidersExecuteInAnAgentProcess()
        {
            // Note:
            // The correctness of the assertions is based on the metadata defined in the attribute of the
            // test providers below. If those provider classes are changed, then this test will be invalidated
            // and will need to be updated.

            // In the dictionary below, the key is the provider type for the component and the value is the expected
            // monitoring provider type responsible for monitoring the step.
            Dictionary<Type, Type> providerTypes = new Dictionary<Type, Type>
            {
                [typeof(TestEnvironmentCleanupExecutesOnNode)] = typeof(MonitorAgentEnvironmentCleanup),
                [typeof(TestEnvironmentCleanupExecutesOnVm)] = typeof(MonitorAgentEnvironmentCleanup),
                [typeof(TestEnvironmentSetupExecutesOnNode)] = typeof(MonitorAgentEnvironmentSetup),
                [typeof(TestEnvironmentSetupExecutesOnVm)] = typeof(MonitorAgentEnvironmentSetup),
                [typeof(TestPayloadExecutesOnNode)] = typeof(MonitorAgentPayload),
                [typeof(TestPayloadExecutesOnVm)] = typeof(MonitorAgentPayload),
                [typeof(TestWorkloadExecutesOnNode)] = typeof(MonitorAgentWorkload),
                [typeof(TestWorkloadExecutesOnVm)] = typeof(MonitorAgentWorkload)
            };

            foreach (var entry in providerTypes)
            {
                Type providerType = entry.Key;
                ExperimentComponent component = FixtureExtensions.CreateExperimentComponent(providerType);

                DateTime referenceTime = DateTime.Now;
                Task.Delay(5).GetAwaiter().GetResult(); // Just to ensure a valid comparison for Created/LastModified dates

                int expectedSequence = 100;
                IEnumerable<ExperimentStepInstance> steps = this.stepFactory.CreateOrchestrationSteps(component, this.exampleExperiment.Id, expectedSequence);
                Assert.IsNotNull(steps);
                Assert.IsNotEmpty(steps);
                Assert.IsTrue(steps.Count() == 1);

                // Each of the steps that are expected to execute on an agent (e.g. Host, Guest) will be
                // added as child steps to an 'agent step monitoring' step. This uses a special provider
                // to create agent steps and then monitor their status for completion.
                ExperimentStepInstance step = steps.First();
                IEnumerable<ExperimentComponent> childSteps = step.Definition.GetChildSteps();
                Assert.IsNotNull(childSteps);
                Assert.IsNotEmpty(childSteps);
                Assert.IsTrue(childSteps.Count() == 1);
                Assert.IsTrue(component.Equals(childSteps.First()));

                Assert.AreEqual(this.exampleExperiment.Id, step.ExperimentId);
                Assert.AreEqual(component.Group, step.ExperimentGroup);
                Assert.AreEqual(ExecutionStatus.Pending, step.Status);
                Assert.AreEqual(component.GetSupportedStepType(), step.StepType);
                Assert.AreEqual(expectedSequence, step.Sequence);
                Assert.AreEqual(0, step.Attempts);
                Assert.IsTrue(step.Created > referenceTime);
                Assert.IsTrue(step.LastModified > referenceTime);
                Assert.IsNull(step.AgentId);
                Assert.IsNull(step.ParentStepId);
                Assert.IsNull(step.EndTime);
                Assert.IsNull(step.StartTime);

                // ...And the provider should be a monitoring provider
                Type monitoringProviderType = entry.Value;
                Assert.AreEqual(monitoringProviderType.FullName, step.Definition.ComponentType);
                Assert.IsTrue(step.Definition.HasExtension(ContractExtension.Steps));
                Assert.IsTrue(step.Definition.GetChildSteps().First().Equals(component), "Must have original component as a child component");
            }
        }

        [Test]
        public void FactoryAppliesTheCorrectSequencesToASetOfOrchestrationSteps()
        {
            List<ExperimentComponent> components = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesRemotely)),
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesRemotely)),
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesRemotely))
            };

            int expectedSequence = 100;
            IEnumerable<ExperimentStepInstance> steps = this.stepFactory.CreateOrchestrationSteps(components, this.exampleExperiment.Id, expectedSequence);

            Assert.IsNotNull(steps);
            Assert.IsNotEmpty(steps);

            foreach (ExperimentStepInstance step in steps)
            {
                Assert.IsTrue(step.Sequence == expectedSequence);
                expectedSequence += 100; // The default sequence increment
            }
        }

        [Test]
        public void FactoryAppliesTheCorrectSequencesToASetOfOrchestrationStepsWhenParallelExecutionIsRequired()
        {
            List<ExperimentComponent> components = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesRemotely)),
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesRemotely)),
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesRemotely))
            };

            ExperimentComponent parallelExecution = ExperimentStepFactoryTests.CreateParallelExecutionStep(components);

            int expectedSequence = 100;
            IEnumerable<ExperimentStepInstance> steps = this.stepFactory.CreateOrchestrationSteps(parallelExecution, this.exampleExperiment.Id, expectedSequence);

            Assert.IsNotNull(steps);
            Assert.IsNotEmpty(steps);

            foreach (ExperimentStepInstance step in steps)
            {
                Assert.IsTrue(step.Sequence == expectedSequence);
            }
        }

        [Test]
        public void FactoryAddsTheExpectedStepsWhenAutoTriageDiagnosticsIsRequestedForTheExperiment()
        {
            List<ExperimentComponent> components = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesRemotely)),
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesRemotely)),
                FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesRemotely))
            };

            int expectedSequence = 100;
            IEnumerable<ExperimentStepInstance> steps = this.stepFactory.CreateOrchestrationSteps(
                components,
                this.exampleExperiment.Id,
                expectedSequence,
                enableDiagnostics: true);

            Assert.IsNotNull(steps);
            Assert.IsNotEmpty(steps);
            Assert.IsTrue(steps.Count() == components.Count + 1);
            CollectionAssert.AllItemsAreUnique(steps.Select(step => step.Sequence));

            ExperimentStepInstance autoTriageStep = steps.Last();
            Assert.IsTrue(autoTriageStep.Definition.ComponentType == typeof(AutoTriageProvider).FullName);
            steps.Take(3).ToList().ForEach(step => Assert.IsTrue(autoTriageStep.Sequence > step.Sequence));
        }

        [Test]
        public void FactoryDoesNotSupportMonitoringStepsForExperimentCriteriaSteps()
        {
            ExperimentComponent component = FixtureExtensions.CreateExperimentComponent(
                typeof(TestEnvironmentCriteriaUnsupported),
                "Cannot run on an agent.");

            Assert.Throws<NotSupportedException>(() => this.stepFactory.CreateOrchestrationSteps(component, this.exampleExperiment.Id, 100));
        }

        [Test]
        public void FactoryAddsParametersDefinedOnTheChildStepToTheParentStepForStepsThatRequireAMonitoringProvider()
        {
            ExperimentComponent childComponent = FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesOnVm));

            // Ensure that is at least one parameter defined
            childComponent.Parameters.Add("Any", Guid.NewGuid().ToString()); 

            List<ExperimentComponent> components = new List<ExperimentComponent> { childComponent };
            IEnumerable<ExperimentStepInstance> steps = this.stepFactory.CreateOrchestrationSteps(components, this.exampleExperiment.Id, 100);

            Assert.IsNotNull(steps);
            Assert.IsNotEmpty(steps);
            CollectionAssert.AreEquivalent(
                childComponent.Parameters.Select(p => $"{p.Key}={p.Value}"),
                steps.First().Definition.Parameters.Select(p => $"{p.Key}={p.Value}"));
        }

        [Test]
        public void FactoryAddsTagsDefinedOnTheChildStepToTheParentStepForStepsThatRequireAMonitoringProvider()
        {
            ExperimentComponent childComponent = FixtureExtensions.CreateExperimentComponent(typeof(TestWorkloadExecutesOnVm));

            // Ensure that is at least one tag defined
            childComponent.Tags.Add("Any", Guid.NewGuid().ToString());

            List<ExperimentComponent> components = new List<ExperimentComponent> { childComponent };
            IEnumerable<ExperimentStepInstance> steps = this.stepFactory.CreateOrchestrationSteps(components, this.exampleExperiment.Id, 100);

            Assert.IsNotNull(steps);
            Assert.IsNotEmpty(steps);
            CollectionAssert.AreEquivalent(
                childComponent.Tags.Select(p => $"{p.Key}={p.Value}"),
                steps.First().Definition.Tags.Select(p => $"{p.Key}={p.Value}"));
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

        // The following provider classes are used to help isolate expectations so that the step factory
        // can setup and validate behaviors based upon metadata defined in the attribute on each of the
        // providers.
        private class TestProvider : ExperimentProvider
        {
            public TestProvider(IServiceCollection services)
                : base(services)
            {
            }

            protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
            }
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentCriteria, SupportedStepTarget.ExecuteRemotely)]
        private class TestEnvironmentCriteriaExecutesRemotely : TestProvider
        {
            public TestEnvironmentCriteriaExecutesRemotely(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentCriteria, SupportedStepTarget.ExecuteOnVirtualMachine)]
        private class TestEnvironmentCriteriaUnsupported : TestProvider
        {
            public TestEnvironmentCriteriaUnsupported(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
        private class TestEnvironmentSetupExecutesRemotely : TestProvider
        {
            public TestEnvironmentSetupExecutesRemotely(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteOnNode)]
        private class TestEnvironmentSetupExecutesOnNode : TestProvider
        {
            public TestEnvironmentSetupExecutesOnNode(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteOnVirtualMachine)]
        private class TestEnvironmentSetupExecutesOnVm : TestProvider
        {
            public TestEnvironmentSetupExecutesOnVm(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentCleanup, SupportedStepTarget.ExecuteRemotely)]
        private class TestEnvironmentCleanupExecutesRemotely : TestProvider
        {
            public TestEnvironmentCleanupExecutesRemotely(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentCleanup, SupportedStepTarget.ExecuteOnNode)]
        private class TestEnvironmentCleanupExecutesOnNode : TestProvider
        {
            public TestEnvironmentCleanupExecutesOnNode(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentCleanup, SupportedStepTarget.ExecuteOnVirtualMachine)]
        private class TestEnvironmentCleanupExecutesOnVm : TestProvider
        {
            public TestEnvironmentCleanupExecutesOnVm(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteRemotely)]
        private class TestWorkloadExecutesRemotely : TestProvider
        {
            public TestWorkloadExecutesRemotely(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteOnNode)]
        private class TestWorkloadExecutesOnNode : TestProvider
        {
            public TestWorkloadExecutesOnNode(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteOnVirtualMachine)]
        private class TestWorkloadExecutesOnVm : TestProvider
        {
            public TestWorkloadExecutesOnVm(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteRemotely)]
        private class TestPayloadExecutesRemotely : TestProvider
        {
            public TestPayloadExecutesRemotely(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnVirtualMachine)]
        private class TestPayloadExecutesOnVm : TestProvider
        {
            public TestPayloadExecutesOnVm(IServiceCollection services)
                : base(services)
            {
            }
        }

        [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
        private class TestPayloadExecutesOnNode : TestProvider
        {
            public TestPayloadExecutesOnNode(IServiceCollection services)
                : base(services)
            {
            }
        }
    }
}
