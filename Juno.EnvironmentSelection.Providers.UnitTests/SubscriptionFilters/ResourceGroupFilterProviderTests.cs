namespace Juno.EnvironmentSelection.SubscriptionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ResourceGroupFilterProviderTests
    {
        private Fixture mockFixture;
        private IEnvironmentSelectionProvider provider;
        private Mock<IServiceLimitClient> mockClient;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupEnvironmentSelectionMocks();
            this.mockClient = new Mock<IServiceLimitClient>();
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<IServiceLimitClient>(this.mockClient.Object);

            this.provider = new ResourceGroupFilterProvider(services, new Mock<IConfiguration>().Object, NullLogger.Instance);
        }

        [Test]
        public void ExecuteAsyncPostsCorrectSubscriptionsWhenSubscriptionsAreGiven()
        {
            string sub = Guid.NewGuid().ToString();
            EnvironmentFilter filter = new EnvironmentFilter(typeof(ResourceGroupFilterProvider).FullName, new Dictionary<string, IConvertible>()
            { ["includeSubscription"] = sub });

            this.mockClient.Setup(client => client.GetAllResourceGroupUsageAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()))
                .Callback<CancellationToken, IList<string>>((token, subs) => 
                {
                    Assert.IsTrue(subs.Contains(sub));
                })
                .Returns(Task.FromResult(this.GetResourceGroups()));

            _ = this.provider.ExecuteAsync(filter, CancellationToken.None).GetAwaiter().GetResult();

            this.mockClient.Verify(client => client.GetAllResourceGroupUsageAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()), Times.Once());
        }

        [Test]
        public void ExecuteAsyncOnlyReturnsSubscriptionsThatAreBelowTheDefaultThreshold()
        {
            string subOne = Guid.NewGuid().ToString();
            string subTwo = Guid.NewGuid().ToString();
            IList<string> subs = new List<string>() { subOne };
            for (int i = 0; i < 981; i++)
            {
                subs.Add(subTwo);
            }

            EnvironmentFilter filter = new EnvironmentFilter(typeof(ResourceGroupFilterProvider).FullName, new Dictionary<string, IConvertible>());
            this.mockClient.Setup(client => client.GetAllResourceGroupUsageAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()))
                .Returns(Task.FromResult(this.GetResourceGroups(subs)));

            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>()
            { [subOne] = new EnvironmentCandidate(subOne, additionalInfo: new Dictionary<string, string>() { ["resourceGroupUsage"] = "1" }) };

            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.ExecuteAsync(filter, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(expectedResult, actualResult);
            Assert.AreEqual(expectedResult[subOne].AdditionalInfo, actualResult[subOne].AdditionalInfo);
        }

        [Test]
        public void ExecuteAsyncOnlyReturnsSubscriptionThatAreBelowGivenThresholdWhenThresholdIsGiven()
        {
            string subOne = Guid.NewGuid().ToString();
            string subTwo = Guid.NewGuid().ToString();
            IList<string> subs = new List<string>() { subOne };
            for (int i = 0; i < 50; i++)
            {
                subs.Add(subTwo);
            }

            EnvironmentFilter filter = new EnvironmentFilter(typeof(ResourceGroupFilterProvider).FullName, new Dictionary<string, IConvertible>()
            { ["resourceGroupLimit"] = 49 });

            this.mockClient.Setup(client => client.GetAllResourceGroupUsageAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()))
                .Returns(Task.FromResult(this.GetResourceGroups(subs)));

            IDictionary<string, EnvironmentCandidate> expectedResult = new Dictionary<string, EnvironmentCandidate>()
            { [subOne] = new EnvironmentCandidate(subOne, additionalInfo: new Dictionary<string, string>() { ["resourceGroupUsage"] = "1" }) };

            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.ExecuteAsync(filter, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(expectedResult, actualResult);
            Assert.AreEqual(expectedResult[subOne].AdditionalInfo, actualResult[subOne].AdditionalInfo);
        }

        [Test]
        public void ExecuteReturnsNoSubscriptionWhenNoSubscriptionAreBelowThreshold()
        {
            string subOne = Guid.NewGuid().ToString();
            IList<string> subs = new List<string>();
            for (int i = 0; i < 1000; i++)
            {
                subs.Add(subOne);
            }

            EnvironmentFilter filter = new EnvironmentFilter(typeof(ResourceGroupFilterProvider).FullName, new Dictionary<string, IConvertible>());

            this.mockClient.Setup(client => client.GetAllResourceGroupUsageAsync(It.IsAny<CancellationToken>(), It.IsAny<IList<string>>()))
                .Returns(Task.FromResult(this.GetResourceGroups(subs)));

            IDictionary<string, EnvironmentCandidate> actualResult = this.provider.ExecuteAsync(filter, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(actualResult);
            Assert.IsEmpty(actualResult);
        }

        private IEnumerable<AzureResourceGroup> GetResourceGroups(IList<string> subIds = null)
        { 
            int length = subIds == null ? 3 : subIds.Count;
            IList<AzureResourceGroup> result = new List<AzureResourceGroup>();
            for (int i = 0; i < length; i++)
            {
                result.Add(new AzureResourceGroup(
                    "resource",
                    "resourceID",
                    "region",
                    "provisioningstate",
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(2),
                    new Dictionary<string, string>(),
                    subIds == null ? "subscription" : subIds[i]));
            }

            return result;
        }
    }
}
