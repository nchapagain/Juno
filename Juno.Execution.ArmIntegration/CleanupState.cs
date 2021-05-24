namespace Juno.Execution.ArmIntegration
{
    /// <summary>
    ///     Enum to keep track of delete states
    /// </summary>
    public enum CleanupState
    {
        /// <summary>
        ///     The delete has not started
        /// </summary>
        NotStarted,

        /// <summary>
        ///     Delete request has been accepted
        /// </summary>
        Accepted,

        /// <summary>
        ///     ARM is deleting the resource
        /// </summary>
        Deleting,

        /// <summary>
        ///     The delete request failed
        /// </summary>
        Failed,

        /// <summary>
        ///     The delete request succeeded
        /// </summary>
        Succeeded
    }
}
