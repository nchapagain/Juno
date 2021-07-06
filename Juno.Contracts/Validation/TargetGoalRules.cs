namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Provides a method for validating Juno Target Goals
    /// </summary>
    public class TargetGoalRules : IValidationRule<GoalBasedSchedule>
    {
        private TargetGoalRules()
        {
        }

        /// <summary>
        /// Gets the singleton instance of <see cref="TargetGoalRules"/>
        /// </summary>
        public static TargetGoalRules Instance { get; } = new TargetGoalRules();

        /// <summary>
        /// Executes the validation rule against the targetGoal.
        /// The validation done here confirms that the target goal contains a valid workload in schedule action of a target goal
        /// </summary>
        /// <param name="executionGoal">Execution Goal to validate.</param>
        /// <returns><see cref="ValidationResult"/></returns>
        public ValidationResult Validate(GoalBasedSchedule executionGoal)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            List<string> validationErrors = new List<string>();
            bool isValid = true;

            HashSet<string> workloads = new HashSet<string>();
            foreach (TargetGoal targetGoal in executionGoal.TargetGoals)
            {
                if (targetGoal.Preconditions.Count < 2)
                {
                    validationErrors.Add($"Target Goal: {targetGoal.Name} must contain a Precondition of type {ContractExtension.TimerTriggerType} and {ContractExtension.SuccessfulExperimentsProvider}");
                    isValid = false;
                }

                try
                {
                    string workload = targetGoal.GetWorkload();
                    if (workloads.Contains(workload))
                    {
                        isValid = false;
                        validationErrors.Add($"TargetGoal: {targetGoal.Name} contains workload that is already used for another target goal. Duplicate Workload: {workload}");
                    }

                    workloads.Add(workload);
                }
                catch (SchemaException ex)
                {
                    isValid = false;
                    validationErrors.Add(ex.Message);
                }
            }

            return new ValidationResult(isValid, validationErrors);
        }
    }
}