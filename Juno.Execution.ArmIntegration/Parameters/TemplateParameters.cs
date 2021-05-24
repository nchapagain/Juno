namespace Juno.Execution.ArmIntegration.Parameters
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Encapsulate base template parameters
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class TemplateParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateParameters"/> class.
        /// </summary>
        /// <param name="location">Location aka. region</param>
        public TemplateParameters(
            ParameterValue<string> location)
        {
            this.Location = location;
        }

        /// <summary>
        /// Get location
        /// </summary>
        public ParameterValue<string> Location { get; private set; }
    }
}
