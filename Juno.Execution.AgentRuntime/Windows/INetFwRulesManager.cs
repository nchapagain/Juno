namespace Juno.Execution.AgentRuntime.Windows
{
    /// <summary>
    /// Interacts with the Windows Firewall rules
    /// </summary>
    public interface INetFwRulesManager
    {
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
        bool DeployRules(
            string ruleName, 
            string remotePorts, 
            string localPorts, 
            string remoteAddresses, 
            string localAddresses,
            string application,
            string directionality,
            string action);

        /// <summary>
        /// Remove all rules created by INetFwRuleManager
        /// </summary>
        /// <param name="ruleName">Name for firewall rule</param>
        /// <param name="directionality">Directionality for firewall rule</param>
        /// <param name="action">Action for the firewall rule</param>
        /// <returns>Exit code from the flash command and its output</returns>
        bool RemoveRules(string ruleName, string directionality, string action);
    }
}
