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
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentFilterValidationTests
    {
        private Fixture mockFixture;
        private EnvironmentFilter mockFilter;
        private EnvironmentFilterValidation filterValidation;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupEnvironmentSelectionMocks();
            this.mockFilter = FixtureExtensions.CreateEnvironmentFilterFromType(typeof(TestProvider));
            this.filterValidation = EnvironmentFilterValidation.Instance;
        }

        [Test]
        public void ValidateValidateParameters()
        {
            Assert.Throws<ArgumentException>(() => this.filterValidation.Validate(null));
        }

        [Test]
        public void ValidateHandlesUndefinedRequiredParameters()
        {
            EnvironmentFilter filter = new EnvironmentFilter(
                type: typeof(TestProvider).FullName,
                parameters: new Dictionary<string, IConvertible>()
                {
                    { "Singleton1", "value" }
                });
            ValidationResult result = this.filterValidation.Validate(filter);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotNull(result.ValidationErrors);
            string error = result.ValidationErrors.First();
            Assert.AreEqual($"Required parameter 'Required1' is not defined for filter '{typeof(TestProvider)}'.", error);
        }

        [Test]
        public void ValidateThrowsErrorForUnsupportedParameters()
        {
            EnvironmentFilter filter = new EnvironmentFilter(
            type: typeof(TestProvider).FullName,
            parameters: new Dictionary<string, IConvertible>()
            {
                { "Required1", "value" },
                { "Singleton2", "singleton" },
                { "Singleton3", "otherValue" },
                { "Whatamidoinghere", 10 }
            });

            ValidationResult result = this.filterValidation.Validate(filter);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.Any());
            Assert.AreEqual("Unsupported parameters found. The following parameters on component 'Juno.Providers.Validation.EnvironmentFilterValidationTests+TestProvider' are not supported: Singleton2, Singleton3, Whatamidoinghere", result.ValidationErrors.First());
        }

        [Test]
        public void ValidateThrowsErrorWhenParameterCanBeAListAndHasDuplicates()
        {
            EnvironmentFilter filter = new EnvironmentFilter(
            type: typeof(TestProvider).FullName,
            parameters: new Dictionary<string, IConvertible>()
            {
                { "Required1", "value,value,value1" }
            });

            ValidationResult result = this.filterValidation.Validate(filter);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors.Any());
            Assert.AreEqual("Listable parameters can not have duplicate values. The parameter: 'Required1' on componenet 'Juno.Providers.Validation.EnvironmentFilterValidationTests+TestProvider' has duplicate values.The duplicated values are: value", result.ValidationErrors.First());
        }

        [SupportedFilter(Name = "Required1", Type = typeof(string), Required = true)]
        [SupportedFilter(Name = "Optional1", Type = typeof(string), Required = false)]
        private class TestProvider : EnvironmentSelectionProvider
        {
            public TestProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
                : base(services, TimeSpan.FromMinutes(15), configuration, logger)
            { 
            }

            protected override Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token)
            {
                return (Task<IDictionary<string, EnvironmentCandidate>>)Task.CompletedTask;
            }
        }
    }
}
