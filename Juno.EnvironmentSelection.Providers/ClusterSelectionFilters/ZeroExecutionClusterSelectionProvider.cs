namespace Juno.EnvironmentSelection.ClusterSelectionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provider to select clusters and it's regions based on the execution count.
    /// </summary>
    [KustoColumn(Name = ProviderConstants.ExperimentName, AdditionalInfo = true, ComposesCacheKey = true)]
    [KustoColumn(Name = ProviderConstants.CpuId, AdditionalInfo = false, ComposesCacheKey = true)]
    [SupportedFilter(Name = FilterParameters.CpuId, Type = typeof(string), Required = true, CacheLabel = ProviderConstants.CpuId)]
    [SupportedFilter(Name = FilterParameters.ExperimentName, Type = typeof(string), Required = true, CacheLabel = ProviderConstants.ExperimentName)]
    public class ZeroExecutionClusterSelectionProvider : ClusterSelectionFilter
    {
        /// <inheritdoc />
        public ZeroExecutionClusterSelectionProvider(IServiceCollection services, IConfiguration configuration, ILogger logger)
            : base(services, CacheConstants.ZeroExecutionClusterSelectionTtl, configuration, logger, Properties.Resources.ZeroExecutionClusterSelectionQuery)
        {
        }

        /// <inheritdoc/>
        protected override IDictionary<Type, Func<string, IConvertible, string, string>> ParameterReplacers
        {
            get
            {
                var baseReplacers = base.ParameterReplacers;
                baseReplacers[typeof(string)] = (key, value, query) =>
                {
                    if (value == null)
                    {
                        return query.Replace($"${key}$", $"dynamic([])", StringComparison.OrdinalIgnoreCase);
                    }

                    string[] parameters = ((string)value).Split(',', ';');
                    return query.Replace($"${key}$", $"dynamic([{string.Join(", ", parameters.Select(param => $"'{param.Trim()}'"))}])", StringComparison.Ordinal);
                };

                return baseReplacers;
            }
        }

        internal static class FilterParameters
        {
            internal const string ExperimentName = "experimentName";
            internal const string CpuId = "cpuId";
        }
    }
}
