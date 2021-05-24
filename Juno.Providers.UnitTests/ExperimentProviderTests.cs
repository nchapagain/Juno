namespace Juno.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentProviderTests
    {
        private ProviderFixture mockFixture;
        private TestExperimentProvider provider;
        private IServiceCollection services;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(TestExperimentProvider));
            this.services = new ServiceCollection();
            this.provider = new TestExperimentProvider(this.services);
        }

        [Test]
        public async Task ExperimentProviderReturnsTheExpectedExecutionResult()
        {
            ExecutionResult expectedResult = new ExecutionResult(ExecutionStatus.InProgress);
            this.provider.OnExecuteAsync = (context, component, telemetryContext, token) => expectedResult;

            ExecutionResult actualResult = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(actualResult);
            Assert.IsNull(actualResult.Error);
            Assert.AreEqual(expectedResult.Status, actualResult.Status);
        }

        [Test]
        public async Task ExperimentProviderReturnsTheExpectedExecutionResultWhenCancelled()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                tokenSource.Cancel();
                ExecutionResult actualResult = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, tokenSource.Token);

                Assert.IsNotNull(actualResult);
                Assert.IsNull(actualResult.Error);
                Assert.IsTrue(actualResult.Status == ExecutionStatus.Cancelled);
            }
        }

        [Test]
        public async Task ExperimentProviderReturnsTheExpectedExecutionResultWhenExceptionsOccur()
        {
            ProviderException expectedError = new ProviderException(ErrorReason.ProviderDefinitionInvalid);
            this.provider.OnExecuteAsync = (context, component, telemetryContext, token) => throw expectedError;

            ExecutionResult actualResult = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(actualResult);
            Assert.IsNotNull(actualResult.Error);
            Assert.IsTrue(object.ReferenceEquals(expectedError, actualResult.Error));
            Assert.IsTrue(actualResult.Status == ExecutionStatus.Failed);
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
        private class TestExperimentProvider : ExperimentProvider
        {
            public TestExperimentProvider(IServiceCollection services)
                : base(services)
            {
            }

            public Func<ExperimentContext, ExperimentComponent, EventContext, CancellationToken, ExecutionResult> OnExecuteAsync { get; set; }

            protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                return this.OnExecuteAsync != null
                    ? Task.FromResult(this.OnExecuteAsync.Invoke(context, component, telemetryContext, cancellationToken))
                    : Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
            }
        }
    }
}
