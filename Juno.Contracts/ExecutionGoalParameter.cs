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
    /// Execution Goal Parameter that can be modified by used to create new execution goal based on existing execution goal template.
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class ExecutionGoalParameter : IEquatable<ExecutionGoalParameter>
    {
        private int? hashCode;

        /// <summary>
        /// Json constructor for <see cref="ExecutionGoalParameter"/>
        /// </summary>
        /// <param name="sharedParameters">shared parameters between target goal parameters</param>
        /// <param name="targetGoals">Parameters required inside TargetGoals <see cref="TargetGoalParameter"/></param>
        /// <param name="metadata">Metadata about the execution goal.</param>
        [JsonConstructor]
        public ExecutionGoalParameter(IEnumerable<TargetGoalParameter> targetGoals, IDictionary<string, IConvertible> metadata = null, IDictionary<string, IConvertible> sharedParameters = null)
        {
            targetGoals.ThrowIfNullOrEmpty(nameof(targetGoals));
            this.TargetGoals = new List<TargetGoalParameter>(targetGoals);

            this.Metadata = metadata == null
                ? new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, IConvertible>(metadata, StringComparer.OrdinalIgnoreCase);

            this.SharedParameters = sharedParameters == null
                ? new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, IConvertible>(sharedParameters, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// List of parameters shared by ALL targetGoals
        /// </summary>
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        [JsonProperty(PropertyName = "sharedParameters", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 20)]
        public IDictionary<string, IConvertible> SharedParameters { get; }

        /// <summary>
        /// List of parameters shared by ALL targetGoals
        /// </summary>
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        [JsonProperty(PropertyName = "metadata", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 30)]
        public IDictionary<string, IConvertible> Metadata { get; }

        /// <summary>
        /// List of any paramaters in the execution goal template (only applicable to templates)
        /// </summary>
        [JsonProperty(PropertyName = "targetGoals", Required = Required.Always, Order = 40)]
        public List<TargetGoalParameter> TargetGoals { get; }

        /// <summary>
        /// Gets the experiment name.
        /// </summary>
        [JsonIgnore]
        public string ExperimentName => this.Metadata.GetValue<string>(ExecutionGoalMetadata.ExperimentName, string.Empty);

        /// <inheritdoc />
        public bool Equals(ExecutionGoalParameter other)
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

            ExecutionGoalParameter itemDescription = obj as ExecutionGoalParameter;
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
                    .AppendComponents(this.TargetGoals)
                    .ToString().ToUpperInvariant().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
