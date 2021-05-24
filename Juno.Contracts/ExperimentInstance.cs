namespace Juno.Contracts
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// An instance of an <see cref="Experiment"/> used for storing the experiment definition
    /// in a data store.
    /// </summary>
    public class ExperimentInstance : Item<Experiment>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="Item{TData}"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the item.</param>
        /// <param name="definition">The data associated with the item.</param>
        public ExperimentInstance(string id, Experiment definition)
            : this(id, DateTime.UtcNow, DateTime.UtcNow, definition, ExperimentStatus.Pending)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Item{TData}"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the item.</param>
        /// <param name="created">The date at which the item was created.</param>
        /// <param name="lastModified">The data at which the item was last modified.</param>
        /// <param name="definition">The data associated with the item.</param>
        public ExperimentInstance(string id, DateTime created, DateTime lastModified, Experiment definition)
            : this(id, created, lastModified, definition, ExperimentStatus.Pending)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Item{TData}"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the item.</param>
        /// <param name="created">The date at which the item was created.</param>
        /// <param name="lastModified">The data at which the item was last modified.</param>
        /// <param name="definition">The data associated with the item.</param>
        /// <param name="status">The status of the experiment.</param>
        [JsonConstructor]
        public ExperimentInstance(string id, DateTime created, DateTime lastModified, Experiment definition, ExperimentStatus status)
            : base(id, created, lastModified, definition)
        {
            this.Status = status;
        }

        /// <summary>
        /// Gets or sets the status of the experiment.
        /// </summary>
        [JsonProperty(PropertyName = "status", Required = Required.Always, Order = 4)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ExperimentStatus Status { get; set; }

        /// <summary>
        /// Override enables the creation of an accurate hash code for the
        /// object.
        /// </summary>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder($"{base.GetHashCode().ToString()},{this.Definition.GetHashCode()}")
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
