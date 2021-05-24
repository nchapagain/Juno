namespace Juno.Execution.Providers.Verification
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Contract;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SsdVerificationProviderTests
    {
        private const string FirmwareVersion = nameof(SsdVerificationProviderTests.FirmwareVersion);
        private const string TargetModel = nameof(SsdVerificationProviderTests.TargetModel);
        private ProviderFixture fixture;
        private SsdVerificationProvider provider;
        private Mock<IFirmwareReader<IEnumerable<SsdInfo>>> mockReader;

        [OneTimeSetUp]
        public void SetupTests()
        {
            this.fixture = new ProviderFixture(typeof(SsdVerificationProvider));
            this.mockReader = new Mock<IFirmwareReader<IEnumerable<SsdInfo>>>();
            this.fixture.Services.AddSingleton<IFirmwareReader<IEnumerable<SsdInfo>>>(this.mockReader.Object);

            this.provider = new SsdVerificationProvider(this.fixture.Services);
            this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component).GetAwaiter().GetResult();
            this.fixture.Component.Parameters.Add(SsdVerificationProviderTests.FirmwareVersion, Guid.NewGuid().ToString());
            this.fixture.Component.Parameters.Add(SsdVerificationProviderTests.TargetModel, Guid.NewGuid().ToString());
        }

        [SetUp]
        public void SetupDefaultMockBehavior()
        {
            this.mockReader.Setup(r => r.Read())
                .Returns(new List<SsdInfo>() { this.CreateMockSsdInfo() });
            this.fixture.Component.Parameters[SsdVerificationProviderTests.FirmwareVersion] = Guid.NewGuid().ToString();
            this.fixture.Component.Parameters[SsdVerificationProviderTests.TargetModel] = Guid.NewGuid().ToString();
        }

        [Test]
        public void ExecuteAsyncReturnsExpectedResultWhenFirmwareVersionsAreTheSameOnTargetModel()
        {
            string targetModel = this.fixture.Component.Parameters[SsdVerificationProviderTests.TargetModel].ToString();
            string expectedFirmware = this.fixture.Component.Parameters[SsdVerificationProviderTests.FirmwareVersion].ToString();
            this.mockReader.Setup(r => r.Read())
                .Returns(new List<SsdInfo>() { this.CreateMockSsdInfo(targetModel, expectedFirmware) });

            ExecutionResult result = this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }

        [Test]
        public void ExecuteAsyncReturnsExpectedResultWhenFirmwareVersionsAreDifferentOnTargetModel()
        {
            string targetModel = this.fixture.Component.Parameters[SsdVerificationProviderTests.TargetModel].ToString();
            this.mockReader.Setup(r => r.Read())
                .Returns(new List<SsdInfo>() { this.CreateMockSsdInfo(targetModel, Guid.NewGuid().ToString()) });

            ExecutionResult result = this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
        }

        [Test]
        public void ExecuteAsyncThrowsErrorWhenNoDevicesAreOfTheDesiredModel()
        {
            ExecutionResult result = this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsInstanceOf<ArgumentException>(result.Error);
        }

        [Test]
        public void ExecuteAsyncReturnsExpectedResultWhenMultipleFirmwaresAndModelsArePresent()
        {
            string targetModel = this.fixture.Component.Parameters[SsdVerificationProviderTests.TargetModel].ToString();
            string expectedFirmware = this.fixture.Component.Parameters[SsdVerificationProviderTests.FirmwareVersion].ToString();
            this.mockReader.Setup(r => r.Read())
                .Returns(new List<SsdInfo>() { this.CreateMockSsdInfo(targetModel, expectedFirmware), this.CreateMockSsdInfo() });

            ExecutionResult result = this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResultWhenMultipleModelsAreGiven()
        {
            List<string> expectedModels = new List<string>() { "Model1", "Model2" };
            string expectedFirmware = this.fixture.Component.Parameters[SsdVerificationProviderTests.FirmwareVersion].ToString();
            this.fixture.Component.Parameters[SsdVerificationProviderTests.TargetModel] = string.Join(", ", expectedModels);

            List<SsdInfo> info = new List<SsdInfo>() { this.CreateMockSsdInfo(expectedModels[0], expectedFirmware), this.CreateMockSsdInfo(expectedModels[1], expectedFirmware) };
            this.mockReader.Setup(r => r.Read())
                .Returns(info);

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResultWhenMultipleModelsAreGivenAndOneDoesNotHaveFirmware()
        {
            List<string> expectedModels = new List<string>() { "Model1", "Model2" };
            string expectedFirmware = this.fixture.Component.Parameters[SsdVerificationProviderTests.FirmwareVersion].ToString();
            this.fixture.Component.Parameters[SsdVerificationProviderTests.TargetModel] = string.Join(", ", expectedModels);

            List<SsdInfo> info = new List<SsdInfo>() { this.CreateMockSsdInfo(expectedModels[0], expectedFirmware), this.CreateMockSsdInfo(expectedModels[1], Guid.NewGuid().ToString()) };
            this.mockReader.Setup(r => r.Read())
                .Returns(info);

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsInstanceOf<ProviderException>(result.Error);
        }

        [Test]
        public async Task ExecuteAsyncReturnsExpectedResultWhenMultipleModelsAreGivenAndAllDoNotHaveFirmware()
        {
            List<string> expectedModels = new List<string>() { "Model1", "Model2" };
            string expectedFirmware = this.fixture.Component.Parameters[SsdVerificationProviderTests.FirmwareVersion].ToString();
            this.fixture.Component.Parameters[SsdVerificationProviderTests.TargetModel] = string.Join(", ", expectedModels);

            List<SsdInfo> info = new List<SsdInfo>() { this.CreateMockSsdInfo(expectedModels[0], Guid.NewGuid().ToString()), this.CreateMockSsdInfo(expectedModels[1], Guid.NewGuid().ToString()) };
            this.mockReader.Setup(r => r.Read())
                .Returns(info);

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsInstanceOf<ProviderException>(result.Error);
        }

        [Test]
        public async Task ExecuteAsyncIgnoresModelsNotOfInterest()
        {
            List<string> expectedModels = new List<string>() { "Model1", "Model2" };
            string expectedFirmware = this.fixture.Component.Parameters[SsdVerificationProviderTests.FirmwareVersion].ToString();
            this.fixture.Component.Parameters[SsdVerificationProviderTests.TargetModel] = string.Join(", ", expectedModels);

            List<SsdInfo> info = new List<SsdInfo>() 
            { 
                this.CreateMockSsdInfo(expectedModels[0], expectedFirmware),
                this.CreateMockSsdInfo(expectedModels[1], expectedFirmware),
                this.CreateMockSsdInfo(Guid.NewGuid().ToString(), Guid.NewGuid().ToString())
            };
            this.mockReader.Setup(r => r.Read())
                .Returns(info);

            ExecutionResult result = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }

        private SsdInfo CreateMockSsdInfo(string modelNumber = null, string firmwareVersion = null)
        {
            return new SsdInfo(
                new SsdDrive("name", "type", "protocol"),
                new SsdHealth("passed"),
                modelNumber ?? "modelOne",
                firmwareVersion ?? "firmwareVersion",
                "serialNumber");
        }
    }
}
