namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.Providers.Payloads;
    using Moq;
    using NUnit.Framework;
    using State = AgentExecuteCommandProvider.State;

    [TestFixture]
    [Category("Integration")]
    public class AgentExecuteCommandProviderTests
    {
        private static State providerState;
        private AgentExecuteCommandProvider provider;
        private ProviderFixture fixture;

        [OneTimeSetUp]
        public void SetupTests()
        {
            this.fixture = new ProviderFixture(typeof(AgentExecuteCommandProvider));
            this.fixture.DataClient.Setup(dc => dc.GetOrCreateStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(() => AgentExecuteCommandProviderTests.providerState);
            this.fixture.DataClient.Setup(dc => dc.SaveStateAsync<State>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<State>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .Callback<string, string, State, CancellationToken, string>((id, key, state, token, shared) =>
                {
                    AgentExecuteCommandProviderTests.providerState = state;
                }).Returns(Task.CompletedTask);

            this.provider = new AgentExecuteCommandProvider(this.fixture.Services);
            this.fixture.Component.Parameters.Add("ConfigurationId", string.Empty);
            this.fixture.Component.Parameters.Add("Timeout", "00:01:00");
        }

        [Test]
        public async Task ExecuteAsyncPerformsSuccessfulExecutionOfCommand()
        {
            this.fixture.Component.Parameters["ConfigurationId"] = "SsdDowngrade";
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);

            // 1. Start at start state and move to in-progress state
            ExecutionResult resultOne = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None);
            Assert.IsNotNull(resultOne);
            Assert.AreEqual(ExecutionStatus.InProgress, resultOne.Status);

            // 2. Move from In Progress state, back to in -progress state if task is not completed.
            ExecutionResult blockingResult = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None);

            while (blockingResult.Status == ExecutionStatus.InProgress)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                blockingResult = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None);
            }

            // 3. Once here, in an accept state, should be Success.
            Assert.AreEqual(ExecutionStatus.Succeeded, blockingResult.Status);
        }

        [Test]
        public async Task ExecuteAsyncperformsSuccessfulExecutionWhenUnderlyingProcessReturnsNonZeroExitCode()
        {
            this.fixture.Component.Parameters["ConfigurationId"] = "SsdUpgrade";
            await this.provider.ConfigureServicesAsync(this.fixture.Context, this.fixture.Component);

            // 1. Start at start state and move to in-progress state
            ExecutionResult resultOne = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None);
            Assert.IsNotNull(resultOne);
            Assert.AreEqual(ExecutionStatus.InProgress, resultOne.Status);

            // 2. Move from In Progress state, back to in -progress state if task is not completed.
            ExecutionResult blockingResult = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None);

            while (blockingResult.Status == ExecutionStatus.InProgress)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                blockingResult = await this.provider.ExecuteAsync(this.fixture.Context, this.fixture.Component, CancellationToken.None);
            }

            // 3. Once here, in an accept state, should be Failed.
            Assert.AreEqual(ExecutionStatus.Failed, blockingResult.Status);
        }
    }
}
