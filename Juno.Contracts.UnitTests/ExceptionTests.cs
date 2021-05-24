namespace Juno.Contracts
{
    using System;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExceptionTests
    {
        [Test]
        public void ExperimentExceptionClassIsImplementedCorrectly()
        {
            ExceptionAssert.IsImplementedCorrectly<ExperimentException>();
        }

        [Test]
        public void ExperimentExceptionClassIsJsonSerializableWithCallStack()
        {
            ExperimentException expectedError = new ExperimentException("An error occurred");

            try
            {
                throw expectedError;
            }
            catch (ExperimentException exc)
            {
                ExperimentException serializedError = exc.ToJson().FromJson<ExperimentException>();

                Assert.IsNotNull(serializedError);
                Assert.AreEqual(expectedError.Message, serializedError.Message);
            }
        }

        [Test]
        public void ExperimentExceptionClassIsJsonSerializableWithoutCallStack()
        {
            ExperimentException expectedError = new ExperimentException("An error occurred");
            ExperimentException serializedError = expectedError.ToJson().FromJson<ExperimentException>();

            Assert.IsNotNull(serializedError);
            Assert.AreEqual(expectedError.Message, serializedError.Message);
        }

        [Test]
        public void ExecutionExceptionClassIsImplementedCorrectly()
        {
            ExceptionAssert.IsImplementedCorrectly<ExecutionException>();
        }

        [Test]
        public void ExecutionExceptionClassIsJsonSerializableWithCallStack()
        {
            ExecutionException expectedError = new ExecutionException("An error occurred");

            try
            {
                throw expectedError;
            }
            catch (ExecutionException exc)
            {
                ExecutionException serializedError = exc.ToJson().FromJson<ExecutionException>();

                Assert.IsNotNull(serializedError);
                Assert.AreEqual(expectedError.Message, serializedError.Message);
            }
        }

        [Test]
        public void ExecutionExceptionClassIsJsonSerializableWithoutCallStack()
        {
            ExecutionException expectedError = new ExecutionException("An error occurred");
            ExecutionException serializedError = expectedError.ToJson().FromJson<ExecutionException>();

            Assert.IsNotNull(serializedError);
            Assert.AreEqual(expectedError.Message, serializedError.Message);
        }

        [Test]
        public void ProviderExceptionClassIsImplementedCorrectly()
        {
            ExceptionAssert.IsImplementedCorrectly<ProviderException>();
        }

        [Test]
        public void ProviderExceptionClassIsJsonSerializableWithCallStack()
        {
            ProviderException expectedError = new ProviderException("An error occurred", ErrorReason.ProviderDefinitionInvalid);

            try
            {
                throw expectedError;
            }
            catch (ProviderException exc)
            {
                ProviderException serializedError = exc.ToJson().FromJson<ProviderException>();

                Assert.IsNotNull(serializedError);
                Assert.AreEqual(expectedError.Message, serializedError.Message);
                Assert.AreEqual(expectedError.Reason, serializedError.Reason);
            }
        }

        [Test]
        public void ProviderExceptionClassIsJsonSerializableWithoutCallStack()
        {
            ProviderException expectedError = new ProviderException("An error occurred", ErrorReason.ProviderDefinitionInvalid);
            ProviderException serializedError = expectedError.ToJson().FromJson<ProviderException>();

            Assert.IsNotNull(serializedError);
            Assert.AreEqual(expectedError.Message, serializedError.Message);
            Assert.AreEqual(expectedError.Reason, serializedError.Reason);
        }
    }
}
