namespace Juno.EnvironmentSelection.SubscriptionFilters
{
    using System;
    using System.Collections.Generic;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SelectionStrategyTests
    {
        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void CreateEnvironmentCandidateValidatesStringParameters(string invalidParam)
        {
            TestSelectionStrategy strategy = new TestSelectionStrategy();
            strategy.Subscription = invalidParam;
            strategy.Regions = new List<string>();
            strategy.VmSkus = new List<string>();
            Assert.Throws<ArgumentException>(() => strategy.GetEnvironmentCandidates(null, 0, null));
        }

        [Test]
        public void CreateEnvironmentCandidateValidatesListParameters()
        {
            TestSelectionStrategy strategy = new TestSelectionStrategy();

            strategy.Subscription = "subscription";
            strategy.Regions = new List<string>();
            strategy.VmSkus = null;
            Assert.Throws<ArgumentException>(() => strategy.GetEnvironmentCandidates(null, 0, null));

            strategy.Subscription = "subscription";
            strategy.Regions = null;
            strategy.VmSkus = new List<string>();
            Assert.Throws<ArgumentException>(() => strategy.GetEnvironmentCandidates(null, 0, null));
        }

        [Test]
        public void CreateEnvironmentCandidateReturnsExpectedResult()
        {
            TestSelectionStrategy strategy = new TestSelectionStrategy();

            IDictionary<string, string> expectedAdditionalInfo = new Dictionary<string, string>()
            { 
                ["regionSearchSpace"] = "region1;region2",
                ["vmSkuSearchSpace"] = "vmSku1;vmsku2"
            };

            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>()
            { ["sub1"] = new EnvironmentCandidate("sub1", additionalInfo: expectedAdditionalInfo) };

            strategy.Subscription = "sub1";
            strategy.Regions = new List<string> { "region1", "region2" };
            strategy.VmSkus = new List<string> { "vmSku1", "vmSku2" };

            IDictionary<string, EnvironmentCandidate> actualResult = strategy.GetEnvironmentCandidates(null, 0, null);

            Assert.IsNotNull(actualResult);
            Assert.IsTrue(actualResult.Count == 1);

            Assert.AreEqual(expectedResult, actualResult);
        }

        private class TestSelectionStrategy : SelectionStrategy
        {
            public IList<string> Regions { get; set; }

            public IList<string> VmSkus { get; set; }
            
            public string Subscription { get; set; }

            public override IDictionary<string, EnvironmentCandidate> GetEnvironmentCandidates(IList<ServiceLimitAvailibility> limits, int threshold, EventContext telemetryContext)
            {
                return new Dictionary<string, EnvironmentCandidate>()
                { [this.Subscription] = SelectionStrategy.CreateEnvironmentCandidate(this.Subscription, this.VmSkus, this.Regions) };
            }
        }
    }
}
