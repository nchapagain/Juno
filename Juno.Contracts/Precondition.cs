namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Describe Preconditions Scheduler can take
    /// </summary>
    public class Precondition : GoalComponent
    {
        /// <summary>
        /// Initializes an instance of <see cref="Precondition"/>
        /// </summary>
        /// <param name="type">The type of precondition</param>
        /// <param name="parameters">The Paremeters needed for execution of a Precondition</param>
        [JsonConstructor]
        public Precondition(string type, Dictionary<string, IConvertible> parameters)
            : base(type, parameters)
        { 
        }
    }
}