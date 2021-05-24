namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
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
        /// <param name="executionGoal"><see cref="GoalBasedSchedule"/></param>
        /// <param name="targetGoal"><see cref="TargetGoalTrigger"/></param>
        /// <param name="configuration"><see cref="IConfiguration"/></param>
        public ScheduleContext(GoalBasedSchedule executionGoal, TargetGoalTrigger targetGoal, IConfiguration configuration)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));
            targetGoal.ThrowIfNull(nameof(targetGoal));
            configuration.ThrowIfNull(nameof(configuration));
            
            this.ExecutionGoal = executionGoal;
            this.TargetGoalTrigger = targetGoal;
            this.Configuration = configuration;
        }

        /// <summary>
        /// Current Execution Goal
        /// </summary>
        public GoalBasedSchedule ExecutionGoal { get; }

        /// <summary>
        /// Current Target Goal
        /// </summary>
        public TargetGoalTrigger TargetGoalTrigger { get; }

        /// <summary>
        /// Current Configuration
        /// </summary>
        public IConfiguration Configuration { get; }
    }
}
