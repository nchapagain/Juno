namespace Juno.Execution.ArmIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ArmClientTests
    {
        private readonly IEnumerable<string> fieldList = new List<string>() 
        {
            "id", 
            "eventName", 
            "eventTimestamp", 
            "resourceGroupName", 
            "resourceProviderName", 
            "operationId",
            "operationName", 
            "correlationId", 
            "submissionTimestamp", 
            "level", 
            "status", 
            "subStatus"
        }; 

        private ExperimentFixture mockFixture;
        private ArmClient armClient;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ExperimentFixture();
            this.armClient = new ArmClient(this.mockFixture.RestClient.Object);
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void ArmClientMakesTheExpectedApiCallToGetSubscriptionActivityLogs(bool validParameters)
        {
            string expectedSubscription = Guid.NewGuid().ToString();
            string expectedFilter = "resourceGroupName eq 'anyGroup'";
            IEnumerable<string> expectedFields;
            if (validParameters)
            {
                expectedFields = this.fieldList;
            }
            else
            {
                expectedFields = null;
            }

            using (HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK))
            {
                this.mockFixture.RestClient
                    .Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, options) =>
                    {
                        string expectedUri = $"https://management.azure.com/subscriptions/{expectedSubscription}/providers/microsoft.insights/eventtypes/management/values" +
                            $"?api-version=2017-03-01-preview&$filter={expectedFilter}";

                        if (expectedFields != null)
                        {
                            string s = string.Join(",", expectedFields.ToArray());
                            expectedUri += $"&$select={s}";
                        }

                        string actualUri = HttpUtility.UrlDecode(uri.AbsoluteUri);

                        Assert.AreEqual(expectedUri, actualUri);
                    })
                    .Returns(Task.FromResult(response));

                this.armClient.GetSubscriptionActivityLogsAsync(expectedSubscription, expectedFilter, CancellationToken.None, expectedFields)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void ArmClientMakesTheExpectedApiCallToGetSubscriptionActivityLogsWhenABeginTimeAndResourceGroupIsProvided(bool validParameters)
        {
            string expectedSubscription = Guid.NewGuid().ToString();
            string expectedResourceGroup = "rg01";
            DateTime expectedBeginTime = DateTime.UtcNow;

            string expectedFilter = $"eventTimestamp ge '{expectedBeginTime.ToString("o")}' and resourceGroupName eq '{expectedResourceGroup}'";
            IEnumerable<string> expectedFields;
            if (validParameters)
            {
                expectedFields = this.fieldList;
            }
            else
            {
                expectedFields = null;
            }

            using (HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK))
            {
                this.mockFixture.RestClient
                    .Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, options) =>
                    {
                        string expectedUri = $"https://management.azure.com/subscriptions/{expectedSubscription}/providers/microsoft.insights/eventtypes/management/values" +
                            $"?api-version=2017-03-01-preview&$filter=eventTimestamp ge '{expectedBeginTime.ToString("o")}' and resourceGroupName eq '{expectedResourceGroup}'";
                        if (expectedFields != null)
                        {
                            string s = string.Join(",", expectedFields.ToArray());
                            expectedUri += $"&$select={s}";
                        }

                        string actualUri = HttpUtility.UrlDecode(uri.AbsoluteUri);

                        Assert.AreEqual(expectedUri, actualUri);
                    })
                    .Returns(Task.FromResult(response));

                this.armClient.GetSubscriptionActivityLogsAsync(expectedSubscription, expectedFilter, CancellationToken.None, expectedFields)
                    .GetAwaiter().GetResult();
            }
        }
    }
}