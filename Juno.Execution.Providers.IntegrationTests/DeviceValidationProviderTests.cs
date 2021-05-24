using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Juno.Contracts;
using Juno.Execution.Providers.Watchdog;
using Juno.Execution.Providers.Workloads;
using Microsoft.Azure.CRC.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace Juno.Execution.Providers
{
    [TestFixture]
    [Category("Integration/Live")]
    public class DeviceValidationProviderTests
    {
        private Fixture mockFixture;
        private ExperimentComponent experimentComponent;
        private ExperimentInstance experiment;
        private ServiceCollection serviceCollection;

        [SetUp]
        public void Initialize()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.experimentComponent = this.mockFixture.Create<ExperimentComponent>();

            this.experiment = this.mockFixture.Create<ExperimentInstance>();

            ServiceCollection svc = new ServiceCollection();
            svc.AddSingleton<ExperimentComponent>(this.experimentComponent);
            svc.AddSingleton<ExperimentInstance>(this.experiment);
            this.serviceCollection = svc;
        }

        [Test]
        public async Task ProviderShouldNotFindUnknownDevice()
        {
            var provider = new DeviceValidationProvider(this.serviceCollection);
            var state = new DeviceValidationProvider.DeviceValidationProviderState()
            {
                StepInitializationTime = DateTime.UtcNow
            };
            EventContext telemetryContext = new EventContext(Guid.NewGuid());
            this.experimentComponent.Parameters.Add("deviceClass", "Win32_PnPEntity");
            this.experimentComponent.Parameters.Add("deviceName", "weird_device");
            bool deviceFound = await provider.CheckDeviceExistsAsync(this.experimentComponent, telemetryContext, new CancellationToken(false));

            // unknown device, this should return false
            Assert.IsFalse(deviceFound);
        }

        [Test]
        public async Task ProviderShouldFindKnownNetworkAdapter()
        {
            var provider = new DeviceValidationProvider(this.serviceCollection);
            var state = new DeviceValidationProvider.DeviceValidationProviderState()
            {
                StepInitializationTime = DateTime.UtcNow
            };
            EventContext telemetryContext = new EventContext(Guid.NewGuid());
            this.experimentComponent.Parameters.Add("deviceClass", "Win32_NetworkAdapter");
            this.experimentComponent.Parameters.Add("deviceName", "Bluetooth Device (Personal Area Network)");
            bool deviceFound = await provider.CheckDeviceExistsAsync(this.experimentComponent, telemetryContext, new CancellationToken(false));

            // unknown device, this should return false
            Assert.IsTrue(deviceFound);
        }
    }
}
