namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents an experiment definition
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class Experiment : IEquatable<Experiment>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="Experiment"/> class.
        /// </summary>
        /// <param name="experiment">An experiment to match.</param>
        public Experiment(Experiment experiment)
            : this(
                  experiment?.Name,
                  experiment?.Description,
                  experiment?.ContentVersion,
                  experiment?.Metadata,
                  experiment?.Parameters,
                  experiment?.Workflow,
                  experiment?.Schema)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Experiment"/> class.
        /// </summary>
        /// <param name="name">The name of the experiment.</param>
        /// <param name="description">A description of the experiment.</param>
        /// <param name="contentVersion">The content version of the experiment.</param>
        /// <param name="workflow">The set of experiment components that collectively define the flow of the experiment.</param>
        public Experiment(
            string name,
            string description,
            string contentVersion,
            IEnumerable<ExperimentComponent> workflow)
            : this(name, description, contentVersion, null, null, workflow, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Experiment"/> class.
        /// </summary>
        /// <param name="name">The name of the experiment.</param>
        /// <param name="description">A description of the experiment.</param>
        /// <param name="contentVersion">The content version of the experiment.</param>
        /// <param name="workflow">The set of experiment components that collectively define the flow of the experiment.</param>
        /// <param name="metadata">Metadata associated with the experiment.</param>
        /// <param name="parameters">Shared parameters associated with the experiment.</param>
        /// <param name="schema">The experiment schema.</param>
        [JsonConstructor]
        public Experiment(
            string name,
            string description,
            string contentVersion,
            IDictionary<string, IConvertible> metadata,
            IDictionary<string, IConvertible> parameters,
            IEnumerable<ExperimentComponent> workflow,
            string schema = null)
        {
            this.Name = !string.IsNullOrWhiteSpace(name)
                ? name
                : throw new ArgumentException("The experiment name parameter is required.", nameof(name));

            this.ContentVersion = !string.IsNullOrWhiteSpace(contentVersion)
                ? contentVersion
                : throw new ArgumentException("The experiment content version parameter is required.", nameof(contentVersion));

            if (workflow?.Any() != true)
            {
                throw new ArgumentException(
                    "The experiment workflow parameter is required and must define a set of components describing the experiment flow.",
                    nameof(workflow));
            }

            this.Description = description;
            this.Schema = schema;
            this.Workflow = new List<ExperimentComponent>(workflow);
            this.Metadata = new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase);
            this.Parameters = new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase);

            if (metadata?.Any() == true)
            {
                this.Metadata.AddRange(metadata);
            }

            if (parameters?.Any() == true)
            {
                this.Parameters.AddRange(parameters);
            }
        }

        /// <summary>
        /// Gets the schema of the experiment.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "$schema", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public string Schema { get; }

        /// <summary>
        /// Gets the content version of the experiment.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "contentVersion", Required = Required.Always)]
        public string ContentVersion { get; }

        /// <summary>
        /// Gets the name of the experiment.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; }

        /// <summary>
        /// Gets a description of the experiment.
        /// </summary>
        [JsonProperty(PropertyName = "description", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; }

        /// <summary>
        /// Gets the set of metadata associated with the experiment.
        /// </summary>
        [JsonProperty(PropertyName = "metadata", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        public IDictionary<string, IConvertible> Metadata { get; }

        /// <summary>
        /// Gets the set of shared parameters associated with the experiment.
        /// </summary>
        [JsonProperty(PropertyName = "parameters", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        public IDictionary<string, IConvertible> Parameters { get; }

        /// <summary>
        /// Gets the experiment workflow definition.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "workflow", Required = Required.Always)]
        public IEnumerable<ExperimentComponent> Workflow { get; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(Experiment lhs, Experiment rhs)
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
        public static bool operator !=(Experiment lhs, Experiment rhs)
        {
            return !(lhs == rhs);
        }

        // Notes:
        // We do not have a consensus on where the recommendation ID should be created or the format of it. We are
        // leaving this here for reference as we come back to this so that we do not lose track of the work we did
        // to integrate it and can simply refactor that to match the requisite semantics in the future.
        /////// <summary>
        /////// Generates a "calculated" (and repeatable) Guid from the set of identifiers provided.
        /////// </summary>
        /////// <param name="identifiers">The set of 1 or more identifiers collectively used to generate the hash.</param>
        /////// <returns>
        /////// A repeatable "calculated" hash created from the identifiers.
        /////// </returns>
        ////[SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms", Justification = "This algorithm is NOT being used for cryptography. This is a Guid/hash generator.")]
        ////public static Guid GenerateHash(params string[] identifiers)
        ////{
        ////    identifiers.ThrowIfNullOrEmpty(nameof(identifiers));

        ////    using (MD5 md5 = MD5.Create())
        ////    {
        ////        byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(string.Join(',', identifiers)));
        ////        return new Guid(hashBytes);
        ////    }
        ////}

        /////// <summary>
        /////// Adds a "recommendationId" to the experiment metadata given the experiment
        /////// name, revision and tenantId metadata.
        /////// </summary>
        ////public void AddRecommendationId(bool replace = false)
        ////{
        ////    if (replace || !this.Metadata.ContainsKey(MetadataProperty.RecommendationId))
        ////    {
        ////        if (!this.Metadata.TryGetValue(MetadataProperty.Revision, out IConvertible revision))
        ////        {
        ////            throw new SchemaException(
        ////                $"Required metadata properties missing. In order to create a recommendation ID, the experiment must " +
        ////                $"have a '{MetadataProperty.Revision}' property defined in the metadata.");
        ////        }

        ////        if (!this.Metadata.TryGetValue(MetadataProperty.TenantId, out IConvertible tenantId))
        ////        {
        ////            throw new SchemaException(
        ////                $"Required metadata properties missing. In order to create a recommendation ID, the experiment must " +
        ////                $"have a '{MetadataProperty.TenantId}' property defined in the metadata.");
        ////        }

        ////        this.Metadata[MetadataProperty.RecommendationId] = Experiment.GenerateHash(this.Name, revision.ToString(), tenantId.ToString()).ToString();
        ////    }
        ////}

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
                Experiment itemDescription = obj as Experiment;
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
        public virtual bool Equals(Experiment other)
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
                    .AppendProperties(this.Name, this.Description, this.ContentVersion, this.Schema)
                    .AppendParameters(this.Metadata)
                    .AppendParameters(this.Parameters)
                    .AppendComponents(this.Workflow)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
