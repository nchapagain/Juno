namespace Juno.EnvironmentSelection.SubscriptionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Subscriptions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class PublicIPAdressFilterProviderTests
    {
        private EnvironmentFilterFixture mockfixture;
        private PublicIPAddressFilterProvider provider;
        private EnvironmentFilter filter;

        [SetUp]
        public void SetupTests()
        {
            this.mockfixture = new EnvironmentFilterFixture(false);
            this.provider = new PublicIPAddressFilterProvider(this.mockfixture.Services, this.mockfixture.Configuration, this.mockfixture.Logger.Object);
            this.filter = FixtureExtensions.CreateEnvironmentFilterFromType(typeof(PublicIPAddressFilterProvider));
        }

        [Test]
        public void ExecuteAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteAsync(null, CancellationToken.None));
        }

        [Test]
        public void ExecuteAsyncExitsThreadWhenCancellationIsRequested()
        {
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                CancellationToken token = source.Token;
                source.Cancel();
                IDictionary<string, EnvironmentCandidate> result = this.provider.ExecuteAsync(this.mockfixture.Create<EnvironmentFilter>(), token).GetAwaiter().GetResult();

                Assert.IsNotNull(result);
                Assert.IsEmpty(result);
            }
        }

        [Test]
        public void ExecuteAsyncPostsExpectedValuesToServiceLimitClientWhenNoSubscriptionIsProvided()
        {
            IEnumerable<AzureIPUsage> expectedResult = this.GetAzureIPUsages(5);
            this.mockfixture.MockServiceLimitClient.Setup(client => client.GetAllAzureIpUsageAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()))
                .Callback<CancellationToken, IList<string>>((token, subscriptions) =>
                {
                    Assert.IsNull(subscriptions);
                })
                .Returns(Task.FromResult(expectedResult));

            _ = this.provider.ExecuteAsync(this.filter, CancellationToken.None).GetAwaiter().GetResult();

            this.mockfixture.MockServiceLimitClient.Verify(client => client.GetAllAzureIpUsageAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()), Times.Once());
        }

        [Test]
        public void ExecuteAsyncPostsExpectedValuesToServiceLimitClientWhenSubscriptionsAreProvided()
        {
            string subscriptionId = Guid.NewGuid().ToString();
            this.filter.Parameters.Add("includeSubscription", subscriptionId);

            IEnumerable<AzureIPUsage> expectedResult = this.GetAzureIPUsages(5);
            this.mockfixture.MockServiceLimitClient.Setup(client => client.GetAllAzureIpUsageAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()))
                .Callback<CancellationToken, IList<string>>((token, subscriptions) =>
                {
                    Assert.IsNotNull(subscriptions);
                    Assert.IsNotEmpty(subscriptions);
                    Assert.IsTrue(subscriptions.All(sub => sub.Equals(subscriptionId, StringComparison.OrdinalIgnoreCase)));
                })
                .Returns(Task.FromResult(expectedResult));

            _ = this.provider.ExecuteAsync(this.filter, CancellationToken.None).GetAwaiter().GetResult();

            this.mockfixture.MockServiceLimitClient.Verify(client => client.GetAllAzureIpUsageAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()), Times.Once());
        }

        [Test]
        public void ExecuteAsyncReturnsExpectedResultWhenValidSubscriptionsArePresent()
        {
            List<AzureIPUsage> ipAddresses = this.GetAzureIPUsages(2);
            string expectedSubscription = ipAddresses[1].SubscriptionId;
            const int ipLimit = 10;
            for (int i = 0; i < ipLimit; i++)
            {
                ipAddresses.Add(new AzureIPUsage(ipAddresses[0]));
            }

            this.filter.Parameters.Add("publicIpAddressLimit", ipLimit);
            this.mockfixture.MockServiceLimitClient.Setup(client => client.GetAllAzureIpUsageAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()))
                .Returns(Task.FromResult(ipAddresses as IEnumerable<AzureIPUsage>));

            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.ExecuteAsync(this.filter, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsNotNull(actualResult);
            Assert.IsNotEmpty(actualResult);
            Assert.IsTrue(actualResult.Keys.All(key => key.Equals(expectedSubscription, StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(actualResult.Values.Select(v => v.Subscription).All(sub => sub.Equals(expectedSubscription)));
        }

        [Test]
        public void ExecuteAsyncReturnsExpectedREsultWhenNoValidSubscriptionsArePresent()
        {
            List<AzureIPUsage> ipAddresses = this.GetAzureIPUsages(2);
            const int ipLimit = 10;
            for (int i = 0; i < ipLimit; i++)
            {
                ipAddresses.Add(new AzureIPUsage(ipAddresses[0]));
                ipAddresses.Add(new AzureIPUsage(ipAddresses[1]));
            }

            this.filter.Parameters.Add("publicIpAddressLimit", ipLimit);
            this.mockfixture.MockServiceLimitClient.Setup(client => client.GetAllAzureIpUsageAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()))
                .Returns(Task.FromResult(ipAddresses as IEnumerable<AzureIPUsage>));

            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.ExecuteAsync(this.filter, CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsNotNull(actualResult);
            Assert.IsEmpty(actualResult);
        }

        private List<AzureIPUsage> GetAzureIPUsages(int listLength)
        {
            List<AzureIPUsage> result = new List<AzureIPUsage>();
            for (int i = 0; i < listLength; i++)
            {
                result.Add(new AzureIPUsage(
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString()));
            }

            return result;
        }
    }
}
