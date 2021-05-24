namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Interacts with Windows Firewall rules on the Windows Host.  Uses COM HWNetCfg. Juno.Execution.AgentRuntime is .Net Standard 2.0. which does not support
    /// dynamic (https://github.com/dotnet/runtime/issues/27541) so invoking HNetCfg with Reflectin InvokeMember.  Manager deploys rules blocking control plane
    /// access accepts for ranges needed to connect to storage nodes for virtual disks and for access to Juno Host Agent.
    /// 
    /// Ports needing to be eventually blocked for full disconnect from control plane: 
    /// Inbound: 1 to 65536 (excluding port(s) used by Juno Host Agent for inbound)
    /// Outbound 1 to 49300 and 49450 to 65535 (excluding port(s) used by Juno Host Agent for outbound)
    /// </summary>
    public class NetFwRulesManager : INetFwRulesManager
    {
        /// <summary>
        /// Bindings needed for access
        /// </summary>
        public const BindingFlags MemberAccess = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.IgnoreCase;

        private const string JunoHostAgentTipChangeIdLocation = @"%TIPCHANGEID%\Juno.HostAgent.exe";

        private const int TcpProtocol = 6;

        private const string FwRuleDirectionalityInbound = "IN";
        private const string FwRuleDirectionalityOutbound = "OUT";

        private const string FwRuleActionAllowParam = "ALLOW";
        private const string FwRuleActionBlockParam = "BLOCK";

        private const int FwRuleDirectionInbound = 1;
        private const int FwRuleDirectionOutbound = 2;

        private const int FwRuleActionBlock = 0;
        private const int FwRuleActionAllow = 1;

        private const string InterfaceTypeAll = "All";

        private const int ProfileDomain = 1;
        private const int ProfilePrivate = 2;
        private const int ProfilePublic = 4;

        private const string JunoBlockRuleDescription = "Rule created during Juno experiment by the NetFwRulesProvider.";

        private object policy;

        /// <summary>
        /// Interacts with the Windows Firewall rules
        /// </summary>
        public NetFwRulesManager(ILogger logger = null)
        {
            this.Logger = logger ?? NullLogger.Instance;

            this.policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
        }

        /// <summary>
        /// Gets the logger for capturing operation telemetry.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Configures the Windows Firewall to include rule to block ports and IPs provided
        /// </summary>
        /// <param name="ruleName">Name for firewall rule</param>
        /// <param name="remotePorts">Remote ports for firewall rule</param>
        /// <param name="localPorts">Local ports for firewall rule</param>
        /// <param name="remoteAddresses">Remote Addresses for firewall rule</param>
        /// <param name="localAddresses">Local Addresses for firewall rule</param>
        /// <param name="application">Application for firewall rule</param>
        /// <param name="directionality">Directionality for firewall rule</param>
        /// <param name="action">Action for the firewall rule</param>
        /// <returns>Exit code from the reconfig command and its output</returns>
        public bool DeployRules(
            string ruleName, 
            string remotePorts, 
            string localPorts, 
            string remoteAddresses, 
            string localAddresses,
            string application,
            string directionality,
            string action)
        {
            remotePorts.ThrowIfNull(nameof(remotePorts));
            localPorts.ThrowIfNull(nameof(localPorts));
            remoteAddresses.ThrowIfNull(nameof(remoteAddresses));
            localAddresses.ThrowIfNull(nameof(localAddresses));
            application.ThrowIfNull(nameof(application));

            ruleName.ThrowIfNullOrWhiteSpace(nameof(ruleName));
            directionality.ThrowIfNullOrWhiteSpace(nameof(directionality));
            action.ThrowIfNullOrWhiteSpace(nameof(action));

            if (string.IsNullOrEmpty(remotePorts) && string.IsNullOrEmpty(localPorts) && string.IsNullOrEmpty(remoteAddresses) &&
                (string.IsNullOrEmpty(localAddresses) && (string.IsNullOrEmpty(application))))
            {
                throw new ArgumentException("All parameters remotePorts, localPorts, remoteAddresses, localAddresses cannot be empty");
            }

            if (!string.Equals(directionality, NetFwRulesManager.FwRuleDirectionalityInbound, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(directionality, NetFwRulesManager.FwRuleDirectionalityOutbound, StringComparison.OrdinalIgnoreCase))
            {
                throw new ProviderException("Invalid directionality parameter", ErrorReason.FirewallRuleApplicationFailure);
            }

            if (!string.Equals(action, NetFwRulesManager.FwRuleActionAllowParam, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(action, NetFwRulesManager.FwRuleActionBlockParam, StringComparison.OrdinalIgnoreCase))
            {
                throw new ProviderException("Invalid action parameter", ErrorReason.FirewallRuleApplicationFailure);
            }

            // If policy rule is direction outbound action allow set al profiles for DefaultOutboundAction Block
            if (string.Equals(action, NetFwRulesManager.FwRuleActionAllowParam, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(directionality, NetFwRulesManager.FwRuleDirectionalityOutbound, StringComparison.OrdinalIgnoreCase))
            {
                // Set Domain Profile DefaultOutboundAction to Block
                this.InvokeMember("DefaultOutboundAction", this.policy, new object[] { NetFwRulesManager.ProfileDomain, NetFwRulesManager.FwRuleActionBlock }, BindingFlags.SetProperty);

                // Set Private Profile DefaultOutboundAction to Block
                this.InvokeMember("DefaultOutboundAction", this.policy, new object[] { NetFwRulesManager.ProfilePrivate, NetFwRulesManager.FwRuleActionBlock }, BindingFlags.SetProperty);

                // Set Public Profile DefaultOutboundAction to Block
                this.InvokeMember("DefaultOutboundAction", this.policy, new object[] { NetFwRulesManager.ProfilePublic, NetFwRulesManager.FwRuleActionBlock }, BindingFlags.SetProperty);
            }

            object rule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwRule", false));

            this.InvokeMember("Name", rule, new object[] { ruleName }, BindingFlags.SetProperty);
            this.InvokeMember("Description", rule, new object[] { NetFwRulesManager.JunoBlockRuleDescription }, BindingFlags.SetProperty);

            if (string.Equals(directionality, NetFwRulesManager.FwRuleDirectionalityInbound, StringComparison.OrdinalIgnoreCase))
            {
                this.InvokeMember("Direction", rule, new object[1] { NetFwRulesManager.FwRuleDirectionInbound }, BindingFlags.SetProperty);
            }
            else
            {
                this.InvokeMember("Direction", rule, new object[1] { NetFwRulesManager.FwRuleDirectionOutbound }, BindingFlags.SetProperty);
            }

            this.InvokeMember("Enabled", rule, new object[1] { true }, BindingFlags.SetProperty);
            this.InvokeMember("InterfaceTypes", rule, new object[1] { NetFwRulesManager.InterfaceTypeAll }, BindingFlags.SetProperty);
            this.InvokeMember("Protocol", rule, new object[1] { NetFwRulesManager.TcpProtocol }, BindingFlags.SetProperty);

            if (!string.IsNullOrWhiteSpace(remotePorts))
            {
                this.InvokeMember("RemotePorts", rule, new object[] { remotePorts }, BindingFlags.SetProperty);
            }

            if (!string.IsNullOrWhiteSpace(localPorts))
            {
                this.InvokeMember("LocalPorts", rule, new object[] { localPorts }, BindingFlags.SetProperty);
            }

            if (!string.IsNullOrWhiteSpace(remoteAddresses))
            {
                this.InvokeMember("RemoteAddresses", rule, new object[] { remoteAddresses }, BindingFlags.SetProperty);
            }

            if (!string.IsNullOrWhiteSpace(localAddresses))
            {
                this.InvokeMember("LocalAddresses", rule, new object[] { localAddresses }, BindingFlags.SetProperty);
            }
            
            if (!string.IsNullOrWhiteSpace(application))
            {
                if (string.Equals(application, NetFwRulesManager.JunoHostAgentTipChangeIdLocation, StringComparison.OrdinalIgnoreCase))
                {
                    this.InvokeMember("Applicationname", rule, new object[] { Environment.CurrentDirectory + @"\Juno.HostAgent.exe" }, BindingFlags.SetProperty);
                }
                else
                {
                    this.InvokeMember("Applicationname", rule, new object[] { application }, BindingFlags.SetProperty);
                }
            }

            if (string.Equals(action, NetFwRulesManager.FwRuleActionBlockParam, StringComparison.OrdinalIgnoreCase))
            {
                this.InvokeMember("Action", rule, new object[1] { NetFwRulesManager.FwRuleActionBlock }, BindingFlags.SetProperty);
            }
            else
            {
                this.InvokeMember("Action", rule, new object[1] { NetFwRulesManager.FwRuleActionAllow }, BindingFlags.SetProperty);
            }       
            
            object rules = this.InvokeMember("Rules", this.policy, null, BindingFlags.GetProperty);
            
            this.InvokeMember("Add", rules, new object[1] { rule }, BindingFlags.InvokeMethod);
                
            // TO DO:
            return true;
        }

        /// <summary>
        /// Remove all rules created by INetFwRuleManager
        /// </summary>
        /// <param name="ruleName">Name for firewall rule</param>
        /// <param name="directionality">Directionality for firewall rule</param>
        /// <param name="action">Action for the firewall rule</param>
        /// <returns>Exist code from the flash command and its output</returns>
        public bool RemoveRules(string ruleName, string directionality, string action)
        {
            directionality.ThrowIfNullOrWhiteSpace(nameof(directionality));
            action.ThrowIfNullOrWhiteSpace(nameof(action));

            // if pollicy rule was direction outbound and action allow set all profiles back to DefaultOutboundActin Allow
            if (string.Equals(action, NetFwRulesManager.FwRuleActionAllowParam, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(directionality, NetFwRulesManager.FwRuleDirectionalityOutbound, StringComparison.OrdinalIgnoreCase))
            {
                // Set Domain Profile DefaultOutboundAction to Block
                this.InvokeMember("DefaultOutboundAction", this.policy, new object[] { NetFwRulesManager.ProfileDomain, NetFwRulesManager.FwRuleActionAllow }, BindingFlags.SetProperty);

                // Set Private Profile DefaultOutboundAction to Block
                this.InvokeMember("DefaultOutboundAction", this.policy, new object[] { NetFwRulesManager.ProfilePrivate, NetFwRulesManager.FwRuleActionAllow }, BindingFlags.SetProperty);

                // Set Public Profile DefaultOutboundAction to Block
                this.InvokeMember("DefaultOutboundAction", this.policy, new object[] { NetFwRulesManager.ProfilePublic, NetFwRulesManager.FwRuleActionAllow }, BindingFlags.SetProperty);
            }

            object rules = this.InvokeMember("Rules", this.policy, null, BindingFlags.GetProperty);

            this.InvokeMember("Remove", rules, new object[] { ruleName }, BindingFlags.InvokeMethod);

            // TO DO:
            return true;
        }

        /// <summary>
        /// Invokes member property/methods on object provided
        /// </summary>
        /// <param name="propertyName">property name to set</param>
        /// <param name="objectToSet">object who's value is to be set</param>
        /// <param name="propertyValue">value to set on property</param>
        /// <param name="bindingFlag">BindingFlag for the invoke</param>
        protected virtual object InvokeMember(string propertyName, object objectToSet, object[] propertyValue, BindingFlags bindingFlag)
        {
            EventContext telemetryContext = EventContext.Persisted();

            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("Missing or empty parameter name", nameof(propertyName));
            }

            if (objectToSet == null)
            {
                throw new ArgumentNullException(nameof(objectToSet));
            }

            object invokedMember = null;

            this.Logger.LogTelemetry($"{nameof(NetFwRulesManager)}.InvokeMember", telemetryContext, () =>
            {
                invokedMember = objectToSet.GetType().InvokeMember(propertyName, NetFwRulesManager.MemberAccess | bindingFlag, null, objectToSet, propertyValue);
            });

            return invokedMember;
        }
    }
}
