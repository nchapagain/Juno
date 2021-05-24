namespace Juno.DataManagement
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Repository.Storage;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentNotificationManagerTests
    {
        private Fixture mockFixture;
        private ExperimentNotificationManager notificationManager;
        private ExperimentMetadata exampleNotice;
        private ExperimentMetadataInstance exampleNoticeInstance;

        private Mock<IQueueStore<QueueAddress>> mockQueueStore;
        private Mock<ILogger> mockLogger;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockQueueStore = new Mock<IQueueStore<QueueAddress>>();
            this.mockLogger = new Mock<ILogger>();

            this.exampleNotice = this.mockFixture.Create<ExperimentMetadata>();
            this.notificationManager = new ExperimentNotificationManager(
                this.mockQueueStore.Object,
                this.mockLogger.Object);

            this.exampleNoticeInstance = new ExperimentMetadataInstance(Guid.NewGuid().ToString(), this.exampleNotice);
            this.exampleNoticeInstance.Extensions.Add(AzureQueueStore.MessageIdExtension, Guid.NewGuid().ToString());
            this.exampleNoticeInstance.Extensions.Add(AzureQueueStore.PopReceiptExtension, Guid.NewGuid().ToString());
        }

        [Test]
        public void NotificationManagerConstructorsValidateRequiredParameters()
        {
            Assert.Throws<ArgumentException>(() => new ExperimentNotificationManager(null, this.mockLogger.Object));
        }

        [Test]
        public void NotificationManagerConstructorsSetPropertiesToExpectedValues()
        {
            var manager = new ExperimentNotificationManager(this.mockQueueStore.Object);

            EqualityAssert.PropertySet(manager, "QueueStore", this.mockQueueStore.Object);
            EqualityAssert.PropertySet(manager, "Logger", NullLogger.Instance);

            manager = new ExperimentNotificationManager(this.mockQueueStore.Object, this.mockLogger.Object);

            EqualityAssert.PropertySet(manager, "QueueStore", this.mockQueueStore.Object);
            EqualityAssert.PropertySet(manager, "Logger", this.mockLogger.Object);
        }

        [Test]
        public async Task NotificationManagerQueuesNoticesToTheExpectedLocationWhenAQueueNameIsProvided()
        {
            // Mock Setup:
            // Ensure the address used to queue the message in the queue store matches expected.
            string expectedQueueName = "any queue name";

            this.mockQueueStore
                .Setup(store => store.EnqueueItemAsync<ExperimentMetadataInstance>(
                    It.Is<QueueAddress>(addr => addr.QueueName == expectedQueueName),
                    It.IsAny<ExperimentMetadataInstance>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .Returns(Task.FromResult(this.exampleNoticeInstance))
                .Verifiable();

            await this.notificationManager.CreateNoticeAsync(
                expectedQueueName,
                this.exampleNotice,
                CancellationToken.None);

            this.mockQueueStore.VerifyAll();
        }

        [Test]
        public Task NotificationManagerQueuesTheExpectedNotice()
        {
            // Mock Setup:
            // Ensure the queued message is the one expected
            string expectedMessage = this.exampleNotice.ToJson();

            this.mockQueueStore
                .Setup(store => store.EnqueueItemAsync<ExperimentMetadataInstance>(
                    It.IsAny<QueueAddress>(),
                    It.IsAny<ExperimentMetadataInstance>(),
                    It.IsAny<CancellationToken>(),
                    null))
                .Callback<QueueAddress, ExperimentMetadataInstance, CancellationToken, TimeSpan?>((address, notice, token, visibilityTimeout) =>
                {
                    Assert.IsTrue(this.exampleNotice.Equals(notice.Definition));
                })
                .Returns(Task.FromResult(this.exampleNoticeInstance));

            return this.notificationManager.CreateNoticeAsync("any queue", this.exampleNotice, CancellationToken.None);
        }

        [Test]
        public async Task NotificationManagerUsesTheVisibilityTimeoutSuppliedWhenQueueingNotices()
        {
            TimeSpan expectedVisibilityTimeout = TimeSpan.FromSeconds(10);

            this.mockQueueStore
                .Setup(store => store.EnqueueItemAsync<ExperimentMetadataInstance>(
                    It.IsAny<QueueAddress>(),
                    It.IsAny<ExperimentMetadataInstance>(),
                    It.IsAny<CancellationToken>(),
                    expectedVisibilityTimeout))
                .Returns(Task.FromResult(null as ExperimentMetadataInstance))
                .Verifiable();

            await this.notificationManager.CreateNoticeAsync("AnyQueue", this.exampleNotice, CancellationToken.None, expectedVisibilityTimeout);

            this.mockQueueStore.VerifyAll();
        }

        [Test]
        public async Task NotificationManagerDeletesNoticesFromTheExpectedQueue()
        {
            // Mock Setup:
            // Ensure the address used to queue the message in the queue store matches expected.
            string expectedQueueName = "any queue name";

            this.mockQueueStore
                .Setup(store => store.DeleteItemAsync(
                    It.Is<QueueAddress>(addr => addr.QueueName == expectedQueueName),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await this.notificationManager.DeleteNoticeAsync(
                expectedQueueName,
                this.exampleNoticeInstance.Extension<string>(AzureQueueStore.MessageIdExtension),
                this.exampleNoticeInstance.Extension<string>(AzureQueueStore.PopReceiptExtension),
                CancellationToken.None);

            this.mockQueueStore.VerifyAll();
        }

        [Test]
        public async Task NotificationManagerIncludesTheInformationRequiredToSuccessfullyDeleteAMessage()
        {
            // Mock Setup:
            // Ensure the address used to queue the message in the queue store matches expected.
            string expectedQueueName = "any queue name";

            this.mockQueueStore
                .Setup(store => store.DeleteItemAsync(
                    It.Is<QueueAddress>(addr => addr.QueueName == expectedQueueName
                        && addr.MessageId == this.exampleNoticeInstance.Extension<string>(AzureQueueStore.MessageIdExtension)
                        && addr.PopReceipt == this.exampleNoticeInstance.Extension<string>(AzureQueueStore.PopReceiptExtension)),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await this.notificationManager.DeleteNoticeAsync(
                expectedQueueName,
                this.exampleNoticeInstance.Extension<string>(AzureQueueStore.MessageIdExtension),
                this.exampleNoticeInstance.Extension<string>(AzureQueueStore.PopReceiptExtension),
                CancellationToken.None);

            this.mockQueueStore.VerifyAll();
        }

        [Test]
        public async Task NotificationManagerReadsTheExpectedQueueToPeekNotices()
        {
            // Mock Setup:
            // Ensure the queued message is the one expected
            string expectedQueueName = "any queue name";

            this.mockQueueStore
                .Setup(store => store.PeekItemAsync<ExperimentMetadataInstance>(
                    It.Is<QueueAddress>(addr => addr.QueueName == expectedQueueName),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .Returns(Task.FromResult(null as ExperimentMetadataInstance))
                .Verifiable();

            await this.notificationManager.PeekNoticeAsync(expectedQueueName, CancellationToken.None);

            this.mockQueueStore.VerifyAll();
        }

        [Test]
        public async Task NotificationManagerReturnsTheExpectedResultWhenNoticesExist()
        {
            this.mockQueueStore
                .Setup(store => store.PeekItemAsync<ExperimentMetadataInstance>(
                    It.IsAny<QueueAddress>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TimeSpan?>()))
                .Returns(Task.FromResult(this.exampleNoticeInstance));

            ExperimentMetadataInstance actualNotice = await this.notificationManager.PeekNoticeAsync("any queue", CancellationToken.None);

            Assert.IsTrue(this.exampleNoticeInstance.Equals(actualNotice));
        }

        [Test]
        public async Task NotificationManagerReturnsTheExpectedResultWhenNoticesDoNotExist()
        {
            // Mock Setup:
            // There are not any notices present on the queue.
            this.mockQueueStore
                .Setup(store => store.DequeueItemAsync<ExperimentMetadataInstance>(
                    It.IsAny<QueueAddress>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(null as ExperimentMetadataInstance));

            Assert.IsNull(await this.notificationManager.PeekNoticeAsync("any queue", CancellationToken.None));
        }

        [Test]
        public async Task NotificationManagerUsesTheVisibilityTimeoutSuppliedWhenPeekingNotices()
        {
            TimeSpan expectedVisibilityTimeout = TimeSpan.FromSeconds(10);

            this.mockQueueStore
                .Setup(store => store.PeekItemAsync<ExperimentMetadataInstance>(
                    It.IsAny<QueueAddress>(),
                    It.IsAny<CancellationToken>(),
                    expectedVisibilityTimeout))
                .Returns(Task.FromResult(null as ExperimentMetadataInstance))
                .Verifiable();

            await this.notificationManager.PeekNoticeAsync("AnyQueue", CancellationToken.None, expectedVisibilityTimeout);

            this.mockQueueStore.VerifyAll();
        }

        [Test]
        public async Task NotificationManagerSetsVisibilityOfNoticesOnTheExpectedQueue()
        {
            // Mock Setup:
            // Ensure the address used to queue the message in the queue store matches expected.
            string expectedQueueName = "any queue name";

            this.mockQueueStore
                .Setup(store => store.SetItemVisibilityAsync(
                    It.Is<QueueAddress>(addr => addr.QueueName == expectedQueueName),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await this.notificationManager.SetNoticeVisibilityAsync(
                expectedQueueName,
                this.exampleNoticeInstance.Extension<string>(AzureQueueStore.MessageIdExtension),
                this.exampleNoticeInstance.Extension<string>(AzureQueueStore.PopReceiptExtension),
                TimeSpan.FromSeconds(1),
                CancellationToken.None);

            this.mockQueueStore.VerifyAll();
        }

        [Test]
        public async Task NotificationManagerIncludesTheInformationRequiredToSuccessfullyChangeTheVisibilityOfAMessage()
        {
            // Mock Setup:
            // Ensure the address used to queue the message in the queue store matches expected.
            string expectedQueueName = "any queue name";

            this.mockQueueStore
                .Setup(store => store.SetItemVisibilityAsync(
                    It.Is<QueueAddress>(addr => addr.QueueName == expectedQueueName
                        && addr.MessageId == this.exampleNoticeInstance.Extension<string>(AzureQueueStore.MessageIdExtension)
                        && addr.PopReceipt == this.exampleNoticeInstance.Extension<string>(AzureQueueStore.PopReceiptExtension)),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await this.notificationManager.SetNoticeVisibilityAsync(
                expectedQueueName,
                this.exampleNoticeInstance.Extension<string>(AzureQueueStore.MessageIdExtension),
                this.exampleNoticeInstance.Extension<string>(AzureQueueStore.PopReceiptExtension),
                TimeSpan.FromSeconds(1),
                CancellationToken.None);

            this.mockQueueStore.VerifyAll();
        }

        [Test]
        public async Task NotificationManagerAppliesTheSpecifiedVisibilityTimeoutToNoticesWhenChangingTheirVisibility()
        {
            TimeSpan expectedVisibilityTimeout = TimeSpan.FromSeconds(10);

            this.mockQueueStore
                .Setup(store => store.SetItemVisibilityAsync(
                    It.IsAny<QueueAddress>(),
                    expectedVisibilityTimeout,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await this.notificationManager.SetNoticeVisibilityAsync(
                "AnyQueue",
                this.exampleNoticeInstance.Extension<string>(AzureQueueStore.MessageIdExtension),
                this.exampleNoticeInstance.Extension<string>(AzureQueueStore.PopReceiptExtension),
                expectedVisibilityTimeout,
                CancellationToken.None);

            this.mockQueueStore.VerifyAll();
        }
    }
}