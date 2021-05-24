using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Juno.Contracts;
using Juno.Execution.AgentRuntime;
using Juno.Providers;
using Microsoft.Azure.CRC.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace Juno.Execution.Providers.Watchdog
{
    [TestFixture]
    [Category("Unit")]
    public class DeviceValidationProviderTests
    {
        private Fixture mockFixture;
        private ExperimentComponent experimentComponent;
        private ExperimentContext experimentContext;
        private ExperimentInstance experiment;

        [SetUp]
        public void Initialize()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.experimentComponent = this.mockFixture.Create<ExperimentComponent>();

            this.experiment = this.mockFixture.Create<ExperimentInstance>();

            var mockConfiguration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    @"Configuration"))
                .AddJsonFile($"juno-dev01.environmentsettings.json")
                .Build();

            this.experimentContext = new ExperimentContext(
                this.mockFixture.Create<ExperimentInstance>(),
                this.mockFixture.Create<ExperimentStepInstance>(),
                mockConfiguration);
        }

        [Test]
        public void ProviderShouldCalculateTimeoutFromParameters()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);

            ServiceCollection svc = new ServiceCollection();
            svc.AddSingleton<ExperimentComponent>(this.experimentComponent);
            svc.AddSingleton<ExperimentInstance>(this.experiment);

            var provider = new DeviceValidationProvider(svc);
            var state = new DeviceValidationProvider.DeviceValidationProviderState()
            {
                StepInitializationTime = DateTime.UtcNow
            };

            bool timedOut = provider.HasStepTimedOut(this.experimentComponent, state);
            // default timeout is 10 min so this should return false
            Assert.IsFalse(timedOut);

            state.StepInitializationTime = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));
            timedOut = provider.HasStepTimedOut(this.experimentComponent, state);
            // default timeout is 10 min so given start time is 1 hr ago, we should return true
            Assert.IsTrue(timedOut);

            state.StepInitializationTime = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(30));
            this.experimentComponent.Parameters.Add("timeout", "0:00:00:01");
            timedOut = provider.HasStepTimedOut(this.experimentComponent, state);
            // requested timeout is 1 sec and we started 30s ago, so it should return true
            Assert.IsTrue(timedOut);
        }

        [Test]
        public async Task ProviderShouldFindExpectedDeviceIfExistInDeviceList()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            var mockPropReader = new Mock<ISystemPropertyReader>();
            mockPropReader.Setup(s => s.ReadDevices("foobar")).Returns(new List<string>() { "foo", "bar" });

            var mockDataClient = new Mock<IProviderDataClient>();
            mockDataClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));
            ServiceCollection svc = new ServiceCollection();
            svc.AddSingleton<ExperimentComponent>(this.experimentComponent);
            svc.AddSingleton<ExperimentInstance>(this.experiment);
            svc.AddSingleton<IProviderDataClient>(mockDataClient.Object);

            var provider = new DeviceValidationProvider(svc, mockPropReader.Object);
           
            this.experimentComponent.Parameters.Add("deviceClass", "foobar");
            this.experimentComponent.Parameters.Add("deviceName", "bar");

            var result = await provider.ExecuteAsync(this.experimentContext, this.experimentComponent, CancellationToken.None);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public async Task ProviderShouldNotFindExpectedDeviceIfDoesNotExistInDeviceList()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            var mockPropReader = new Mock<ISystemPropertyReader>();
            mockPropReader.Setup(s => s.ReadDevices("foobar")).Returns(new List<string>() { "foo", "bar" });

            var mockDataClient = new Mock<IProviderDataClient>();
            mockDataClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));
            ServiceCollection svc = new ServiceCollection();
            svc.AddSingleton<ExperimentComponent>(this.experimentComponent);
            svc.AddSingleton<ExperimentInstance>(this.experiment);
            svc.AddSingleton<IProviderDataClient>(mockDataClient.Object);

            var provider = new DeviceValidationProvider(svc, mockPropReader.Object);

            this.experimentComponent.Parameters.Add("deviceClass", "foobar");
            this.experimentComponent.Parameters.Add("deviceName", "paris");

            var result = await provider.ExecuteAsync(this.experimentContext, this.experimentComponent, CancellationToken.None);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
        }
    }
}
