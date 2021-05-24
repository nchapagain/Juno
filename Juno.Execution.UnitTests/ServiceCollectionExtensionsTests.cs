namespace Juno.Execution
{
    using System;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ServiceCollectionExtensionsTests
    {
        private ServiceCollection services;

        [SetUp]
        public void SetupTest()
        {
            this.services = new ServiceCollection();
        }

        [Test]
        public void GetServiceExtensionSupportsSingletonInstanceServices()
        {
            Mock<ILogger> mockLogger = new Mock<ILogger>();

            this.services.AddSingleton<ILogger>(mockLogger.Object);
            ILogger logger1 = this.services.GetService<ILogger>();
            ILogger logger2 = this.services.GetService<ILogger>();

            Assert.IsTrue(object.ReferenceEquals(mockLogger.Object, logger1));
            Assert.IsTrue(object.ReferenceEquals(mockLogger.Object, logger2));
        }

        [Test]
        public void GetServiceExtensionSupportsServicesCreateUsingAFactory()
        {
            ILogger logger1 = null;
            ILogger logger2 = null;

            this.services.AddSingleton((serviceProvider) =>
            {
                return new Mock<ILogger>().Object;
            });

            logger1 = this.services.GetService<ILogger>();
            logger2 = this.services.GetService<ILogger>();

            Assert.IsNotNull(logger1);
            Assert.IsNotNull(logger2);
            Assert.IsFalse(object.ReferenceEquals(logger1, logger2));
        }

        [Test]
        public void GetServiceExtensionSupportsServicesCreatedByTypeAlone()
        {
            // The dependencies required to construct the service must exist in the collection
            // as well.
            IConfiguration anyConfiguration = new ConfigurationBuilder().Build();
            ILogger anyLogger = NullLogger.Instance;

            this.services.AddSingleton<IConfiguration>(anyConfiguration);
            this.services.AddSingleton<ILogger>(anyLogger);
            this.services.AddSingleton<MockDependency>();

            MockDependency service = this.services.GetService<MockDependency>();

            Assert.IsNotNull(service);
            Assert.IsTrue(object.ReferenceEquals(anyConfiguration, service.Configuration));
            Assert.IsTrue(object.ReferenceEquals(anyLogger, service.Logger));
        }

        [Test]
        public void GetServiceExtensionThrowsAnExceptionWhenTheExpectedServiceDoesNotExistInTheCollection()
        {
            Assert.Throws<InvalidOperationException>(() => this.services.GetService<MockDependency>());
        }

        [Test]
        public void GetServiceExtensionThrowsAnExceptionWhenTheExpectedDependenciesOfAServiceDoNotExistInTheCollection()
        {
            this.services.AddSingleton<MockDependency>();
            Assert.Throws<InvalidOperationException>(() => this.services.GetService<MockDependency>());
        }

        private class MockDependency
        {
            public MockDependency(IConfiguration configuration, ILogger logger)
            {
                this.Configuration = configuration;
                this.Logger = logger;
            }

            public IConfiguration Configuration { get; }

            public ILogger Logger { get; }
        }
    }
}