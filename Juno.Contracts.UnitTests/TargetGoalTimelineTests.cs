namespace Juno.Contracts
{
    using System;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class TargetGoalTimelineTests
    {
        private string experimentName;
        private string environment;
        private string executionGoal;
        private string targetGoal;
        private int targetExperimentInstances;
        private int successfulExperimentInstances;
        private DateTime estimatedTimeOfCompletion;
        private DateTime lastModifiedTime;
        private string teamName;
        private ExperimentStatus status;

        private TargetGoalTimeline targetGoalTimeline;

        [SetUp]
        public void SetupTest()
        {
            this.targetGoal = "mock Target goal Name";
            this.executionGoal = "mock Execution Goal Name";
            this.experimentName = "Mock ExperimentName";
            this.environment = "juno-mock01";
            this.teamName = "mock teamName";
            this.status = ExperimentStatus.InProgress;
            this.targetExperimentInstances = 100;
            this.successfulExperimentInstances = 99;
            this.estimatedTimeOfCompletion = DateTime.Now;
            this.lastModifiedTime = DateTime.Now;

            this.targetGoalTimeline = new TargetGoalTimeline(
                this.targetGoal,
                this.executionGoal,
                this.experimentName,
                this.environment,
                this.teamName,
                this.targetExperimentInstances,
                this.successfulExperimentInstances,
                this.estimatedTimeOfCompletion,
                this.status,
                this.lastModifiedTime);
        }

        [Test]
        public void TargetGoalTimelineIsJsonSerializableByDefault()
        {
            SerializationAssert.IsJsonSerializable(this.targetGoalTimeline);
        }

        [Test]
        public void TargetGoalTimelineIsJsonSerializableUsingExpectedSerializerSettings()
        {
            SerializationAssert.IsJsonSerializable(
                this.targetGoalTimeline,
                ContractSerialization.DefaultJsonSerializationSettings);
        }

        [Test]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void TargetGoalTimelineConstructorValidatesRequiredStringParameters(string invalidParameter)
        {
            Assert.Throws<ArgumentException>(() => new TargetGoalTimeline(
                invalidParameter,
                this.executionGoal,
                this.experimentName,
                this.environment,
                this.teamName,
                this.targetExperimentInstances,
                this.successfulExperimentInstances,
                this.estimatedTimeOfCompletion,
                this.status,
                this.lastModifiedTime));

            Assert.Throws<ArgumentException>(() => new TargetGoalTimeline(
                this.targetGoal,
                invalidParameter,
                this.experimentName,
                this.environment,
                this.teamName,
                this.targetExperimentInstances,
                this.successfulExperimentInstances,
                this.estimatedTimeOfCompletion,
                this.status,
                this.lastModifiedTime));

            Assert.Throws<ArgumentException>(() => new TargetGoalTimeline(
                this.targetGoal,
                this.executionGoal,
                invalidParameter,
                this.environment,
                this.teamName,
                this.targetExperimentInstances,
                this.successfulExperimentInstances,
                this.estimatedTimeOfCompletion,
                this.status,
                this.lastModifiedTime));

            Assert.Throws<ArgumentException>(() => new TargetGoalTimeline(
                this.targetGoal,
                this.executionGoal,
                this.experimentName,
                invalidParameter,
                this.teamName,
                this.targetExperimentInstances,
                this.successfulExperimentInstances,
                this.estimatedTimeOfCompletion,
                this.status,
                this.lastModifiedTime));

            Assert.Throws<ArgumentException>(() => new TargetGoalTimeline(
               this.targetGoal,
               this.executionGoal,
               this.experimentName,               
               this.environment,
               invalidParameter,
               this.targetExperimentInstances,
               this.successfulExperimentInstances,
               this.estimatedTimeOfCompletion,
               this.status,
               this.lastModifiedTime));
        }

        [Test]
        public void TargetGoalTimelineCorrectlyImplementsGetHashcode()
        {
            var instance1 = new TargetGoalTimeline(
                this.targetGoal,
                this.executionGoal,
                this.experimentName,
                this.environment,
                this.teamName,
                this.targetExperimentInstances,
                this.successfulExperimentInstances,
                this.estimatedTimeOfCompletion,
                this.status,
                this.lastModifiedTime);

            var instance2 = new TargetGoalTimeline(
                this.targetGoal,
                this.executionGoal,
                this.experimentName,
                this.environment,
                this.teamName,
                this.targetExperimentInstances,
                this.successfulExperimentInstances,
                DateTime.Now.AddSeconds(10),
                this.status,
                DateTime.Now.AddSeconds(10));

            Assert.AreEqual(instance1.GetHashCode(), this.targetGoalTimeline.GetHashCode());
            Assert.AreNotEqual(instance1.GetHashCode(), instance2.GetHashCode());

            Assert.IsTrue(instance1.Equals(this.targetGoalTimeline));
            Assert.IsFalse(instance1.Equals(instance2));
        }
    }
}