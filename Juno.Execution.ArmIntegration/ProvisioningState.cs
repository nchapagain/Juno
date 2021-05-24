namespace Juno.Execution.ArmIntegration
{
    /// <summary>
    /// The provisioning state of a resource.
    /// </summary>
    public enum ProvisioningState 
    {
        /// <summary>
        /// Pending
        /// </summary>
        Pending,

        /// <summary>
        /// Accepted
        /// </summary>
        Accepted,

        /// <summary>
        /// Creating
        /// </summary>
        Creating,

        /// <summary>
        /// Failed
        /// </summary>
        Failed,

        /// <summary>
        /// Succeeded
        /// </summary>
        Succeeded,

        /// <summary>
        /// Deleting
        /// </summary>
        Deleting,

        /// <summary>
        /// Deleted
        /// </summary>
        Deleted,

        /// <summary>
        /// Moving
        /// </summary>
        Moving,

        /// <summary>
        /// Running
        /// </summary>
        Running,

        /// <summary>
        /// Unknown. Default is pending, this will be used if we are not able to parse
        /// ARM provision state to our enum list
        /// </summary>
        Unknown
    }
}