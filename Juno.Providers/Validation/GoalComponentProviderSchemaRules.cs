namespace Juno.Providers.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// 
    /// </summary>
    public class GoalComponentProviderSchemaRules : IValidationRule<GoalComponent>
    {
        private static readonly Type ConvertibleType = typeof(IConvertible);

        private static readonly List<Type> SupportedParameterTypes = new List<Type>
        {
            // Add types here as we want to support more parameter types.
        };

        private static readonly List<Func<IConvertible, Type, bool>> ParamaterTypeValidators = new List<Func<IConvertible, Type, bool>>
        {
            (value, targetType) =>
            { 
                // The only data type that is support as of now is IConvertible 
                bool isValid = false;

                if (targetType.GetInterfaces().Contains(GoalComponentProviderSchemaRules.ConvertibleType))
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
            }
        };

        private GoalComponentProviderSchemaRules()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="GoalComponentProviderSchemaRules"/> class.
        /// </summary>
        public static GoalComponentProviderSchemaRules Instance { get; } = new GoalComponentProviderSchemaRules();

        /// <summary>
        /// Throws an exception if the Precondition component parameters do not meet the requirements
        /// defined by the <see cref="SupportedParameterAttribute"/> definition.
        /// </summary>
        /// <param name="component">The Precondition component to validate </param>
        /// <param name="parameterDefinition">An attribute defining the requirements of a parameter</param>
        public static void ValidateParameter(GoalComponent component, SupportedParameterAttribute parameterDefinition)
        {
            component.ThrowIfNull(nameof(component));
            parameterDefinition.ThrowIfNull(nameof(parameterDefinition));

            bool parameterDefined = component.Parameters?.ContainsKey(parameterDefinition.Name) == true;
            if (parameterDefinition.Required && !parameterDefined)
            {
                throw new SchemaException($"Required parameter '{parameterDefinition.Name}' is not defined for component '{component.Type}'.");
            }

            if (parameterDefined)
            {
                Type expectedDataType = parameterDefinition.Type;
                if (!expectedDataType.GetInterfaces().Contains(GoalComponentProviderSchemaRules.ConvertibleType)
                    && !GoalComponentProviderSchemaRules.SupportedParameterTypes.Contains(expectedDataType))
                {
                    throw new SchemaException(
                        $"Unsupported parameter data type '{expectedDataType.FullName}' for parameter '{parameterDefinition.Name}' on " +
                        $"component '{component.Type}'");
                }

                IConvertible value = component.Parameters[parameterDefinition.Name];
                if (!GoalComponentProviderSchemaRules.ParamaterTypeValidators.Any(validator => validator.Invoke(value, expectedDataType)))
                {
                    throw new SchemaException(
                        $"Invalid data type format for parameter '{parameterDefinition.Name}' on component '{component.Type}'. " +
                        $"The value '{value.ToString()}' cannot be formatted as a '{expectedDataType.FullName}' value.");
                }
            }
        }

        /// <summary>
        /// Valdiates parameters for the Precondition Component
        /// </summary>
        /// <param name="component">Component on which to validate</param>
        /// <returns>The result of the Validation</returns>
        public ValidationResult Validate(GoalComponent component)
        {
            component.ThrowIfNull(nameof(component));

            List<string> validationErrors = new List<string>();

            GoalComponentProviderSchemaRules.ValidatePreconditionParameters(validationErrors, component);

            return new ValidationResult(!validationErrors.Any(), validationErrors);
        }

        private static void ValidatePreconditionParameters(List<string> validationErrors, GoalComponent component)
        {
            IEnumerable<SupportedParameterAttribute> attributes = component.GetSupportedParameters();
            if (attributes?.Any() == true)
            {
                attributes.ToList().ForEach(parameter =>
                {
                    try
                    {
                        GoalComponentProviderSchemaRules.ValidateParameter(component, parameter);
                    }
                    catch (Exception exc)
                    {
                        validationErrors.Add(exc.Message);
                    }
                });

                List<string> unsupportedParameters = component.Parameters?.Keys
                    .Where(p => !attributes.Any(a => string.Equals(a.Name, p, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (unsupportedParameters?.Any() == true)
                {
                    validationErrors.Add(
                        $"Unsupported parameters found. The following parameters on component '{component.Type}' are not supported: " +
                        $"{string.Join(", ", unsupportedParameters)}");
                }
            }
        }
    }
}
