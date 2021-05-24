namespace Juno.Contracts
{
    using System;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentInstanceStatusTests
    {
        private string experimentId;
        private string experimentName;
        private ExperimentStatus experimentStatus;
        private string environment;
        private string executionGoal;
        private string targetGoal;
        private ImpactType impactType;
        private DateTime experimentStartTime;
        private DateTime lastIngestionTime;

        private ExperimentInstanceStatus experimentInstanceStatus;

        [SetUp]
        public void SetupTest()
        {
            this.experimentId = Guid.NewGuid().ToString();
            this.experimentName = "Mock ExperimentName";
            this.experimentStatus = ExperimentStatus.InProgress;
            this.environment = "juno-prod01";
            this.executionGoal = "mock Execution Goal Name";
            this.targetGoal = "mock Target goal Name";
            this.impactType = ImpactType.Impactful;
            this.experimentStartTime = DateTime.Now;
            this.lastIngestionTime = DateTime.Now;

            this.experimentInstanceStatus = new ExperimentInstanceStatus(
                this.experimentId,
                this.experimentName,
                this.experimentStatus,
                this.environment,
                this.executionGoal,
                this.targetGoal,
                this.impactType,
                this.experimentStartTime,
                this.lastIngestionTime);
        }

        [Test]
        public void ExperimentInstanceStatusIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.experimentInstanceStatus);
        }

        [Test]
        public void ExperimentInstanceStatusIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.experimentInstanceStatus,
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void ExperimentInstanceStatusConstructorValidatesRequiredStringParameters(string invalidParameter)
        {
            Assert.Throws<ArgumentException>(() => new ExperimentInstanceStatus(
                invalidParameter,
                this.experimentName,
                this.experimentStatus,
                this.environment,
                this.executionGoal,
                this.targetGoal,
                this.impactType,
                this.experimentStartTime,
                this.lastIngestionTime));

            Assert.Throws<ArgumentException>(() => new ExperimentInstanceStatus(
                this.experimentId,
                invalidParameter,
                this.experimentStatus,
                this.environment,
                this.executionGoal,
                this.targetGoal,
                this.impactType,
                this.experimentStartTime,
                this.lastIngestionTime));

            Assert.Throws<ArgumentException>(() => new ExperimentInstanceStatus(
                this.experimentId,
                this.experimentName,
                this.experimentStatus,
                invalidParameter,
                this.executionGoal,
                this.targetGoal,
                this.impactType,
                this.experimentStartTime,
                this.lastIngestionTime));

            Assert.Throws<ArgumentException>(() => new ExperimentInstanceStatus(
                this.experimentId,
                this.experimentName,
                this.experimentStatus,
                this.environment,
                invalidParameter,
                this.targetGoal,
                this.impactType,
                this.experimentStartTime,
                this.lastIngestionTime));

            Assert.Throws<ArgumentException>(() => new ExperimentInstanceStatus(
                this.experimentId,
                this.experimentName,
                this.experimentStatus,
                this.environment,
                this.executionGoal,
                invalidParameter,
                this.impactType,
                this.experimentStartTime,
                this.lastIngestionTime));
        }

        [Test]
        public void ExperimentInstanceStatusCorrectlyImplementsGetHashcode()
        {
            ExperimentInstanceStatus instance1 = new ExperimentInstanceStatus(
                this.experimentId,
                this.experimentName,
                this.experimentStatus,
                this.environment,
                this.executionGoal,
                this.targetGoal,
                this.impactType,
                this.experimentStartTime,
                this.lastIngestionTime);

            ExperimentInstanceStatus instance2 = new ExperimentInstanceStatus(
                this.experimentId,
                this.experimentName,
                this.experimentStatus,
                this.environment,
                this.executionGoal,
                this.targetGoal,
                this.impactType,
                DateTime.Now.AddSeconds(1),
                DateTime.Now.AddSeconds(1));
            
            Assert.AreEqual(instance1.GetHashCode(), this.experimentInstanceStatus.GetHashCode());
            Assert.AreNotEqual(instance1.GetHashCode(), instance2.GetHashCode());
            Assert.IsTrue(instance1.Equals(this.experimentInstanceStatus));
            Assert.IsFalse(instance1.Equals(instance2));
        }
    }
}
