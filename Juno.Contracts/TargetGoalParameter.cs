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
        /// <param name="id">Unique ID that seperates the Target goals within Execution Goal</param>
        /// <param name="workload"> Workload specfied for each target goal</param>
        /// <param name="parameters">Parameters associated with the Target Goal.</param>
        [JsonConstructor]
        public TargetGoalParameter(string id, string workload, IDictionary<string, IConvertible> parameters = null)
        {
            workload.ThrowIfNullOrWhiteSpace(nameof(workload));
            id.ThrowIfNullOrWhiteSpace(nameof(id));

            this.Id = id;
            this.Workload = workload;
            this.Parameters = parameters == null
                ? new Dictionary<string, IConvertible>()
                : new Dictionary<string, IConvertible>(parameters);
        }

        /// <summary>
        /// Unique ID of the Target Goal
        /// </summary>
        [JsonProperty(PropertyName = "id", Required = Required.Always, Order = 1)]
        public string Id { get; }

        /// <summary>
        /// Designated workload of the Target Goal
        /// </summary>
        [JsonProperty(PropertyName = "workload", Required = Required.Always, Order = 2)]
        public string Workload { get; }

        /// <summary>
        /// Dictonary parameters of the Target Goal
        /// </summary>
        [JsonProperty(PropertyName = "parameters", Required = Required.Default, Order = 3)]
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
                    .Append(this.Workload)
                    .Append(this.Id)
                    .AppendParameters(this.Parameters)
                    .ToString().ToUpperInvariant().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
