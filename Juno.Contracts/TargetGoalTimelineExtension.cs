namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using NCrontab;

    /// <summary>
    /// Extension methods for <see cref="TargetGoalTimeline"/>
    /// </summary>
    /// 
    public static class TargetGoalTimelineExtension
    {
        private const double ExperimentBuffer = 0.40;

        /// <summary>
        /// Find the target gaol status based on input parameters for <see cref="TargetGoalTimeline"/>
        /// </summary>
        public static ExperimentStatus CalculateStatus(int successfulExperimentInstances, int targetExperimentInstances)
        {
            bool targetGoalAccomplished = successfulExperimentInstances >= targetExperimentInstances;
            return targetGoalAccomplished ? ExperimentStatus.Succeeded : ExperimentStatus.InProgress;
        }

        /// <summary>
        /// Calculates the Estimated Completion of a Target Goal based on input parameters for <see cref="TargetGoalTimeline"/>
        /// </summary>
        /// <param name="cronExpression">Cron Expression of the Target Goal</param>
        /// <param name="targetExperimentInstances">Necessary experiment instances for Target Goal to be successful</param>
        /// <param name="successfulExperimentInstances">Current count of successful instances</param>
        public static DateTime CalculateEstimatedCompletionTime(string cronExpression, double targetExperimentInstances, double successfulExperimentInstances = 0)
        {
            CrontabSchedule crontabSchedule = CrontabSchedule.Parse(cronExpression);
            IEnumerable<DateTime> targetGoalOccurrences = crontabSchedule.GetNextOccurrences(DateTime.Now, DateTime.UtcNow.AddDays(+1));
            double daysForCompletion = (targetExperimentInstances - successfulExperimentInstances) / (double)targetGoalOccurrences.Count();
            double daysAfterAdjustingForBuffer = daysForCompletion + (daysForCompletion * TargetGoalTimelineExtension.ExperimentBuffer);

            return DateTime.Today.AddDays(Math.Ceiling(daysAfterAdjustingForBuffer));
        }
    }
}
