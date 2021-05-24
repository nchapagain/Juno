namespace Juno.Providers
{
    /// <summary>
    /// Tip group restriction
    /// </summary>
    public enum TipGroupRestriction
    {
        /// <summary>
        /// The tip sessions need to be in the same cluster.
        /// </summary>
        SameCluster,

        /// <summary>
        /// The tip sessions need to be in the same rack.
        /// </summary>
        SameRack
    }
}
