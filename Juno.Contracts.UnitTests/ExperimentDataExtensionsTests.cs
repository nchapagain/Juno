namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AutoFixture;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentDataExtensionsTests
    {
        private Fixture mockFixture;
        private List<ExperimentStepInstance> mockSteps;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();

            this.mockSteps = new List<ExperimentStepInstance>
            {
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>()
            };

            this.mockSteps.SetSequences();
        }

        [Test]
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.Failed)]
        [TestCase(ExecutionStatus.InProgress)]
        [TestCase(ExecutionStatus.InProgressContinue)]
        [TestCase(ExecutionStatus.Pending)]
        public void AllPreviousStepsSucceededExtensionReturnsTheExpectedResultWhenOneOrMoreStepsAreNotSucceeded(ExecutionStatus status)
        {
            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.Succeeded);
            this.mockSteps.First().Status = status;

            Assert.IsFalse(this.mockSteps.AllPreviousStepsSucceeded(this.mockSteps.Last()));
        }

        [Test]
        public void AllPreviousStepsSucceededExtensionReturnsTheExpectedResultWhenAllPreviousStepsAreSucceeded()
        {
            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.Succeeded);

            Assert.IsTrue(this.mockSteps.AllPreviousStepsSucceeded(this.mockSteps.Last()));
        }

        [Test]
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.Failed)]
        [TestCase(ExecutionStatus.InProgress)]
        [TestCase(ExecutionStatus.InProgressContinue)]
        [TestCase(ExecutionStatus.Pending)]
        [TestCase(ExecutionStatus.Succeeded)]
        public void AnyPreviousStepsInStatusExtensionReturnsTheExpectedResultWhenOneOrMoreStepsAreInTheStatusSupplied(ExecutionStatus status)
        {
            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.Succeeded);
            this.mockSteps.First().Status = status;

            Assert.IsTrue(this.mockSteps.AnyPreviousStepsInStatus(this.mockSteps.Last(), status));
        }

        [Test]
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.InProgress)]
        [TestCase(ExecutionStatus.InProgressContinue)]
        [TestCase(ExecutionStatus.Pending)]
        [TestCase(ExecutionStatus.Succeeded)]
        public void AnyPreviousStepsInStatusExtensionReturnsTheExpectedResultWhenThereAreNotAnyPreviousStepsInTheStatusSupplied(ExecutionStatus status)
        {
            this.mockSteps.ForEach(step => step.Status = status);

            Assert.IsFalse(this.mockSteps.AnyPreviousStepsInStatus(this.mockSteps.Last(), ExecutionStatus.Failed));
        }

        [Test]
        public void GetErrorExtensionGetsTheExpectedErrorFromTheExperimentStep()
        {
            ExperimentStepInstance step = this.mockSteps.First();
            ProviderException expectedError = new ProviderException("Any error", ErrorReason.ProviderDefinitionInvalid);

            step.Extensions.Add("error", JToken.FromObject(expectedError));
            ExperimentException actualError = step.GetError() as ExperimentException;

            Assert.IsNotNull(actualError);
            Assert.AreEqual(expectedError.Message, actualError.Message);
            Assert.AreEqual(expectedError.Reason, actualError.Reason);
        }

        [Test]
        public void GetErrorExtensionGetsTheExpectedErrorFromTheExperimentStep_IncludingInnerExceptions()
        {
            ExperimentStepInstance step = this.mockSteps.First();
            InvalidCastException innerException = new InvalidCastException("Any other error");
            ProviderException expectedError = new ProviderException("Any error", ErrorReason.ProviderDefinitionInvalid, innerException);

            step.Extensions.Add("error", JToken.FromObject(expectedError));
            ExperimentException actualError = step.GetError() as ExperimentException;

            Assert.IsNotNull(actualError);
            Assert.IsNotNull(actualError.InnerException);
            Assert.AreEqual(expectedError.Message, actualError.Message);
            Assert.AreEqual(innerException.Message, actualError.InnerException.Message);
            Assert.AreEqual(expectedError.Reason, actualError.Reason);
        }

        [Test]
        public void SetErrorExtensionAddsTheErrorToTheExperimentStepAsExpected()
        {
            ExperimentStepInstance step = this.mockSteps.First();
            ProviderException expectedError = new ProviderException("Any error", ErrorReason.ProviderDefinitionInvalid);

            step.SetError(expectedError);
            Assert.IsTrue(step.Extensions.ContainsKey("error"));

            ExperimentException actualError = step.Extensions["error"].ToObject<ExperimentException>();

            Assert.IsNotNull(actualError);
            Assert.AreEqual(expectedError.Message, actualError.Message);
            Assert.AreEqual(expectedError.Reason, actualError.Reason);
        }

        [Test]
        public void SetErrorExtensionAddsTheErrorToTheExperimentStepAsExpected_WithInnerException()
        {
            ExperimentStepInstance step = this.mockSteps.First();
            InvalidCastException innerException = new InvalidCastException("Any other error");
            ProviderException expectedError = new ProviderException("Any error", ErrorReason.ProviderDefinitionInvalid, innerException);

            step.SetError(expectedError);
            Assert.IsTrue(step.Extensions.ContainsKey("error"));

            ExperimentException actualError = step.Extensions["error"].ToObject<ExperimentException>();

            Assert.IsNotNull(actualError);
            Assert.IsNotNull(actualError.InnerException);
            Assert.AreEqual(expectedError.Message, actualError.Message);
            Assert.AreEqual(innerException.Message, actualError.InnerException.Message);
            Assert.AreEqual(expectedError.Reason, actualError.Reason);
        }

        [Test]
        public void ContainsTerminalStepsExtensionReturnsTrueIfAnyStepsAreInAnExpectedTerminalStatus()
        {
            // All failed
            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.Failed);
            Assert.IsTrue(this.mockSteps.ContainsTerminalSteps());

            // All cancelled
            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.Cancelled);
            Assert.IsTrue(this.mockSteps.ContainsTerminalSteps());

            // Some failed
            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.Succeeded);
            this.mockSteps.Last().Status = ExecutionStatus.Failed;
            Assert.IsTrue(this.mockSteps.ContainsTerminalSteps());

            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.InProgress);
            this.mockSteps.Last().Status = ExecutionStatus.Failed;
            Assert.IsTrue(this.mockSteps.ContainsTerminalSteps());

            // Some cancelled
            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.Succeeded);
            this.mockSteps.Last().Status = ExecutionStatus.Cancelled;
            Assert.IsTrue(this.mockSteps.ContainsTerminalSteps());

            this.mockSteps.ForEach(step => step.Status = ExecutionStatus.InProgress);
            this.mockSteps.Last().Status = ExecutionStatus.Cancelled;
            Assert.IsTrue(this.mockSteps.ContainsTerminalSteps());
        }

        [Test]
        public void SetSequencesExtensionSetsStepSequencesToExpectedDefaultValues()
        {
            List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>
            {
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>()
            };

            steps.SetSequences();

            int expectedIncrement = 100;
            int currentSequence = 0;
            steps.ForEach(step =>
            {
                Assert.AreEqual(currentSequence += expectedIncrement, step.Sequence);
            });
        }

        [Test]
        public void SetSequencesExtensionSetsStepSequencesToExpectedValuesGivenAnExplicitIncrement()
        {
            List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>
            {
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>()
            };

            int expectedIncrement = 1000;
            steps.SetSequences(expectedIncrement);

            int currentSequence = 0;
            steps.ForEach(step =>
            {
                Assert.AreEqual(currentSequence += expectedIncrement, step.Sequence);
            });
        }
    }
}
