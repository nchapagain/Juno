namespace Juno.EnvironmentSelection.SubscriptionFilters
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class RegionMaximizationStrategyTests
    {
        private ISelectionStrategy strategy;
        private EventContext mockContext;
        private string regionKey = "regionSearchSpace";
        private string vmSkuKey = "vmSkuSearchSpace";

        [SetUp]
        public void SetupTests()
        {
            this.strategy = new RegionMaximizationStrategy();
            this.mockContext = EventContext.Persisted();
        }

        [Test]
        public void StrategyReturnsElligibleEnvironmentCandidates()
        {
            string expectedSub = Guid.NewGuid().ToString();
            string expectedVmSku = Guid.NewGuid().ToString();
            string expectedRegion = Guid.NewGuid().ToString();
            ServiceLimitAvailibility limit1 = new ServiceLimitAvailibility()
            {
                SubscriptionId = expectedSub,
                SkuName = expectedVmSku,
                Region = expectedRegion,
                VirtualCPU = 1,
                Usage = 50,
                Limit = 100
            };

            string otherVmSku = Guid.NewGuid().ToString();
            ServiceLimitAvailibility limit2 = new ServiceLimitAvailibility()
            {
                SubscriptionId = Guid.NewGuid().ToString(),
                SkuName = otherVmSku,
                Region = Guid.NewGuid().ToString(),
                VirtualCPU = 1,
                Usage = 99,
                Limit = 100
            };

            IList<ServiceLimitAvailibility> limits = new List<ServiceLimitAvailibility>() { limit1, limit2 };

            IDictionary<string, EnvironmentCandidate> actualResult = this.strategy.GetEnvironmentCandidates(limits, 2, this.mockContext);

            Assert.AreEqual(actualResult.Count, 1);
            Assert.AreEqual(expectedSub, actualResult.First().Key);

            var expectedAdditionalInfo = actualResult.First().Value.AdditionalInfo;
            Assert.AreEqual(expectedAdditionalInfo[this.regionKey], expectedRegion);
            Assert.AreEqual(expectedAdditionalInfo[this.vmSkuKey], expectedVmSku);
        }

        [Test]
        public void StrategyCalculatesAvailableQuotaBasedOnSkuvCPU()
        {
            string expectedSub = Guid.NewGuid().ToString();
            string expectedVmSku = Guid.NewGuid().ToString();
            string expectedRegion = Guid.NewGuid().ToString();
            ServiceLimitAvailibility limit1 = new ServiceLimitAvailibility()
            {
                SubscriptionId = expectedSub,
                SkuName = expectedVmSku,
                Region = expectedRegion,
                VirtualCPU = 20,
                Usage = 50,
                Limit = 100
            };

            string otherVmSku = Guid.NewGuid().ToString();
            ServiceLimitAvailibility limit2 = new ServiceLimitAvailibility()
            {
                SubscriptionId = Guid.NewGuid().ToString(),
                SkuName = otherVmSku,
                Region = Guid.NewGuid().ToString(),
                VirtualCPU = 20,
                Usage = 99,
                Limit = 100
            };

            IList<ServiceLimitAvailibility> limits = new List<ServiceLimitAvailibility>() { limit1, limit2 };

            IDictionary<string, EnvironmentCandidate> actualResult = this.strategy.GetEnvironmentCandidates(limits, 2, this.mockContext);

            Assert.AreEqual(actualResult.Count, 1);
            Assert.AreEqual(expectedSub, actualResult.First().Key);

            var expectedAdditionalInfo = actualResult.First().Value.AdditionalInfo;
            Assert.AreEqual(expectedAdditionalInfo[this.regionKey], expectedRegion);
            Assert.AreEqual(expectedAdditionalInfo[this.vmSkuKey], expectedVmSku);
        }

        [Test]
        public void StrategyReturnsNoEligibleSubsWhenNoEligibleSubsExist()
        {
            string expectedSub = Guid.NewGuid().ToString();
            string expectedVmSku = Guid.NewGuid().ToString();
            string expectedRegion = Guid.NewGuid().ToString();
            ServiceLimitAvailibility limit1 = new ServiceLimitAvailibility()
            {
                SubscriptionId = expectedSub,
                SkuName = expectedVmSku,
                Region = expectedRegion,
                VirtualCPU = 40,
                Usage = 50,
                Limit = 100
            };

            string otherVmSku = Guid.NewGuid().ToString();
            ServiceLimitAvailibility limit2 = new ServiceLimitAvailibility()
            {
                SubscriptionId = Guid.NewGuid().ToString(),
                SkuName = otherVmSku,
                Region = Guid.NewGuid().ToString(),
                VirtualCPU = 40,
                Usage = 99,
                Limit = 100
            };

            IList<ServiceLimitAvailibility> limits = new List<ServiceLimitAvailibility>() { limit1, limit2 };

            IDictionary<string, EnvironmentCandidate> actualResult = this.strategy.GetEnvironmentCandidates(limits, 2, this.mockContext);

            Assert.IsNotNull(actualResult);
            Assert.IsEmpty(actualResult);
        }
    }
}
