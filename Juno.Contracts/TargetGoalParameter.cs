namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Class to help wrap dictionary parameters into a json parameters for targetgoals of <see cref="ExecutionGoalParameter"/> 
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class TargetGoalParameter : IEquatable<TargetGoalParameter>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="TargetGoalParameter"/> class.
        /// </summary>
        /// <param name="name">The name of the target goal.</param>
        /// <param name="enabled">True/false if the target goal is enabled.</param>
        /// <param name="parameters">Parameters associated with the Target Goal.</param>
        [JsonConstructor]
        public TargetGoalParameter(string name, bool enabled, IDictionary<string, IConvertible> parameters = null)
        {
            name.ThrowIfNullOrWhiteSpace(nameof(name));

            this.Name = name;
            this.Enabled = enabled;
            this.Parameters = parameters == null
                ? new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, IConvertible>(parameters, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Designated workload of the Target Goal
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always, Order = 10)]
        public string Name { get; }

        /// <summary>
        /// True/False if the target goal is enabled.
        /// </summary>
        [JsonProperty(PropertyName = "enabled", Required = Required.Always, Order = 20)]
        public bool Enabled { get; }

        /// <summary>
        /// Dictonary parameters of the Target Goal
        /// </summary>
        [JsonProperty(PropertyName = "parameters", Required = Required.Default, Order = 30)]
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        public IDictionary<string, IConvertible> Parameters { get; }

        /// <inheritdoc />/>
        public bool Equals(TargetGoalParameter other)
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

            TargetGoalParameter itemDescription = obj as TargetGoalParameter;
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
                    .Append(this.Name)
                    .Append(this.Enabled)
                    .AppendParameters(this.Parameters)
                    .ToString().ToUpperInvariant().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
