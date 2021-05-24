namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class MicrocodeActivationProviderTests
    {
        private Mock<IProviderDataClient> mockDataClient;
        private Mock<IPayloadActivator> mockPayloadActivator;
        private ActivationResult testActivationResult;
        private ProviderFixture mockFixture;
        private TestMicrocodeActivationProvider provider;
        private MicrocodeActivationProvider.MicrocodeActivationProviderState mockState;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(MicrocodeActivationProvider));
            this.mockFixture.SetupExperimentMocks();

            // A valid experiment component definition for the provider.
            this.mockFixture.Component = new ExperimentComponent(
                typeof(MicrocodeActivationProvider).FullName,
                "Apply Microcode for",
                "Intel Gen6 CPU",
                "Group B",
                parameters: new Dictionary<string, IConvertible>
                {
                    // The following parameters are required by the provider.
                    ["microcodeVersion"] = "b000039"
                });

            this.testActivationResult = new ActivationResult(true, DateTime.UtcNow);
            this.mockPayloadActivator = new Mock<IPayloadActivator>();

            this.mockDataClient = new Mock<IProviderDataClient>();
            this.mockFixture.Services = new ServiceCollection()
                .AddSingleton<IPayloadActivator>(this.mockPayloadActivator.Object)
                .AddSingleton<IProviderDataClient>(this.mockDataClient.Object);

            this.provider = new TestMicrocodeActivationProvider(this.mockFixture.Services);
            this.mockPayloadActivator.Setup(r => r.ActivateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(this.testActivationResult);

            this.mockState = new MicrocodeActivationProvider.MicrocodeActivationProviderState();
        }

        [Test]
        public void MicrocodeActivationProviderValidatesRequiredParametersAreProvidedForTheExperiment()
        {
            // Invalidate the definition by removing required parameters
            this.mockFixture.Component.Parameters.Clear();

            ExecutionResult result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
        }

        [Test]
        public void MicrocodeActivationProviderMaintainsStateInItsOwnIndividualStateObject()
        {
            ExecutionResult result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockDataClient.Verify(client => client.GetOrCreateStateAsync<MicrocodeActivationProvider.MicrocodeActivationProviderState>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.Is<string>(stateId => !string.IsNullOrEmpty(stateId))));

            this.mockDataClient.Verify(client => client.SaveStateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<MicrocodeActivationProvider.MicrocodeActivationProviderState>(),
                It.IsAny<CancellationToken>(),
                It.Is<string>(stateId => !string.IsNullOrEmpty(stateId))));
        }

        [Test]
        public void MicrocodeActivationProviderHandlesCancellationBeforeAttemptToRequestMicrocodeActivate()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                MicrocodeActivationProviderTests.CancelExecution(tokenSource);
                ExecutionResult result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, tokenSource.Token)
                    .GetAwaiter().GetResult();

                Assert.IsNotNull(result);
                Assert.IsTrue(result.Status == ExecutionStatus.Cancelled);
            }
        }

        [Test]
        public void MicrocodeActivationProviderToVerifyTheMicrocodeWasActuallyApplied()
        {
            ExecutionResult result = this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public void MicrocodeActivationProviderSupportsFeatureFlags()
        {
            string expectedFlag = "AnyFeatureFlag";
            this.mockFixture.Component.Parameters[StepParameters.FeatureFlag] = expectedFlag;

            Assert.IsTrue(this.provider.HasFeatureFlag(this.mockFixture.Component, expectedFlag));
            Assert.DoesNotThrow(() => this.provider.ValidateParameters(this.mockFixture.Component));
        }

        private static void CancelExecution(CancellationTokenSource tokenSource)
        {
            try
            {
                tokenSource.Cancel();
            }
            catch
            {
            }
        }

        private void SetupMockDefaults()
        {
            this.mockDataClient.OnGetState<MicrocodeActivationProvider.MicrocodeActivationProviderState>()
                .Returns(Task.FromResult(this.mockState));

            this.mockDataClient.OnSaveState<MicrocodeActivationProvider.MicrocodeActivationProviderState>()
                .Returns(Task.CompletedTask);
        }

        private class TestMicrocodeActivationProvider : MicrocodeActivationProvider
        {
            public TestMicrocodeActivationProvider(IServiceCollection services)
                : base(services)
            {
            }

            public new void ValidateParameters(ExperimentComponent component)
            {
                base.ValidateParameters(component);
            }
        }
    }
}
