namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Provides a cache for object types associated with Juno experiments.
    /// </summary>
    public class ExperimentProviderTypeCache : Dictionary<string, Type>
    {
        private static readonly Type ProviderBaseType = typeof(ExperimentProvider);

        private ExperimentProviderTypeCache()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="ExperimentProviderTypeCache"/>
        /// </summary>
        public static ExperimentProviderTypeCache Instance { get; } = new ExperimentProviderTypeCache();

        /// <summary>
        /// Loads provider types from assemblies in the path provided.
        /// </summary>
        public void LoadProviders(string assemblyDirectory)
        {
            foreach (string assemblyPath in ExperimentProviderTypeCache.GetAssemblies(assemblyDirectory))
            {
                try
                {
                    Assembly assembly = Assembly.LoadFrom(assemblyPath);
                    if (ExperimentProviderTypeCache.IsProviderAssembly(assembly))
                    {
                        this.CacheProviderTypes(assembly);
                    }
                }
                catch (BadImageFormatException)
                {
                    // For the case that our exclusions miss assemblies that are NOT intermediate/IL
                    // assemblies, we need to handle this until we have a better model.
                }
            }
        }

        private static IEnumerable<string> GetAssemblies(string directoryPath)
        {
            List<string> assemblies = new List<string>();
            IEnumerable<string> allAssemblies = Directory.GetFiles(directoryPath, "*.dll");

            foreach (string assemblyPath in allAssemblies)
            {
                string fileName = Path.GetFileName(assemblyPath);
                if (!ExperimentProviderTypeCache.IsExcluded(fileName))
                {
                    assemblies.Add(assemblyPath);
                }
            }

            return assemblies;
        }

        private static bool IsExcluded(string fileName)
        {
            // Only Juno assemblies at the moment.
            return !fileName.Contains("Juno", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProviderAssembly(Assembly assembly)
        {
            return assembly.GetCustomAttribute<ExperimentProviderAssemblyAttribute>() != null;
        }

        private void CacheProviderTypes(Assembly providerAssembly)
        {
            IEnumerable<Type> providerTypes = providerAssembly.GetTypes()
                .Where(type => type.IsSubclassOf(ExperimentProviderTypeCache.ProviderBaseType));

            if (providerTypes?.Any() == true)
            {
                foreach (Type providerType in providerTypes)
                {
                    if (!this.ContainsKey(providerType.FullName))
                    {
                        this.Add(providerType.FullName, providerType);
                    }
                }
            }
        }
    }
}
