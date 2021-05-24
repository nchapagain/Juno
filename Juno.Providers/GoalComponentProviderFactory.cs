namespace Juno.Providers
{
    using System;
    using System.Linq;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provides a factory for the creation of providers that facilitate different
    /// aspects of executing an Execution Goal.
    /// </summary>
    public static class GoalComponentProviderFactory
    {
        /// <summary>
        /// Creates a <see cref="IPreconditionProvider"/>
        /// </summary>
        /// <param name="component">A static definition of a precondition that gives 
        /// instruction on which provider to instantiate.</param>
        /// <param name="services">A list of services that can be used for dependency injection.</param>
        /// <returns>A provider that offers instructions on execution.</returns>
        public static IPreconditionProvider CreatePreconditionProvider(Precondition component, IServiceCollection services)
        {
            component.ThrowIfNull(nameof(component));
            services.ThrowIfNull(nameof(services));

            try
            {
                if (services.HasService<IPreconditionProvider>())
                {
                    return services.GetService<IPreconditionProvider>();
                }

                IPreconditionProvider provider = (IPreconditionProvider)Activator.CreateInstance(component.GetProviderType(), services);
                return provider;
            }
            catch (MissingMethodException exc)
            {
                throw new ProviderException(
                   $"The precondition provider class for this component '{component.Type}' must have a constructor " +
                   $"that takes in a single '{typeof(IServiceProvider)}' parameter.",
                   ErrorReason.ProviderDefinitionInvalid,
                   exc);
            }
            catch (TypeLoadException exc)
            {
                throw new ProviderException(
                    $"A precondition provider of type '{component.Type}' does not exist in the app domain.",
                    ErrorReason.ProviderNotFound,
                    exc);
            }
            catch (InvalidCastException exc)
            {
                throw new ProviderException(
                    $"The type '{component.Type}' is not a valid precondition provider.",
                    ErrorReason.ProviderNotSupported,
                    exc);
            }
        }

        /// <summary>
        /// Creates a <see cref="IScheduleActionProvider"/>
        /// </summary>
        /// <param name="component">A static definition of a Schedule Action that 
        /// gives instruction on which provider to instantiate.</param>
        /// <param name="services">A list of services that can be used for dependency injection.</param>
        /// <returns>A provider that offers instructions on execution.</returns>
        public static IScheduleActionProvider CreateScheduleActionProvider(ScheduleAction component, IServiceCollection services)
        {
            component.ThrowIfNull(nameof(component));
            services.ThrowIfNull(nameof(services));

            try
            {
                if (services.HasService<IScheduleActionProvider>())
                {
                    return services.GetService<IScheduleActionProvider>();
                }

                IScheduleActionProvider provider = (ScheduleActionProvider)Activator.CreateInstance(component.GetProviderType(), services);
                return provider;
            }
            catch (MissingMethodException exc)
            {
                throw new ProviderException(
                   $"The schedule action provider class for this component '{component.Type}' must have a constructor " +
                   $"that takes in a single '{typeof(IServiceProvider)}' parameter.",
                   ErrorReason.ProviderDefinitionInvalid,
                   exc);
            }
            catch (TypeLoadException exc)
            {
                throw new ProviderException(
                    $"A schedule action provider of type '{component.Type}' does not exist in the app domain.",
                    ErrorReason.ProviderNotFound,
                    exc);
            }
            catch (InvalidCastException exc)
            {
                throw new ProviderException(
                    $"The type '{component.Type}' is not a valid schedule action provider.",
                    ErrorReason.ProviderNotSupported,
                    exc);
            }
        }
    }
}
