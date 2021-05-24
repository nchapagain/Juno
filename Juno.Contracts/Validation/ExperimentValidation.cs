namespace Juno.Contracts.Validation
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Provides a method for validating Juno experiments and related
    /// components.
    /// </summary>
    public class ExperimentValidation : List<IValidationRule<Experiment>>, IValidationRule<Experiment>
    {
        private ExperimentValidation()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="ExperimentValidation"/> class.
        /// </summary>
        public static ExperimentValidation Instance { get; } = new ExperimentValidation();

        /// <summary>
        /// Executes all <see cref="IValidationRule{T}"/> instances that exist in the
        /// validation set against the experiment provided.
        /// </summary>
        /// <param name="experiment">The experiment to validate.</param>
        public ValidationResult Validate(Experiment experiment)
        {
            experiment.ThrowIfNull(nameof(experiment));

            bool isValid = true;
            List<string> validationErrors = new List<string>();

            foreach (IValidationRule<Experiment> rule in this)
            {
                ValidationResult result = rule.Validate(experiment);
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
