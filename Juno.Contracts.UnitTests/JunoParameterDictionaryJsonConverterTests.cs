namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class JunoParameterDictionaryJsonConverterTests
    {
        private IConvertible[] convertibleObjects;

        [SetUp]
        public void InitializeTest()
        {
            this.convertibleObjects = new IConvertible[]
            {
                true,
                (byte)123,
                'z',

                // Important:
                // The trailing digit on the date/time must not be a zero.  The JSON serializer
                // will truncate a trailing zero (ex:  .5167850 -> .516785)
                DateTime.Parse("2017-10-03T11:04:36.5167851-07:00"),
                (double)11.123,
                short.MaxValue,
                int.MaxValue,
                long.MaxValue,
                sbyte.MaxValue,
                10.123M,
                12.123F,
                "AnyOldString",
                ushort.MaxValue,
                uint.MaxValue,
                ulong.MaxValue,
                new JunoParameter(typeof(string).FullName, "AnotherOldString")
            };
        }

        [Test]
        public void ParameterDictionaryJsonConverterHandlesSerializationOfDataTypesThatImplementIConvertible()
        {
            IDictionary<string, IConvertible> dictionary = new Dictionary<string, IConvertible>();
            Dictionary<string, IConvertible> dictionary2 = new Dictionary<string, IConvertible>();
            foreach (IConvertible convertibleValue in this.convertibleObjects)
            {
                dictionary.Add(convertibleValue.GetType().Name, convertibleValue);
                dictionary2.Add(convertibleValue.GetType().Name, convertibleValue);
            }

            SerializationAssert.IsJsonSerializable(new MockSerializableObject1(dictionary));
            SerializationAssert.IsJsonSerializable(new MockSerializableObject2(dictionary2));
        }

        [Test]
        public void ParameterDictionaryJsonConverterHandlesKeysThatHaveSpecialCharactersInThem()
        {
            IDictionary<string, IConvertible> parameters = new Dictionary<string, IConvertible>
            {
                ["periods.in.the.key"] = "anything",
                ["underscores_in_the_key"] = "anything",
                ["dashes-in-the-key"] = "anything",
                ["numbers123inkey"] = "anything",
                ["numbers.123.in.key.with.dots"] = "anything"
            };

            MockSerializableObject1 originalObject = new MockSerializableObject1(parameters);
            string serializedObject = originalObject.ToJson();

            MockSerializableObject1 deserializedObject = serializedObject.FromJson<MockSerializableObject1>();

            foreach (KeyValuePair<string, IConvertible> entry in deserializedObject.Metadata)
            {
                Assert.IsTrue(parameters.ContainsKey(entry.Key));
            }
        }

        [Test]
        public void ParameterDictionaryJsonConverterHandlesNullValues()
        {
            IDictionary<string, IConvertible> dictionary = new Dictionary<string, IConvertible>
            {
                ["AnyKey"] = null
            };

            SerializationAssert.IsJsonSerializable(new MockSerializableObject1(dictionary));
        }

        [Test]
        public void ParameterDictionaryJsonConverterDoesNotChangeTheDataTypeDuringDeserializationToAnUnexpectedType()
        {
            IDictionary<string, IConvertible> parameters = new Dictionary<string, IConvertible>
            {
                // The letter at the beginning of the key is to ensure unique keys in
                // the dictionary. It is stripped before comparison later.
                ["A" + typeof(string).FullName] = "any string",

                // Numeric data types
                ["B" + typeof(long).FullName] = (sbyte)100,
                ["C" + typeof(long).FullName] = (byte)255,
                ["D" + typeof(long).FullName] = (ushort)1000,
                ["E" + typeof(long).FullName] = (int)5000,
                ["F" + typeof(long).FullName] = 8000U,
                ["G" + typeof(long).FullName] = 1000000UL,
                ["H" + typeof(long).FullName] = 100000000L,
                ["I" + typeof(double).FullName] = 1234.56F,
                ["J" + typeof(double).FullName] = 12345.78D,
                ["K" + typeof(double).FullName] = 123456.910M,

                // Date types
                ["L" + typeof(DateTime).FullName] = DateTime.UtcNow,

                // Boolean types
                ["M" + typeof(bool).FullName] = true
            };

            MockSerializableObject1 originalObject = new MockSerializableObject1(parameters);
            string serializedObject = originalObject.ToJson();

            MockSerializableObject1 deserializedObject = serializedObject.FromJson<MockSerializableObject1>();

            foreach (KeyValuePair<string, IConvertible> entry in deserializedObject.Metadata)
            {
                Type expectedType = Type.GetType(entry.Key.Substring(1));
                Type actualType = entry.Value.GetType();

                Assert.AreEqual(expectedType, actualType);
            }
        }

        private class MockSerializableObject1
        {
            public MockSerializableObject1(IDictionary<string, IConvertible> metadata)
            {
                this.Metadata = metadata;
            }

            /// <summary>
            /// Gets the set of metadata associated with the dicovery job.
            /// </summary>
            [JsonProperty]
            [JsonConverter(typeof(JunoParameterDictionaryJsonConverter))]
            public IDictionary<string, IConvertible> Metadata { get; }
        }

        private class MockSerializableObject2
        {
            public MockSerializableObject2(Dictionary<string, IConvertible> metadata)
            {
                this.Metadata = metadata;
            }

            /// <summary>
            /// Gets the set of metadata associated with the dicovery job.
            /// </summary>
            [JsonProperty]
            [JsonConverter(typeof(JunoParameterDictionaryJsonConverter))]
            public Dictionary<string, IConvertible> Metadata { get; }
        }
    }
}
