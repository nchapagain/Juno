namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// 
    /// </summary>
    public static class ExperimentProviderTypeExtensions
    {
        /// <summary>
        /// Returns the matching runtime data type for the component type provided.
        /// </summary>
        /// <param name="component">The component for which to get the runtime type/provider.</param>
        /// <returns>
        /// The runtime/provider <see cref="Type"/> for the component type.
        /// </returns>
        public static Type GetProviderType(this ExperimentComponent component)
        {
            component.ThrowIfNull(nameof(component));

            Type matchingType;
            if (!ExperimentProviderTypeCache.Instance.TryGetValue(component.ComponentType, out matchingType))
            {
                matchingType = Type.GetType(component.ComponentType, throwOnError: false);
                if (matchingType == null)
                {
                    // Derived types might be in other assemblies (e.g. mocks).
                    matchingType = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(assembly => assembly.GetType(component.ComponentType, throwOnError: false) != null)
                        ?.GetType(component.ComponentType);
                }

                if (matchingType != null)
                {
                    // Cache the type for efficiency. We want to support the component type being defined
                    // in an assembly other than the core Juno assemblies.  However, there could be a lot
                    // of assemblies at runtime, so we only want to take the cost once to find the type.
                    ExperimentProviderTypeCache.Instance.Add(component.ComponentType, matchingType);
                }
            }

            if (matchingType == null)
            {
                throw new TypeLoadException(
                    $"A component provider of type '{component.ComponentType}' does not exist in the app domain.");
            }

            return matchingType;
        }

        /// <summary>
        /// Returns all the supported parameters for an experiment component defined in attributes.
        /// </summary>
        /// <param name="component">
        /// The component for which to get the supported runtime execution targets.
        /// </param>
        /// <returns>
        /// The supported parameters for the component.
        /// </returns>
        public static IEnumerable<SupportedParameterAttribute> GetSupportedParameters(this ExperimentComponent component)
        {
            component.ThrowIfNull(nameof(component));
            var providerType = component.GetProviderType();
            return providerType.GetCustomAttributes<SupportedParameterAttribute>(true);
        }

        /// <summary>
        /// Returns the supported execution targets for the step provider/type.
        /// </summary>
        /// <param name="stepProviderType">The data type of the step provider.</param>
        /// <returns>
        /// The supported runtime execution targets for the provider.
        /// </returns>
        public static SupportedStepTarget GetSupportedStepTarget(this Type stepProviderType)
        {
            stepProviderType.ThrowIfNull(nameof(stepProviderType));

            IEnumerable<ExecutionConstraintsAttribute> attributes = stepProviderType.GetCustomAttributes<ExecutionConstraintsAttribute>(true);
            if (attributes?.Any() != true)
            {
                throw new ProviderException(
                    $"A provider of type '{stepProviderType}' is not a valid provider because it is not attributed " +
                    $"with a '{typeof(ExecutionConstraintsAttribute).FullName}' describing the experiment step target.",
                    ErrorReason.ProviderDefinitionInvalid);
            }

            return attributes.First().StepTarget;
        }

        /// <summary>
        /// Returns the supported execution targets for the step provider/type that
        /// handles the runtime execution requirements of the component.
        /// </summary>
        /// <param name="component">
        /// The component for which to get the supported runtime execution targets.
        /// </param>
        /// <returns>
        /// The supported runtime execution targets for the component.
        /// </returns>
        public static SupportedStepTarget GetSupportedStepTarget(this ExperimentComponent component)
        {
            component.ThrowIfNull(nameof(component));

            return component.GetProviderType().GetSupportedStepTarget();
        }

        /// <summary>
        /// Returns the supported experiment step type for the step provider/type.
        /// </summary>
        /// <param name="stepProviderType">The data type of the step provider.</param>
        /// <returns>
        /// The supported step type for the provider.
        /// </returns>
        public static SupportedStepType GetSupportedStepType(this Type stepProviderType)
        {
            stepProviderType.ThrowIfNull(nameof(stepProviderType));

            IEnumerable<ExecutionConstraintsAttribute> attributes = stepProviderType.GetCustomAttributes<ExecutionConstraintsAttribute>(true);
            if (attributes?.Any() != true)
            {
                throw new ProviderException(
                    $"A provider of type '{stepProviderType}' is not a valid provider because it is not attributed " +
                    $"with a '{typeof(ExecutionConstraintsAttribute).FullName}' describing the experiment step type it supports.",
                    ErrorReason.ProviderDefinitionInvalid);
            }

            return attributes.First().StepType;
        }

        /// <summary>
        /// Returns the supported experiment step type for the step provider/type that
        /// handles the runtime execution requirements of the component.
        /// </summary>
        /// <param name="component">The component for which to get the runtime type/provider.</param>
        /// <returns>
        /// The supported step type for the component.
        /// </returns>
        public static SupportedStepType GetSupportedStepType(this ExperimentComponent component)
        {
            component.ThrowIfNull(nameof(component));

            return component.GetProviderType().GetSupportedStepType();
        }
    }
}
