namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Rule to validate the basic schema of an experiment.
    /// </summary>
    public class SchemaRules : IValidationRule<Experiment>
    {
        private SchemaRules()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="SchemaRules"/>
        /// </summary>
        public static SchemaRules Instance { get; } = new SchemaRules();

        /// <summary>
        /// Validates the schema of the experiment to ensure it is authored/constructed
        /// correctly.
        /// </summary>
        /// <param name="experiment">The experiment to validate.</param>
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance method preferred to match usage pattern.")]
        public ValidationResult Validate(Experiment experiment)
        {
            experiment.ThrowIfNull(nameof(experiment));
            List<string> validationErrors = new List<string>();

            try
            {
                SchemaRules.ValidateParameterReferences(experiment.Workflow, experiment, validationErrors);
            }
            catch (Exception exc)
            {
                validationErrors.Add(exc.Message);
            }

            return new ValidationResult(!validationErrors.Any(), validationErrors);
        }

        private static void ValidateParameterReferences(IEnumerable<ExperimentComponent> workflowComponents, Experiment experiment, List<string> validationErrors)
        {
            foreach (ExperimentComponent component in workflowComponents)
            {
                if (component.Parameters?.Any() == true)
                {
                    foreach (KeyValuePair<string, IConvertible> entry in component.Parameters)
                    {
                        string parameterName;
                        if (entry.TryGetParameterReference(out parameterName))
                        {
                            if (experiment.Parameters?.ContainsKey(parameterName) != true)
                            {
                                validationErrors.Add(
                                    $"Invalid experiment parameter reference. The parameter '{entry.Key}' in experiment component step " +
                                    $"'{component.Name}' references a shared parameter that does not exist in the experiment definition.");
                            }
                        }
                    }
                }

                if (component.Dependencies?.Any() == true)
                {
                    SchemaRules.ValidateParameterReferences(component.Dependencies, experiment, validationErrors);
                }

                if (component.HasExtension(ContractExtension.Steps))
                {
                    try
                    {
                        IEnumerable<ExperimentComponent> childSteps = component.GetChildSteps();

                        if (childSteps?.Any() == true)
                        {
                            SchemaRules.ValidateParameterReferences(childSteps, experiment, validationErrors);
                        }
                    }
                    catch (Exception exc)
                    {
                        validationErrors.Add(exc.Message);
                    }
                }
            }
        }
    }
}
