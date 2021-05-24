namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Will dispose in teardown")]
    public class GetExperimentStepsCmdletTests
    {
        private IAsyncPolicy retryPolicy;
        private int currentRetries;
        private TestGetExperimentStepsCmdlet cmdlet;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private IEnumerable<ExperimentStepInstance> mockResponseContent;
        private HttpResponseMessage mockGetResponse;
        private bool exceptionThrown;
        private string expectedExperimentId;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockDependencies = new FixtureDependencies();
            this.mockFixture.SetupExperimentMocks();
            List<ExperimentStepInstance> responseContent = new List<ExperimentStepInstance>();
            responseContent.Add(this.mockFixture.CreateExperimentStep());
            this.mockResponseContent = responseContent;
            this.mockGetResponse = this.mockFixture.CreateHttpResponse(System.Net.HttpStatusCode.OK, this.mockResponseContent);

            this.retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                4,
                (retries) => TimeSpan.Zero,
                onRetry: (response, delay, retryCount, context) =>
                {
                    this.currentRetries = retryCount;
                });
            this.expectedExperimentId = "an id";
            this.cmdlet = new TestGetExperimentStepsCmdlet(this.retryPolicy);
            this.cmdlet.ExperimentId = "an id";
            this.cmdlet.ViewType = View.Full;
            this.cmdlet.ExperimentsClient = this.mockDependencies.ExperimentClient.Object;
            this.cmdlet.AsJson = false;
            this.exceptionThrown = false;
        }

        [TearDown]
        public void TearDown()
        {
            this.cmdlet.Dispose();
        }

        [Test]
        public void CmdletThrowsExpectedExceptionWhenInvalidParametersAreGiven()
        {
            this.cmdlet.ExperimentId = string.Empty;

            try
            {
                this.cmdlet.ProcessInternal();
            }
            catch (Exception e)
            {
                this.exceptionThrown = true;
                Assert.AreEqual(typeof(ArgumentException), e.GetType());
            }

            Assert.IsTrue(this.exceptionThrown);
            Assert.AreEqual(string.Empty, this.cmdlet.ExperimentId);
            Assert.AreNotEqual(string.Empty, this.cmdlet.ViewType);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
        }

        [Test]
        public void CmdletThrowsExpectedExceptionWhenClientThrowException()
        {
            Exception expectedException = new Exception("Expected Exception Message");
            this.mockDependencies.ExperimentClient.Setup(m => m.GetExperimentStepsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Contracts.View>(), It.IsAny<IEnumerable<ExecutionStatus>>()))
                .Callback((string experimentId, CancellationToken token, Contracts.View view, IEnumerable<ExecutionStatus> status) =>
                {
                    Assert.AreEqual(this.cmdlet.ExperimentId, experimentId);
                    Assert.IsFalse(this.cmdlet.AsJson);
                    Assert.AreEqual(View.Full, view);
                }).ThrowsAsync(expectedException);

            try
            {
                this.cmdlet.ProcessInternal();
            }
            catch (Exception e)
            {
                this.exceptionThrown = true;
                Assert.AreEqual(expectedException.GetType(), e.GetType());
                Assert.AreEqual(expectedException.Message, e.Message);
                Assert.AreEqual(expectedException, e);
            }

            Assert.IsTrue(this.exceptionThrown);
            Assert.AreEqual(this.expectedExperimentId, this.cmdlet.ExperimentId);
            Assert.IsFalse(this.cmdlet.AsJson);
            Assert.IsFalse(this.cmdlet.IsJsonObject);
            Assert.IsNull(this.cmdlet.Results);
            Assert.Greater(this.currentRetries, 1);
        }

        [Test]
        public void CmdletWritesExpectedObject()
        {
            this.cmdlet.AsJson = true;
            this.mockDependencies.ExperimentClient.Setup(m => m.GetExperimentStepsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Contracts.View>(), It.IsAny<IEnumerable<ExecutionStatus>>()))
                .Callback((string experimentId, CancellationToken token, Contracts.View view, IEnumerable<ExecutionStatus> status) =>
                {
                    Assert.AreEqual(this.cmdlet.ExperimentId, experimentId);
                    Assert.IsTrue(this.cmdlet.AsJson);
                    Assert.AreEqual(View.Full, view);
                }).ReturnsAsync(this.mockGetResponse);

            try
            {
                this.cmdlet.ProcessInternal();
            }
            catch (Exception)
            {
                Assert.Fail();
            }

            Assert.IsNotNull(this.cmdlet.Results);
            Assert.AreEqual(this.expectedExperimentId, this.cmdlet.ExperimentId);
            Assert.IsTrue(this.cmdlet.AsJson);
            Assert.IsTrue(this.cmdlet.IsJsonObject);
            Assert.AreEqual(this.mockResponseContent, this.cmdlet.Results);
        }

        private class TestGetExperimentStepsCmdlet : GetExperimentStepsCmdlet
        {
            public TestGetExperimentStepsCmdlet(IAsyncPolicy retryPolicy)
                : base(retryPolicy)
            {
            }

            public bool IsJsonObject { get; set; }

            public object Results { get; set; }

            public void ProcessInternal()
            {
                this.ProcessRecord();
            }

            protected override void WriteResults(object results)
            {
                this.Results = results;
                this.IsJsonObject = false;
            }

            protected override void WriteResultsAsJson(object results)
            {
                this.Results = results;
                this.IsJsonObject = true;
            }
        }
    }
}
