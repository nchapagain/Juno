namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Describes the goal for the schedule
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class Goal : IEquatable<Goal>
    {
        private int? hashCode;

        /// <summary>
        /// Json Constructor for a Goal instance
        /// </summary>
        /// <param name="name">Name of the Goal</param>
        /// <param name="preconditions">Parameters needed for Goal to execute properly</param>
        /// <param name="actions">List of Schedule Actions </param>
        /// <param name="id">Distinct Goal ID</param>
        [JsonConstructor]
        public Goal(string name, List<Precondition> preconditions, List<ScheduleAction> actions, string id = null)
        {
            name.ThrowIfNullOrWhiteSpace(nameof(name));
            preconditions.ThrowIfNullOrEmpty(nameof(preconditions));
            actions.ThrowIfNullOrEmpty(nameof(actions));

            this.Preconditions = new List<Precondition>(preconditions);
            this.Actions = new List<ScheduleAction>(actions);
            this.Name = name;

            this.Id = id ?? Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Copy Constructor for Goal Object
        /// </summary>
        /// <param name="other">
        /// Goal Object to be copied into current instance
        /// </param>
        public Goal(Goal other) 
            : this(other?.Name, other?.Preconditions, other?.Actions)
        {
        }

        /// <summary>
        /// Friendly Name of the Goal
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always, Order = 10)]
        public string Name { get; }

        /// <summary>
        /// Distinct Goal Id
        /// </summary>
        [JsonProperty(PropertyName = "id", Required = Required.Default, Order = 20)]
        public string Id { get; }

        /// <summary>
        /// Describes the list of preconditions to assign for this goal
        /// </summary>
        [JsonProperty(PropertyName = "preconditions", Required = Required.Always, Order = 30)]
        public List<Precondition> Preconditions { get; }

        /// <summary>
        /// Describes the list of Actions to assgin for this goal
        /// </summary>
        [JsonProperty(PropertyName = "actions", Required = Required.Always, Order = 40)]
        public List<ScheduleAction> Actions { get; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            Goal itemDescription = obj as Goal;
            if (itemDescription == null)
            {
                return false;
            }

            return this.Equals(itemDescription);
        }

        /// <summary>
        /// Determines if the other Goal is equal to this instance
        /// </summary>
        /// <param name="other">
        /// Defines the other object to compare against
        /// </param>
        /// <returns></returns>
        public virtual bool Equals(Goal other) 
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Calculates the unique HashCode for instance
        /// </summary>
        /// <returns>
        /// Type: System.Int32
        /// A unique identifier for the class instance.
        /// </returns>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                StringBuilder builder = new StringBuilder();
                List<GoalComponent> components = new List<GoalComponent>();
                components.AddRange(this.Preconditions);
                components.AddRange(this.Actions);

                foreach (GoalComponent component in components)
                {
                    builder.Append(component.GetHashCode().ToString());
                }

                this.hashCode = builder
                    .Append(this.Name)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            return this.hashCode.Value;
        }
    }
}
