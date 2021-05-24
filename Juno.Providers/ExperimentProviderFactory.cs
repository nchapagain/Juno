namespace Juno.Providers
{
    using System;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;  
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provides a factory for the creation of providers that facilitate different
    /// aspects of building and executing experiments.
    /// </summary>
    public static class ExperimentProviderFactory
    {
        /// <summary>
        /// Creates a workflow step execution provider for the experiment component.
        /// </summary>
        /// <param name="component">The experiment component.</param>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        /// <param name="providerType">The type of the provider (e.g. EnvironmentSelection, EnvironmentSetup, EnvironmentCleanup).</param>
        /// <returns>
        /// An <see cref="IExperimentProvider"/> instance that can handle the specifics of
        /// the experiment workflow step/component definition.
        /// </returns>
        public static IExperimentProvider CreateProvider(ExperimentComponent component, IServiceCollection services, SupportedStepType? providerType = null)
        {
            component.ThrowIfNull(nameof(component));
            services.ThrowIfNull(nameof(services));

            try
            {
                if (providerType.HasValue)
                {
                    SupportedStepType componentStepType = component.GetSupportedStepType();
                    if (componentStepType != providerType.Value)
                    {
                        throw new SchemaException(
                            $"Invalid component definition. The component '{component.Name}' does not reference a provider of type '{providerType.ToString()}'");
                    }
                }

                IExperimentProvider provider = ExperimentProviderFactory.CreateExperimentProvider(component, services);

                return provider;
            }
            catch (SchemaException exc)
            {
                throw new ProviderException(
                    $"The '{component.ComponentType}' supported step type does not match the specified step type '{providerType.ToString()}'.",
                    ErrorReason.ProviderDefinitionInvalid,
                    exc);
            }
            catch (TypeLoadException exc)
            {
                throw new ProviderException(
                    $"An experiment provider of type '{component.ComponentType}' does not exist in the app domain.",
                    ErrorReason.ProviderNotFound,
                    exc);
            }
            catch (InvalidCastException exc)
            {
                throw new ProviderException(
                    $"The type '{component.ComponentType}' is not a valid experiment provider.",
                    ErrorReason.ProviderNotSupported,
                    exc);
            }
        }

        private static IExperimentProvider CreateExperimentProvider(ExperimentComponent component, IServiceCollection services)
        {
            try
            {
                return (IExperimentProvider)Activator.CreateInstance(component.GetProviderType(), services);
            }
            catch (MissingMethodException exc)
            {
                throw new ProviderException(
                   $"The experiment provider class for this component '{component.ComponentType}' must have a constructor " +
                   $"that takes in a single '{typeof(IServiceProvider)}' parameter.",
                   ErrorReason.ProviderDefinitionInvalid,
                   exc);
            }
        }
    }
}
