namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentComponentTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void ExperimentComponentConstructorsValidateRequiredParameters(string invalidParameter)
        {
            ExperimentComponent validComponent = this.mockFixture.Create<ExperimentComponent>();

            Assert.Throws<ArgumentException>(() => new ExperimentComponent(
                invalidParameter,
                validComponent.Name,
                validComponent.Description,
                validComponent.Group,
                validComponent.Parameters,
                validComponent.Tags));

            Assert.Throws<ArgumentException>(() => new ExperimentComponent(
                validComponent.ComponentType,
                invalidParameter,
                validComponent.Description,
                validComponent.Group,
                validComponent.Parameters,
                validComponent.Tags));
        }

        [Test]
        public void ExperimentComponentIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.mockFixture.Create<ExperimentComponent>());
        }

        [Test]
        public void ExperimentComponentIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.mockFixture.Create<ExperimentComponent>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void ExperimentComponentCorrectlyImplementsEqualitySemantics()
        {
            ExperimentComponent instance1 = this.mockFixture.Create<ExperimentComponent>();
            ExperimentComponent instance2 = this.mockFixture.Create<ExperimentComponent>();

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentComponentCorrectlyImplementsEqualitySemanticsWhenExtensionsExist()
        {
            ExperimentComponent instance1 = this.mockFixture.Create<ExperimentComponent>();
            instance1.Extensions.Add("customExtension1", "value");
            instance1.Extensions.Add("customExtension2", 1234);
            instance1.Extensions.Add("customExtension3", 1234.56);
            instance1.Extensions.Add("customExtension4", true);
            instance1.Extensions.Add("customExtension5", DateTime.UtcNow);
            instance1.Extensions.Add("customExtension6", JToken.Parse(new List<string> { "one" }.ToJson()));
            instance1.Extensions.Add("customExtension7", JToken.Parse(new Dictionary<int, string> { [1] = "two" }.ToJson()));

            ExperimentComponent instance2 = this.mockFixture.Create<ExperimentComponent>();
            instance2.Extensions.Add("customExtension1", "otherValue");
            instance2.Extensions.Add("customExtension2", 5678);
            instance2.Extensions.Add("customExtension3", 5678.90);
            instance2.Extensions.Add("customExtension4", false);
            instance2.Extensions.Add("customExtension5", DateTime.UtcNow.AddSeconds(5));
            instance2.Extensions.Add("customExtension6", JToken.Parse(new List<string> { "two" }.ToJson()));
            instance2.Extensions.Add("customExtension7", JToken.Parse(new Dictionary<int, string> { [1] = "three" }.ToJson()));

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentComponentCorrectlyImplementsHashcodeSemantics()
        {
            ExperimentComponent instance1 = this.mockFixture.Create<ExperimentComponent>();
            ExperimentComponent instance2 = this.mockFixture.Create<ExperimentComponent>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void ExperimentComponentHashCodesAreNotCaseSensitive()
        {
            ExperimentComponent template = this.mockFixture.Create<ExperimentComponent>();
            ExperimentComponent instance1 = new ExperimentComponent(
                template.ComponentType.ToLowerInvariant(),
                template.Name.ToLowerInvariant(),
                template.Description.ToLowerInvariant(),
                template.Group.ToLowerInvariant(),
                template.Parameters,
                template.Tags);

            ExperimentComponent instance2 = new ExperimentComponent(
                template.ComponentType.ToUpperInvariant(),
                template.Name.ToUpperInvariant(),
                template.Description.ToUpperInvariant(),
                template.Group.ToUpperInvariant(),
                template.Parameters,
                template.Tags);

            Assert.AreEqual(instance1.GetHashCode(), instance2.GetHashCode());
        }

        [Test]
        public void ExperimentComponentInstancesDoNotLoseDataDuringJsonSerializationAndDeserialization()
        {
            ExperimentComponent originalComponent = this.mockFixture.Create<ExperimentComponent>();

            // The JSON serializer can sometimes lose context of the original data type
            // during serialization/deserialization (e.g. DateTime values). We want to 
            // ensure the custom serialization definitions + attributes used on ExperimentComponent
            // classes handle that for all primitive types at least.
            originalComponent.Parameters.Add("string", "any string");
            originalComponent.Parameters.Add("int", 1234);
            originalComponent.Parameters.Add("float", 1234.56F);
            originalComponent.Parameters.Add("decimal", 1234.56789123M);
            originalComponent.Parameters.Add("datetime", DateTime.UtcNow);
            originalComponent.Parameters.Add("bool", true);

            originalComponent.Tags.Add("string", "any string");
            originalComponent.Tags.Add("int", 1234);
            originalComponent.Tags.Add("float", 1234.56F);
            originalComponent.Tags.Add("decimal", 1234.56789123M);
            originalComponent.Tags.Add("datetime", DateTime.UtcNow);
            originalComponent.Tags.Add("bool", true);

            string serializedComponent = originalComponent.ToJson();
            ExperimentComponent deserializedComponent = serializedComponent.FromJson<ExperimentComponent>();

            Assert.IsTrue(originalComponent.Equals(deserializedComponent));
        }

        [Test]
        public void ExperimentComponentHashCodesHandlePropertiesWithNullValues()
        {
            // The Description, Parameters and Tags properties are all null here.
            ExperimentComponent instance = new ExperimentComponent("Any.Component.Type", "Any Name", null);
            Assert.DoesNotThrow(() => instance.GetHashCode());
        }

        [Test]
        public void ExperimentComponentHandlesSimplePropertyExtensionsToTheSchema()
        {
            string componentWithExtensions = 
                $@"{{
                        'type': '{typeof(ExperimentComponent).FullName}',
                        'name': 'AnyName',
                        'parameters': {{ 
                            'anyParameter': 'anyValue'
                        }},
                        'customProperty1': 112233,
                        'customProperty2': true,
                        'customProperty3': '2019-10-15T13:45:30.1234567Z'
                  }}";

            ExperimentComponent deserializedComponent = componentWithExtensions.FromJson<ExperimentComponent>();

            Assert.IsNotNull(deserializedComponent.Extensions);
            Assert.IsTrue(deserializedComponent.Extensions.ContainsKey("customProperty1"));
            Assert.IsTrue(deserializedComponent.Extensions.ContainsKey("customProperty2"));
            Assert.IsTrue(deserializedComponent.Extensions.ContainsKey("customProperty3"));

            // ...and the entity should serialize/deserialize without data loss.
            string serializedEntity = deserializedComponent.ToJson();
            SerializationAssert.JsonEquals(componentWithExtensions, serializedEntity);
        }

        [Test]
        public void ExperimentComponentHandlesComplexObjectExtensionsToTheSchema()
        {
            string componentWithExtensions =
                $@"{{
                        'type': '{typeof(ExperimentComponent).FullName}',
                        'name': 'AnyName',
                        'parameters': {{ 
                            'anyParameter': 'anyValue'
                        }},
                        'customObject1': {{
                            'otherObject1': {{
                                'customProperty1': 112233,
                                'customProperty2': true,
                                'customProperty3': '2019-10-15T13:45:30.1234567Z'
                            }}
                        }},
                        'customObject2': {{
                            'otherObject2': {{
                                'customProperty1': 223344,
                                'customProperty2': false,
                                'customProperty3': '2019-10-15T20:30:30.9876543Z'
                            }}
                        }},
                        'customObject3': [
                            'larry',
                            'curley',
                            'moe'
                        ]
                  }}";

            ExperimentComponent deserializedComponent = componentWithExtensions.FromJson<ExperimentComponent>();

            Assert.IsNotNull(deserializedComponent.Extensions);
            Assert.IsTrue(deserializedComponent.Extensions.ContainsKey("customObject1"));
            Assert.IsTrue(deserializedComponent.Extensions.ContainsKey("customObject2"));

            // ...and the entity should serialize/deserialize without data loss.
            string serializedEntity = deserializedComponent.ToJson();
            SerializationAssert.JsonEquals(componentWithExtensions, serializedEntity);
        }
    }
}
