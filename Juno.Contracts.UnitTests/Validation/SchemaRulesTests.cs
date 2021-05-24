namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Providers.Dependencies;
    using Juno.Providers.Environment;
    using Juno.Providers.Workloads;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SchemaRulesTests
    {
        private Fixture mockFixture;
        private Experiment validExperiment;
        private ExperimentComponent validParallelExecutionComponent;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.validExperiment = this.mockFixture.Create<Experiment>();

            this.validParallelExecutionComponent = FixtureExtensions.CreateExperimentComponent(ExperimentComponent.ParallelExecutionType);
            this.validParallelExecutionComponent.AddOrReplaceChildSteps(
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider)),
                FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider)));
        }

        [Test]
        public void SchemaValidationPassesForAValidExperiment()
        {
            ValidationResult result = SchemaRules.Instance.Validate(this.validExperiment);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Any() != true);
        }

        [Test]
        public void SchemaValidationHandlesParallelExecutionSteps()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                new List<ExperimentComponent>
                {
                    this.validParallelExecutionComponent,
                    this.validParallelExecutionComponent
                });

            ValidationResult result = SchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenInvalidParameterReferencesExistInWorkflowComponentParameters()
        {
            Experiment experiment = this.mockFixture.Create<Experiment>();

            // Ensure there are no shared parameters that can be referenced. Thus any reference
            // to a shared parameter should fail validation.
            experiment.Parameters.Clear();

            ValidationResult result = SchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);

            KeyValuePair<string, IConvertible> invalidReference = new KeyValuePair<string, IConvertible>(
               $"Parameter{Guid.NewGuid()}",
               "$.parameters.DoesNotExist");

            // Invalid references in environment parameters
            experiment.Workflow.First().Parameters.Add(invalidReference);

            result = SchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenInvalidParameterReferencesExistInWorkflowComponentDependencyParameters()
        {
            ExperimentComponent invalidDependency = FixtureExtensions.CreateExperimentComponent(typeof(ExampleDependencyProvider));

            // Invalid references in environment parameters
            invalidDependency.Parameters.Add($"Parameter{Guid.NewGuid()}", "$.parameters.DoesNotExist");

            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), dependencies: new List<ExperimentComponent>
                    {
                        invalidDependency
                    })
                });

            ValidationResult result = SchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenInvalidParameterReferencesExistInWorkflowComponentDependencyParameters_InParallelExecutionSteps()
        {
            ExperimentComponent invalidDependency = FixtureExtensions.CreateExperimentComponent(typeof(ExampleDependencyProvider));

            // Invalid references in environment parameters
            invalidDependency.Parameters.Add($"Parameter{Guid.NewGuid()}", "$.parameters.DoesNotExist");

            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), dependencies: new List<ExperimentComponent>
                        {
                            invalidDependency
                        }))
                });

            ValidationResult result = SchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenInvalidParameterReferencesExistInWorkflowComponentChildStepParameters()
        {
            ExperimentComponent invalidChildStep = FixtureExtensions.CreateExperimentComponent(typeof(ExampleDependencyProvider));

            // Invalid references in environment parameters
            invalidChildStep.Parameters.Add($"Parameter{Guid.NewGuid()}", "$.parameters.DoesNotExist");

            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider))
                        .AddOrReplaceChildSteps(invalidChildStep)
                });

            ValidationResult result = SchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenInvalidParameterReferencesExistInWorkflowComponentChildStepParameters_InParallelExecutionSteps()
        {
            ExperimentComponent invalidChildStep = FixtureExtensions.CreateExperimentComponent(typeof(ExampleDependencyProvider));

            // Invalid references in environment parameters
            invalidChildStep.Parameters.Add($"Parameter{Guid.NewGuid()}", "$.parameters.DoesNotExist");

            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider))
                            .AddOrReplaceChildSteps(invalidChildStep))
                });

            ValidationResult result = SchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }
    }
}
