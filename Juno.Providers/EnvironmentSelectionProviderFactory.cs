namespace Juno.Providers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Class that offers methods to create EnvironmentFilterProviders
    /// </summary>
    public static class EnvironmentSelectionProviderFactory
    {
        /// <summary>
        /// Create an environment filter provider with the required constructor parameters
        /// </summary>
        /// <param name="filter">The environment filter to derive the provider from</param>
        /// <param name="services">List of services used for dependecny injection</param>
        /// <param name="configuration">The configuration of the current execution environment</param>
        /// <param name="logger">A logger used for capturing telemetry</param>
        /// <returns></returns>
        public static IEnvironmentSelectionProvider CreateEnvironmentFilterProvider(EnvironmentFilter filter, IServiceCollection services, IConfiguration configuration, ILogger logger)
        {
            filter.ThrowIfNull(nameof(filter));
            services.ThrowIfNull(nameof(services));
            configuration.ThrowIfNull(nameof(configuration));
            logger = logger ?? NullLogger.Instance;

            try
            {
                IEnvironmentSelectionProvider provider = (IEnvironmentSelectionProvider)Activator.CreateInstance(filter.GetProviderType(), services, configuration, logger);
                return provider;
            }
            catch (MissingMethodException exc)
            {
                throw new ProviderException(
                   $"The precondition provider class for this component '{filter.Type}' must have a constructor " +
                   $"that takes in a single '{typeof(IServiceProvider)}' parameter.",
                   ErrorReason.ProviderDefinitionInvalid,
                   exc);
            }
            catch (TypeLoadException exc)
            {
                throw new ProviderException(
                    $"A precondition provider of type '{filter.Type}' does not exist in the app domain.",
                    ErrorReason.ProviderNotFound,
                    exc);
            }
            catch (InvalidCastException exc)
            {
                throw new ProviderException(
                    $"The type '{filter.Type}' is not a valid precondition provider.",
                    ErrorReason.ProviderNotSupported,
                    exc);
            }
        }

        /// <summary>
        /// Instantiate all classes that implement the <see cref="IAccountable"/> interface
        /// </summary>
        /// <returns>IEnumerable of IAccountable instances.</returns>
        public static IEnumerable<IAccountable> CreateAccountableInstances(string assemblyPath, IServiceCollection services, IConfiguration configuration, ILogger logger)
        {
            IEnumerable<Type> reserveableTypes = EnvironmentSelectionProviderFactory.GetProviderTypes(assemblyPath)
                .Where(type => typeof(IAccountable).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

            List<IAccountable> result = new List<IAccountable>();
            List<string> errorList = new List<string>();
            foreach (Type reserveableType in reserveableTypes)
            {
                try
                {
                    IAccountable reserveable = (IAccountable)Activator.CreateInstance(reserveableType, services, configuration, logger);
                    result.Add(reserveable);
                }
                catch (MissingMethodException)
                {
                    errorList.Add(reserveableType.Name);
                }
            }

            if (errorList.Any())
            {
                throw new ProviderException($"The {nameof(IAccountable)} components with implementations: {string.Join(", ", errorList)} does not" +
                    $"have a constructor that takes: {nameof(IServiceCollection)}, {nameof(IConfiguration)}, {nameof(ILogger)}");
            }

            return result;
        }

        private static IEnumerable<Type> GetProviderTypes(string directoryPath)
        {
            List<Type> providerTypes = new List<Type>();
            IEnumerable<string> allAssemblies = Directory.GetFiles(directoryPath, "*.dll");

            foreach (string assemblyPath in allAssemblies)
            {
                string fileName = Path.GetFileName(assemblyPath);
                if (fileName.StartsWith("Juno", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Assembly assembly = Assembly.LoadFrom(assemblyPath);
                        if (assembly.GetCustomAttribute<EnvironmentSelectionProviderAssemblyAttribute>() != null)
                        {
                            providerTypes.AddRange(assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(EnvironmentSelectionProvider))));
                        }
                    }
                    catch (BadImageFormatException)
                    { 
                    }
                }
            }

            return providerTypes;
        }
    }
}
