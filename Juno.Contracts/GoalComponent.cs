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
    /// 
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class GoalComponent
    {
        private int? hashCode;

        /// <summary>
        /// Json Constructor for Schedule Action
        /// </summary>
        /// <param name="type">The fully qualified Juno Precondition type</param>
        /// <param name="parameters">List of parameters that the Juno Precondition needs to execute successfully</param>
        [JsonConstructor]
        public GoalComponent(string type, Dictionary<string, IConvertible> parameters = null)
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
        /// Type of Precondition
        /// </summary>
        [JsonProperty(PropertyName = "type", Required = Required.Always, Order = 1)]
        public string Type { get; set; }

        /// <summary>
        /// Parameters for precondition
        /// </summary>
        [JsonConverter(typeof(JunoParameterDictionaryJsonConverter))]
        [JsonProperty(PropertyName = "parameters", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 2)]
        public Dictionary<string, IConvertible> Parameters { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static bool operator ==(GoalComponent lhs, GoalComponent rhs)
        {
            if (object.ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (object.ReferenceEquals(null, lhs) || object.ReferenceEquals(null, rhs))
            {
                return false;
            }

            return lhs.Equals(rhs);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static bool operator !=(GoalComponent lhs, GoalComponent rhs)
        {
            return !(lhs == rhs); 
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            GoalComponent itemDescription = obj as GoalComponent;
            if (itemDescription == null)
            {
                return false;
            }

            return this.Equals(itemDescription);
        }

        /// <summary>
        /// Method determines if the other Object is equal to this instance
        /// </summary>
        /// <param name="other">
        /// Defines the other Precondtion to compare against
        /// </param>
        /// <returns>True if the objects are equal, false otherwise</returns>
        public virtual bool Equals(GoalComponent other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Override method returns a unique integer hash code
        /// </summary>
        /// <returns>
        /// Tpye: System.Int32
        /// A unique Identifier for this Precondition instance.
        /// </returns>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder()
                    .AppendProperties(this.Type)
                    .AppendParameters(this.Parameters)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
