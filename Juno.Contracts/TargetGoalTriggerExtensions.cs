namespace Juno.Contracts
{
    using System;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;
    using NCrontab;

    /// <summary>
    /// Extension methods for <see cref="TargetGoalTrigger"/>
    /// </summary>
    public static class TargetGoalTriggerExtensions
    {
        /// <summary>
        /// Evaluates whether the cron expression occures within the given range of date times.
        /// For examples on how to write cron expressions: https://crontab.guru/examples.html
        /// </summary>
        /// <param name="targetGoal">The target goal whose cron expression is evaluated.</param>
        /// <param name="startTime">The start time of evalaution.</param>
        /// <param name="endTime">The end time of evaluation.</param>
        /// <returns>True/False if the <see cref="TargetGoalTrigger"/> has an occurence within the given date times.</returns>
        public static bool HasOccurence(this TargetGoalTrigger targetGoal, DateTime startTime, DateTime endTime)
        {
            targetGoal.ThrowIfNull(nameof(targetGoal));
            CrontabSchedule crontabSchedule = CrontabSchedule.Parse(targetGoal.CronExpression);
            return crontabSchedule.GetNextOccurrences(startTime, endTime).Any();
        }
    }
}
