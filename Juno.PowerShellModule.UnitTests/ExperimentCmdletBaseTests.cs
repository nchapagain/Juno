namespace Juno.PowerShellModule
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using Juno.Api.Client;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Will dispose in teardown")]
    public class ExperimentCmdletBaseTests
    {
        private TestExperimentCmdletBase experimentCmdletBase;
        private Mock<IExperimentClient> mockClient;
        private IAsyncPolicy retryPolicy;
        private int defaultRetryCount;
        private int currentRetries;

        [SetUp]
        public void SetupTest()
        {
            this.defaultRetryCount = 4;
            this.currentRetries = 0;
            this.retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                this.defaultRetryCount, 
                (retries) => TimeSpan.Zero,
                onRetry: (response, delay, retryCount, context) =>
                {
                    this.currentRetries = retryCount;
                });
            this.experimentCmdletBase = new TestExperimentCmdletBase(this.retryPolicy);
            this.mockClient = new Mock<IExperimentClient>();
            this.experimentCmdletBase.ExperimentsClient = this.mockClient.Object;
        }

        [TearDown]
        public void TearDown()
        {
            this.experimentCmdletBase.Dispose();
        }

        [Test]
        public void GetExperimentTemplateListAsyncRetriesOnException()
        {
            bool exceptionThrown = false;
            Exception expectedException = new Exception("Expected Exception Message");
            this.mockClient.Setup(m => m.GetExperimentTemplateListAsync(It.IsAny<CancellationToken>())).ThrowsAsync(expectedException);

            try
            {
                HttpResponseMessage actualResponse = this.experimentCmdletBase.GetExperimentTemplateListAsync().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Assert.AreEqual(e, expectedException);
                exceptionThrown = true;
            }

            Assert.Greater(this.currentRetries, 1);
            Assert.True(exceptionThrown);
        }

        [Test]
        public void GetExperimentTemplateListAsyncReturnsExpectedObject()
        {
            HttpResponseMessage expectedResponse = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
            HttpContent expectedContent = new StringContent("Something bad happened.");
            expectedResponse.Content = expectedContent;
            this.mockClient.Setup(m => m.GetExperimentTemplateListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expectedResponse);
            HttpResponseMessage actualResponse = null;

            try
            {
                actualResponse = this.experimentCmdletBase.GetExperimentTemplateListAsync().GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                Assert.Fail();
            }

            Assert.AreEqual(expectedResponse.StatusCode, actualResponse.StatusCode);
            Assert.AreEqual(expectedResponse.Content, actualResponse.Content);
        }

        [Test]
        public void GetExperimentTemplateAsyncRetriesOnException()
        {
            bool exceptionThrown = false;
            Exception expectedException = new Exception("Expected Exception Message");
            this.mockClient.Setup(m => m.GetExperimentTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(expectedException);
            string experimentTemplateId = "some template id";
            string teamName = "some team name";

            try
            {
                HttpResponseMessage actualResponse = this.experimentCmdletBase.GetExperimentTemplateAsync(experimentTemplateId, teamName).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Assert.AreEqual(e, expectedException);
                exceptionThrown = true;
            }

            Assert.Greater(this.currentRetries, 1);
            Assert.True(exceptionThrown);
        }

        [Test]
        public void GetExperimentTemplateAsyncReturnsExpectedObject()
        {
            HttpResponseMessage expectedResponse = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
            HttpContent expectedContent = new StringContent("Something bad happened.");
            expectedResponse.Content = expectedContent;
            this.mockClient.Setup(m => m.GetExperimentTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(expectedResponse);
            string experimentTemplateId = "some template id";
            string teamName = "some team name";
            HttpResponseMessage actualResponse = null;

            try
            {
                actualResponse = this.experimentCmdletBase.GetExperimentTemplateAsync(experimentTemplateId, teamName).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                Assert.Fail();
            }

            Assert.AreEqual(expectedResponse.StatusCode, actualResponse.StatusCode);
            Assert.AreEqual(expectedResponse.Content, actualResponse.Content);
        }

        private class TestExperimentCmdletBase : ExperimentCmdletBase
        {
            public TestExperimentCmdletBase(IAsyncPolicy retryPolicy = null)
                : base(retryPolicy)
            {
            }
        }
    }
}
