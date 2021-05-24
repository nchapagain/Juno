namespace Juno.Contracts
{
    using System;
    using AutoFixture;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentParameterExtensionsTests
    {
        private Fixture mockFixture;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
        }

        [Test]
        public void TimeoutExtensionReturnsTheExpectedValueDefinedInTheComponentParameters()
        {
            TimeSpan expectedTimeout = TimeSpan.Parse("00:10:30");
            ExperimentComponent component = this.mockFixture.Create<ExperimentComponent>();
            component.Parameters.Clear();
            component.Parameters.Add("timeout", expectedTimeout.ToString());

            TimeSpan? actualTimeout = component.Timeout();
            Assert.IsNotNull(actualTimeout);
            Assert.AreEqual(expectedTimeout, actualTimeout);
        }

        [Test]
        public void TimeoutExtensionHandlesComponentsWhereTheParameterIsNotDefined()
        {
            ExperimentComponent component = this.mockFixture.Create<ExperimentComponent>();
            component.Parameters.Clear();

            TimeSpan? actualTimeout = component.Timeout();
            Assert.IsNull(actualTimeout);
        }
    }
}
