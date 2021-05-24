namespace Juno.Providers.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Class that allows the validation of Environment Filters
    /// </summary>
    public class EnvironmentFilterValidation : IValidationRule<EnvironmentFilter>
    {
        private static readonly Type ConvertibleType = typeof(IConvertible);
        private static readonly Type TimespanType = typeof(TimeSpan);

        private static readonly IList<Type> SupportedParameterTypes = new List<Type>
        { 
            EnvironmentFilterValidation.TimespanType
        };

        private static readonly IList<Type> SupportedListableParameterTypes = new List<Type>
        {
            typeof(string),
            typeof(SsdDriveType)
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
                        string representation = value?.ToString().Trim();
                        // Allow for a list of the specified enumeration
                        if (representation.Contains(",", StringComparison.OrdinalIgnoreCase) || representation.Contains(";", StringComparison.OrdinalIgnoreCase))
                        {
                            IList<string> enumValues = representation.ToList(',', ';');
                            foreach (string enumValue in enumValues)
                            {
                                if (!Enum.GetNames(targetType).Contains(enumValue))
                                {
                                    return false;
                                }
                            }

                            return true;
                        }

                        // Single Enumeration value.
                        isValid = Enum.GetNames(targetType).Contains(representation);
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

                if (targetType.GetInterfaces().Contains(EnvironmentFilterValidation.ConvertibleType))
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
                if (targetType == EnvironmentFilterValidation.TimespanType)
                {
                    isValid = TimeSpan.TryParse(value?.ToString(), out TimeSpan actualValue);
                }

                return isValid;
            }
        };

        private EnvironmentFilterValidation()
        {
        }

        /// <summary>
        /// Returns a Singleton instance of a <see cref="EnvironmentFilterValidation"/>
        /// </summary>
        public static EnvironmentFilterValidation Instance { get; } = new EnvironmentFilterValidation();

        /// <summary>
        /// Validates the EnvironmentFilter
        /// </summary>
        /// <param name="filter">the filter to validate</param>
        /// <returns>A validation result that denotes if the filter was validated successfully</returns>
        public ValidationResult Validate(EnvironmentFilter filter)
        {
            filter.ThrowIfNull(nameof(filter));

            List<string> errorList = new List<string>();
            var parameters = filter.GetProviderType().GetCustomAttributes<SupportedFilterAttribute>(true);
            if (parameters?.Any() == true)
            {
                foreach (SupportedFilterAttribute parameter in parameters)
                {
                    try
                    {
                        EnvironmentFilterValidation.ValidateParameter(filter, parameter);
                    }
                    catch (Exception exc)
                    {
                        errorList.Add(exc.Message);
                    }
                }

                try
                {
                    EnvironmentFilterValidation.ValidateUnsupportedParameters(filter, parameters);
                }
                catch (Exception exc)
                {
                    errorList.Add(exc.Message);
                }
            }

            return new ValidationResult(!errorList.Any(), errorList.Any() ? errorList : null);
        }

        /// <summary>
        /// Validates one parameter against the given filter
        /// </summary>
        /// <param name="filter">The filter to validate.</param>
        /// <param name="parameterDefinition">The parameter to validate with</param>
        private static void ValidateParameter(EnvironmentFilter filter, SupportedFilterAttribute parameterDefinition)
        {
            filter.ThrowIfNull(nameof(filter));
            parameterDefinition.ThrowIfNull(nameof(parameterDefinition));

            bool parameterDefined = filter.Parameters?.ContainsKey(parameterDefinition.Name) == true;
            if (parameterDefinition.Required && !parameterDefined)
            {
                throw new SchemaException($"Required parameter '{parameterDefinition.Name}' is not defined for filter '{filter.Type}'.");
            }

            if (parameterDefined)
            {
                EnvironmentFilterValidation.ValidateParameterType(parameterDefinition, filter);
            }
        }

        private static void ValidateUnsupportedParameters(EnvironmentFilter filter, IEnumerable<SupportedFilterAttribute> attributes)
        {
            List<string> unsupportedParameters = new List<string>();
            foreach (string key in filter.Parameters?.Keys)
            {
                // If the key can not be found in the list of supported filter attributes, add to the unsupported parameters.
                if (attributes?.Any(attr => string.Equals(attr.Name, key, StringComparison.OrdinalIgnoreCase)) == false)
                {
                    unsupportedParameters.Add(key);
                }
            }

            if (unsupportedParameters.Any() == true)
            {
                throw new SchemaException(
                    $"Unsupported parameters found. The following parameters on component '{filter.Type}' are not supported: " +
                    $"{string.Join(", ", unsupportedParameters)}");
            }
        }

        private static void ValidateParameterType(SupportedFilterAttribute parameterDefinition, EnvironmentFilter filter)
        {
            Type expectedDataType = parameterDefinition.Type;
            if (!expectedDataType.GetInterfaces().Contains(EnvironmentFilterValidation.ConvertibleType)
                && !EnvironmentFilterValidation.SupportedParameterTypes.Contains(expectedDataType))
            {
                throw new SchemaException(
                    $"Unsupported parameter data type '{expectedDataType.FullName}' for parameter '{parameterDefinition.Name}' on " +
                    $"component '{filter.Type}'");
            }

            IConvertible value = filter.Parameters[parameterDefinition.Name];
            if (!EnvironmentFilterValidation.ParameterTypeValidators.Any(validator => validator.Invoke(value, expectedDataType)))
            {
                throw new SchemaException(
                    $"Invalid data type format for parameter '{parameterDefinition.Name}' on component '{filter.Type}'. " +
                    $"The value '{value}' cannot be formatted as a '{expectedDataType.FullName}' value.");
            }

            if (EnvironmentFilterValidation.SupportedListableParameterTypes.Contains(expectedDataType))
            {
                IList<string> stringValue = value.ToString().ToList(',', ';');
                if (stringValue.GroupBy(v => v).Any(g => g.Count() > 1))
                {
                    throw new SchemaException(
                        $"Listable parameters can not have duplicate values. The parameter: '{parameterDefinition.Name}' on componenet '{filter.Type}' has duplicate values." +
                        $"The duplicated values are: {string.Join(", ", stringValue.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key))}");
                }
            }
        }
    }
}
