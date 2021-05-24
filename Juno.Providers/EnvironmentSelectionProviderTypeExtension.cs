namespace Juno.Providers
{
    using System;
    using System.CodeDom;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension methods for the Environment filter provider
    /// </summary>
    public static class EnvironmentSelectionProviderTypeExtension
    {
        /// <summary>
        /// Retrieve the type of Provider
        /// </summary>
        /// <param name="filter">The filter which to derive the type from</param>
        /// <returns>Type of the environment filter</returns>
        public static Type GetProviderType(this EnvironmentFilter filter)
        {
            filter.ThrowIfNull(nameof(filter));

            Type matchingType = Type.GetType(filter.Type, throwOnError: false);
            if (matchingType == null)
            {
                matchingType = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => assembly.GetType(filter.Type, throwOnError: false) != null)
                    ?.GetType(filter.Type);
            }

            if (matchingType == null)
            {
                throw new TypeLoadException(
                    $"A component provider of type '{filter.Type}' does not exist in the app domain. ");
            }

            return matchingType;
        }

        /// <summary>
        /// Returns the category in which the environment filter is to belong
        /// </summary>
        /// <param name="filter">The filter to categorize</param>
        /// <returns>The Filter Category</returns>
        public static FilterCategory GetProviderCategory(this EnvironmentFilter filter)
        {
            string type = filter.GetProviderType().ToString();
            if (type.Contains(Constants.NodeSelection, StringComparison.OrdinalIgnoreCase))
            {
                return FilterCategory.Node;
            }

            if (type.Contains(Constants.SubscriptionSelection, StringComparison.OrdinalIgnoreCase))
            {
                return FilterCategory.Subscription;
            }

            if (type.Contains(Constants.ClusterSelection, StringComparison.OrdinalIgnoreCase))
            {
                return FilterCategory.Cluster;
            }

            return FilterCategory.Undefined;
        }

        private static class Constants
        {
            internal const string NodeSelection = "NodeSelectionFilters";
            internal const string SubscriptionSelection = "SubscriptionFilters";
            internal const string ClusterSelection = "ClusterSelectionFilters";
        }
    }
}
