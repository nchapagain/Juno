namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentProviderTypeCacheTests
    {
        private static Type providerBaseType;
        private static string testAssemblyDirectory;

        [OneTimeSetUp]
        public static void SetupFixture()
        {
            ExperimentProviderTypeCacheTests.providerBaseType = typeof(ExperimentProvider);
            ExperimentProviderTypeCacheTests.testAssemblyDirectory = Path.GetDirectoryName(Assembly.GetAssembly(typeof(ExperimentProviderTypeCacheTests)).Location);
        }

        // The tests below must run in order because the provider type cache is a singleton
        // object.  The tests would conflict with one another if they run concurrently.

        [Test]
        [Order(1)]
        public void TypeCacheLoadsExpectedProviderAssemblies()
        { 
            ExperimentProviderTypeCache.Instance.Clear();
            Assert.IsTrue(ExperimentProviderTypeCache.Instance.Count == 0);
            ExperimentProviderTypeCache.Instance.LoadProviders(ExperimentProviderTypeCacheTests.testAssemblyDirectory);
            Assert.IsTrue(ExperimentProviderTypeCache.Instance.Count > 0);
        }

        [Test]
        [Order(2)]
        public void TypeCacheLoadsExpectedProviderTypes()
        {
            ExperimentProviderTypeCache.Instance.Clear();
            Assert.IsTrue(ExperimentProviderTypeCache.Instance.Count == 0);
            IEnumerable<Assembly> providerAssemblies = ExperimentProviderTypeCacheTests.GetProviderAssemblies(ExperimentProviderTypeCacheTests.testAssemblyDirectory);

            IEnumerable<Type> expectedProviderTypes = providerAssemblies.SelectMany(assembly => assembly.GetTypes()
                .Where(type => type.IsSubclassOf(ExperimentProviderTypeCacheTests.providerBaseType)));

            ExperimentProviderTypeCache.Instance.LoadProviders(ExperimentProviderTypeCacheTests.testAssemblyDirectory);
            
            Assert.IsTrue(ExperimentProviderTypeCache.Instance.Count > 0);
            CollectionAssert.AreEquivalent(expectedProviderTypes.Select(type => type.FullName), ExperimentProviderTypeCache.Instance.Keys);
            CollectionAssert.AreEquivalent(expectedProviderTypes, ExperimentProviderTypeCache.Instance.Values);
        }

        private static IEnumerable<Assembly> GetProviderAssemblies(string assemblyDirectory)
        {
            List<Assembly> providerAssemblies = new List<Assembly>();
            Type providerBaseType = typeof(ExperimentProvider);

            foreach (string assemblyPath in Directory.GetFiles(assemblyDirectory, "*.dll"))
            {
                string fileName = Path.GetFileName(assemblyPath);

                if (fileName.Contains("Juno"))
                {
                    Assembly assembly = Assembly.LoadFrom(assemblyPath);
                    if (assembly.GetCustomAttribute<ExperimentProviderAssemblyAttribute>() != null)
                    {
                        providerAssemblies.Add(assembly);
                    }
                }
            }

            return providerAssemblies;
        }
    }
}
