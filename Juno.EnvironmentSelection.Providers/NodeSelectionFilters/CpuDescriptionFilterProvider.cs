namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Juno.Providers.Validation;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Filter that retrieves nodes based on their CPU Description
    /// </summary>
    [SupportedFilter(Name = FilterParameters.IncludeCpuDescription, Type = typeof(string), Required = false, CacheLabel = ProviderConstants.IncludeRootString, Default = ProviderConstants.IncludeRootString)]
    [SupportedFilter(Name = FilterParameters.ExcludeCpuDescription, Type = typeof(string), Required = false, CacheLabel = ProviderConstants.ExcludeRootString, Default = ProviderConstants.ExcludeRootString)]
    [KustoColumn(Name = ProviderConstants.CpuDescription, AdditionalInfo = true)]
    [KustoColumn(Name = ProviderConstants.IncludeRootString, AdditionalInfo = true, ComposesCacheKey = true)]
    [KustoColumn(Name = ProviderConstants.ExcludeRootString, AdditionalInfo = true, ComposesCacheKey = true)]
    public class CpuDescriptionFilterProvider : NodeSelectionFilter
    {
        /// <inheritdoc/>
        public CpuDescriptionFilterProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.CpuIdTtl, configuration, logger, Properties.Resources.CpuDescriptionQuery)
        {
        }

        /// <inheritdoc/>
        protected override void ValidateParameters(EnvironmentFilter filter)
        {
            filter.ThrowIfNull(nameof(filter));
            base.ValidateParameters(filter);

            // This is a provider where we do not support a string to take an implicit form of a list.
            // Either include/exclude filter must be present.
            // Both include and exclude can not be present.
            if (!filter.Parameters.ContainsKey(FilterParameters.IncludeCpuDescription) && !filter.Parameters.ContainsKey(FilterParameters.ExcludeCpuDescription))
            {
                throw new SchemaException($"{nameof(CpuDescriptionFilterProvider)} requires {FilterParameters.IncludeCpuDescription} or {FilterParameters.ExcludeCpuDescription} be present in parameter list.");
            }

            if (filter.Parameters.ContainsKey(FilterParameters.IncludeCpuDescription) && filter.Parameters.ContainsKey(FilterParameters.ExcludeCpuDescription))
            {
                throw new SchemaException($"{nameof(CpuDescriptionFilterProvider)} does not support execution with both {FilterParameters.IncludeCpuDescription} and {FilterParameters.ExcludeCpuDescription}");
            }

            IConvertible parameter = filter.Parameters.ContainsKey(FilterParameters.IncludeCpuDescription)
                ? filter.Parameters[FilterParameters.IncludeCpuDescription]
                : filter.Parameters[FilterParameters.ExcludeCpuDescription];

            IList<string> parameterList = parameter.ToString().ToList(',', ';');
            if (parameterList.Count > 1)
            {
                throw new SchemaException($"{FilterParameters.IncludeCpuDescription} and {FilterParameters.ExcludeCpuDescription} does not support string parameters which are implicitly lists. Please place one {typeof(string)} value for " +
                    $"either {FilterParameters.IncludeCpuDescription} or {FilterParameters.ExcludeCpuDescription}");
            }
        }

        internal static class FilterParameters
        {
            internal const string IncludeCpuDescription = "includeCpuDescription";
            internal const string ExcludeCpuDescription = "excludeCpuDescription";
        }
    }
}
