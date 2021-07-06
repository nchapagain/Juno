namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// A Target Goal that is comprised of preconditions and actions.
    /// Can be in a state of enabled/disabled.
    /// </summary>
    public class TargetGoal : Goal, IEquatable<TargetGoal>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="TargetGoal"/> class.
        /// </summary>
        /// <param name="name">The name of the target goal.</param>
        /// <param name="enabled">True/False if target goal is enabled.</param>
        /// <param name="preconditions">List of preconditions.</param>
        /// <param name="actions">List of schedule actions.</param>
        /// <param name="id">The unique identifier of the target goal.</param>
        [JsonConstructor]
        public TargetGoal(string name, bool enabled, List<Precondition> preconditions, List<ScheduleAction> actions, string id = null)
            : base(name, preconditions, actions, id)
        {
            this.Enabled = enabled;
        }

        /// <summary>
        /// Copy constructor for <see cref="TargetGoal"/>
        /// </summary>
        /// <param name="other">The other target goal instance to copy.</param>
        public TargetGoal(TargetGoal other)
            : this(other?.Name, other.Enabled, other?.Preconditions, other?.Actions, other?.Id)
        {
        }

        /// <summary>
        /// Determines if this target goal is disabled or enabled.
        /// </summary>
        [JsonProperty(PropertyName = "enabled", Required = Required.Always, Order = 25)]
        public bool Enabled { get; }

        /// <summary>
        /// Determines equality between this instance and the given 
        /// instance.
        /// </summary>
        /// <param name="other">The other target goal instance.</param>
        /// <returns>True/false if the two instances demonstrate equality.</returns>
        public bool Equals(TargetGoal other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Determines equality between this instance and an object.
        /// </summary>
        /// <param name="obj">The object instance to evaluate equality against.</param>
        /// <returns>True/False if the two instances demonstrate equality.</returns>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            if (!(obj is TargetGoal itemDescription))
            {
                return false;
            }

            return this.Equals(itemDescription);
        }

        /// <summary>
        /// Calculates the unique hash code for this instance.
        /// </summary>
        /// <returns>A unique hash code for this instance.</returns>
        public override int GetHashCode()
        {
            this.hashCode ??= base.GetHashCode() + this.Enabled.GetHashCode();
            return this.hashCode.Value;
        }
    }
}
