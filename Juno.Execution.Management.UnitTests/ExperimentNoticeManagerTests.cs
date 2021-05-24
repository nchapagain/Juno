namespace Juno.Execution.Management
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentNoticeManagerTests
    {
        private FixtureDependencies mockFixture;
        private ExperimentMetadataInstance mockWorkNotice;
        private ExperimentNoticeManager noticeManager;
        private string workQueue;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new FixtureDependencies(MockBehavior.Strict);
            this.mockFixture.SetupExperimentMocks();
            this.workQueue = "any-work-queue";

            // The original notice
            this.mockWorkNotice = ExperimentNoticeManagerTests.CreateMockNotice();

            this.noticeManager = new ExperimentNoticeManager(
                new ExecutionClient(this.mockFixture.RestClient.Object, new Uri("https://any/where/on/earth"), Policy.NoOpAsync()),
                this.workQueue,
                NullLogger.Instance,
                Policy.NoOpAsync());
        }

        [Test]
        public void NoticeManagerConstructorsSetPropertiesToExpectedValues()
        {
            ExecutionClient expectedClient = new ExecutionClient(this.mockFixture.RestClient.Object, new Uri("https://any/where/on/earth"), Policy.NoOpAsync());
            string expectedWorkQueue = "anyQueue";
            ILogger expectedLogger = NullLogger.Instance;

            this.noticeManager = new ExperimentNoticeManager(expectedClient, expectedWorkQueue);

            Assert.IsTrue(object.ReferenceEquals(expectedClient, this.noticeManager.Client));
            Assert.IsTrue(string.Equals(expectedWorkQueue, this.noticeManager.WorkQueue, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(object.ReferenceEquals(NullLogger.Instance, this.noticeManager.Logger));

            this.noticeManager = new ExperimentNoticeManager(expectedClient, expectedWorkQueue, expectedLogger);

            Assert.IsTrue(object.ReferenceEquals(expectedClient, this.noticeManager.Client));
            Assert.IsTrue(string.Equals(expectedWorkQueue, this.noticeManager.WorkQueue, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(object.ReferenceEquals(expectedLogger, this.noticeManager.Logger));
        }

        [Test]
        public async Task NoticeManagerExecutesTheExpectedWorkflowStepsWhenDeletingWorkNotices()
        {
            this.mockFixture.RestClient
                .Setup(client => client.DeleteAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockFixture.CreateHttpResponse(HttpStatusCode.NoContent))
                .Verifiable();

            await this.noticeManager.DeleteWorkNoticeAsync(this.mockWorkNotice, CancellationToken.None);

            this.mockFixture.RestClient.Verify();
            this.mockFixture.RestClient.VerifyNoOtherCalls();
        }

        [Test]
        public Task NoticeManagerDeletesWorkNoticesFromTheExpectedQueue()
        {
            this.mockFixture.RestClient
                .Setup(client => client.DeleteAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockFixture.CreateHttpResponse(HttpStatusCode.NoContent))
                .Callback<Uri, CancellationToken>((uri, token) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Contains($"workQueue={this.workQueue}"));
                });

            return this.noticeManager.DeleteWorkNoticeAsync(this.mockWorkNotice, CancellationToken.None);
        }

        [Test]
        public Task NoticeManagerDeletesTheExpectedWorkNoticesFromTheQueue()
        {
            int count = 0;
            this.mockFixture.RestClient
                .Setup(client => client.DeleteAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockFixture.CreateHttpResponse(HttpStatusCode.NoContent))
                .Callback<Uri, CancellationToken>((uri, token) =>
                {
                    Assert.IsTrue(uri.PathAndQuery.Contains($"messageId={this.mockWorkNotice.MessageId()}"));
                    Assert.IsTrue(uri.PathAndQuery.Contains($"popReceipt={this.mockWorkNotice.PopReceipt()}"));
                    count++;
                });

            return this.noticeManager.DeleteWorkNoticeAsync(this.mockWorkNotice, CancellationToken.None);
        }

        [Test]
        public void NoticeManagerHandlesAttemptsDeleteNoticesThatDoNotExistAsExpected()
        {
            // Ensure that we DO surface exceptions for scenarios other than status code == NotFound
            this.mockFixture.RestClient
                .Setup(client => client.DeleteAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockFixture.CreateHttpResponse(HttpStatusCode.BadRequest));

            Assert.ThrowsAsync<ExperimentException>(() => this.noticeManager.DeleteWorkNoticeAsync(this.mockWorkNotice, CancellationToken.None));

            // Now ensure that we DO handle status code == NotFound appropriately.
            this.mockFixture.RestClient
               .Setup(client => client.DeleteAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(this.mockFixture.CreateHttpResponse(HttpStatusCode.NotFound));

            Assert.DoesNotThrowAsync(() => this.noticeManager.DeleteWorkNoticeAsync(this.mockWorkNotice, CancellationToken.None));
        }

        [Test]
        public async Task NoticeManagerExecutesTheExpectedWorkflowStepsWhenGettingWorkNotices()
        {
            // 1) Peek next notice from the queue
            this.mockFixture.RestClient
                .Setup(client => client.GetAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>(), It.IsAny<HttpCompletionOption>()))
                .ReturnsAsync(this.mockFixture.CreateHttpResponse(HttpStatusCode.OK, this.mockWorkNotice))
                .Verifiable();

            await this.noticeManager.GetWorkNoticeAsync(CancellationToken.None);

            this.mockFixture.RestClient.Verify();
            this.mockFixture.RestClient.VerifyNoOtherCalls();
        }

        [Test]
        public async Task NoticeManagerGetsWorkNoticesFromTheExpectedQueue()
        {
            this.SetupMockDefaultBehaviors();
            await this.noticeManager.GetWorkNoticeAsync(CancellationToken.None);

            this.mockFixture.RestClient.Verify(client => client.GetAsync(
                It.Is<Uri>(uri => uri.PathAndQuery.Contains($"workQueue={this.workQueue}")),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        [Test]
        public async Task NoticeManagerAppliesTheExpectedVisibilityDelayToTheOriginalPeekedNotice()
        {
            this.SetupMockDefaultBehaviors();
            await this.noticeManager.GetWorkNoticeAsync(CancellationToken.None);

            TimeSpan expectedVisibilityTimeout = TimeSpan.FromMinutes(5);

            this.mockFixture.RestClient.Verify(client => client.GetAsync(
                It.Is<Uri>(uri => uri.PathAndQuery.Contains($"visibilityDelay={expectedVisibilityTimeout.TotalSeconds}")),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        [Test]
        public async Task NoticeManagerReturnsTheExpectedNoticeFromTheQueue()
        {
            this.SetupMockDefaultBehaviors();
            ExperimentMetadataInstance notice = await this.noticeManager.GetWorkNoticeAsync(CancellationToken.None);

            Assert.AreEqual(this.mockWorkNotice.Id, notice.Id);
            Assert.AreEqual(this.mockWorkNotice.MessageId(), notice.MessageId());
            Assert.AreEqual(this.mockWorkNotice.PopReceipt(), notice.PopReceipt());
            Assert.AreEqual(this.mockWorkNotice.Definition.Metadata.Count, notice.Definition.Metadata.Count);

            CollectionAssert.AreEquivalent(
                this.mockWorkNotice.Definition.Metadata.Select(entry => $"{entry.Key}={entry.Value}"),
                notice.Definition.Metadata.Select(entry => $"{entry.Key}={entry.Value}"));
        }

        [Test]
        public async Task NoticeManagerExecutesTheExpectedWorkflowStepsWhenUpdatingWorkNotices()
        {
            // The update makes the notice visible on the queue
            this.mockFixture.RestClient
                .Setup(client => client.PutAsync(
                    It.Is<Uri>(uri => uri.PathAndQuery.Contains($"workQueue={this.workQueue}")),
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockFixture.CreateHttpResponse(HttpStatusCode.OK))
                .Verifiable();

            await this.noticeManager.SetWorkNoticeVisibilityAsync(this.mockWorkNotice, TimeSpan.FromSeconds(1), CancellationToken.None);

            this.mockFixture.RestClient.Verify();
        }

        [Test]
        public async Task NoticeManagerMakesTheExpectedWorkNoticeVisibleWhenUpdatingWorkNotices()
        {
            // The update makes the notice visible on the queue
            string expectedMessageId = this.mockWorkNotice.MessageId();
            string expectedPopReceipt = this.mockWorkNotice.PopReceipt();

            this.mockFixture.RestClient
                .Setup(client => client.PutAsync(
                    It.Is<Uri>(uri => uri.PathAndQuery.Contains($"messageId={expectedMessageId}&popReceipt={expectedPopReceipt}")),
                    It.IsAny<HttpContent>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.mockFixture.CreateHttpResponse(HttpStatusCode.OK))
                .Verifiable();

            await this.noticeManager.SetWorkNoticeVisibilityAsync(this.mockWorkNotice, TimeSpan.FromSeconds(1), CancellationToken.None);

            this.mockFixture.RestClient.Verify();
        }

        [Test]
        public async Task NoticeManagerAppliesTheExpectedVisibilityDelayToMakeTheNoticeVisibleOnUpdate()
        {
            this.SetupMockDefaultBehaviors();

            TimeSpan expectedVisibilityTimeout = TimeSpan.FromSeconds(30);
            await this.noticeManager.SetWorkNoticeVisibilityAsync(this.mockWorkNotice, expectedVisibilityTimeout, CancellationToken.None);

            this.mockFixture.RestClient.Verify(client => client.PutAsync(
                It.Is<Uri>(uri => uri.PathAndQuery.Contains($"visibilityDelay={expectedVisibilityTimeout.TotalSeconds}")),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        private static ExperimentMetadataInstance CreateMockNotice(ExperimentMetadataInstance withReferenceToOriginalNotice = null)
        {
            ExperimentMetadataInstance notice = new ExperimentMetadataInstance(
                Guid.NewGuid().ToString(),
                new ExperimentMetadata(Guid.NewGuid().ToString(), new Dictionary<string, IConvertible>
                {
                    [NoticeMetadataKey.PreviousMessageId] = (withReferenceToOriginalNotice != null)
                        ? withReferenceToOriginalNotice.MessageId() : Guid.NewGuid().ToString(),

                    [NoticeMetadataKey.PreviousPopReceipt] = (withReferenceToOriginalNotice != null)
                        ? withReferenceToOriginalNotice.PopReceipt() : Guid.NewGuid().ToString(),
                }));

            notice.Extensions["messageId"] = Guid.NewGuid().ToString();
            notice.Extensions["popReceipt"] = Guid.NewGuid().ToString();

            return notice;
        }

        private void SetupMockDefaultBehaviors()
        {
            // Peek next notice from the queue
            this.mockFixture.RestClient
                .Setup(client => client.GetAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>(), It.IsAny<HttpCompletionOption>()))
                .ReturnsAsync(this.mockFixture.CreateHttpResponse(HttpStatusCode.OK, this.mockWorkNotice));

            // Delete a notice
            this.mockFixture.RestClient
               .Setup(client => client.DeleteAsync(
                   It.IsAny<Uri>(),
                   It.IsAny<CancellationToken>()))
               .ReturnsAsync(this.mockFixture.CreateHttpResponse(HttpStatusCode.NoContent));

            // Make notice visible on the queue.
            this.mockFixture.RestClient
               .Setup(client => client.PutAsync(It.IsAny<Uri>(), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(this.mockFixture.CreateHttpResponse(HttpStatusCode.OK));
        }
    }
}
