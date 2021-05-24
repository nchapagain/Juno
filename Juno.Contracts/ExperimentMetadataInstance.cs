namespace Juno.Contracts
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a notice of work to do in relation to a Juno experiment.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class ExperimentMetadataInstance : Item<ExperimentMetadata>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentMetadata"/> class.
        /// </summary>
        /// <param name="id">The unique ID of the notice.</param>
        /// <param name="definition">Provides context information that define the specifics of the notice.</param>
        public ExperimentMetadataInstance(string id, ExperimentMetadata definition)
            : base(id, definition)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id">The ID of the notice.</param>
        /// <param name="definition">Provides context information that define the specifics of the notice.</param>
        /// <param name="created">The date at which the notice was created.</param>
        /// <param name="lastModified">The date at which the notice was last modified.</param>
        [JsonConstructor]
        public ExperimentMetadataInstance(string id, ExperimentMetadata definition, DateTime created, DateTime lastModified)
            : base(id, created, lastModified, definition)
        {
        }

        /// <summary>
        /// Override enables the creation of an accurate hash code for the
        /// object.
        /// </summary>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder(base.GetHashCode().ToString())
                    .Append(this.Definition.GetHashCode())
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
