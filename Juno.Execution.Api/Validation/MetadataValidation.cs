namespace Juno.Execution.Api.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Juno.Contracts.Validation;

    /// <summary>
    /// Provides validation for experiment metadata requirements
    /// </summary>
    public class MetadataValidation : IValidationRule<Experiment>
    {
        private MetadataValidation()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="MetadataValidation"/> class.
        /// </summary>
        public static MetadataValidation Instance { get; } = new MetadataValidation();

        /// <summary>
        /// Required metadata properties for ALL experiments.
        /// </summary>
        internal static List<string> RequiredMetadata { get; } = new List<string>
        {
            "experimentType",
            "generation",
            "nodeCpuId",
            "payload",
            "payloadType",
            "payloadVersion",
            "payloadPFVersion",
            "workload",
            "workloadType",
            "workloadVersion",
            "impactType"
        };

        /// <summary>
        /// Validates the metadata properties of the experiment.
        /// </summary>
        /// <param name="experiment">The experiment to validate.</param>
        public ValidationResult Validate(Experiment experiment)
        {
            List<string> validationErrors = new List<string>();

            if (experiment != null)
            {
                MetadataValidation.ValidateRequiredMetadataDefined(experiment, validationErrors);
            }

            return new ValidationResult(!validationErrors.Any(), validationErrors);
        }

        private static void ValidateRequiredMetadataDefined(Experiment experiment, List<string> validationErrors)
        {
            List<string> missingMetadata = new List<string>();

            foreach (string property in MetadataValidation.RequiredMetadata)
            {
                if (experiment.Metadata?.TryGetValue(property, out IConvertible value) != true)
                {
                    missingMetadata.Add(property);
                }
            }

            if (missingMetadata.Any())
            {
                validationErrors.Add(
                    $"The following required metadata properties are not defined on the experiment: " +
                    $"{string.Join(", ", missingMetadata)}");
            }
        }
    }
}
