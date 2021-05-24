namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Provides a JSON converter that can handle the Serialization/deserialization of
    /// <see cref="IDictionary{String, IConvertible}"/> with the addition of IConvertible being of Implementation type
    /// JunoParameter
    /// </summary>
    public class JunoParameterDictionaryJsonConverter : ParameterDictionaryJsonConverter
    {
        /// <summary>
        /// Reads the JSON text from the reader and converts it into an <see cref="IDictionary{String, IConvertible}"/>
        /// object instance
        /// </summary>
        /// <param name="reader">Contains the JSON text defining the <see cref="IDictionary{String, IConvertible}"/> object.</param>
        /// <param name="objectType">The type of object (in practice this will only be an <see cref="IDictionary{String, IConvertible}"/> type).</param>
        /// <param name="existingValue">Unused.</param>
        /// <param name="serializer">Unused.</param>
        /// <returns>
        /// A deserialized <see cref="IDictionary{String, IConvertible}"/> object converted from JSON text.
        /// </returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            reader.ThrowIfNull(nameof(reader));
            serializer.ThrowIfNull(nameof(serializer));
            IDictionary<string, IConvertible> dictionary = new Dictionary<string, IConvertible>();
            if (reader.TokenType == JsonToken.StartObject)
            {
                JObject providerJsonObject = JObject.Load(reader);
                JunoParameterDictionaryJsonConverter.ReadDictionaryEntries(providerJsonObject, dictionary, serializer);
            }

            return dictionary;
        }

        private static void ReadDictionaryEntries(JToken providerJsonObject, IDictionary<string, IConvertible> dictionary, JsonSerializer serializer)
        {
            IEnumerable<JToken> children = providerJsonObject.Children();
            if (children.Any())
            {
                foreach (JToken child in children)
                {
                    if (child.Type == JTokenType.Property)
                    {
                        if (child.First != null)
                        {
                            IConvertible settingValue = null;
                            JValue propertyValue = child.First as JValue;
                            if (propertyValue != null)
                            {
                                // Primitive type IConvertible
                                settingValue = propertyValue.Value as IConvertible;
                            }
                            else
                            {
                                // JunoParameter object IConvertible
                                using (JsonReader reader = child.First.CreateReader())
                                {
                                    settingValue = serializer.Deserialize(reader, typeof(JunoParameter)) as IConvertible;
                                }
                            }

                            // JSON properties that have periods (.) in them will have a path representation
                            // like this:  ['this.is.a.path'].  We have to account for that when adding the key
                            // to the dictionary. The key we want to add is 'this.is.a.path'
                            string key = child.Path;
                            if (key.IndexOf(".", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // ['this.is.a.path'] -> this.is.a.path
                                key = child.Path.Trim('[', '\'', ']');
                            }

                            dictionary.Add(key, settingValue);
                        }
                    }
                }
            }
        }
    }
}
