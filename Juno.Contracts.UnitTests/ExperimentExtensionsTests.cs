namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using AutoFixture;
    using Juno.Providers.Dependencies;
    using Juno.Providers.Environment;
    using Juno.Providers.Payloads;
    using Juno.Providers.Workloads;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentExtensionsTests
    {
        private Fixture mockFixture;
        private IDictionary<string, IConvertible> mockSharedParameters;
        private IDictionary<string, IConvertible> mockParameterReferences;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);

            // Experiment shared parameters are referenced by other components (e.g. environment criteria,
            // setup, cleanup and workload, payload) in the experiment definition.
            this.mockSharedParameters = new Dictionary<string, IConvertible>
            {
                ["SharedParameter1"] = 123456789,
                ["SharedParameter2"] = "Any parameter value",
                ["SharedParameter3"] = true
            };

            this.mockParameterReferences = new Dictionary<string, IConvertible>
            {
                ["Parameter1"] = "$.parameters.SharedParameter1",
                ["Parameter2"] = "$.parameters.SharedParameter2",
                ["Parameter3"] = "$.parameters.SharedParameter3"
            };
        }

        [Test]
        public void AppendComponentsExtensionHandlePropertiesWithNullValues()
        {
            Assert.DoesNotThrow(() => new StringBuilder().AppendComponents(new List<ExperimentComponent>
            {
                new ExperimentComponent("Any.Type", "Any Name", null),
                null,
                new ExperimentComponent("Any.Type", "Any Name", null),
                null
            }));
        }

        [Test]
        public void AppendComponentsExtensionHandleTargetGoalParameterWithNullValues()
        {
            Assert.DoesNotThrow(() => new StringBuilder().AppendComponents(new List<TargetGoalParameter>
            {
                new TargetGoalParameter("1", "Workload.Any", null),
                new TargetGoalParameter("1", "Workload.Any", new Dictionary<string, IConvertible>()),
                new TargetGoalParameter("1", "Workload.Any", new Dictionary<string, IConvertible>()
                {
                    ["Value"] = "valuetesting"  
                })
            }));
        }

        [Test]
        public void AppendPropertiesExtensionHandlePropertiesWithNullValues()
        {
            Assert.DoesNotThrow(() => new StringBuilder().AppendProperties(
                "string",
                123,
                true,
                null,
                4.0M,
                null,
                DateTime.Now));
        }

        [Test]
        public void AppendParametersExtensionHandlePropertiesWithNullValues()
        {
            Assert.DoesNotThrow(() => new StringBuilder().AppendParameters(new Dictionary<string, IConvertible>
            {
                ["1"] = "string",
                ["2"] = 123,
                ["3"] = null,
                ["4"] = 4.0M,
                ["5"] = null,
                ["6"] = DateTime.Now
            }));
        }

        [Test]
        public void AddChildStepsExtensionAddsTheSetOfSubComponentsToTheParentComponentAsExpected()
        {
            IEnumerable<ExperimentComponent> expectedSubComponents = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Child Workload1"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Child Workload2")
            };

            ExperimentComponent workload = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Parent Workload");
            workload.AddOrReplaceChildSteps(expectedSubComponents.ToArray());

            IEnumerable<ExperimentComponent> actualSubComponents = workload.Extensions[ContractExtension.Steps]
                .ToObject<IEnumerable<ExperimentComponent>>();

            CollectionAssert.AreEqual(expectedSubComponents, actualSubComponents);
        }

        [Test]
        public void GetChildStepsExtensionReturnsTheExpectedSetOfSubComponents()
        {
            IEnumerable<ExperimentComponent> expectedSubComponents = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Child Workload1"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Child Workload2")
            };

            ExperimentComponent workload = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Parent Workload");
            workload.Extensions[ContractExtension.Steps] = JToken.FromObject(expectedSubComponents);

            IEnumerable<ExperimentComponent> actualSubComponents = workload.GetChildSteps();

            CollectionAssert.AreEqual(expectedSubComponents, actualSubComponents);
        }

        [Test]
        public void HasChildStepsExtensionReturnsTheExpectedSetOfSubComponents()
        {
            IEnumerable<ExperimentComponent> expectedSubComponents = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Child Workload1"),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Child Workload2")
            };

            ExperimentComponent workload = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Parent Workload");
            workload.Extensions[ContractExtension.Steps] = JToken.FromObject(expectedSubComponents);

            Assert.IsTrue(workload.HasExtension(ContractExtension.Steps));
        }

        [Test]
        public void IsDiagnosticsEnabledReturnsTheExpectedValueGivenTheMetadataDefinedForAnExperiment()
        {
            Experiment experiment = this.mockFixture.Create<Experiment>();
            experiment.Metadata.Clear();

            Assert.IsFalse(experiment.IsDiagnosticsEnabled());

            experiment.Metadata[Experiment.EnableDiagnostics] = false;
            Assert.IsFalse(experiment.IsDiagnosticsEnabled());

            experiment.Metadata[Experiment.EnableDiagnostics] = true;
            Assert.IsTrue(experiment.IsDiagnosticsEnabled());
        }

        [Test]
        public void IsInlinedExtensionCorrectlyIdentifiesWhenAnExperimentIsInlined()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                template.Metadata,
                template.Parameters,
                template.Workflow);

            experiment.Parameters.Clear();
            experiment.Metadata.Clear();

            // The experiment is not considered inlined until 
            // 1) The payload and workload components have been moved to the workflow
            // 2) No parameter references remain in any components.
            // 3) No parameter references remain in the experiment metadata.
            // 4) No shared parameters are defined on the experiment.
            Assert.IsTrue(experiment.IsInlined());
        }

        [Test]
        public void IsInlinedExtensionCorrectlyIdentifiesComponentsWithParameterReferences()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                template.Metadata,
                template.Parameters,
                new List<ExperimentComponent>
                {
                    // This component references shared parameters of the experiment. An experiment
                    // is not inlined when parameter references remain.
                    new ExperimentComponent(typeof(ExampleSetupProvider).FullName, "Any Name", "Any Description", "Any Group", this.mockParameterReferences)
                });

            experiment.Metadata?.Clear();
            experiment.Parameters?.Clear();
            Assert.IsFalse(experiment.IsInlined());

            experiment.Workflow.First().Parameters.Clear();
            Assert.IsTrue(experiment.IsInlined());
        }

        [Test]
        public void IsInlinedExtensionCorrectlyIdentifiesMetadataWithParameterReferences()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                metadata: this.mockParameterReferences,
                parameters: null,
                workflow: new List<ExperimentComponent>
                {
                    // This component references shared parameters of the experiment. An experiment
                    // is not inlined when parameter references remain.
                    new ExperimentComponent(typeof(ExampleSetupProvider).FullName, "Any Name", "Any Description", "Any Group")
                });

            Assert.IsFalse(experiment.IsInlined());

            experiment.Metadata.Clear();
            Assert.IsTrue(experiment.IsInlined());
        }

        [Test]
        public void IsInlinedExtensionCorrectlyIdentifiesChildComponentsHavingParameterReferences1()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                template.Metadata,
                template.Parameters,
                new List<ExperimentComponent>
                {
                    // This component will have a child compoent that references shared parameters of the experiment. An experiment
                    // is not inlined when parameter references remain.
                    new ExperimentComponent(typeof(ExampleSetupProvider).FullName, "Any Parent", "Any Description", "Any Group")
                });

            experiment.Parameters?.Clear();

            // Add a child component/step with parameter references
            experiment.Workflow.First().AddOrReplaceChildSteps(
                new ExperimentComponent(typeof(ExampleSetupProvider).FullName, "Any Child", "Any Description", "Any Group", this.mockParameterReferences));

            Assert.IsFalse(experiment.IsInlined());

            // Add a child component/step without parameter references
            experiment.Workflow.First().AddOrReplaceChildSteps(
                new ExperimentComponent(typeof(ExampleSetupProvider).FullName, "Any Child", "Any Description", "Any Group"));

            Assert.IsTrue(experiment.IsInlined());
        }

        [Test]
        public void IsInlinedExtensionCorrectlyIdentifiesChildComponentsHavingParameterReferences2()
        {
            // Validating that child steps far down the hierarchy are evaluated for parameter references.

            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                template.Metadata,
                template.Parameters,
                new List<ExperimentComponent>
                {
                    // This component will have a child compoent that references shared parameters of the experiment. An experiment
                    // is not inlined when parameter references remain.
                    new ExperimentComponent(typeof(ExampleSetupProvider).FullName, "Parent", "Any Description", "Any Group")
                });

            experiment.Parameters?.Clear();

            ExperimentComponent childStep1 = new ExperimentComponent(
                typeof(ExampleSetupProvider).FullName, "Parent.Child", "Any Description", "Any Group");

            ExperimentComponent childStep2 = new ExperimentComponent(
                typeof(ExampleSetupProvider).FullName, "Parent.Child.Child", "Any Description", "Any Group");

            // 3-levels down in the child hierarchy there is a component with parameter references
            ExperimentComponent childStep3 = new ExperimentComponent(
                typeof(ExampleSetupProvider).FullName, "Parent.Child.Child.Child", "Any Description", "Any Group", this.mockParameterReferences);

            // Add a child component/step with parameter references
            childStep2.AddOrReplaceChildSteps(childStep3);
            childStep1.AddOrReplaceChildSteps(childStep2);
            experiment.Workflow.First().AddOrReplaceChildSteps(childStep1);

            Assert.IsFalse(experiment.IsInlined());

            // Remove the parameter references
            childStep3.Parameters.Clear();
            childStep2.AddOrReplaceChildSteps(childStep3);
            childStep1.AddOrReplaceChildSteps(childStep2);
            experiment.Workflow.First().AddOrReplaceChildSteps(childStep1);

            Assert.IsTrue(experiment.IsInlined());
        }

        [Test]
        public void InlinedExtensionValidatesRequiredParameters()
        {
            Experiment invalid = null;
            Assert.Throws<ArgumentException>(() => invalid.Inlined());
        }

        [Test]
        public void InlinedExtensionReplacesSharedParametersInWorkflowComponents()
        {
            // Override the fixture default setup to ensure that individual environment
            // groups have criteria (i.e. no shared criteria).
            // Add the shared parameters to the experiment
            Experiment experiment = this.mockFixture.Create<Experiment>();

            experiment.Parameters.Clear();
            experiment.Parameters.AddRange(this.mockSharedParameters);

            // Add references to the shared parameters to the environment group
            // components. These are expected to be replaced when the experiment is
            // inlined with the values in the experiment-wide parameters
            experiment.Workflow.First().Parameters.Clear();
            experiment.Workflow.First().Parameters.AddRange(this.mockParameterReferences);

            Experiment inlinedExperiment = experiment.Inlined();

            Assert.IsFalse(inlinedExperiment.Parameters.Any());

            // Add references to the shared parameters to the environment group
            // components. These are expected to be replaced when the experiment is
            // inlined with the values in the experiment-wide parameters
            Assert.IsTrue(inlinedExperiment.Workflow.First().Parameters.ContainsKey("Parameter1"));
            Assert.IsTrue(inlinedExperiment.Workflow.First().Parameters.ContainsKey("Parameter2"));
            Assert.IsTrue(inlinedExperiment.Workflow.First().Parameters.ContainsKey("Parameter3"));

            Assert.AreEqual(this.mockSharedParameters["SharedParameter1"], inlinedExperiment.Workflow.First().Parameters["Parameter1"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter2"], inlinedExperiment.Workflow.First().Parameters["Parameter2"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter3"], inlinedExperiment.Workflow.First().Parameters["Parameter3"]);
        }

        [Test]
        public void InlinedExtensionReplacesSharedParametersInDependencyComponentsOfWorkflowComponents1()
        {
            // Add a dependency component that references experiment parameters.
            ExperimentComponent dependencyComponent = FixtureExtensions.CreateExperimentComponent(typeof(ExampleDependencyProvider));
            dependencyComponent.Parameters.Clear();
            dependencyComponent.Parameters.AddRange(this.mockParameterReferences);

            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                template.Metadata,
                template.Parameters,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), dependencies: new List<ExperimentComponent>
                    {
                        dependencyComponent
                    })
                });

            // Add shared parameters to experiment.
            experiment.Parameters.Clear();
            experiment.Parameters.AddRange(this.mockSharedParameters);

            Experiment inlinedExperiment = experiment.Inlined();
            Assert.IsFalse(inlinedExperiment.Parameters.Any());

            ExperimentComponent updatedDependencyComponent = experiment.Workflow.First().Dependencies.First();

            Assert.IsTrue(updatedDependencyComponent.Parameters.ContainsKey("Parameter1"));
            Assert.IsTrue(updatedDependencyComponent.Parameters.ContainsKey("Parameter2"));
            Assert.IsTrue(updatedDependencyComponent.Parameters.ContainsKey("Parameter3"));

            Assert.AreEqual(this.mockSharedParameters["SharedParameter1"], updatedDependencyComponent.Parameters["Parameter1"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter2"], updatedDependencyComponent.Parameters["Parameter2"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter3"], updatedDependencyComponent.Parameters["Parameter3"]);
        }

        [Test]
        public void InlinedExtensionReplacesSharedParametersInDependencyComponentsOfWorkflowComponents2()
        {
            // Test multi-level hierarchy (e.g. deep nested components).
            // Add a dependency component that references experiment parameters. This dependency will be
            // a dependency of another dependency component.
            ExperimentComponent dependencyComponent1 = FixtureExtensions.CreateExperimentComponent(typeof(ExampleDependencyProvider));
            dependencyComponent1.Parameters.Clear();
            dependencyComponent1.Parameters.AddRange(this.mockParameterReferences);

            // Add a dependency component, that itself has dependencies
            ExperimentComponent dependencyComponent2 = FixtureExtensions.CreateExperimentComponent(typeof(ExampleDependencyProvider), dependencies: new List<ExperimentComponent>
            {
                dependencyComponent1
            });

            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                template.Metadata,
                template.Parameters,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), dependencies: new List<ExperimentComponent>
                    {
                        dependencyComponent2
                    })
                });

            // Add shared parameters to experiment.
            experiment.Parameters.Clear();
            experiment.Parameters.AddRange(this.mockSharedParameters);

            Experiment inlinedExperiment = experiment.Inlined();
            Assert.IsFalse(inlinedExperiment.Parameters.Any());

            ExperimentComponent updatedDependencyComponent = experiment.Workflow.First().Dependencies.First()
                .Dependencies.First();

            Assert.IsTrue(updatedDependencyComponent.Parameters.ContainsKey("Parameter1"));
            Assert.IsTrue(updatedDependencyComponent.Parameters.ContainsKey("Parameter2"));
            Assert.IsTrue(updatedDependencyComponent.Parameters.ContainsKey("Parameter3"));

            Assert.AreEqual(this.mockSharedParameters["SharedParameter1"], updatedDependencyComponent.Parameters["Parameter1"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter2"], updatedDependencyComponent.Parameters["Parameter2"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter3"], updatedDependencyComponent.Parameters["Parameter3"]);
        }

        [Test]
        public void InlinedExtensionReplacesSharedParametersInChildComponentsOfWorkflowComponents1()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                template.Metadata,
                template.Parameters,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider))
                });

            // Add a child component that references experiment parameters.
            ExperimentComponent childComponent = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider));
            childComponent.Parameters.Clear();
            childComponent.Parameters.AddRange(this.mockParameterReferences);
            experiment.Workflow.First().AddOrReplaceChildSteps(childComponent);

            // Add shared parameters to experiment.
            experiment.Parameters.Clear();
            experiment.Parameters.AddRange(this.mockSharedParameters);

            Experiment inlinedExperiment = experiment.Inlined();
            Assert.IsFalse(inlinedExperiment.Parameters.Any());

            ExperimentComponent updatedChildComponent = experiment.Workflow.First().GetChildSteps().First();

            Assert.IsTrue(updatedChildComponent.Parameters.ContainsKey("Parameter1"));
            Assert.IsTrue(updatedChildComponent.Parameters.ContainsKey("Parameter2"));
            Assert.IsTrue(updatedChildComponent.Parameters.ContainsKey("Parameter3"));

            Assert.AreEqual(this.mockSharedParameters["SharedParameter1"], updatedChildComponent.Parameters["Parameter1"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter2"], updatedChildComponent.Parameters["Parameter2"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter3"], updatedChildComponent.Parameters["Parameter3"]);
        }

        [Test]
        public void InlinedExtensionReplacesSharedParametersInChildComponentsOfWorkflowComponents2()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                template.Metadata,
                template.Parameters,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider))
                });

            // Multiple levels of child hierarchy.
            ExperimentComponent childComponent1 = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider));
            ExperimentComponent childComponent2 = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider));
            ExperimentComponent childComponent3 = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider));

            // Ensure that parameter references are in a child component that is multiple levels
            // down the parent/child hierarchy.
            childComponent1.Parameters.Clear();
            childComponent2.Parameters.Clear();
            childComponent3.Parameters.Clear();
            childComponent3.Parameters.AddRange(this.mockParameterReferences);

            childComponent2.AddOrReplaceChildSteps(childComponent3);
            childComponent1.AddOrReplaceChildSteps(childComponent2);
            experiment.Workflow.First().AddOrReplaceChildSteps(childComponent1);

            experiment.Parameters.Clear();
            experiment.Parameters.AddRange(this.mockSharedParameters);

            Experiment inlinedExperiment = experiment.Inlined();
            Assert.IsFalse(inlinedExperiment.Parameters.Any());

            // Add references to the shared parameters to the environment group
            // components. These are expected to be replaced when the experiment is
            // inlined with the values in the experiment-wide parameters
            ExperimentComponent updatedChildComponent = experiment.Workflow.First()
                .GetChildSteps().First()
                .GetChildSteps().First()
                .GetChildSteps().First();

            Assert.IsTrue(updatedChildComponent.Parameters.ContainsKey("Parameter1"));
            Assert.IsTrue(updatedChildComponent.Parameters.ContainsKey("Parameter2"));
            Assert.IsTrue(updatedChildComponent.Parameters.ContainsKey("Parameter3"));

            Assert.AreEqual(this.mockSharedParameters["SharedParameter1"], updatedChildComponent.Parameters["Parameter1"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter2"], updatedChildComponent.Parameters["Parameter2"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter3"], updatedChildComponent.Parameters["Parameter3"]);
        }

        [Test]
        public void InlinedExtensionHandlesComponentsWithoutParametersWhenInliningSharedParameters()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                template.Metadata,
                template.Parameters,
                new List<ExperimentComponent>
                {
                    new ExperimentComponent(typeof(ExampleSetupProvider).FullName, "Any Name", "Does not contain any parameters")
                });

            Assert.DoesNotThrow(() => experiment.Inlined());
        }

        [Test]
        public void InlinedExtensionThrowsIfComponentsReferencesSharedParametersThatDoNoExist()
        {
            // Ensure that are NO shared parameters
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                template.Metadata,
                template.Parameters,
                new List<ExperimentComponent>
                {
                    new ExperimentComponent(typeof(ExampleSetupProvider).FullName, "Any Name", "Any Description", "Any Group", new Dictionary<string, IConvertible>
                    {
                        ["AnyParameter"] = "$.parameters.DoesNotExist"
                    })
                });

            Assert.Throws<SchemaException>(() => experiment.Inlined());
        }

        [Test]
        public void InlinedExtensionAllowsParameterReferencesWithCertainAdvancedNamingConventions()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                null,
                new Dictionary<string, IConvertible>
                {
                    ["metadata.Parameter1"] = "NailedIt!"
                },
                new List<ExperimentComponent>
                {
                    new ExperimentComponent(typeof(ExampleSetupProvider).FullName, "Any Name", "Any Description", "Any group", new Dictionary<string, IConvertible>
                    {
                        ["Parameter1"] = "$.parameters.metadata.Parameter1"
                    })
                });

            Experiment inlinedExperiment = experiment.Inlined();

            Assert.IsTrue(inlinedExperiment.Workflow.First().Parameters.ContainsKey("Parameter1"));
            Assert.AreEqual("NailedIt!", inlinedExperiment.Workflow.First().Parameters["Parameter1"]);

            // Ensure JSON serialization mechanics do not change the scenario somehow
            Experiment serializedExperiment = experiment.ToJson().FromJson<Experiment>();

            inlinedExperiment = serializedExperiment.Inlined();

            Assert.IsTrue(inlinedExperiment.Workflow.First().Parameters.ContainsKey("Parameter1"));
            Assert.AreEqual("NailedIt!", inlinedExperiment.Workflow.First().Parameters["Parameter1"]);
        }

        [Test]
        public void InlinedExtensionReplacesSharedParametersInExperimentMetadata()
        {
            Experiment experiment = this.mockFixture.Create<Experiment>();

            experiment.Parameters.Clear();
            experiment.Parameters.AddRange(this.mockSharedParameters);

            // Add references to the shared parameters in the experiment metadata.
            experiment.Metadata.Clear();
            experiment.Metadata.AddRange(this.mockParameterReferences);

            // Ensure that no other workflow steps/components have any parameter references.
            experiment.Workflow.ToList().ForEach(step => step.Parameters.Clear());

            Experiment inlinedExperiment = experiment.Inlined();

            Assert.IsFalse(inlinedExperiment.Parameters.Any());

            // Add references to the shared parameters to the environment group
            // components. These are expected to be replaced when the experiment is
            // inlined with the values in the experiment-wide parameters
            Assert.IsTrue(inlinedExperiment.Metadata.ContainsKey("Parameter1"));
            Assert.IsTrue(inlinedExperiment.Metadata.ContainsKey("Parameter2"));
            Assert.IsTrue(inlinedExperiment.Metadata.ContainsKey("Parameter3"));

            Assert.AreEqual(this.mockSharedParameters["SharedParameter1"], inlinedExperiment.Metadata["Parameter1"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter2"], inlinedExperiment.Metadata["Parameter2"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter3"], inlinedExperiment.Metadata["Parameter3"]);
        }

        [Test]
        public void InlinedExperimentReplacesSharedParametersinExperimentFirstClassFields()
        {
            Experiment template = this.mockFixture.Create<Experiment>();

            template.Parameters.Clear();
            template.Parameters.AddRange(this.mockSharedParameters);

            string reference = this.mockParameterReferences["Parameter2"].ToString();

            Experiment outlinedExperiment = new Experiment(
                reference,
                template.Description,
                template.ContentVersion,
                template.Metadata,
                template.Parameters,
                template.Workflow);

            Experiment inlinedExperiment = outlinedExperiment.Inlined();

            string expectedValue = this.mockSharedParameters["SharedParameter2"].ToString();
            Assert.AreEqual(expectedValue, inlinedExperiment.Name);
        }

        [Test]
        public void InlinedExtensionThrowsIfExperimentMetadataReferencesSharedParametersThatDoNoExist()
        {
            // Ensure that are NO shared parameters
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                new Dictionary<string, IConvertible>
                {
                    ["AnyParameter"] = "$.parameters.DoesNotExist"
                },
                template.Parameters,
                new List<ExperimentComponent>
                {
                    new ExperimentComponent(typeof(ExampleSetupProvider).FullName, "Any Name", "Any Description")
                });

            Assert.Throws<SchemaException>(() => experiment.Inlined());
        }

        [Test]
        public void InlinedExtensionAllowsParameterReferencesInMetadataWithCertainAdvancedNamingConventions()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                new Dictionary<string, IConvertible>
                {
                    ["Parameter1"] = "$.parameters.metadata.Parameter1"
                },
                new Dictionary<string, IConvertible>
                {
                    ["metadata.Parameter1"] = "NailedIt!"
                },
                new List<ExperimentComponent>
                {
                    new ExperimentComponent(typeof(ExampleSetupProvider).FullName, "Any Name", "Any Description")
                });

            Experiment inlinedExperiment = experiment.Inlined();

            Assert.IsTrue(inlinedExperiment.Metadata.ContainsKey("Parameter1"));
            Assert.AreEqual("NailedIt!", inlinedExperiment.Metadata["Parameter1"]);

            // Ensure JSON serialization mechanics do not change the scenario somehow
            Experiment serializedExperiment = experiment.ToJson().FromJson<Experiment>();

            inlinedExperiment = serializedExperiment.Inlined();

            Assert.IsTrue(inlinedExperiment.Metadata.ContainsKey("Parameter1"));
            Assert.AreEqual("NailedIt!", inlinedExperiment.Metadata["Parameter1"]);
        }

        [Test]
        public void IsParallelExecutionExtensionIdentifiesComponentDefinitionsThatRequireParallelExecutionOfStepsWithin()
        {
            ExperimentComponent component = this.mockFixture.Create<ExperimentComponent>();
            ExperimentComponent parallelExecutionComponent = FixtureExtensions.CreateExperimentComponent(
                ExperimentComponent.ParallelExecutionType,
                "Parallel Execution");

            Assert.IsFalse(component.IsParallelExecution());
            Assert.IsTrue(parallelExecutionComponent.IsParallelExecution());
        }

        [Test]
        [TestCase("$.parameters.anyparameterreference")]
        [TestCase("$.PARAMETERS.ANYPARAMETERREFERENCE")]
        public void IsParameterReferenceExtensionIsNotCaseSensitive(string reference)
        {
            KeyValuePair<string, IConvertible> parameter = new KeyValuePair<string, IConvertible>(
                "ParameterReference",
                reference);

            string parameterName;
            Assert.IsTrue(parameter.TryGetParameterReference(out parameterName));
            Assert.AreEqual("ANYPARAMETERREFERENCE", parameterName.ToUpperInvariant());
        }

        [Test]
        public void TryGetParameterReferenceExtensionCorrectlyIdentifiesReferences()
        {
            KeyValuePair<string, IConvertible> parameter1 = new KeyValuePair<string, IConvertible>(
                "NotAParameterReference",
                "AnyValue");

            KeyValuePair<string, IConvertible> parameter2 = new KeyValuePair<string, IConvertible>(
                "ParameterReference",
                "$.parameters.AnyParameterReference");

            string parameterName;
            Assert.IsFalse(parameter1.TryGetParameterReference(out parameterName));
            Assert.IsTrue(parameter2.TryGetParameterReference(out parameterName));
            Assert.AreEqual("AnyParameterReference", parameterName);
        }

        [Test]
        public void FlowExtensionReturnsNullWhenAFlowDefinitionDoesNotExist()
        {
            var component = new ExperimentComponent(typeof(ExamplePayloadProvider).FullName, "Any Name", "Any Description", "Any Group");

            ExperimentFlow flowReturned = component.Flow();

            Assert.IsNull(flowReturned);
        }

        [Test]
        public void FlowExtensionReturnsTheFlowObjectWhenAFlowDefinitionExists()
        {
            var flow = new ExperimentFlow("Block A", "Block A");
            var component = new ExperimentComponent(typeof(ExamplePayloadProvider).FullName, "Any Name", "Any Description", "Any Group");
            component.Extensions[ContractExtension.Flow] = JToken.FromObject(flow);

            ExperimentFlow flowReturned = component.Flow();

            Assert.IsNotNull(flowReturned);
            Assert.AreEqual(flowReturned.BlockName, flow.BlockName);
            Assert.AreEqual(flowReturned.OnFailureExecuteBlock, flow.OnFailureExecuteBlock);
        }

        [Test]
        public void FlowExtensionReturnsTheFlowObjectWhenAFlowDefinitionWithOnFailureExecuteBlockExists()
        {
            var flow = new ExperimentFlow(null, "Block A");
            var component = new ExperimentComponent(typeof(ExamplePayloadProvider).FullName, "Any Name", "Any Description", "Any Group");
            component.Extensions[ContractExtension.Flow] = JToken.FromObject(flow);

            ExperimentFlow flowReturned = component.Flow();

            Assert.IsNotNull(flowReturned);
            Assert.IsNull(flowReturned.BlockName);
            Assert.AreEqual(flowReturned.OnFailureExecuteBlock, flow.OnFailureExecuteBlock);
        }

        [Test]
        public void FlowExtensionReturnsTheFlowObjectWhenAFlowDefinitionWithBlockNamexists()
        {
            var flow = new ExperimentFlow("Block A", null);
            var component = new ExperimentComponent(typeof(ExamplePayloadProvider).FullName, "Any Name", "Any Description", "Any Group");
            component.Extensions[ContractExtension.Flow] = JToken.FromObject(flow);

            ExperimentFlow flowReturned = component.Flow();

            Assert.IsNotNull(flowReturned);
            Assert.IsNull(flowReturned.OnFailureExecuteBlock);
            Assert.AreEqual(flowReturned.BlockName, flow.BlockName);
        }

        private void AssertInlinedParametersMatch(ExperimentComponent component)
        {
            Assert.IsTrue(component.Parameters.ContainsKey("Parameter1"));
            Assert.IsTrue(component.Parameters.ContainsKey("Parameter2"));
            Assert.IsTrue(component.Parameters.ContainsKey("Parameter3"));

            Assert.AreEqual(this.mockSharedParameters["SharedParameter1"], component.Parameters["Parameter1"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter2"], component.Parameters["Parameter2"]);
            Assert.AreEqual(this.mockSharedParameters["SharedParameter3"], component.Parameters["Parameter3"]);
        }

        private void AssertInlinedParametersMatch(IEnumerable<ExperimentComponent> components)
        {
            foreach (ExperimentComponent component in components)
            {
                this.AssertInlinedParametersMatch(component);
            }
        }
    }
}
