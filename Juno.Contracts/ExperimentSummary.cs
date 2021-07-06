namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Describes summary of an experiment.
    /// </summary>
    public class ExperimentSummary : IEquatable<ExperimentSummary>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentSummary"/> class.
        /// </summary>
        /// <param name="experimentSummary">An instance of Experiment Summary.</param>
        public ExperimentSummary(ExperimentSummary experimentSummary)
            : this(
                 experimentSummary?.ExperimentName,
                 experimentSummary?.Revision,
                 (experimentSummary is null) ? 0 : experimentSummary.Progress,
                 (experimentSummary is null) ? default : DateTime.Parse(experimentSummary.ExperimentDateUtc),
                 experimentSummary.BusinessSignalKPIs)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentSummary"/> class.
        /// </summary>
        /// <param name="experimentName">Name of the experiment.</param>
        /// <param name="revision">Revision of the experiment.</param>
        /// <param name="progress">Integer within 0 - 100 depicting the progress of the experiment.</param>
        /// <param name="experimentDateUtc">Start date of the experiment.</param>
        /// <param name="businessSignalKPIs">Collection of Semaphores/BusinessSignalKPI/Light-weight Business Signals of the experiment.</param>
        [JsonConstructor]
        public ExperimentSummary(
            string experimentName,
            string revision,
            int progress,
            DateTime experimentDateUtc,
            IEnumerable<BusinessSignalKPI> businessSignalKPIs)
        {
            experimentName.ThrowIfNullOrWhiteSpace(nameof(experimentName));
            this.ExperimentName = experimentName;

            revision.ThrowIfNullOrWhiteSpace(nameof(revision));
            this.Revision = revision;

            this.Progress = (progress >= 0 && progress <= 100)
                ? progress
                : throw new ArgumentException("The Progress parameter should be >= 0 and <=100.", nameof(progress));

            this.ExperimentDateUtc = ((experimentDateUtc > default(DateTime)) && (experimentDateUtc <= DateTime.UtcNow))
                ? experimentDateUtc.ToShortDateString()
                : throw new ArgumentException("The experimentDateUtc parameter has to be in between default and UtcNow.", nameof(experimentDateUtc));

            this.BusinessSignalKPIs = businessSignalKPIs;
        }

        /// <summary>
        /// Name of the experiment.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "experimentName", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public string ExperimentName { get; }

        /// <summary>
        /// Revision of the experiment.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "revision", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public string Revision { get; }

        /// <summary>
        /// Integer within 0 - 100 depicting the progress of the experiment.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "progress", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public int Progress { get; }

        /// <summary>
        /// Start date of the experiment.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "experimentDateUtc", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public string ExperimentDateUtc { get; }

        /// <summary>
        /// Collection of Semaphores/BusinessSignalKPIs/Light-weight Business Signals of the experiment.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "semaphores", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public IEnumerable<BusinessSignalKPI> BusinessSignalKPIs { get; }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(ExperimentSummary lhs, ExperimentSummary rhs)
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
        public static bool operator !=(ExperimentSummary lhs, ExperimentSummary rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="obj">Defines the object to compare against the current instance.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
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
                if (obj != null)
                {
                    areEqual = this.Equals(obj as ExperimentSummary);
                }
            }

            return areEqual;
        }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="other">Defines the object to compare against the current instance.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public virtual bool Equals(ExperimentSummary other)
        {
            var result = true;

            if (other != null && this.GetHashCode() == other.GetHashCode())
            {
                if (this.BusinessSignalKPIs.Count() == other.BusinessSignalKPIs.Count())
                {
                    for (int i = 0; i < this.BusinessSignalKPIs.Count(); i++)
                    {
                        if (!this.BusinessSignalKPIs.ElementAt(i).Equals(other.BusinessSignalKPIs.ElementAt(i)))
                        {
                            result = false;
                        }
                    }
                }
                else
                {
                    result = false;
                }
            }
            else
            {
                result = false;
            }

            return result;
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
                    .AppendProperties(
                    this.ExperimentName,
                    this.Revision,
                    this.Progress.ToString(),
                    this.ExperimentDateUtc)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}