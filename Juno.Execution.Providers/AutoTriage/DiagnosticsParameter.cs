namespace Juno.Execution.Providers.AutoTriage
{
    /// <summary>
    /// Hold all context parameter name that can be used across providers and diagnostics framework
    /// </summary>
    public static class DiagnosticsParameter
    {
        /// <summary>
        /// Specifies a subscription id.
        /// </summary>
        public const string SubscriptionId = "subscriptionId";

        /// <summary>
        /// Specifies a resource group name.
        /// </summary>
        public const string ResourceGroupName = "resourceGroupName";

        /// <summary>
        /// Specifies a TIP node id.
        /// </summary>
        public const string TipNodeId = "tipNodeId";

        /// <summary>
        /// Specifies a TIP session id.
        /// </summary>
        public const string TipSessionId = "tipSessionId";

        /// <summary>
        /// Specifies a TIP session change id.
        /// </summary>
        public const string TipSessionChangeId = "tipSessionChangeId";

        /// <summary>
        /// Specifies a time range begin for diagnostics.
        /// </summary>
        public const string TimeRangeEnd = "timeRangeEnd";

        /// <summary>
        /// Specifies a time range end for diagnostics.
        /// </summary>
        public const string TimeRangeBegin = "timeRangeBegin";

        /// <summary>
        /// Specifies an experiment id.
        /// </summary>
        public const string ExperimentId = "experimentId";

        /// <summary>
        /// Specifies a provider name who requested for diagnostics.
        /// </summary>
        public const string ProviderName = "providerName";
    }
}