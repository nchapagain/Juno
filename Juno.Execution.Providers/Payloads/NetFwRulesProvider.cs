namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime.Windows;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;

    /// <summary>
    /// Provider that reconfigures windows firewall rules on a host
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [ProviderInfo(Name = "Configure Windows Firewall Rules", Description = "Configures Windows Firewall Rules on nodes/blades in the experiment group", FullDescription = "Step to configure Windows Firewall Rules on nodes/blades in the experiment group.")]
    [SupportedParameter(Name = Parameters.RuleName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.RemotePorts, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.LocalPorts, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.RemoteIPs, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.LocalIPs, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.Application, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.Directionality, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.Action, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.RuleDuration, Type = typeof(string), Required = true)]
    public class NetFwRulesProvider : ExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NetFwRulesProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public NetFwRulesProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            INetFwRulesManager netFwManager;
            if (!this.Services.TryGetService<INetFwRulesManager>(out netFwManager))
            {
                this.Services.AddTransient<INetFwRulesManager>((provider) => new NetFwRulesManager(this.Logger));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgressContinue);

            NetFwRuleProviderState state = await this.GetStateAsync<NetFwRuleProviderState>(context, cancellationToken, sharedState: false).ConfigureDefaults()
                 ?? new NetFwRuleProviderState()
                 {
                     RuleDeployed = false,
                     RuleExpirationTime = DateTime.UtcNow.Add(TimeSpan.Parse(component.Parameters.GetValue<string>(Parameters.RuleDuration)))
                 };

            if (!state.RuleDeployed && !state.IsRuleDurationExpired)
            {
                // Fresh run, deploy firewall rule
                result = await this.ApplyFirewallRule(context, component, telemetryContext, state, cancellationToken)
                    .ConfigureDefaults();
            }
            else if (state.RuleDeployed && state.IsRuleDurationExpired)
            {
                telemetryContext.AddContext("ruleExpirationTime", state.RuleExpirationTime);
                telemetryContext.AddContext("isRuleDurationExpired", true);
                // Rule time has expired, remove rule and mark as succeeded
                result = await this.RemoveFirewallRule(context, component, telemetryContext, state, cancellationToken)
                    .ConfigureDefaults();
            }

            return result;
        }

        [SuppressMessage("Naming", "AZCA1002:AsyncMethodNaming Rule", Justification = "Partner team preference")]
        private async Task<ExecutionResult> ApplyFirewallRule(
            ExperimentContext context,
            ExperimentComponent component,
            EventContext telemetryContext,
            NetFwRuleProviderState state,
            CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult executionResult = new ExecutionResult(ExecutionStatus.InProgressContinue);
            if (!cancellationToken.IsCancellationRequested)
            {
                EventContext relatedContext = telemetryContext.Clone(withProperties: true);

                await this.Logger.LogTelemetryAsync($"{nameof(NetFwRulesProvider)}.StartDeployingFirewallRules", relatedContext, async () =>
                {
                    if (this.DeployFirewallRules(component))
                    {
                        state.RuleDeployed = true;
                        state.RuleExpirationTime = DateTime.UtcNow.Add(TimeSpan.Parse(component.Parameters.GetValue<string>(Parameters.RuleDuration)));

                        await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
                    }
                    else
                    {
                        throw new ProviderException("Unable to create windows firewall rule", ErrorReason.FirewallRuleApplicationFailure);
                    }
                }).ConfigureDefaults();
            }

            return executionResult;
        }

        [SuppressMessage("Naming", "AZCA1002:AsyncMethodNaming Rule", Justification = "Partner team preference")]
        private async Task<ExecutionResult> RemoveFirewallRule(
            ExperimentContext context,
            ExperimentComponent component,
            EventContext telemetryContext,
            NetFwRuleProviderState state,
            CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult executionResult = new ExecutionResult(ExecutionStatus.InProgressContinue);
            if (!cancellationToken.IsCancellationRequested)
            {
                EventContext relatedContext = telemetryContext.Clone(withProperties: true);
                await this.Logger.LogTelemetryAsync($"{nameof(NetFwRulesProvider)}.RemoveFirewallRule", relatedContext, async () =>
                {
                    if (this.RemoveFirewallRules(component))
                    {
                        state.RuleDeployed = false;

                        executionResult = new ExecutionResult(ExecutionStatus.Succeeded);

                        await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
                    }
                    else
                    {
                        throw new ProviderException("Unable to remove windows firewall rules.", ErrorReason.FirewallRuleRemovalFailure);
                    }
                }).ConfigureDefaults();
            }

            return executionResult;
        }

        private bool DeployFirewallRules(ExperimentComponent component)
        {
            var netFwRulesManager = this.Services.GetService<INetFwRulesManager>();

            return netFwRulesManager.DeployRules(
                component.Parameters.GetValue<string>(Parameters.RuleName),
                component.Parameters.GetValue<string>(Parameters.RemotePorts, string.Empty),
                component.Parameters.GetValue<string>(Parameters.LocalPorts, string.Empty),
                component.Parameters.GetValue<string>(Parameters.RemoteIPs, string.Empty),
                component.Parameters.GetValue<string>(Parameters.LocalIPs, string.Empty),
                component.Parameters.GetValue<string>(Parameters.Application, string.Empty),
                component.Parameters.GetValue<string>(Parameters.Directionality),
                component.Parameters.GetValue<string>(Parameters.Action));
        }

        private bool RemoveFirewallRules(ExperimentComponent component)
        {
            var netFwRuleManager = this.Services.GetService<INetFwRulesManager>();

            return netFwRuleManager.RemoveRules(
                component.Parameters.GetValue<string>(Parameters.RuleName),
                component.Parameters.GetValue<string>(Parameters.Directionality),
                component.Parameters.GetValue<string>(Parameters.Action));
        }

        private class Parameters
        {
            internal const string RuleName = "ruleName";
            internal const string RemotePorts = "remotePorts";
            internal const string LocalPorts = "localPorts";
            internal const string RemoteIPs = "remoteIPs";
            internal const string LocalIPs = "localIPs";
            internal const string Application = "application";
            internal const string Directionality = "directionality";
            internal const string Action = "action";
            internal const string RuleDuration = "ruleDuration";
        }

        internal class NetFwRuleProviderState
        {
            public bool RuleDeployed { get; set; }

            public DateTime RuleExpirationTime { get; set; }

            [JsonIgnore]
            public bool IsRuleDurationExpired => DateTime.UtcNow > this.RuleExpirationTime;
        }
    }
}
