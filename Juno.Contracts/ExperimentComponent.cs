namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents an experiment component or fundamental part of an experiment
    /// definition.
    /// </summary>
    [DebuggerDisplay("{Name}")]
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class ExperimentComponent : IEquatable<ExperimentComponent>
    {
        /// <summary>
        /// Represents a component that targets all experiment environment groups.
        /// </summary>
        public const string AllGroups = "*";

        /// <summary>
        /// Represents the 'type' for a 'parallel execution' component which contains steps that should
        /// be executed in parallel.
        /// </summary>
        public const string ParallelExecutionType = "ParallelExecution";

        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentComponent"/> class.
        /// </summary>
        /// <param name="component">Defines the experiment component.</param>
        public ExperimentComponent(ExperimentComponent component)
            : this(component?.ComponentType, component?.Name, component?.Description, component?.Group, component?.Parameters, component?.Tags)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentComponent"/> class.
        /// </summary>
        /// <param name="type">
        /// The type of the component/provider which handles runtime execution details based on the definition 
        /// (e.g. Juno.Environments.ExperimentEnvironment).
        /// </param>
        /// <param name="name">The name of the entity.</param>
        /// <param name="description">The description of the entity.</param>
        /// <param name="group">The experiment environment group for which the component/step is targeted.</param>
        /// <param name="parameters">A set of parameters associated with the entity.</param>
        /// <param name="tags">A set of tags associated with the entity.</param>
        /// <param name="dependencies">A set of dependencies of the workflow step.</param>      
        [JsonConstructor]
        public ExperimentComponent(
            string type,
            string name,
            string description,
            string group = null,
            IDictionary<string, IConvertible> parameters = null,
            IDictionary<string, IConvertible> tags = null,
            IEnumerable<ExperimentComponent> dependencies = null)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException("The type parameter is required.", nameof(type));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The name parameter is required.", nameof(name));
            }

            this.ComponentType = type;
            this.Name = name;
            this.Description = description;
            this.Group = group;
            this.Extensions = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);

            if (parameters?.Any() == true)
            {
                this.Parameters = new Dictionary<string, IConvertible>(parameters, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                this.Parameters = new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase);
            }

            if (tags?.Any() == true)
            {
                this.Tags = new Dictionary<string, IConvertible>(tags, StringComparer.OrdinalIgnoreCase);
            }

            if (dependencies?.Any() == true)
            {
                this.Dependencies = new List<ExperimentComponent>(dependencies);
            }
        }

        /// <summary>
        /// Gets the type of the component/provider which handles runtime execution details
        /// based on the definition (e.g. Juno.Environments.ExperimentEnvironment).
        /// </summary>
        [JsonProperty(PropertyName = "type", Required = Required.Always, Order = 1)]
        public string ComponentType { get; }

        /// <summary>
        /// Gets the name of the entity.
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always, Order = 2)]
        public string Name { get; }

        /// <summary>
        /// Gets the description of the entity.
        /// </summary>
        [JsonProperty(PropertyName = "description", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 3)]
        public string Description { get; }

        /// <summary>
        /// Gets the environment group for which the component/step is targeted.
        /// </summary>
        [JsonProperty(PropertyName = "group", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 4)]
        public string Group { get; }

        /// <summary>
        /// Gets the set of parameters associated with the entity.
        /// </summary>
        [JsonProperty(PropertyName = "parameters", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 5)]
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        public IDictionary<string, IConvertible> Parameters { get; }

        /// <summary>
        /// Gets the set of tags associated with the entity.
        /// </summary>
        [JsonProperty(PropertyName = "tags", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 6)]
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        public IDictionary<string, IConvertible> Tags { get; }

        /// <summary>
        /// Gets the set of dependencies required by the component.
        /// </summary>
        [JsonProperty(PropertyName = "dependencies", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore, Order = 7)]
        public IEnumerable<ExperimentComponent> Dependencies { get; }

        /// <summary>
        /// Gets extension data/objects associated with the entity definition. This is used
        /// to provide an extension point to the fundamental entity properties for more
        /// complex entity definitions.
        /// </summary>
        [JsonExtensionData(ReadData = true, WriteData = true)]
        public IDictionary<string, JToken> Extensions { get; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(ExperimentComponent lhs, ExperimentComponent rhs)
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
        public static bool operator !=(ExperimentComponent lhs, ExperimentComponent rhs)
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
                ExperimentComponent itemDescription = obj as ExperimentComponent;
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
        public virtual bool Equals(ExperimentComponent other)
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
                this.hashCode = new StringBuilder()
                    .AppendProperties(this.ComponentType, this.Name, this.Description, this.Group)
                    .AppendParameters(this.Parameters)
                    .AppendParameters(this.Tags)
                    .AppendComponents(this.Dependencies)
                    .AppendExtensions(this.Extensions)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
