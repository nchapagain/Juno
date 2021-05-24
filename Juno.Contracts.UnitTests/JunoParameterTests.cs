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
    public class JunoParameterTests
    {
        [Test]
        public void JunoParameterDeserializesAndSerializesWithoutInformationLoss()
        {
            TestJunoParameter originalContent = new TestJunoParameter();
            JunoParameter original = new JunoParameter(typeof(TestJunoParameter).FullName, originalContent);
            string originalString = JsonConvert.SerializeObject(original);
            JunoParameter translated = JsonConvert.DeserializeObject<JunoParameter>(originalString);
            TestJunoParameter translatedContent = translated.Definition as TestJunoParameter;

            Assert.AreEqual(original, translated);
            Assert.AreEqual(originalContent.GetHashCode(), translatedContent.GetHashCode());
        }

        [Test]
        public void JunoParameterIsJsonSerializable()
        {
            TestJunoParameter content = new TestJunoParameter();
            JunoParameter component = new JunoParameter(typeof(TestJunoParameter).FullName, content);
            SerializationAssert.IsJsonSerializable(component);
        }

        [Test]
        public void JunoParameterIsJsonSerializableUsingExpectedSerializerSettings()
        {
            TestJunoParameter content = new TestJunoParameter();
            JunoParameter component = new JunoParameter(typeof(TestJunoParameter).FullName, content);
            SerializationAssert.IsJsonSerializable(component, ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void JunoParameterValidatesParametersOnConstruction()
        {
            TestJunoParameter content = new TestJunoParameter();
            JunoParameter component = new JunoParameter(typeof(TestJunoParameter).FullName, content);
            Assert.Throws<ArgumentException>(() => new JunoParameter(null, component.Definition));
            Assert.Throws<ArgumentException>(() => new JunoParameter(component.ParameterType, null));
        }

        [Test]
        public void JunoParameterCorrectlyImplementsEqualitySemantics()
        {
            TestJunoParameter content = new TestJunoParameter();
            JunoParameter component1 = new JunoParameter(typeof(TestJunoParameter).FullName, content);
            JunoParameter component2 = new JunoParameter("differentType", content);

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => component1, () => component2);
        }

        internal class TestJunoParameter
        {
            [JsonConstructor]
            public TestJunoParameter(string fieldOne, int fieldTwo, IList<string> fieldThree, IDictionary<string, IConvertible> parameters)
            {
                this.FieldOne = fieldOne;
                this.FieldTwo = fieldTwo;
                this.FieldThree = fieldThree;
                this.Parameters = parameters;
            }

            public TestJunoParameter()
            {
                this.FieldOne = "FieldOne";
                this.FieldTwo = 10;
                this.FieldThree = new List<string>() { "FieldThree.1", "FieldThree.2", "FieldThree.3" };
                this.Parameters = new Dictionary<string, IConvertible>()
                {
                    ["parameterone"] = 111,
                    ["parametertwo"] = "foobar",
                    ["nestedJunoParameter"] = new JunoParameter(
                        typeof(TestJunoParameter).FullName,
                        new TestJunoParameter("I am nested", 11, new List<string>() { "once you", "have serialized me", "you have succeeded" }, new Dictionary<string, IConvertible>()))
                };
            }

            [JsonProperty(PropertyName = "fieldOne", Required = Required.Always)]
            public string FieldOne { get; }

            [JsonProperty(PropertyName = "fieldTwo", Required = Required.Always)]
            public int FieldTwo { get; }

            [JsonProperty(PropertyName = "fieldThree", Required = Required.Always)]
            public IList<string> FieldThree { get; }

            [JsonConverter(typeof(JunoParameterDictionaryJsonConverter))]
            [JsonProperty(PropertyName = "parameters", Required = Required.Always)]
            public IDictionary<string, IConvertible> Parameters { get; }

            public override int GetHashCode()
            {
                return new StringBuilder().AppendProperties(this.FieldOne, this.FieldTwo, this.FieldThree.ToString())
                    .AppendParameters(this.Parameters).ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
