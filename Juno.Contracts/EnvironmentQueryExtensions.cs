namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension methods for environmentquery
    /// </summary>
    public static class EnvironmentQueryExtensions
    {
        private static readonly IDictionary<FilterParameterSource, string> SourceTags = new Dictionary<FilterParameterSource, string>()
        { 
            [FilterParameterSource.Subscription] = "$.subscription.",
            [FilterParameterSource.External] = "$.external.",
            [FilterParameterSource.Cluster] = "$.cluster."
        };

        /// <summary>
        /// Returns if this query has references to parameters whose source is external
        /// </summary>
        /// <param name="query">The query to check</param>
        /// <returns>True if the qeury references external parameters</returns>
        public static bool HasExternalReferences(this EnvironmentQuery query)
        {
            query.ThrowIfNull(nameof(query));
            return EnvironmentQueryExtensions.HasReferences(query, FilterParameterSource.External);
        }

        /// <summary>
        /// Returns if this query has references to parameters whose source comes from the
        /// subscription filters.
        /// </summary>
        /// <param name="query">The query to check</param>
        /// <returns>True if the qeury references subscription parameters</returns>
        public static bool HasSubscriptionReferences(this EnvironmentQuery query)
        {
            query.ThrowIfNull(nameof(query));
            return EnvironmentQueryExtensions.HasReferences(query, FilterParameterSource.Subscription);
        }

        /// <summary>
        /// Returns if this query has references to parameters whose source comes from the
        /// cluster filters.
        /// </summary>
        /// <param name="query">The query to check</param>
        /// <returns>True if the query references cluster parameters</returns>
        public static bool HasClusterReference(this EnvironmentQuery query)
        {
            query.ThrowIfNull(nameof(query));
            return EnvironmentQueryExtensions.HasReferences(query, FilterParameterSource.Cluster);
        }

        /// <summary>
        /// Replaces parameter references where the parameter value came from an external source
        /// </summary>
        /// <param name="query">The query whose parameters should be replaced</param>
        public static EnvironmentQuery ReplaceExternalReferences(this EnvironmentQuery query)
        {
            query.ThrowIfNull(nameof(query));
            return EnvironmentQueryExtensions.ReplaceReferences(query, FilterParameterSource.External);
        }

        /// <summary>
        /// Replaces parameter references where the parameter value came from cluster filters
        /// </summary>
        /// <param name="query">The query whose parameters should be replaced</param>
        public static EnvironmentQuery ReplaceClusterReferences(this EnvironmentQuery query)
        {
            query.ThrowIfNull(nameof(query));
            return EnvironmentQueryExtensions.ReplaceReferences(query, FilterParameterSource.Cluster);
        }

        /// <summary>
        /// Replaces parameter references where the parameter value came from the subscription filters
        /// </summary>
        /// <param name="query"></param>
        public static EnvironmentQuery ReplaceSubscriptionReferences(this EnvironmentQuery query)
        {
            query.ThrowIfNull(nameof(query));
            return EnvironmentQueryExtensions.ReplaceReferences(query, FilterParameterSource.Subscription);
        }

        private static bool HasReferences(EnvironmentQuery query, FilterParameterSource source)
        {
            string sourceTag = EnvironmentQueryExtensions.SourceTags[source];
            return query.Filters.Any(filter =>
            {
                return filter.Parameters.Any(parameter => parameter.Value.ToString().StartsWith(sourceTag, StringComparison.OrdinalIgnoreCase));
            });
        }

        private static EnvironmentQuery ReplaceReferences(EnvironmentQuery query, FilterParameterSource source)
        {
            string sourceTag = EnvironmentQueryExtensions.SourceTags[source];
            IList<EnvironmentFilter> newFilters = new List<EnvironmentFilter>();
            foreach (EnvironmentFilter filter in query.Filters)
            {
                Dictionary<string, IConvertible> newParameters = new Dictionary<string, IConvertible>();
                foreach (KeyValuePair<string, IConvertible> parameter in filter.Parameters)
                {
                    // Check if the current value has the source tag prefix
                    string reference = parameter.Value.ToString();
                    IConvertible parameterValue = parameter.Value;
                    if (reference.StartsWith(sourceTag, StringComparison.OrdinalIgnoreCase))
                    {
                        // If it does lets strip it of the tag and replace the reference
                        string parameterName = reference.Substring(sourceTag.Length);

                        // If the parameter dictionary doesnt contain the reference then throw an error.
                        if (!query.Parameters.TryGetValue(parameterName, out parameterValue))
                        {
                            throw new SchemaException($"Query: {query.Name}, reference parameter: {parameterName}, but has no value");
                        }
                    }

                    newParameters.Add(parameter.Key, parameterValue);
                }

                newFilters.Add(new EnvironmentFilter(filter.Type, newParameters));
            }

            return new EnvironmentQuery(query.Name, query.NodeCount, newFilters, query.NodeAffinity, query.Parameters);
        }
    }
}
