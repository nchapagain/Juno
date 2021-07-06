namespace Juno.Contracts
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Describes the progress of an experiment.
    /// </summary>
    public class ExperimentProgress : IEquatable<ExperimentProgress>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentProgress"/> class.
        /// </summary>
        /// <param name="experimentProgress">An instance of Experiment Progress.</param>
        public ExperimentProgress(ExperimentProgress experimentProgress)
            : this(
                 experimentProgress?.ExperimentName,
                 experimentProgress?.Revision,
                 (experimentProgress is null) ? 0 : experimentProgress.Progress)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentProgress"/> class.
        /// </summary>
        /// <param name="experimentName">Name of the experiment.</param>
        /// <param name="revision">Revision of the experiment.</param>
        /// <param name="progress">Integer within 0 - 100 depicting the progress of the experiment.</param>
        [JsonConstructor]
        public ExperimentProgress(
            string experimentName,
            string revision,
            int progress)
        {
            experimentName.ThrowIfNullOrWhiteSpace(nameof(experimentName));
            this.ExperimentName = experimentName;

            revision.ThrowIfNullOrWhiteSpace(nameof(revision));
            this.Revision = revision;

            this.Progress = (progress >= 0 && progress <= 100)
                ? progress
                : throw new ArgumentException("The Progress parameter should be >= 0 and <=100.", nameof(progress));
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
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="lhs">The left hand side.</param>
        /// <param name="rhs">The right hand side.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public static bool operator ==(ExperimentProgress lhs, ExperimentProgress rhs)
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
        public static bool operator !=(ExperimentProgress lhs, ExperimentProgress rhs)
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
                    areEqual = this.Equals(obj as ExperimentProgress);
                }
            }

            return areEqual;
        }

        /// <summary>
        /// Determines if two objects are equal.
        /// </summary>
        /// <param name="other">Defines the object to compare against the current instance.</param>
        /// <returns>True if the objects are equal. False otherwise.</returns>
        public virtual bool Equals(ExperimentProgress other)
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
                    .AppendProperties(
                    this.ExperimentName,
                    this.Revision,
                    this.Progress.ToString())
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}