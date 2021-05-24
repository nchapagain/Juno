namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Provides a method validating Juno Execution Goals and related
    /// components.
    /// </summary>
    public class ExecutionGoalValidation : List<IValidationRule<GoalBasedSchedule>>, IValidationRule<GoalBasedSchedule>
    {
        private ExecutionGoalValidation()
        { 
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="ExecutionGoalValidation"/>
        /// </summary>
        public static ExecutionGoalValidation Instance { get; } = new ExecutionGoalValidation();

        /// <summary>
        /// Executes all <see cref="IValidationRule{T}"/> instances that exist in the 
        /// validation set against the ExecutionGoal provided.
        /// </summary>
        /// <param name="executionGoal">The Execution Goal to validate</param>
        /// <returns></returns>
        public ValidationResult Validate(GoalBasedSchedule executionGoal)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            bool isValid = true;
            List<string> validationErrors = new List<string>();

            foreach (IValidationRule<GoalBasedSchedule> rule in this)
            {
                ValidationResult result = rule.Validate(executionGoal);
                if (!result.IsValid)
                {
                    isValid = false;
                    if (result.ValidationErrors?.Any() == true)
                    {
                        validationErrors.AddRange(result.ValidationErrors);
                    }
                }
            }

            return new ValidationResult(isValid, validationErrors);
        }
    }
}
