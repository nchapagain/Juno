namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Class to help wrap dictionary parameters into a json parameters for <see cref="ExperimentTemplate.Override"/> 
    /// and for the parameters in a <see cref="GoalBasedSchedule"/>
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptIn)]
    public class TemplateOverride
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateOverride"/> class.
        /// </summary>
        [JsonConstructor]
        public TemplateOverride(IDictionary<string, IConvertible> parameters)
        {
            parameters.ThrowIfNull(nameof(parameters));
            this.Parameters = parameters;
        }

        /// <summary>
        /// Gets the dictonary with experiment parameters
        /// </summary>
        [JsonProperty(PropertyName = "parameters", Required = Required.Always)]
        [JsonConverter(typeof(ParameterDictionaryJsonConverter))]
        public IDictionary<string, IConvertible> Parameters { get; }
    }
}
