namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class NetFwRulesProviderTests
    {
        private ProviderFixture mockFixture;
        private NetFwRulesProvider.NetFwRuleProviderState providerState;
        private Mock<INetFwRulesManager> netFwRulesManager;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(NetFwRulesProvider));
            this.providerState = new NetFwRulesProvider.NetFwRuleProviderState();
            this.netFwRulesManager = new Mock<INetFwRulesManager>();

            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "ruleName",  "Inbound restrict port 17000 from 192.168.0.4" },
                { "remotePorts", "17000" },
                { "remoteIPs", "192.168.0.4" },
                { "directionality", "IN" },
                { "action", "BLOCK" },
                { "ruleDuration", "00:00:00.001" },
            });

            this.mockFixture.Services.AddSingleton<INetFwRulesManager>(this.netFwRulesManager.Object);

            this.SetupMockDefaults();
        }

        [Test]
        public void ProviderReturnsTheExpectedResultWhenItFailsToApplyTheFirewallRules()
        {
            this.providerState = null;
            this.mockFixture.DataClient.OnGetState<NetFwRulesProvider.NetFwRuleProviderState>()
                .Returns(Task.FromResult(this.providerState));

            this.netFwRulesManager.Setup(s => s.DeployRules(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(),
                It.IsAny<string>(), 
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>())).Returns(false);

            NetFwRulesProvider provider = new NetFwRulesProvider(this.mockFixture.Services);

            ExecutionResult result = provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);

            this.netFwRulesManager.Verify(s => s.DeployRules(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
            this.netFwRulesManager.Verify(s => s.RemoveRules(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void ProviderReturnsTheExpectedResultWhenItSuccessfullyAppliesTheFirewallRules()
        {
            this.providerState = null;

            this.mockFixture.DataClient.OnGetState<NetFwRulesProvider.NetFwRuleProviderState>()
                .Returns(Task.FromResult(this.providerState));

            this.netFwRulesManager.Setup(s => s.DeployRules(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>())).Returns(true);
            this.netFwRulesManager.Setup(s => s.RemoveRules(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>())).Returns(true);

            NetFwRulesProvider provider = new NetFwRulesProvider(this.mockFixture.Services);

            ExecutionResult result = provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgressContinue);
            Assert.IsTrue(this.providerState.RuleDeployed);

            // Wait for time specified in duration parameter
            Thread.Sleep(TimeSpan.FromMilliseconds(1));

            this.mockFixture.DataClient.OnGetState<NetFwRulesProvider.NetFwRuleProviderState>()
                .Returns(Task.FromResult(this.providerState));
            result = provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
            Assert.IsFalse(this.providerState.RuleDeployed);
        }

        private void SetupMockDefaults()
        {
            this.mockFixture.DataClient.Setup(c => c.SaveStateAsync<NetFwRulesProvider.NetFwRuleProviderState>(
               It.IsAny<string>(),
               It.IsAny<string>(),
               It.IsAny<NetFwRulesProvider.NetFwRuleProviderState>(),
               It.IsAny<CancellationToken>(),
               It.IsAny<string>()))
               .Callback<string, string, NetFwRulesProvider.NetFwRuleProviderState, CancellationToken, string>((experimentId, key, state, cancelationToken, stateId) =>
               {
                   this.providerState = state;
               });
        }
    }
}
