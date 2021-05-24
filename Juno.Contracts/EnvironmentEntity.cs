namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents a part of an environment in which an experiment is
    /// conducted (e.g. Cluster, Node, Virtual Machine).
    /// </summary>
    [DebuggerDisplay("{EntityType}: {Id}, {EnvironmentGroup}")]
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class EnvironmentEntity : IEquatable<EnvironmentEntity>, IIdentifiable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentEntity"/> class.
        /// </summary>
        /// <param name="entityType">The type of entity (e.g. Cluster, Node).</param>
        /// <param name="id">A unique identifier for the entity.</param>
        /// <param name="environmentGroup">The name of the experiment environment group for which the entity is related.</param>
        /// <param name="metadata">A set of metadata properties describing the entity.</param>
        public EnvironmentEntity(EntityType entityType, string id, string environmentGroup, IDictionary<string, IConvertible> metadata = null)
            : this(entityType, id, null, environmentGroup, metadata)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentEntity"/> class.
        /// </summary>
        /// <param name="entityType">The type of entity (e.g. Cluster, Node).</param>
        /// <param name="id">A unique identifier for the entity.</param>
        /// <param name="parentId">A unique identifier for the parent of the entity (e.g. the cluster in which the node exists).</param>
        /// <param name="environmentGroup">The name of the experiment environment group for which the entity is related.</param>
        /// <param name="metadata">A set of metadata properties describing the entity.</param>
        [JsonConstructor]
        public EnvironmentEntity(EntityType entityType, string id, string parentId, string environmentGroup, IDictionary<string, IConvertible> metadata = null)
        {
            id.ThrowIfNullOrWhiteSpace(nameof(id));
            environmentGroup.ThrowIfNullOrWhiteSpace(nameof(environmentGroup));

            this.EntityType = entityType;
            this.Id = id;
            this.ParentId = parentId;
            this.EnvironmentGroup = environmentGroup;

            this.Metadata = metadata != null
                ? new Dictionary<string, IConvertible>(metadata, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the ID of the environment entity.
        /// </summary>
        [JsonProperty(PropertyName = "id", Required = Required.Always)]
        public string Id { get; }

        /// <summary>
        /// Gets the ID of the environment parent entity (e.g. the cluster in which a node exists).
        /// </summary>
        [JsonProperty(PropertyName = "parentId", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public string ParentId { get; }

        /// <summary>
        /// Gets the type of environment entity (e.g. Cluster, Node).
        /// </summary>
        [JsonProperty(PropertyName = "entityType", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public EntityType EntityType { get; }

        /// <summary>
        /// Gets the environment group for which the entity is related (e.g. Group A, Group B).
        /// </summary>
        [JsonProperty(PropertyName = "environmentGroup", Required = Required.Always)]
        public string EnvironmentGroup { get; }

        /// <summary>
        /// Gets a set of metadata describing additional information about the
        /// environment entity.
        /// </summary>
        [JsonProperty(PropertyName = "metadata", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        public IDictionary<string, IConvertible> Metadata { get; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(EnvironmentEntity lhs, EnvironmentEntity rhs)
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
        public static bool operator !=(EnvironmentEntity lhs, EnvironmentEntity rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Creates an environment entity that represents a data center "cluster" (of nodes).
        /// </summary>
        /// <param name="id">A unique identifier for the entity.</param>
        /// <param name="environmentGroup">The name of the experiment environment group for which the entity is related.</param>
        /// <param name="metadata">A set of metadata properties describing the entity.</param>
        /// <returns>
        /// An <see cref="EnvironmentEntity"/> that represents a data center "cluster".
        /// </returns>
        public static EnvironmentEntity Cluster(string id, string environmentGroup, IDictionary<string, IConvertible> metadata = null)
        {
            return new EnvironmentEntity(EntityType.Cluster, id, environmentGroup, metadata);
        }

        /// <summary>
        /// Creates an environment entity that represents a data center "cluster" (of nodes).
        /// </summary>
        /// <param name="id">A unique identifier for the entity.</param>
        /// <param name="parentId">The unique identifier for the entity's parent (in a set of entities).</param>
        /// <param name="environmentGroup">The name of the experiment environment group for which the entity is related.</param>
        /// <param name="metadata">A set of metadata properties describing the entity.</param>
        /// <returns>
        /// An <see cref="EnvironmentEntity"/> that represents a data center "cluster".
        /// </returns>
        public static EnvironmentEntity Cluster(string id, string parentId, string environmentGroup, IDictionary<string, IConvertible> metadata = null)
        {
            return new EnvironmentEntity(EntityType.Cluster, id, parentId, environmentGroup, metadata);
        }

        /// <summary>
        /// Creates an environment entity that represents a data center physical node/blade.
        /// </summary>
        /// <param name="id">A unique identifier for the entity.</param>
        /// <param name="environmentGroup">The name of the experiment environment group for which the entity is related.</param>
        /// <param name="metadata">A set of metadata properties describing the entity.</param>
        /// <returns>
        /// An <see cref="EnvironmentEntity"/> that represents a data center physical node/blade.
        /// </returns>
        public static EnvironmentEntity Node(string id, string environmentGroup, IDictionary<string, IConvertible> metadata = null)
        {
            return new EnvironmentEntity(EntityType.Node, id, environmentGroup, metadata);
        }

        /// <summary>
        /// Creates an environment entity that represents a data center physical node/blade.
        /// </summary>
        /// <param name="id">A unique identifier for the entity.</param>
        /// <param name="parentId">The unique identifier for the entity's parent (in a set of entities).</param>
        /// <param name="environmentGroup">The name of the experiment environment group for which the entity is related.</param>
        /// <param name="metadata">A set of metadata properties describing the entity.</param>
        /// <returns>
        /// An <see cref="EnvironmentEntity"/> that represents a data center physical node/blade.
        /// </returns>
        public static EnvironmentEntity Node(string id, string parentId, string environmentGroup, IDictionary<string, IConvertible> metadata = null)
        {
            return new EnvironmentEntity(EntityType.Node, id, parentId, environmentGroup, metadata);
        }

        /// <summary>
        /// Creates an environment entity that represents a data center rack containing physical nodes/blades.
        /// </summary>
        /// <param name="id">A unique identifier for the entity.</param>
        /// <param name="environmentGroup">The name of the experiment environment group for which the entity is related.</param>
        /// <param name="metadata">A set of metadata properties describing the entity.</param>
        /// <returns>
        /// An <see cref="EnvironmentEntity"/> that represents a data center rack containing physical nodes/blades.
        /// </returns>
        public static EnvironmentEntity Rack(string id, string environmentGroup, IDictionary<string, IConvertible> metadata = null)
        {
            return new EnvironmentEntity(EntityType.Rack, id, environmentGroup, metadata);
        }

        /// <summary>
        /// Creates an environment entity that represents a data center rack containing physical nodes/blades.
        /// </summary>
        /// <param name="id">A unique identifier for the entity.</param>
        /// <param name="parentId">The unique identifier for the entity's parent (in a set of entities).</param>
        /// <param name="environmentGroup">The name of the experiment environment group for which the entity is related.</param>
        /// <param name="metadata">A set of metadata properties describing the entity.</param>
        /// <returns>
        /// An <see cref="EnvironmentEntity"/> that represents a data center rack containing physical nodes/blades.
        /// </returns>
        public static EnvironmentEntity Rack(string id, string parentId, string environmentGroup, IDictionary<string, IConvertible> metadata = null)
        {
            return new EnvironmentEntity(EntityType.Rack, id, parentId, environmentGroup, metadata);
        }

        /// <summary>
        /// Creates an environment entity that represents a TiP (test-in-production) session to one
        /// or more nodes.
        /// </summary>
        /// <param name="id">A unique identifier for the entity.</param>
        /// <param name="environmentGroup">The name of the experiment environment group for which the entity is related.</param>
        /// <param name="metadata">A set of metadata properties describing the entity.</param>
        /// <returns>
        /// An <see cref="EnvironmentEntity"/> that represents a TiP session.
        /// </returns>
        public static EnvironmentEntity TipSession(string id, string environmentGroup, IDictionary<string, IConvertible> metadata = null)
        {
            return new EnvironmentEntity(EntityType.TipSession, id, environmentGroup, metadata);
        }

        /// <summary>
        /// Creates an environment entity that represents a TiP (test-in-production) session to one
        /// or more nodes.
        /// </summary>
        /// <param name="id">A unique identifier for the entity.</param>
        /// <param name="parentId">The unique identifier for the entity's parent (in a set of entities).</param>
        /// <param name="environmentGroup">The name of the experiment environment group for which the entity is related.</param>
        /// <param name="metadata">A set of metadata properties describing the entity.</param>
        /// <returns>
        /// An <see cref="EnvironmentEntity"/> that represents a TiP session.
        /// </returns>
        public static EnvironmentEntity TipSession(string id, string parentId, string environmentGroup, IDictionary<string, IConvertible> metadata = null)
        {
            return new EnvironmentEntity(EntityType.TipSession, id, parentId, environmentGroup, metadata);
        }

        /// <summary>
        /// Creates an environment entity that represents a virtual machine.
        /// </summary>
        /// <param name="id">A unique identifier for the entity.</param>
        /// <param name="environmentGroup">The name of the experiment environment group for which the entity is related.</param>
        /// <param name="metadata">A set of metadata properties describing the entity.</param>
        /// <returns>
        /// An <see cref="EnvironmentEntity"/> that represents a virtual machine.
        /// </returns>
        public static EnvironmentEntity VirtualMachine(string id, string environmentGroup, IDictionary<string, IConvertible> metadata = null)
        {
            return new EnvironmentEntity(EntityType.VirtualMachine, id, environmentGroup, metadata);
        }

        /// <summary>
        /// Creates an environment entity that represents a virtual machine.
        /// </summary>
        /// <param name="id">A unique identifier for the entity.</param>
        /// <param name="parentId">The unique identifier for the entity's parent (in a set of entities).</param>
        /// <param name="environmentGroup">The name of the experiment environment group for which the entity is related.</param>
        /// <param name="metadata">A set of metadata properties describing the entity.</param>
        /// <returns>
        /// An <see cref="EnvironmentEntity"/> that represents a virtual machine.
        /// </returns>
        public static EnvironmentEntity VirtualMachine(string id, string parentId, string environmentGroup, IDictionary<string, IConvertible> metadata = null)
        {
            return new EnvironmentEntity(EntityType.VirtualMachine, id, parentId, environmentGroup, metadata);
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
                EnvironmentEntity itemDescription = obj as EnvironmentEntity;
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
        public virtual bool Equals(EnvironmentEntity other)
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
            StringBuilder hashBuilder = new StringBuilder($"{this.EntityType},{this.Id},{this.ParentId},{this.EnvironmentGroup}");
            if (this.Metadata?.Any() == true)
            {
                hashBuilder.Append($"{string.Join(",", this.Metadata.Select(entry => $"{entry.Key}={entry.Value}"))}");
            }

            return hashBuilder.ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
        }
    }
}
