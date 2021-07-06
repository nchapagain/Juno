namespace Juno.Execution.Providers.Workloads.VCContracts
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a client disk and its specifications.
    /// </summary>
    [DebuggerDisplay("{Name}/{Type}")]
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class ClientDisk : IEquatable<ClientDisk>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientDisk"/> class.
        /// </summary>
        [JsonConstructor]
        public ClientDisk(string name, string type, int? logicalUnit = null)
        {
            name.ThrowIfNullOrWhiteSpace(nameof(name));
            type.ThrowIfNullOrWhiteSpace(nameof(type));

            this.Name = name;
            this.Type = type;
            this.LogicalUnit = logicalUnit;
        }

        /// <summary>
        /// Disk name
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; }

        /// <summary>
        /// Disk type (Premium_LRS, Standard_LRS)
        /// </summary>
        [JsonProperty(PropertyName = "type", Required = Required.Always)]
        public string Type { get; }

        /// <summary>
        /// The SCSI logical unit (LUN) of the disk.
        /// </summary>
        [JsonProperty(PropertyName = "logicalUnit", Required = Required.Default)]
        public int? LogicalUnit { get; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(ClientDisk lhs, ClientDisk rhs)
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
        /// Determines if two objects are NOT equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are NOT equal. False otherwise.</returns>
        public static bool operator !=(ClientDisk lhs, ClientDisk rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Override method determines if the two objects are equal
        /// </summary>
        /// <param name="obj">Defines the object to compare against the current instance.</param>
        /// <returns>
        /// Type:  System.Boolean
        /// True if the objects are equal or False if not
        /// </returns>
        public override bool Equals(object obj)
        {
            bool areEqual = false;

            if (object.ReferenceEquals(this, obj))
            {
                areEqual = true;
            }
            else
            {
                // Apply value-type semantics to determine
                // the equality of the instances
                ClientDisk itemDescription = obj as ClientDisk;
                if (itemDescription != null)
                {
                    areEqual = this.Equals(itemDescription);
                }
            }

            return areEqual;
        }

        /// <summary>
        /// Method determines if the other object is equal to this instance
        /// </summary>
        /// <param name="other">Defines the object to compare against the current instance.</param>
        /// <returns>
        /// Type:  System.Boolean
        /// True if the objects are equal or False if not
        /// </returns>
        public virtual bool Equals(ClientDisk other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Override method returns a unique integer hash code
        /// identifier for the class instance
        /// </summary>
        /// <returns>
        /// Type:  System.Int32
        /// A unique integer identifier for the class instance
        /// </returns>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                StringBuilder hashBuilder = new StringBuilder($"{this.Name},{this.Type}");

                if (this.LogicalUnit != null)
                {
                    hashBuilder.Append(this.LogicalUnit.Value);
                }

                this.hashCode = hashBuilder.ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
