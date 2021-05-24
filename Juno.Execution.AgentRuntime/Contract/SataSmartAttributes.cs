namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// List of Sata Smart Attributes.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class SataSmartAttributes : IEquatable<SataSmartAttributes>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SataSmartAttributes"/> class.
        /// </summary>
        /// <param name="table"></param>
        [JsonConstructor]
        public SataSmartAttributes(IEnumerable<SataSmartAttribute> table)
        {
            table.ThrowIfNull(nameof(table));
            this.SmartAttributes = table == null 
                ? new List<SataSmartAttribute>()    
                : new List<SataSmartAttribute>(table);
        }

        /// <summary>
        /// List of Sata Smart attributes.
        /// </summary>
        [JsonProperty(PropertyName = "table", Required = Required.Always)]
        public IEnumerable<SataSmartAttribute> SmartAttributes { get; }

        /// <summary>
        /// Evaluates the equality beteen two <see cref="SataSmartAttributes"/> instances
        /// </summary>
        /// <param name="other">The other <see cref="SataSmartAttributes"/> to evaluate equality against.</param>
        /// <returns>True/False if the two <see cref="SataSmartAttributes"/> are equal.</returns>
        public bool Equals(SataSmartAttributes other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Evaluates the equality between this and another object.
        /// </summary>
        /// <param name="obj">The object to compare equality against.</param>
        /// <returns>True/False if this and the object are equal.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            SataSmartAttributes other = obj as SataSmartAttributes;
            if (other == null)
            {
                return false;
            }

            return this.Equals(other);
        }

        /// <summary>
        /// Generates a unique hashcode for this.
        /// </summary>
        /// <returns>The hashcode for this.</returns>
        public override int GetHashCode()
        {
            return this.SmartAttributes.Select(a => a.GetHashCode()).Aggregate((result, current) => result += current);
        }
    }
}
