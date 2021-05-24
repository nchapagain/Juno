namespace Juno.Contracts
{ 
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Wrapper for EnvironmentFilters stored in the system
    /// </summary>
    public class EnvironmentQuery : IEquatable<EnvironmentQuery>
    {
        private const int MaxNodeCount = 10;
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of <see cref="EnvironmentQuery"/>
        /// </summary>
        /// <param name="name">Identifier for the environment query</param>
        /// <param name="nodeCount">Number of nodes to return to the user.</param>
        /// <param name="filters">List of filters used to comprise the query</param>
        /// <param name="nodeAffinity">The level at which pairs of nodes parity is evaluated at</param>
        /// <param name="parameters">Parameters used for replacing values in the filters</param>
        [JsonConstructor]
        public EnvironmentQuery(string name, int? nodeCount, IEnumerable<EnvironmentFilter> filters, NodeAffinity nodeAffinity = NodeAffinity.SameRack, IDictionary<string, IConvertible> parameters = null)
        {
            name.ThrowIfNullOrWhiteSpace(nameof(name));
            filters.ThrowIfNullOrEmpty(nameof(filters));
            nodeCount.ThrowIfNull(nameof(nodeCount));

            this.Filters = new List<EnvironmentFilter>(filters);
            this.Name = name;
            this.NodeCount = nodeCount > EnvironmentQuery.MaxNodeCount ? EnvironmentQuery.MaxNodeCount : nodeCount.Value;
            this.NodeAffinity = nodeAffinity;
            this.Parameters = parameters == null ? new Dictionary<string, IConvertible>() : new Dictionary<string, IConvertible>(parameters);
        }

        /// <summary>
        /// Copy Constructor for a <see cref="EnvironmentQuery"/>
        /// </summary>
        /// <param name="other">The original query to to copy</param>
        public EnvironmentQuery(EnvironmentQuery other)
            : this(other?.Name, other?.NodeCount, other?.Filters, other.NodeAffinity, other?.Parameters)
        { 
        }
        
        /// <summary>
        /// Identifier for the Envrionment Query
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; }

        /// <summary>
        /// The max number of nodes to return to the user.
        /// </summary>
        [JsonProperty(PropertyName = "nodeCount", Required = Required.Always)]
        public int NodeCount { get; }

        /// <summary>
        /// The granularity of parity that is applied to the definition of a pair of nodes
        /// </summary>
        [JsonProperty(PropertyName = "nodeAffinity", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public NodeAffinity NodeAffinity { get; }

        /// <summary>
        /// Set of environment filters that together comprise the environment query
        /// </summary>
        [JsonProperty(PropertyName = "filters", Required = Required.Always)]
        public IEnumerable<EnvironmentFilter> Filters { get; }

        /// <summary>
        /// Set of parameters which can be utilized to override values in the filters.
        /// </summary>
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        [JsonProperty(PropertyName = "parameters", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, IConvertible> Parameters { get; }

        /// <summary>
        /// Determines equality between this instance and another
        /// instance of an EnvironmentQuery
        /// </summary>
        /// <param name="other">Other environment query to determine equality</param>
        /// <returns>If the two instances are equal</returns>
        public bool Equals(EnvironmentQuery other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Determines the equality between this instance and another
        /// arbitrary object.
        /// </summary>
        /// <param name="obj">The object to comapte equality against</param>
        /// <returns>If the two objects are equal.</returns>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            EnvironmentQuery itemDescription = obj as EnvironmentQuery;

            if (itemDescription == null)
            {
                return false;
            }

            return this.Equals(itemDescription);
        }

        /// <summary>
        /// Calculates the hashcode of this
        /// </summary>
        /// <returns>the hashcode</returns>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                StringBuilder builder = new StringBuilder(this.Name)
                    .AppendParameters(this.Parameters)
                    .Append(this.NodeCount.ToString());

                builder.Append(this.Filters.Select(filter => filter.GetHashCode().ToString()));

                this.hashCode = builder.ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
