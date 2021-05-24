namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using AutoFixture;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    /// <summary>
    /// This is an abstract class for all runtime contracts to implement
    /// this class offers ready-to-run tests, and also forces base classes
    /// to implement methods that should pass if they are to reach a Juno standard.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [TestFixture]
    [Category("Unit")]
    public abstract class RuntimeContractsTests<T>
        where T : class, IEquatable<T>
    {
        public RuntimeContractsTests()
        {
            this.MockFixture = new RunTimeContractFixture();
            this.MockFixture.SetUpRuntimeContracts();
        }

        public RunTimeContractFixture MockFixture { get; }

        [OneTimeSetUp]
        // Base Classes should override SetupTests and:
        // 1. Register a Specimen Builder of type T
        public virtual void SetupTests()
        {
            try
            {
                T mockInstance = this.MockFixture.Create<T>();
            }
            catch (ObjectCreationException)
            {
                Assert.Fail($"Object of type: {typeof(T).Name} must register a specimen builder with the fixture: {nameof(this.MockFixture)}");
            }
        }

        [Test]
        public void RuntimeContractIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.MockFixture.Create<T>());
        }

        [Test]
        public void RuntimeContractIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.MockFixture.Create<T>(),
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        public void RuntimeContractCorrectlyImplementsHashCodeSemantics()
        {
            T instance1 = this.MockFixture.Create<T>();
            T instance2 = this.MockFixture.Create<T>();

            EqualityAssert.CorrectlyImplementsHashcodeSemantics(() => instance1, () => instance2);
        }

        [Test]
        public void RuntimeContractCorrectlyImplementsEqualitySemantics()
        {
            T instance1 = this.MockFixture.Create<T>();
            T instance2 = this.MockFixture.Create<T>();

            EqualityAssert.CorrectlyImplementsEqualitySemantics(() => instance1, () => instance2);
        }
    }
}
