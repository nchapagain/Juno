namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Contract that define the type of filter to be used for TiP node Selection
    /// the type must be a supported type of filter for the EnvironmentSelectionService.
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class EnvironmentFilter : IEquatable<EnvironmentFilter>
    {
        private int? hashCode;

        /// <summary>
        /// Json Constructor for an Environment Filter
        /// </summary>
        /// <param name="type">Type of the environment filter</param>
        /// <param name="parameters">List of parameters that support the execution of the environment filter</param>
        [JsonConstructor]
        public EnvironmentFilter(string type, Dictionary<string, IConvertible> parameters = null)
        {
            type.ThrowIfNullOrWhiteSpace(nameof(type));

            this.Type = type;

            if (parameters?.Any() == true)
            {
                this.Parameters = new Dictionary<string, IConvertible>(parameters, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                this.Parameters = new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Initializes a <see cref="EnvironmentFilter"/>
        /// </summary>
        /// <param name="other">Other Environment Filter to use as template</param>
        public EnvironmentFilter(EnvironmentFilter other)
            : this(other?.Type, other?.Parameters)
        {
        }

        /// <summary>
        /// Type of environment filter i.e. `Juno.EnvironmentSelction.Filters.BiosFilter`
        /// </summary>
        [JsonProperty(PropertyName = "type", Required = Required.Always)]
        public string Type { get; }

        /// <summary>
        /// List of parameters that support the execution of the 
        /// environment filter.
        /// </summary>
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        [JsonProperty(PropertyName = "parameters", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, IConvertible> Parameters { get; }

        /// <summary>
        /// Translates this on to an EnvironmentFilter
        /// </summary>
        /// <returns>String representation of the object</returns>
        public override string ToString()
        {
            IEnumerable<string> keys = this.Parameters.Keys.OrderBy(key => key);
            StringBuilder builder = new StringBuilder();
            builder.Append(this.Type);
            foreach (string key in keys)
            {
                builder.Append(key);
                builder.Append(this.Parameters[key]);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Determine if the other <see cref="EnvironmentFilter"/>
        /// and this <see cref="Environment"/> are equal
        /// </summary>
        /// <param name="other"><see cref="EnvironmentFilter"/></param>
        /// <returns>If the instances are equal</returns>
        public bool Equals(EnvironmentFilter other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }
            
            EnvironmentFilter itemDescription = obj as EnvironmentFilter;
            if (itemDescription == null)
            {
                return false; 
            }

            return this.Equals(itemDescription);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder()
                    .AppendParameters(this.Parameters)
                    .Append(this.Type)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
