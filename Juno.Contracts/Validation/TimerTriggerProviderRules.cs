namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Provides a method for validating Timer Trigger Provider
    /// </summary>
    public class TimerTriggerProviderRules : IValidationRule<GoalBasedSchedule>
    {
        private TimerTriggerProviderRules()
        { 
        }

        /// <summary>
        /// Gets the singleton instance of <see cref="TimerTriggerProviderRules"/>
        /// </summary>
        public static TimerTriggerProviderRules Instance { get; } = new TimerTriggerProviderRules();

        /// <summary>
        /// Executes the validation rule against the targetGoal.
        /// The validation done here confirms that the target goal contains a valid
        /// TimerTrigger Preconditon.
        /// </summary>
        /// <param name="executionGoal">Execution Goal to validate.</param>
        /// <returns><see cref="ValidationResult"/></returns>
        public ValidationResult Validate(GoalBasedSchedule executionGoal)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            List<string> validationErrors = new List<string>();
            bool isValid = true;

            if (GoalBasedScheduleExtensions.IsExecutionGoalVersion20200727(executionGoal.Version))
            {
                return new ValidationResult(isValid, validationErrors);
            }

            foreach (Goal targetGoal in executionGoal.TargetGoals)
            {
                if (!targetGoal.Preconditions.Any(precondition => precondition.Type.Equals(ContractExtension.TimerTriggerType, StringComparison.OrdinalIgnoreCase)))
                {
                    validationErrors.Add($"TargetGoal: {targetGoal.Name} must contain a Precondition of type {ContractExtension.TimerTriggerType}");
                    isValid = false;
                    continue;
                }

                Precondition temp = targetGoal.Preconditions.First(precondition => precondition.Type.Equals(ContractExtension.TimerTriggerType, StringComparison.OrdinalIgnoreCase));
                if (!temp.Parameters.Keys.Any(key => key.Equals(ContractExtension.CronExpression, StringComparison.OrdinalIgnoreCase)))
                {
                    validationErrors.Add($"{ContractExtension.TimerTriggerType} must contain a field with key `cronExpression` with a valid cron expression as a value in the Target Goal: {targetGoal.Name}.");
                    isValid = false;
                    continue;
                }

                string cronExpression = temp.Parameters.GetValue<string>(ContractExtension.CronExpression);

                if (!CronTabValidation.Validate(cronExpression, out string errorMessage))
                {
                    validationErrors.Add($"The cron expression in the {ContractExtension.TimerTriggerType} precondition in the Target Goal: {targetGoal.Name} is invalid: {errorMessage}");
                    isValid = false;
                    continue;
                }
            }

            return new ValidationResult(isValid, validationErrors);
        }
    }
}
