namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// 
    /// </summary>
    public static class GoalComponentProviderTypeExtension
    {
        /// <summary>
        /// Returns the Goal Component type associated with the
        /// Goal Component component
        /// </summary>
        /// <param name="component">Goal Component to check provider type</param>
        /// <returns>The type of provider</returns>
        public static Type GetProviderType(this GoalComponent component)
        {
            component.ThrowIfNull(nameof(component));
            Type matchingType = Type.GetType(component.Type, throwOnError: false);
            if (matchingType == null)
            {
                matchingType = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => assembly.GetType(component.Type, throwOnError: false) != null)
                    ?.GetType(component.Type);
            }

            if (matchingType == null)
            {
                throw new TypeLoadException(
                    $"A component provider of type '{component.Type}' does not exist in the app domain. ");
            }

            return matchingType;
        }

        /// <summary>
        /// Returns all of the supported parameters for the component
        /// </summary>
        /// <param name="component">Schedule Action to associate with the list of parameters</param>
        /// <returns>A list of supported parameters</returns>
        public static IEnumerable<SupportedParameterAttribute> GetSupportedParameters(this GoalComponent component)
        {
            component.ThrowIfNull(nameof(component));
            var providerType = component.GetProviderType();
            return providerType.GetCustomAttributes<SupportedParameterAttribute>(true);
        }
    }
}
