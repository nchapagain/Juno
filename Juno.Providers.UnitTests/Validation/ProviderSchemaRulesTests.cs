namespace Juno.Providers.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Juno.Providers;
    using Juno.Providers.Environment;
    using Juno.Providers.Workloads;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ProviderSchemaRulesTests
    {
        private Fixture mockFixture;
        private Experiment validExperiment;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.validExperiment = this.mockFixture.Create<Experiment>();
        }

        [Test]
        public void SchemaValidationPassesForAValidExperiment()
        {
            ValidationResult result = ProviderSchemaRules.Instance.Validate(this.validExperiment);
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
                    FixtureExtensions.CreateParallelExecutionExperimentComponent()
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationAllowsParallelExecutionStepsThatAreDifferentStepTypes()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), group: "Group A"))
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationAllowsParallelExecutionStepsThatTargetMultipleExperimentGroups()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group A"),
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), group: "Group B"))
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenAnyParallelExecutionComponentsDoNotHaveChildStepsDefined()
        {
            Experiment template = this.mockFixture.Create<Experiment>();
            Experiment experiment = new Experiment(
                template.Name,
                template.Description,
                template.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent("ParallelExecution")
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenWorkflowComponentsReferenceInvalidProviders()
        {
            Experiment experiment = new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent("Not.a.valid.provider.type")
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenWorkflowComponentsReferenceInvalidProviders_InParallelExecutionSteps()
        {
            Experiment experiment = new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent("Not.a.valid.provider.type"))
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenWorkflowComponentDependenciesReferenceInvalidProviders()
        {
            Experiment experiment = new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), dependencies: new List<ExperimentComponent>
                    {
                        FixtureExtensions.CreateExperimentComponent("Not.a.valid.provider.type")
                    })
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenWorkflowComponentsDependenciesReferenceInvalidProviders_InParallelExecutionSteps()
        {
            Experiment experiment = new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), dependencies: new List<ExperimentComponent>
                        {
                            FixtureExtensions.CreateExperimentComponent("Not.a.valid.provider.type")
                        }))
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenWorkflowComponentChildStepsReferenceInvalidProviders()
        {
            Experiment experiment = new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider)).AddOrReplaceChildSteps(
                        FixtureExtensions.CreateExperimentComponent("Not.a.valid.provider.type"))
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenWorkflowComponentChildStepsReferenceInvalidProviders_InParallelExecutionSteps()
        {
            Experiment experiment = new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider))
                        .AddOrReplaceChildSteps(FixtureExtensions.CreateExperimentComponent("Not.a.valid.provider.type")))
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationHandlesUndefinedOrNullParameterDefinitions()
        {
            Experiment experiment = new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    new ExperimentComponent(typeof(ExampleCriteriaProvider).FullName, "Any Name", "Any Description", "Group A"),
                    new ExperimentComponent(typeof(ExampleCriteriaProvider).FullName, "Any Name", "Any Description", "Group A", new Dictionary<string, IConvertible>())
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenAComponentHasChildStepsDefinedWhoseTypeDoesNotMatchTheParentType()
        {
            Experiment experiment = new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider))
                    .AddOrReplaceChildSteps(FixtureExtensions.CreateExperimentComponent(
                        typeof(ExampleSetupProvider).FullName,
                        "Not.A.Workload.Component"))
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenAComponentHasChildStepsDefinedWhoseTypeDoesNotMatchTheParentType_InParallelExecutionSteps()
        {
            Experiment experiment = new Experiment(
                this.validExperiment.Name,
                this.validExperiment.Description,
                this.validExperiment.ContentVersion,
                new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider))
                        .AddOrReplaceChildSteps(FixtureExtensions.CreateExperimentComponent(
                            typeof(ExampleSetupProvider).FullName,
                            "Not.A.Workload.Component")))
                });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenAComponentHasChildStepsDefinedThatHaveAnInvalidSchema()
        {
            Experiment experiment = new Experiment(
               this.validExperiment.Name,
               this.validExperiment.Description,
               this.validExperiment.ContentVersion,
               new List<ExperimentComponent>
               {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: "Group A")
               });

            experiment.Workflow.First().Extensions[ContractExtension.Steps] = JToken.FromObject("[ { 'not': 'a', 'valid': 'component' } ]");

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenAComponentHasChildStepsDefinedThatHaveAnInvalidSchema_InParallelExecutionSteps()
        {
            ExperimentComponent invalidComponent = FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: "Group A");
            invalidComponent.Extensions[ContractExtension.Steps] = JToken.FromObject("[ { 'not': 'a', 'valid': 'component' } ]");

            Experiment experiment = new Experiment(
               this.validExperiment.Name,
               this.validExperiment.Description,
               this.validExperiment.ContentVersion,
               new List<ExperimentComponent>
               {
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(invalidComponent)
               });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationParametersAreCaseInsensitive()
        {
            Experiment experiment = new Experiment(
               this.validExperiment.Name,
               this.validExperiment.Description,
               this.validExperiment.ContentVersion,
               new List<ExperimentComponent>
               {
                    new ExperimentComponent(typeof(TestProvider).FullName, "Any Name", "Any Description", "Group A", new Dictionary<string, IConvertible>()
                    {
                        { "required1", "value" },
                        { "reQuired2", "value" },
                        { "optiOnal1", "value" }
                    })
               });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationFailsWhenAComponentHasUnsupportedParameters()
        {
            Experiment experiment = new Experiment(
               this.validExperiment.Name,
               this.validExperiment.Description,
               this.validExperiment.ContentVersion,
               new List<ExperimentComponent>
               {
                    new ExperimentComponent(typeof(TestProvider).FullName, "Any Name", "Any Description", "Group A", new Dictionary<string, IConvertible>()
                    {
                        { "Required1", "value" },
                        { "Required2", "value" },
                        { "Optional1", "value" },
                        { "Optional3", "value" }
                    })
               });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.First() == "Unsupported parameters found. The following parameters on component 'Any Name' are not supported: Optional3");
        }

        [Test]
        public void SchemaValidationFailsWhenAComponentIsMissingRequiredParameters()
        {
            Experiment experiment = new Experiment(
               this.validExperiment.Name,
               this.validExperiment.Description,
               this.validExperiment.ContentVersion,
               new List<ExperimentComponent>
               {
                    new ExperimentComponent(typeof(TestProvider).FullName, "Any Name", "Any Description", "Group A", new Dictionary<string, IConvertible>()
                    {
                        { "Required1", "value" },
                        { "Optional1", "value" }
                    })
               });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.First() == "Required parameter 'Required2' is not defined for component 'Any Name'.");
        }

        [Test]
        public void SchemaValidationFailsWhenAComponentIsMissingRequiredParameters_InParallelExecutionSteps()
        {
            Experiment experiment = new Experiment(
               this.validExperiment.Name,
               this.validExperiment.Description,
               this.validExperiment.ContentVersion,
               new List<ExperimentComponent>
               {
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        new ExperimentComponent(typeof(TestProvider).FullName, "Any Name", "Any Description", "Group A", new Dictionary<string, IConvertible>()
                        {
                            { "Required1", "value" },
                            { "Optional1", "value" }
                        }))
               });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.First() == "Required parameter 'Required2' is not defined for component 'Any Name'.");
        }

        [Test]
        public void SchemaValidationFailsWhenBothSpecificAndWildcardGroupNamesAreUsedInEnvironmentCriteriaComponents()
        {
            Experiment experiment = new Experiment(
               this.validExperiment.Name,
               this.validExperiment.Description,
               this.validExperiment.ContentVersion,
               new List<ExperimentComponent>
               {
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: ExperimentComponent.AllGroups),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: "Group A")
               });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.First() == 
                "Invalid workflow environment criteria component step usage. An experiment workflow cannot have both shared environment " +
                "criteria as well as criteria defined for individual group definitions.");
        }

        [Test]
        public void SchemaValidationFailsWhenBothSpecificAndWildcardGroupNamesAreUsedInEnvironmentCriteriaComponents_InParallelExecutionSteps()
        {
            Experiment experiment = new Experiment(
               this.validExperiment.Name,
               this.validExperiment.Description,
               this.validExperiment.ContentVersion,
               new List<ExperimentComponent>
               {
                    FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: ExperimentComponent.AllGroups),
                        FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), group: "Group A"))
               });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.First() ==
                "Invalid workflow environment criteria component step usage. An experiment workflow cannot have both shared environment " +
                "criteria as well as criteria defined for individual group definitions.");
        }

        [Test]
        public void SchemaValidationCorrectlyIdentifiesRequiredVersusSupportedParameters()
        {
            Experiment experiment = new Experiment(
               this.validExperiment.Name,
               this.validExperiment.Description,
               this.validExperiment.ContentVersion,
               new List<ExperimentComponent>
               {
                    new ExperimentComponent(typeof(TestProvider).FullName, "Any Name", "Any Description", "Group A", new Dictionary<string, IConvertible>()
                    {
                        { "Required1", "value" },
                        { "Required2", "value" },
                        { "Optional1", "value" }
                    })
               });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void SchemaValidationCorrectlyIdentifiesRequiredVersusSupportedParameters_InParallelExecutionSteps()
        {
            Experiment experiment = new Experiment(
               this.validExperiment.Name,
               this.validExperiment.Description,
               this.validExperiment.ContentVersion,
               new List<ExperimentComponent>
               {
                   FixtureExtensions.CreateParallelExecutionExperimentComponent(
                        new ExperimentComponent(typeof(TestProvider).FullName, "Any Name", "Any Description", "Group A", new Dictionary<string, IConvertible>()
                        {
                            { "Required1", "value" },
                            { "Required2", "value" },
                            { "Optional1", "value" }
                        }))
               });

            ValidationResult result = ProviderSchemaRules.Instance.Validate(experiment);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void ValidateParametersSupportsAllIConvertibleParameterDataTypes()
        {
            Dictionary<Type, IConvertible> supportedDataTypes = new Dictionary<Type, IConvertible>
            {
                [typeof(string)] = "Any string",
                [typeof(char)] = char.MaxValue,
                [typeof(byte)] = byte.MaxValue,
                [typeof(short)] = short.MaxValue,
                [typeof(int)] = int.MaxValue,
                [typeof(long)] = long.MaxValue,
                [typeof(ushort)] = ushort.MaxValue,
                [typeof(uint)] = uint.MaxValue,
                [typeof(ulong)] = ulong.MaxValue,
                [typeof(float)] = float.MaxValue,
                [typeof(double)] = double.MaxValue,
                [typeof(bool)] = true
            };

            foreach (var entry in supportedDataTypes)
            {
                SupportedParameterAttribute parameter = new SupportedParameterAttribute
                {
                    Name = "ValidParameter",
                    Type = entry.Key
                };

                ExperimentComponent component = this.mockFixture.Create<ExperimentComponent>();
                component.Parameters.Clear();
                component.Parameters.Add(parameter.Name, entry.Value);

                Assert.DoesNotThrow(() => ProviderSchemaRules.ValidateParameter(component, parameter));
            }
        }

        [Test]
        public void ValidateParametersSupportsExpectedParameterDataTypesThatAreNotIConvertible()
        {
            Dictionary<Type, IConvertible> supportedDataTypes = new Dictionary<Type, IConvertible>
            {
                [typeof(TimeSpan)] = "1.01:23:35.123456",
                [typeof(Uri)] = "any/relative/uri",
                [typeof(Uri)] = "https://any/absolute/uri",
                [typeof(ExecutionStatus)] = ExecutionStatus.InProgress
            };

            foreach (var entry in supportedDataTypes)
            {
                SupportedParameterAttribute parameter = new SupportedParameterAttribute
                {
                    Name = "ValidParameter",
                    Type = entry.Key
                };

                ExperimentComponent component = this.mockFixture.Create<ExperimentComponent>();
                component.Parameters.Clear();
                component.Parameters.Add(parameter.Name, entry.Value);

                Assert.DoesNotThrow(() => ProviderSchemaRules.ValidateParameter(component, parameter));
            }
        }

        [Test]
        public void ValidateParametersChecksForRequiredParameters()
        {
            SupportedParameterAttribute parameter = new SupportedParameterAttribute
            {
                Name = "RequiredParameter",
                Required = true,
                Type = typeof(string)
            };

            ExperimentComponent component = this.mockFixture.Create<ExperimentComponent>();
            component.Parameters.Clear();

            SchemaException error = Assert.Throws<SchemaException>(() => ProviderSchemaRules.ValidateParameter(component, parameter));
            Assert.AreEqual(
                $"Required parameter '{parameter.Name}' is not defined for component '{component.Name}'.",
                error.Message);
        }

        [Test]
        public void ValidateParametersChecksForParametersWhoseDataTypeIsNotSupported()
        {
            SupportedParameterAttribute parameter = new SupportedParameterAttribute
            {
                Name = "UnsupportedDataType",
                Required = true,
                Type = typeof(object)
            };

            ExperimentComponent component = this.mockFixture.Create<ExperimentComponent>();
            component.Parameters.Clear();
            component.Parameters.Add(parameter.Name, "Any value");

            SchemaException error = Assert.Throws<SchemaException>(() => ProviderSchemaRules.ValidateParameter(component, parameter));
            Assert.AreEqual(
                $"Unsupported parameter data type '{parameter.Type.FullName}' for parameter '{parameter.Name}' on component '{component.Name}'",
                error.Message);
        }

        [Test]
        public void ValidateParametersChecksForParametersWhoseValueCannotBeConvertedToTheTypeSpecified()
        {
            SupportedParameterAttribute parameter = new SupportedParameterAttribute
            {
                Name = "InvalidDataTypeFormat",
                Required = true,
                Type = typeof(int)
            };

            ExperimentComponent component = this.mockFixture.Create<ExperimentComponent>();
            component.Parameters.Clear();
            component.Parameters.Add(parameter.Name, "NotAnIntegerAtAll");

            SchemaException error = Assert.Throws<SchemaException>(() => ProviderSchemaRules.ValidateParameter(component, parameter));
            Assert.AreEqual(
                $"Invalid data type format for parameter '{parameter.Name}' on component '{component.Name}'. " +
                $"The value 'NotAnIntegerAtAll' cannot be formatted as a '{parameter.Type.FullName}' value.",
                error.Message);
        }

        [Test]
        public void ValidateParametersChecksForParametersWhoseValueIsAnEnumThatCannotBeConvertedToTheTypeSpecified()
        {
            SupportedParameterAttribute parameter = new SupportedParameterAttribute
            {
                Name = "InvalidEnum",
                Required = true,
                Type = typeof(ExecutionStatus)
            };

            ExperimentComponent component = this.mockFixture.Create<ExperimentComponent>();
            component.Parameters.Clear();
            component.Parameters.Add(parameter.Name, "NonAValidExecutionStatus");

            SchemaException error = Assert.Throws<SchemaException>(() => ProviderSchemaRules.ValidateParameter(component, parameter));
            Assert.AreEqual(
                $"Invalid data type format for parameter '{parameter.Name}' on component '{component.Name}'. " +
                $"The value 'NonAValidExecutionStatus' cannot be formatted as a '{parameter.Type.FullName}' value.",
                error.Message);
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentCriteria, SupportedStepTarget.ExecuteRemotely)]
        [SupportedParameter(Name = "Required1", Type = typeof(string), Required = true)]
        [SupportedParameter(Name = "Required2", Type = typeof(string), Required = true)]
        [SupportedParameter(Name = "Optional1", Type = typeof(string), Required = false)]
        [SupportedParameter(Name = "Optional2", Type = typeof(string))] // Default of 'Required' is set to false.
        private class TestProvider : ExperimentProvider
        {
            public TestProvider(IServiceCollection services)
                : base(services)
            {
            }

            protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
