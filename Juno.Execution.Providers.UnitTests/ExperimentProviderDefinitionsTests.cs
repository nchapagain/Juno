namespace Juno.Execution.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Juno.Contracts;
    using Juno.Execution.Providers.Demo;
    using Juno.Providers;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentProviderDefinitionsTests
    {
        private static IEnumerable<Type> knownProviderTypes;
        private ServiceCollection mockProviderServices;
        
        [OneTimeSetUp]
        public static void SetUpFixture()
        {
            ExperimentProviderDefinitionsTests.knownProviderTypes = ExperimentProviderDefinitionsTests.GetKnownProviderTypes();
        }

        [SetUp]
        public void SetupTest()
        {
            this.mockProviderServices = new ServiceCollection();
        }

        [Test]
        public void KnownExecutionProvidersHaveExpectedAttributes()
        {
            foreach (Type providerType in ExperimentProviderDefinitionsTests.knownProviderTypes)
            {
                Assert.IsTrue(
                    providerType.GetCustomAttributes<ExecutionConstraintsAttribute>()?.Any() == true,
                    $"All experiment providers must be decorated with an '{typeof(ExecutionConstraintsAttribute)}'. Provider '{providerType.FullName}' does not follow this rule.");
            }
        }

        [Test]
        public void KnownExecutionProvidersCanBeInstantiatedByTheProviderFactory()
        {
            foreach (Type providerType in ExperimentProviderDefinitionsTests.knownProviderTypes)
            {
                ExperimentComponent component = new ExperimentComponent(
                    providerType.FullName,
                    providerType.Name,
                    providerType.Name,
                    "Any Group");

                SupportedStepType stepType = component.GetSupportedStepType();
                IExperimentProvider provider = null;

                try
                {
                    provider = ExperimentProviderFactory.CreateProvider(component, this.mockProviderServices);
                }
                catch (Exception exc)
                {
                    Assert.Fail($"Provider '{providerType.FullName}' cannot be instantiated by the provider factory as expected. {exc.Message}");
                }

                Assert.IsNotNull(provider, $"Provider '{providerType.FullName}' was not instantiated by the provider factory as expected.");
            }
        }

        private static IEnumerable<Type> GetKnownProviderTypes()
        {
            List<Type> providerTypes = new List<Type>();
            List<Type> exclusions = new List<Type>
            {
                typeof(ExperimentProvider),
                typeof(MockExperimentProvider)
            };

            Type providerBaseType = typeof(ExperimentProvider);
            Type representativeType = typeof(ExampleClusterSelectionProvider); // Force loading the Juno.Execution.Provider assembly

            foreach (string assemblyPath in Directory.GetFiles(Path.GetDirectoryName(Assembly.GetAssembly(typeof(ExperimentProviderDefinitionsTests)).Location), "*.dll"))
            {
                string fileName = Path.GetFileName(assemblyPath);

                if (fileName.Contains("Juno") && !fileName.Contains("Tests"))
                {
                    Assembly junoAssembly = Assembly.LoadFrom(assemblyPath);
                    IEnumerable<Type> executionProviderTypes = junoAssembly.GetTypes()
                        .Where(t => t.IsSubclassOf(providerBaseType))
                        .Where(t => !exclusions.Contains(t));

                    if (executionProviderTypes?.Any() == true)
                    {
                        foreach (Type providerType in executionProviderTypes)
                        {
                            providerTypes.Add(providerType);
                        }
                    }
                }
            }

            return providerTypes;
        }
    }
}
