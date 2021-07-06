namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Contains the context of a Goal Based Schedule execution.
    /// Allows for low level providers understand their context.
    /// </summary>
    public class ScheduleContext
    {
        /// <summary>
        /// The context in which to allow context to low level execution of an ExecutionGoal
        /// </summary>
        /// <param name="executionGoal">A instance of an execution ogal.</param>
        /// <param name="targetGoal">An instance of a target goal.</param>
        /// <param name="configuration">Configuration for the current runtime environment.</param>
        public ScheduleContext(Item<GoalBasedSchedule> executionGoal, TargetGoalTrigger targetGoal, IConfiguration configuration)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));
            targetGoal.ThrowIfNull(nameof(targetGoal));
            configuration.ThrowIfNull(nameof(configuration));
            
            this.ExecutionGoal = executionGoal;
            this.TargetGoalTrigger = targetGoal;
            this.Configuration = configuration;
            this.RuntimeParameters = new Dictionary<string, IConvertible>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Current Execution Goal
        /// </summary>
        public Item<GoalBasedSchedule> ExecutionGoal { get; }

        /// <summary>
        /// Current Target Goal
        /// </summary>
        public TargetGoalTrigger TargetGoalTrigger { get; }

        /// <summary>
        /// Current Configuration
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Structure to store runtime parameters.
        /// </summary>
        public IDictionary<string, IConvertible> RuntimeParameters { get; }
    }
}
