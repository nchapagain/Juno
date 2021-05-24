namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.CRC;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class JunoParameterJsonConverterTests
    {
        private JsonSerializerSettings jsonSettings;

        [SetUp]
        public void SetupTests()
        { 
            this.jsonSettings = new JsonSerializerSettings()
            { 
                Converters = new List<JsonConverter>() { new JunoParameterJsonConverter() }
            };
        }

        [Test]
        public void JunoParameterJsonConveterSerializesSimpleJunoParamterWithoutDataLoss()
        {
            JunoParameter entity = new JunoParameter(typeof(string).FullName, "test");
            SerializationAssert.IsJsonSerializable(entity, this.jsonSettings);
        }

        [Test]
        public void JunoParameterJsonConverterSerializesSimpleObjectJunoParameterWithoutDataLoss()
        {
            JunoParameter entity = new JunoParameter(typeof(TestObject1).FullName, new TestObject1("test"));
            SerializationAssert.IsJsonSerializable(entity, this.jsonSettings);
        }

        [Test]
        public void JunoParameterJsonConveterSerializesNestedJunoParameterWithoutDataLoss()
        { 
            JunoParameter nestedEntity = new JunoParameter(typeof(TestObject1).FullName, new TestObject1("test"));
            JunoParameter entity = new JunoParameter(typeof(TestObject2).FullName, new TestObject2("test2", nestedEntity));

            SerializationAssert.IsJsonSerializable(entity, this.jsonSettings);
        }

        [Test]
        public void JunoParameterJsonConverterThrowsErrorWhenWritingUnexpectedType()
        {
            JunoParameter invalidComponent = new JunoParameter("this better not be a type ever", "something");
            Assert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(invalidComponent));
        }

        [Test]
        public void JunoParameterJsonConverterThrowsErrorWhenReadingUnexpectedType()
        {
            string invalidString = "{\r\n  \"parameterType\": \"Juno.Contracts.JunoParameterJsonConverterTests+TestObject3\",\r\n  \"defintion\": {\r\n    \"fieldOne\": \"test\"\r\n  }\r\n}";
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<JunoParameter>(invalidString));
        }

        private class TestObject1
        { 
            [JsonConstructor]
            public TestObject1(string fieldOne)
            {
                this.FieldOne = fieldOne;
            }

            [JsonProperty(PropertyName = "fieldOne")]
            public string FieldOne { get; }
        }

        private class TestObject2
        {
            [JsonConstructor]
            public TestObject2(string fieldOne, JunoParameter fieldTwo)
            {
                this.FieldOne = fieldOne;
                this.FieldTwo = fieldTwo;
            }

            [JsonProperty(PropertyName = "fieldOne")]
            public string FieldOne { get; }

            [JsonProperty(PropertyName = "fieldTwo")]
            public JunoParameter FieldTwo { get; }
        }
    }
}
