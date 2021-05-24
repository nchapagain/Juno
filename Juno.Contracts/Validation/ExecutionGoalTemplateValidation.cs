namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Provides a method validating Juno Execution Goals template and related components.
    /// </summary>
    public class ExecutionGoalTemplateValidation : List<IValidationRule<GoalBasedSchedule>>, IValidationRule<GoalBasedSchedule>
    {
        private ExecutionGoalTemplateValidation()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="ExecutionGoalTemplateValidation"/>
        /// </summary>
        public static ExecutionGoalTemplateValidation Instance { get; } = new ExecutionGoalTemplateValidation();

        /// <summary>
        /// Executes all <see cref="IValidationRule{T}"/> instances that exist in the 
        /// validation set against the ExecutionGoal template provided.
        /// </summary>
        /// <param name="executionGoal">The Execution Goal Template to validate</param>
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
