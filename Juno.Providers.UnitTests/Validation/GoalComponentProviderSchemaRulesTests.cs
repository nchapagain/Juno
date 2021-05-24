namespace Juno.Providers.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class GoalComponentProviderSchemaRulesTests
    {
        private Fixture mockFixture;
        private Precondition validPrecondition;
        private ScheduleAction validScheduleAction;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.validPrecondition = this.mockFixture.Create<Precondition>();
            this.validScheduleAction = this.mockFixture.Create<ScheduleAction>();
        }

        [Test]
        public void SchemaValidationPassesForAValidPrecondition()
        {
            ValidationResult result = GoalComponentProviderSchemaRules.Instance.Validate(this.validPrecondition);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Any() != true);
        }

        [Test]
        public void SchemaValidationPassesForAValidScheduleAction()
        {
            ValidationResult result = GoalComponentProviderSchemaRules.Instance.Validate(this.validScheduleAction);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Any() != true);
        }

        [Test]
        public void GoalComponentSchemaValidationHandlesUndefinedParameterDefinitions()
        {
            GoalComponent emptyComponent = new GoalComponent(
                this.validPrecondition.Type,
                new Dictionary<string, IConvertible>());
            ValidationResult result = GoalComponentProviderSchemaRules.Instance.Validate(emptyComponent);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void GoalComponentSchemaValidationHandlesNullParameterDefinition()
        {
            GoalComponent nullComponent = new GoalComponent(
                this.validPrecondition.Type);

            ValidationResult result = GoalComponentProviderSchemaRules.Instance.Validate(nullComponent);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void GoalComponentSchemaValidatesParamatersWithExpectedParameters()
        {
            GoalComponent component = new GoalComponent(
                type: typeof(TestProvider).FullName,
                parameters: new Dictionary<string, IConvertible>()
                {
                    { "Required1", "value" },
                    { "Required2", "value" },
                    { "Optional1", "value" },
                    { "Optional2", "value" }
                });

            ValidationResult result = GoalComponentProviderSchemaRules.Instance.Validate(component);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void GoalComponentSchemaValidationParametersAreCaseInsensitive()
        {
            GoalComponent component = new GoalComponent(
                type: typeof(TestProvider).FullName,
                parameters: new Dictionary<string, IConvertible>() 
                {
                    { "required1", "value" },
                    { "reQuired2", "value" },
                    { "optiOnal1", "value" }
                });

            ValidationResult result = GoalComponentProviderSchemaRules.Instance.Validate(component);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
        }

        [Test]
        public void GoalComponentSchemaValidationFailsWhenComponentHasUnsupportedParameters()
        {
            GoalComponent component = new GoalComponent(
                type: typeof(TestProvider).FullName,
                parameters: new Dictionary<string, IConvertible>() 
                {
                    { "Required1", "value" },
                    { "Required2", "value" },
                    { "Optional1", "value" },
                    { "Optional3", "value" }
                });

            ValidationResult result = GoalComponentProviderSchemaRules.Instance.Validate(component);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.First() == $"Unsupported parameters found. The following parameters on component '{typeof(TestProvider)}' are not supported: Optional3");
        }

        [Test]
        public void GoalComponentSchemaValidationFailsWhenMissingRequiredParameters()
        {
            GoalComponent component = new GoalComponent(
                type: typeof(TestProvider).FullName,
                parameters: new Dictionary<string, IConvertible>()
                {
                    { "Required1", "value" },
                    { "Optional1", "value" }
                });

            ValidationResult result = GoalComponentProviderSchemaRules.Instance.Validate(component);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.First() == $"Required parameter 'Required2' is not defined for component '{component.Type}'.");
        }

        [Test]
        public void GoalComponentSchemaValidationCorrectlyIdentifiesRequiredVersusSupportedParameters()
        {
            GoalComponent component = new GoalComponent(
                type: typeof(TestProvider).FullName,
                parameters: new Dictionary<string, IConvertible>()
                {
                    { "Required1", "value" },
                    { "Required2", "value" },
                    { "Optional1", "value" }
                });

            ValidationResult result = GoalComponentProviderSchemaRules.Instance.Validate(component);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.ValidationErrors);
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

            GoalComponent component = this.mockFixture.Create<Precondition>();
            component.Parameters.Clear();

            SchemaException error = Assert.Throws<SchemaException>(() => GoalComponentProviderSchemaRules.ValidateParameter(component, parameter));
            Assert.AreEqual(
                $"Required parameter '{parameter.Name}' is not defined for component '{component.Type}'.",
                error.Message);
        }

        [SupportedParameter(Name = "Required1", Type = typeof(string), Required = true)]
        [SupportedParameter(Name = "Required2", Type = typeof(string), Required = true)]
        [SupportedParameter(Name = "Optional1", Type = typeof(string), Required = false)]
        [SupportedParameter(Name = "Optional2", Type = typeof(string))]
        private class TestProvider : GoalComponentProvider
        {
            public TestProvider(IServiceCollection services)
                : base(services)
            { 
            }

            public override Task ConfigureServicesAsync(GoalComponent component, ScheduleContext context)
            {
                return Task.CompletedTask;
            }
        }
    }
}
