namespace Juno.Execution.TipIntegration
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using TipGateway.Entities;

    /// <summary>
    /// Class to define TIP parameters
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Json converter needs to be able to set.")]
    public class TipParameters
    {
        /// <summary>
        /// Whether require manual TIP session removal.
        /// </summary>
        public static bool RequireManualRemoval => true;

        /// <summary>
        /// Get or set clusterName
        /// </summary>
        /// <example>yto22prdapp02</example>
        public string ClusterName { get; set; }

        /// <summary>
        /// List of target Machine pool names. This is needed for targeting nodes that aren't otherwise
        /// available to allocator. (ex: decomm machine pools, add-rack etc.)
        /// </summary>
        /// <example>["bnz14prdapp10mp1"]</example>
        public List<string> MachinePoolNames { get; set; }

        /// <summary>
        /// NodeCount in int.
        /// </summary>
        public int NodeCount { get; set; }

        /// <summary>
        /// Get or set list of CandidateNode Ids.
        /// </summary>
        /// <example>["a8cbc655-3e9d-4634-8ee3-d63a30b36750","aaa05876-c34b-4eaf-8857-0d7e05a69506"]</example>
        public List<string> CandidateNodesId { get; set; }

        /// <summary>
        /// Get or set region.
        /// </summary>
        /// <example>canadacentral</example>
        public string Region { get; set; }

        /// <summary>
        /// Get or set duration or tip session in minutes
        /// </summary>
        public int DurationInMinutes { get; set; }

        /// <summary>
        /// Get or set Arbitrary string information for tagging purposes.
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// Get or set whether the Tip include probation nodes.
        /// </summary>
        public bool IsAmberNodeRequest { get; set; }

        /// <summary>
        /// Get or set Pilot fish services list.
        /// </summary>
        public List<KeyValuePair<string, string>> AutopilotServices { get; set; }

        /// <summary>
        /// Get or set Host plugin items.
        /// </summary>
        public List<HostingEnvironmentLineItem> HostingEnvironmentCollection { get; set; }
    }
}