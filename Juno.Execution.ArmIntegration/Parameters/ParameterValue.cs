namespace Juno.Execution.ArmIntegration.Parameters
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// This is wrapper class helps to convert parameter value to ARM template parameters name-value pair format during JSON serialization.
    /// It is not intended to be used for other purpose.
    /// "parameters": {
    ///               "Name": {
    ///                          "value": "Value1"
    ///                       },
    ///              }
    /// https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/parameter-files
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ParameterValue<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ParameterValue{T}"/> class.
        /// </summary>
        /// <param name="value">parameter value</param>
        public ParameterValue(T value)
        {
            this.Value = value;
        }

        /// <summary>
        /// Get or set value
        /// </summary>
        public T Value { get; private set; }
    }
}