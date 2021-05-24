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
    /// Execution Goal Parameter that can be modified by used to create new execution goal based on existing execution goal template.
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class ExecutionGoalParameter : IEquatable<ExecutionGoalParameter>
    {
        private int? hashCode;

        /// <summary>
        /// Json constructor for <see cref="ExecutionGoalParameter"/>
        /// </summary>
        /// <param name="experimentName">The name of the experiment</param>
        /// <param name="executionGoalId">Unique ID for the execution goal</param>
        /// <param name="owner">Owner of the execution goal</param>
        /// <param name="enabled">True if this execution goal is to be exectuted</param>
        /// <param name="sharedParameters">shared parameters between target goal parameters</param>
        /// <param name="targetGoals">Parameters required inside TargetGoals <see cref="TargetGoalParameter"/></param>
        [JsonConstructor]
        public ExecutionGoalParameter(string executionGoalId, string experimentName, string owner, bool? enabled, IEnumerable<TargetGoalParameter> targetGoals, IDictionary<string, IConvertible> sharedParameters = null)
        { 
            executionGoalId.ThrowIfNullOrWhiteSpace(nameof(executionGoalId));
            experimentName.ThrowIfNullOrWhiteSpace(nameof(experimentName));
            targetGoals.ThrowIfNullOrEmpty(nameof(targetGoals));
            owner.ThrowIfNullOrWhiteSpace(nameof(owner));
            enabled.ThrowIfNull(nameof(enabled));

            this.ExecutionGoalId = executionGoalId;
            this.ExperimentName = experimentName;
            this.Owner = owner;
            this.TargetGoals = new List<TargetGoalParameter>(targetGoals);
            this.Enabled = (bool)enabled;
            
            this.SharedParameters = sharedParameters == null
                ? new Dictionary<string, IConvertible>()
                : new Dictionary<string, IConvertible>(sharedParameters);
        }

        /// <summary>
        /// Description of the schedule
        /// </summary>
        [JsonProperty(PropertyName = "executionGoalId", Required = Required.Always, Order = 1)]
        public string ExecutionGoalId { get; }

        /// <summary>
        /// Name of Experiment Template
        /// This is a temporary field and will be removed soon.
        /// </summary>
        [JsonProperty(PropertyName = "experimentName", Required = Required.Always, Order = 2)]
        public string ExperimentName { get; }

        /// <summary>
        /// Execution Goal Owner
        /// </summary>
        [JsonProperty(PropertyName = "owner", Required = Required.Always, Order = 3)]
        public string Owner { get; }

        /// <summary>
        /// Indicates whether an execution goal is enabled
        /// </summary>
        [JsonProperty(PropertyName = "enabled", Required = Required.Always, Order = 4)]
        public bool Enabled { get; }

        /// <summary>
        /// List of parameters shared by ALL targetGoals
        /// </summary>
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        [JsonProperty(PropertyName = "sharedParameters", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 5)]
        public IDictionary<string, IConvertible> SharedParameters { get; }

        /// <summary>
        /// List of any paramaters in the execution goal template (only applicable to templates)
        /// </summary>
        [JsonProperty(PropertyName = "targetGoals", Required = Required.Always, Order = 6)]
        public IEnumerable<TargetGoalParameter> TargetGoals { get; }

        /// <inheritdoc />/>
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
                    .Append(this.Owner)
                    .Append(this.ExperimentName)
                    .Append(this.ExecutionGoalId)
                    .ToString().ToUpperInvariant().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
