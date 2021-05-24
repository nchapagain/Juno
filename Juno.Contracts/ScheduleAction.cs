namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Describe Actions Scheduler can take
    /// </summary>
    public class ScheduleAction : GoalComponent
    {
        /// <summary>
        /// Initializes an instance of <see cref="ScheduleAction"/>
        /// </summary>
        /// <param name="type">The type of scheduleAction</param>
        /// <param name="parameters">The Paremeters needed for execution of a Precondition</param>
        public ScheduleAction(string type, Dictionary<string, IConvertible> parameters)
            : base(type, parameters)
        {
        }
    }
}
