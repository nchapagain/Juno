namespace Juno.Contracts
{
    using System;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Newtonsoft.Json;

    /// <summary>
    /// An instance of an <see cref="ExperimentItem"/> used for storing the experiment definition for scheduling purpose
    /// in a data store.
    /// </summary>
    public class ExperimentTemplateItem : Item<ExperimentTemplate>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="Item{TData}"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the item.</param>
        /// <param name="definition">The data associated with the item.</param>
        public ExperimentTemplateItem(string id, ExperimentTemplate definition)
            : this(id, DateTime.UtcNow, DateTime.UtcNow, definition)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Item{TData}"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the item.</param>
        /// <param name="created">The date at which the item was created.</param>
        /// <param name="lastModified">The data at which the item was last modified.</param>
        /// <param name="definition">The data associated with the item.</param>
        [JsonConstructor]
        public ExperimentTemplateItem(string id, DateTime created, DateTime lastModified, ExperimentTemplate definition)
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
                this.hashCode = new StringBuilder($"{base.GetHashCode().ToString()},{this.Definition.GetHashCode()}")
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
