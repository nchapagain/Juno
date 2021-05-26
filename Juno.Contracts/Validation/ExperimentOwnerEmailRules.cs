namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Provides a method for validating Experiment Owner Emails
    /// </summary>
    public class ExperimentOwnerEmailRules : IValidationRule<GoalBasedSchedule>
    {
        // Regular expression for Microsoft Emails
        private static readonly Regex MicrosoftEmail = new Regex(@"^([a-z0-9!#$%&'*.+/=?^_‘{|}~-]){4,20}@{1}(microsoft.com)(\]?)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private ExperimentOwnerEmailRules()
        {
        }

        /// <summary>
        /// Gets the singleton instance of <see cref="ExperimentOwnerEmailRules"/>
        /// </summary>
        public static ExperimentOwnerEmailRules Instance { get; } = new ExperimentOwnerEmailRules();

        /// <summary>
        /// Executes the validation rule against the executionGoal.
        /// The validation done here confirms that the execution goal contains a valid
        /// experiment owner email address.
        /// </summary>
        /// <param name="executionGoal">Execution Goal to validate.</param>
        /// <returns>Whether or not an executionGoal has a valid experiment owner email address</returns>
        public ValidationResult Validate(GoalBasedSchedule executionGoal)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            List<string> validationErrors = new List<string>();
            bool isValid = true;

            string owner = executionGoal.Owner;
            if (string.IsNullOrEmpty(owner))
            {
                validationErrors.Add($"The execution goal provided failed schema validation. The execution goal is missing an experiment owner email" +
                    $"address in metadata.");
                isValid = false;
                return new ValidationResult(isValid, validationErrors);
            }

            char[] delimiterChars = { ' ', ',', ';', '\t' };
            string[] emails = owner.ToString().Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);

            foreach (string email in emails)
            {
                string recipient = email.Trim();

                if (!ExperimentOwnerEmailRules.MicrosoftEmail.IsMatch(recipient))
                {
                    validationErrors.Add($"The execution goal provided failed schema validation. An experiment owner email address is in an invalid format. Please use a " +
                    $"valid email address on the Microsoft domain. {Environment.NewLine}Expected format: alias@microsoft.com. {Environment.NewLine}" +
                    $"Email provided: {recipient}");
                    isValid = false;
                }
            }

            return new ValidationResult(isValid, validationErrors);
        }
    }
}