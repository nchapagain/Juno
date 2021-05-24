namespace Juno.Contracts
{
    using System;
    using AutoFixture;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class TargetGoalTriggerExtensionsUnitTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
        }

        [Test]
        public void HasOccurenceValidatesNullableParameters()
        {
            TargetGoalTrigger targetGoal = null;
            Assert.Throws<ArgumentException>(() => targetGoal.HasOccurence(DateTime.UtcNow, DateTime.UtcNow));
        }

        [Test]
        public void HasOccurenceReturnsTrueIfTargetGoalHasOccurenceInRangeGiven()
        {
            TargetGoalTrigger targetGoal = TargetGoalTriggerExtensionsUnitTests.CreateTargetGoalTrigger("* * * * *");
            DateTime beginTime = DateTime.UtcNow.AddMinutes(-1);
            DateTime endTime = DateTime.UtcNow.AddMinutes(1);
            bool result = targetGoal.HasOccurence(beginTime, endTime);

            Assert.IsTrue(result);
        }

        [Test]
        public void HasOccurenceReturnsFalseIfTargetGoalDoesNotHaveOccurenceInRangeGiven()
        {
            TargetGoalTrigger targetGoal = TargetGoalTriggerExtensionsUnitTests.CreateTargetGoalTrigger("* * * * *");
            DateTime beginTime = DateTime.UtcNow.AddMinutes(1);
            DateTime endTime = DateTime.UtcNow.AddMinutes(-1);
            bool result = targetGoal.HasOccurence(beginTime, endTime);

            Assert.False(result);
        }

        private static TargetGoalTrigger CreateTargetGoalTrigger(string cronExpression)
        {
            return new TargetGoalTrigger("id", "execGoal", "targetGoal", cronExpression, true, "expName", "teamname", "version", DateTime.UtcNow, DateTime.UtcNow);
        }
    }
}
