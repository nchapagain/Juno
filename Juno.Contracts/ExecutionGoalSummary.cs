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
    /// Info pertaining to Execution Goal templates stored in the system.
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class ExecutionGoalSummary : IEquatable<ExecutionGoalSummary>
    {
        private int? hashCode;

        /// <summary>
        /// Json constructor for <see cref="ExecutionGoalSummary"/>
        /// </summary>
        /// <param name="id">The Id of the execution goal</param>
        /// <param name="description">Description of the execution goal</param>
        /// <param name="teamName">The team that owns the execution goal</param>
        /// <param name="parameterNames">Name of parameters required by template</param>
        /// <param name="metadata">Metadata of execution goal</param>
        [JsonConstructor]
        public ExecutionGoalSummary(string id, string description, string teamName, ExecutionGoalParameter parameterNames, IDictionary<string, IConvertible> metadata = null)
        {
            id.ThrowIfNullOrWhiteSpace(nameof(id));
            description.ThrowIfNullOrWhiteSpace(nameof(description));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));
            parameterNames.ThrowIfNull(nameof(ExecutionGoalParameter));

            this.Id = id;
            this.Description = description;
            this.TeamName = teamName;

            this.ParameterNames = parameterNames;

            this.Metadata = metadata == null
                ? new Dictionary<string, IConvertible>()
                : new Dictionary<string, IConvertible>(metadata);
        }

        /// <summary>
        /// Id of an Execution Goal Template
        /// </summary>
        [JsonProperty(PropertyName = "id", Required = Required.Always, Order = 1)]
        public string Id { get; }

        /// <summary>
        /// Description of the Execution Goal Template
        /// </summary>
        [JsonProperty(PropertyName = "teamName", Required = Required.Always, Order = 2)]
        public string TeamName { get; }

        /// <summary>
        /// Description of the Execution Goal Template
        /// </summary>
        [JsonProperty(PropertyName = "description", Required = Required.Always, Order = 3)]
        public string Description { get; }

        /// <summary>
        /// List of metadata parameters in the execution goal template i.e: Owner, ExperimentIntent, ExperimentCategory
        /// </summary>
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        [JsonProperty(PropertyName = "metadata", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 4)]
        public IDictionary<string, IConvertible> Metadata { get; }

        /// <summary>
        /// List of any parameters in the execution goal template (only applicable to templates)
        /// </summary>
        [JsonProperty(PropertyName = "parameterNames", Required = Required.Always, NullValueHandling = NullValueHandling.Ignore, Order = 5)]
        public ExecutionGoalParameter ParameterNames { get; }

        /// <inheritdoc />/>
        public bool Equals(ExecutionGoalSummary other)
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

            ExecutionGoalSummary itemDescription = obj as ExecutionGoalSummary;
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
                    .Append(this.Id)
                    .Append(this.Description)
                    .Append(this.TeamName)
                    .Append(this.ParameterNames.GetHashCode())
                    .ToString().ToUpperInvariant().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}