namespace Juno.Providers.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Provides a method for validating provider references in Juno experiments 
    /// and related components.
    /// </summary>
    public class ProviderSchemaRules : IValidationRule<Experiment>
    {
        private static readonly Type ConvertibleType = typeof(IConvertible);
        private static readonly Type TimespanType = typeof(TimeSpan);
        private static readonly Type UriType = typeof(Uri);

        private static readonly List<Type> SupportedParameterTypes = new List<Type>
        {
            // Supported parameter types other than those that are IConvertible.
            ProviderSchemaRules.TimespanType,
            ProviderSchemaRules.UriType
        };

        private static readonly List<Func<IConvertible, Type, bool>> ParameterTypeValidators = new List<Func<IConvertible, Type, bool>>
        {
            (value, targetType) =>
            {
                // Enumeration data type validation is supported.
                bool isValid = false;
                if (targetType.IsEnum)
                {
                    try
                    {
                        isValid = Enum.GetNames(targetType).Contains(value?.ToString().Trim());
                    }
                    catch (ArgumentException)
                    {
                        // The enum value is not a valid value for the enum
                        // type specified.
                    }
                }

                return isValid;
            },
            (value, targetType) =>
            {
                // If the value is an IConvertible type and can be converted to the target type
                // is is valid.
                bool isValid = false;

                if (targetType.GetInterfaces().Contains(ProviderSchemaRules.ConvertibleType))
                {
                    try
                    {
                        Convert.ChangeType(value, targetType);
                        isValid = true;
                    }
                    catch
                    {
                    }
                }

                return isValid;
            },
            (value, targetType) =>
            {
                // TimeSpan conversion is supported.
                bool isValid = false;
                if (targetType == ProviderSchemaRules.TimespanType)
                {
                    TimeSpan actualValue;
                    isValid = TimeSpan.TryParse(value?.ToString(), out actualValue);
                }

                return isValid;
            },
            (value, targetType) =>
            {
                // URI conversion is supported.
                bool isValid = false;
                if (targetType == ProviderSchemaRules.UriType)
                {
                    Uri actualValue;
                    isValid = Uri.TryCreate(value?.ToString(), UriKind.RelativeOrAbsolute, out actualValue);
                }

                return isValid;
            }
        };

        private ProviderSchemaRules()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="ProviderSchemaRules"/> class.
        /// </summary>
        public static ProviderSchemaRules Instance { get; } = new ProviderSchemaRules();

        /// <summary>
        /// Throws an exception if the experiment component parameters do not meet the requirements
        /// defined by the <see cref="SupportedParameterAttribute"/> definition.
        /// </summary>
        /// <param name="component">The experiment component to validate.</param>
        /// <param name="parameterDefinition">An attribute defining the requirements of a parameter for the component.</param>
        public static void ValidateParameter(ExperimentComponent component, SupportedParameterAttribute parameterDefinition)
        {
            component.ThrowIfNull(nameof(component));
            parameterDefinition.ThrowIfNull(nameof(parameterDefinition));

            bool parameterDefined = component.Parameters?.ContainsKey(parameterDefinition.Name) == true;
            if (parameterDefinition.Required && !parameterDefined)
            {
                throw new SchemaException($"Required parameter '{parameterDefinition.Name}' is not defined for component '{component.Name}'.");
            }

            if (parameterDefined)
            {
                Type expectedDataType = parameterDefinition.Type;
                if (!expectedDataType.GetInterfaces().Contains(ProviderSchemaRules.ConvertibleType)
                    && !ProviderSchemaRules.SupportedParameterTypes.Contains(expectedDataType))
                {
                    throw new SchemaException(
                        $"Unsupported parameter data type '{expectedDataType.FullName}' for parameter '{parameterDefinition.Name}' on " +
                        $"component '{component.Name}'");
                }

                IConvertible value = component.Parameters[parameterDefinition.Name];
                if (!ProviderSchemaRules.ParameterTypeValidators.Any(validator => validator.Invoke(value, expectedDataType)))
                {
                    throw new SchemaException(
                        $"Invalid data type format for parameter '{parameterDefinition.Name}' on component '{component.Name}'. " +
                        $"The value '{value.ToString()}' cannot be formatted as a '{expectedDataType.FullName}' value.");
                }
            }
        }

        /// <summary>
        /// Throws an exception if the experiment component contains unsupported parameters not defined
        /// by the <see cref="SupportedParameterAttribute"/> definitions.
        /// </summary>
        /// <param name="component">The experiment component to validate.</param>
        /// <param name="parameterDefinitions">Attributes defining supported parameters for the component.</param>
        public static void ValidateUnsupportedParameters(ExperimentComponent component, IEnumerable<SupportedParameterAttribute> parameterDefinitions)
        {
            component.ThrowIfNull(nameof(component));
            parameterDefinitions.ThrowIfNull(nameof(parameterDefinitions));

            List<string> unsupportedParameters = component.Parameters?.Keys
                .Where(p => parameterDefinitions?.Any(a => string.Equals(a.Name, p, StringComparison.OrdinalIgnoreCase)) != true)
                .ToList();

            if (unsupportedParameters?.Any() == true)
            {
                throw new SchemaException(
                    $"Unsupported parameters found. The following parameters on component '{component.Name}' are not supported: " +
                    $"{string.Join(", ", unsupportedParameters)}");
            }
        }

        /// <summary>
        /// Validates the provider references in the experiment component definition.
        /// </summary>
        /// <param name="experiment">The experiment component to validate.</param>
        public ValidationResult Validate(Experiment experiment)
        {
            experiment.ThrowIfNull(nameof(experiment));

            List<string> validationErrors = new List<string>();

            try
            {
                ProviderSchemaRules.ValidateProviderReferences(validationErrors, experiment.Workflow.ToArray());
                ProviderSchemaRules.ValidateComponentParameters(validationErrors, experiment.Workflow.ToArray());
                ProviderSchemaRules.ValidateParallelExecution(experiment.Workflow, validationErrors);
                ProviderSchemaRules.ValidateEnvironmentCriteria(experiment.Workflow, validationErrors);
            }
            catch (Exception exc)
            {
                validationErrors.Add(exc.Message);
            }

            return new ValidationResult(!validationErrors.Any(), validationErrors);
        }

        private static void ValidateComponentParameters(List<string> validationErrors, params ExperimentComponent[] workflowComponents)
        {
            if (workflowComponents?.Any() == true)
            {
                foreach (ExperimentComponent component in workflowComponents)
                {
                    if (component.IsParallelExecution())
                    {
                        ProviderSchemaRules.ValidateComponentParameters(validationErrors, component.GetChildSteps().ToArray());
                    }
                    else
                    {
                        // Validate that any parameters supplied to the component match the supported
                        // parameters defined for the provider.
                        IEnumerable<SupportedParameterAttribute> attributes = component.GetSupportedParameters();
                        if (attributes?.Any() == true)
                        {
                            attributes.ToList().ForEach(parameter =>
                            {
                                try
                                {
                                    ProviderSchemaRules.ValidateParameter(component, parameter);
                                }
                                catch (Exception exc)
                                {
                                    validationErrors.Add(exc.Message);
                                }
                            });
                        }

                        try
                        {
                            ProviderSchemaRules.ValidateUnsupportedParameters(component, attributes);
                        }
                        catch (Exception exc)
                        {
                            validationErrors.Add(exc.Message);
                        }

                        // Validate the parameters in any dependency components.
                        if (component.Dependencies?.Any() == true)
                        {
                            ProviderSchemaRules.ValidateComponentParameters(validationErrors, component.Dependencies.ToArray());
                        }

                        // Validate the parameters in any child steps/components.
                        if (component.HasExtension(ContractExtension.Steps))
                        {
                            try
                            {
                                IEnumerable<ExperimentComponent> childSteps = component.GetChildSteps();

                                if (childSteps?.Any() == true)
                                {
                                    foreach (ExperimentComponent componentStep in childSteps)
                                    {
                                        ProviderSchemaRules.ValidateComponentParameters(validationErrors, componentStep);
                                    }
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

        private static void ValidateEnvironmentCriteria(IEnumerable<ExperimentComponent> workflowComponents, List<string> validationErrors)
        {
            IEnumerable<ExperimentComponent> parallelExecutionSteps = workflowComponents
                .Where(cmp => cmp.IsParallelExecution())?.SelectMany(cmp => cmp.GetChildSteps());

            if (parallelExecutionSteps?.Any() == true)
            {
                ProviderSchemaRules.ValidateEnvironmentCriteria(parallelExecutionSteps, validationErrors);
            }

            IEnumerable<ExperimentComponent> environmentCriteria = workflowComponents.Where(component => !component.IsParallelExecution())
                ?.Where(component => component.GetSupportedStepType() == SupportedStepType.EnvironmentCriteria);

            if (environmentCriteria?.Any() == true)
            {
                if (environmentCriteria.Any(criteria => criteria.Group == ExperimentComponent.AllGroups)
                    && environmentCriteria.Any(criteria => criteria.Group != ExperimentComponent.AllGroups))
                {
                    validationErrors.Add(
                        "Invalid workflow environment criteria component step usage. An experiment workflow cannot have both shared environment criteria " +
                        "as well as criteria defined for individual group definitions.");
                }
            }
        }

        private static void ValidateParallelExecution(IEnumerable<ExperimentComponent> components, List<string> validationErrors)
        {
            if (components?.Any() == true)
            {
                foreach (ExperimentComponent component in components)
                {
                    if (component.IsParallelExecution())
                    {
                        IEnumerable<ExperimentComponent> workloadSteps = component.GetChildSteps();

                        if (workloadSteps?.Any() != true)
                        {
                            validationErrors.Add(
                                $"The experiment component schema does not match the requirements of a parallel execution definition. " +
                                $"The experiment component definition must contain an array '{ContractExtension.Steps}' that defines a set of ." +
                                $"one or more valid components that should be executed in-parallel.");
                        }
                    }
                }
            }
        }

        private static void ValidateProviderReferences(List<string> validationErrors, params ExperimentComponent[] workflowComponents)
        {
            if (workflowComponents?.Any() == true)
            {
                foreach (ExperimentComponent component in workflowComponents)
                {
                    try
                    {
                        if (component.IsParallelExecution())
                        {
                            ProviderSchemaRules.ValidateProviderReferences(validationErrors, component.GetChildSteps().ToArray());
                        }
                        else
                        {
                            // Validate provider type references
                            Type referencedProviderType = component.GetProviderType();
                            SupportedStepType stepType = referencedProviderType.GetSupportedStepType();

                            if (stepType == SupportedStepType.Undefined)
                            {
                                validationErrors.Add(
                                    $"Invalid experiment provider definition. The target provider for component step '{component.ComponentType}' does not have a " +
                                    $"valid supported step type defined.");
                            }

                            // Validate the parameters in any dependency components.
                            if (component.Dependencies?.Any() == true)
                            {
                                ProviderSchemaRules.ValidateProviderReferences(validationErrors, component.Dependencies.ToArray());
                            }

                            // Validate provider type references for any child steps/components
                            if (component.HasExtension(ContractExtension.Steps))
                            {
                                try
                                {
                                    IEnumerable<ExperimentComponent> childSteps = component.GetChildSteps();

                                    if (childSteps?.Any() == true)
                                    {
                                        foreach (ExperimentComponent childStep in childSteps)
                                        {
                                            SupportedStepType childStepType = childStep.GetSupportedStepType();
                                            if (childStepType != stepType)
                                            {
                                                validationErrors.Add(
                                                    $"Invalid workflow child component step usage. The child component step '{childStep.Name}' of the parent component step '{component.Name}' " +
                                                    $"supports a step type that does not match the step type of the parent. The step type of child component steps must match the step " +
                                                    $"type of their parent.");
                                            }

                                            ProviderSchemaRules.ValidateProviderReferences(validationErrors, childStep);
                                        }
                                    }
                                }
                                catch (Exception exc)
                                {
                                    validationErrors.Add(exc.Message);
                                }
                            }
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
