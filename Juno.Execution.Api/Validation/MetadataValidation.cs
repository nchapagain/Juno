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
        internal static List<string> RequiredMetadata { get; } = new List<string>()
        {
            MetadataProperty.ExperimentType,
            MetadataProperty.Generation,
            MetadataProperty.NodeCpuId,
            MetadataProperty.Payload,
            MetadataProperty.Revision,
            MetadataProperty.TenantId,
            MetadataProperty.Workload,
            MetadataProperty.ImpactType
        };

        /// <summary>
        /// Required metadata properties given certain other properties exist for ALL experiments.
        /// </summary>
        internal static Dictionary<string, List<string>> RequiredIfExistsMetadata { get; } = new Dictionary<string, List<string>>()
        {
            // In the entries below, the Value is a set of metadata properties that are required if
            // the property defined by the Key exists. For example, if the 'payload'
            { MetadataProperty.PayloadType, new List<string> { MetadataProperty.PayloadVersion, MetadataProperty.PayloadPFVersion } },
            { MetadataProperty.WorkloadType, new List<string> { MetadataProperty.WorkloadVersion } },
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

            // Metadata that is absolutely required.
            MetadataValidation.RequiredMetadata.ForEach(property =>
            {
                if (experiment.Metadata?.TryGetValue(property, out IConvertible value) != true)
                {
                    missingMetadata.Add(property);
                }
            });

            // Metadata that is required if certain other metadata properties exist.
            MetadataValidation.RequiredIfExistsMetadata.ToList().ForEach(entry =>
            {
                if (experiment.Metadata?.TryGetValue(entry.Key, out IConvertible value) == true)
                {
                    entry.Value.ForEach(property =>
                    {
                        if (experiment.Metadata?.TryGetValue(property, out IConvertible value) != true)
                        {
                            missingMetadata.Add(property);
                        }
                    });
                }
            });

            if (missingMetadata.Any())
            {
                validationErrors.Add(
                    $"The following required metadata properties are not defined on the experiment: " +
                    $"{string.Join(", ", missingMetadata)}");
            }
        }
    }
}
