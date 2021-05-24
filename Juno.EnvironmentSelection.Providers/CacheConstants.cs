namespace Juno.EnvironmentSelection
{
    using System;

    internal class CacheConstants
    {
        internal static readonly TimeSpan QuotaLimitTtl = TimeSpan.FromMinutes(30);
        internal static readonly TimeSpan ResourceGroupTtl = TimeSpan.FromMinutes(30);
        internal static readonly TimeSpan PublicIpAddressTtl = TimeSpan.FromMinutes(30);
        internal static readonly TimeSpan HealthyNodeTtl = TimeSpan.FromMinutes(30);
        internal static readonly TimeSpan BlockedNodeTtl = TimeSpan.FromMinutes(30);
        internal static readonly TimeSpan KnownClusterTtl = TimeSpan.FromDays(1);
        internal static readonly TimeSpan VmSkuTtl = TimeSpan.FromMinutes(30);
        internal static readonly TimeSpan SsdTtl = TimeSpan.FromDays(1);
        internal static readonly TimeSpan SoCTtl = TimeSpan.FromDays(1);
        internal static readonly TimeSpan BiosTtl = TimeSpan.FromDays(1);
        internal static readonly TimeSpan OsTtl = TimeSpan.FromDays(1);
        internal static readonly TimeSpan CpuIdTtl = TimeSpan.FromDays(1);
        internal static readonly TimeSpan ClusterSkuTtl = TimeSpan.FromDays(1);
        internal static readonly TimeSpan HwSkuTtl = TimeSpan.FromDays(1);
        internal static readonly TimeSpan MicrocodeTtl = TimeSpan.FromDays(1);
        internal static readonly TimeSpan CRPCheckTtl = TimeSpan.FromDays(1);
        internal static readonly TimeSpan ZeroExecutionClusterSelectionTtl = TimeSpan.FromHours(1);
    }
}
