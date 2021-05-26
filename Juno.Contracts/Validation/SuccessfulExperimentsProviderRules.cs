namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Provides a method for validating Successful Experiments Provider
    /// </summary>
    public class SuccessfulExperimentsProviderRules : IValidationRule<GoalBasedSchedule>
    {
        private SuccessfulExperimentsProviderRules()
        {
        }

        /// <summary>
        /// Gets the singleton instance of <see cref="SuccessfulExperimentsProviderRules"/>
        /// </summary>
        public static SuccessfulExperimentsProviderRules Instance { get; } = new SuccessfulExperimentsProviderRules();

        /// <summary>
        /// Executes the validation rule against the targetGoal.
        /// The validation done here confirms that the target goal contains a valid
        /// Successful Experiment Preconditon.
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
                if (!targetGoal.Preconditions.Any(precondition => precondition.Type.Equals(ContractExtension.SuccessfulExperimentsProvider, StringComparison.OrdinalIgnoreCase)))
                {
                    validationErrors.Add($"TargetGoal: {targetGoal.Name} must contain a Precondition of type {ContractExtension.SuccessfulExperimentsProvider}");
                    isValid = false;
                    continue;
                }

                Precondition temp = targetGoal.Preconditions.First(precondition => precondition.Type.Equals(ContractExtension.SuccessfulExperimentsProvider, StringComparison.OrdinalIgnoreCase));
                if (!temp.Parameters.Keys.Any(key => key.Equals(ContractExtension.TargetExperimentInstances, StringComparison.OrdinalIgnoreCase)))
                {
                    validationErrors.Add($"{ContractExtension.SuccessfulExperimentsProvider} must contain a field with key `{ContractExtension.TargetExperimentInstances}` with a valid integer value in the Target Goal: {targetGoal.Name}.");
                    isValid = false;
                    continue;
                }

                string targetExperimentInstances = temp.Parameters.GetValue<string>(ContractExtension.TargetExperimentInstances);
                if ((!int.TryParse(targetExperimentInstances, out int result)) || result < 1)
                {
                    validationErrors.Add($"The {ContractExtension.TargetExperimentInstances} in the {ContractExtension.SuccessfulExperimentsProvider} precondition in the Target Goal: {targetGoal.Name} is invalid.");
                    isValid = false;
                    continue;
                }
            }

            return new ValidationResult(isValid, validationErrors);
        }
    }
}