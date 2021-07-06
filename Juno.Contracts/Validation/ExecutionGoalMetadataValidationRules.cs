namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Validates that required metadata is present in the execution goal
    /// </summary>
    public class ExecutionGoalMetadataValidationRules : IValidationRule<GoalBasedSchedule>
    {
        private ExecutionGoalMetadataValidationRules()
        { 
        }

        /// <summary>
        /// Gets a singleton instance of a <see cref="ExecutionGoalMetadataValidationRules"/>
        /// </summary>
        public static ExecutionGoalMetadataValidationRules Instance { get; } = new ExecutionGoalMetadataValidationRules();

        /// <summary>
        /// Validates the <see cref="GoalBasedSchedule"/>
        /// </summary>
        /// <param name="data">The <see cref="GoalBasedSchedule"/> to validate.</param>
        /// <returns>A result that encapsulates whether or not the information is valid.</returns>
        public ValidationResult Validate(GoalBasedSchedule data)
        {
            data.ThrowIfNull(nameof(data));
            try
            {
                this.ValidateMetadata(data.Metadata);
            }
            catch (SchemaException exc)
            {
                return new ValidationResult(false, new List<string>() { exc.Message });
            }

            return new ValidationResult(true);
        }

        private void ValidateMetadata(IDictionary<string, IConvertible> metadata)
        {
            List<string> missingMetadata = new List<string>();
            foreach (string requiredMetadata in ExecutionGoalMetadata.RequiredMetadata)
            {
                if (!metadata.ContainsKey(requiredMetadata))
                {
                    missingMetadata.Add(requiredMetadata);
                }
            }

            if (missingMetadata.Any())
            {
                throw new SchemaException($"The metadata supplied does not have all required metadata. Missing metadata: {string.Join(", ", missingMetadata)}");
            }
        }
    }
}
