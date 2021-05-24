namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Json Converter for JunoParameter
    /// </summary>
    public class JunoParameterJsonConverter : JsonConverter
    {
        /// <inheritdoc/>
        public override bool CanConvert(Type objectType)
        {
            objectType.ThrowIfNull(nameof(objectType));
            return objectType == typeof(JunoParameter);
        }

        /// <inheritdoc/>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            reader.ThrowIfNull(nameof(reader));
            objectType.ThrowIfNull(nameof(objectType));
            serializer.ThrowIfNull(nameof(serializer));

            JObject providerJson = JObject.Load(reader);
            string parameterType = JunoParameterJsonConverter.GetTokenFromPropertyName(serializer, providerJson, nameof(JunoParameter.ParameterType), typeof(string)) as string;
            Type definitionType = JunoParameterJsonConverter.GetDefinitionType(parameterType);
            if (definitionType == null)
            {
                throw new JsonSerializationException($"Cannot serialize {nameof(JunoParameter)}. Cannot retrieve type provided by property {nameof(JunoParameter.ParameterType)}");
            }

            object definition = JunoParameterJsonConverter.GetTokenFromPropertyName(serializer, providerJson, nameof(JunoParameter.Definition), definitionType); 
           
            return new JunoParameter(parameterType, definition);
        }

        /// <inheritdoc/>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.ThrowIfNull(nameof(writer));
            value.ThrowIfNull(nameof(value));
            serializer.ThrowIfNull(nameof(serializer));

            writer.Formatting = Formatting.Indented;

            JunoParameter junoParameter = value as JunoParameter;

            // Validate the ParameterType so that we dont serialize something we can not deserialize again.
            Type definitionType = JunoParameterJsonConverter.GetDefinitionType(junoParameter.ParameterType);
            if (definitionType == null)
            {
                throw new JsonSerializationException($"Cannot serialize {nameof(JunoParameter)}. {nameof(JunoParameter.ParameterType)}: {junoParameter.ParameterType} is invalid");
            }

            writer.WriteStartObject();

            // Write property ParameterType
            JsonPropertyAttribute parameterJsonAttr = JunoParameterJsonConverter.GetJsonPropertyAttribute(nameof(JunoParameter.ParameterType));
            writer.WritePropertyName(parameterJsonAttr.PropertyName);
            writer.WriteValue(junoParameter.ParameterType);

            // Write property Definition
            JsonPropertyAttribute definitionJsonAttr = JunoParameterJsonConverter.GetJsonPropertyAttribute(nameof(JunoParameter.Definition));
            writer.WritePropertyName(definitionJsonAttr.PropertyName);
            serializer.Serialize(writer, junoParameter.Definition, definitionType);

            writer.WriteEndObject();
        }

        private static Type GetDefinitionType(string parameterType)
        {
            Type definitionType = Type.GetType(parameterType, throwOnError: false);
            if (definitionType == null)
            {
                definitionType = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => assembly.GetType(parameterType, throwOnError: false) != null)
                    ?.GetType(parameterType);
            }

            return definitionType;
        }

        private static JsonPropertyAttribute GetJsonPropertyAttribute(string propertyName)
        {
            Type objectType = typeof(JunoParameter);
            PropertyInfo propertyTypeInfo = objectType.GetProperty(propertyName);
            return propertyTypeInfo.GetCustomAttribute<JsonPropertyAttribute>();
        }

        private static object GetTokenFromPropertyName(JsonSerializer serializer, JObject providerJson, string propertyName, Type finalType)
        {
            JsonPropertyAttribute propertyJsonInfo = JunoParameterJsonConverter.GetJsonPropertyAttribute(propertyName);
            JToken jsonValue = providerJson.GetValue(propertyJsonInfo.PropertyName, StringComparison.OrdinalIgnoreCase);

            object result;
            using (JsonReader reader = jsonValue.CreateReader())
            {
                result = serializer.Deserialize(reader, finalType);
            }

            return result;
        }
    }
}
