namespace Juno.EnvironmentSelection.NodeSelectionFilters
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Abstract class Node Selection Filters must implement. This abstract class offers a contract that each
    /// cluster selection filter must adhere to.
    /// </summary>
    [KustoColumn(Name = ProviderConstants.NodeId, AdditionalInfo = false)]
    [KustoColumn(Name = ProviderConstants.ClusterId, AdditionalInfo = false, ComposesCacheKey = true)]
    [KustoColumn(Name = ProviderConstants.Region, AdditionalInfo = false)]
    [KustoColumn(Name = ProviderConstants.Rack, AdditionalInfo = false)]
    [SupportedFilter(Name = FilterParametersBase.IncludeCluster, Type = typeof(string), Required = true, CacheLabel = ProviderConstants.ClusterId, SetName = ProviderConstants.ClusterId)]
    [SupportedFilter(Name = FilterParametersBase.ExcludeCluster, Type = typeof(string), Required = false, SetName = ProviderConstants.ClusterId)]
    public abstract class NodeSelectionFilter : KustoFilterProvider
    {
        /// <inheritdoc/>
        public NodeSelectionFilter(IServiceCollection services, TimeSpan ttl, IConfiguration configuration, ILogger logger, string query)
            : base(services, ttl, configuration, logger, query)
        {
            this.DictionaryKey = ProviderConstants.NodeId;
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
                        return query.Replace($"${key}$", "\"\"", StringComparison.OrdinalIgnoreCase);
                    }

                    string[] parameters = ((string)value).Split(',', ';');
                    return query.Replace($"${key}$", $"{string.Join(", ", parameters.Select(param => $"'{param.Trim()}'"))}", StringComparison.OrdinalIgnoreCase);
                };

                return baseReplacers;
            }
        }

        internal static class FilterParametersBase
        {
            internal const string IncludeCluster = "includeCluster";
            internal const string ExcludeCluster = "excludeCluster";
        }
    }
}
