namespace Juno.Execution.TipIntegration
{
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using TipGateway.Entities;

    /// <summary>
    /// Extension methods for <see cref="EventContext"/> instances
    /// and related components.
    /// </summary>
    public static class TipContractTelemetryExtensions
    {
        /// <summary>
        /// Extension adds context information defined in the experiment to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="tipSession">Provides information about a TiP session.</param>
        public static EventContext AddContext(this EventContext telemetryContext, TipSession tipSession)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            telemetryContext.AddContext("tipSessionId", tipSession?.TipSessionId)
                .AddContext("tipSessionRegion", tipSession?.Region)
                .AddContext("tipSessionCluster", tipSession?.ClusterName)
                .AddContext("tipSessionNodeId", tipSession?.NodeId)
                .AddContext("tipSessionChangeIds", tipSession?.ChangeIdList)
                .AddContext("tipSessionStatus", tipSession?.Status.ToString())
                .AddContext("tipSessionExpirationTime", tipSession?.ExpirationTimeUtc);

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the experiment to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="tipSessionChangeDetails">Provides information about a TiP session.</param>
        public static EventContext AddContext(this EventContext telemetryContext, TipNodeSessionChangeDetails tipSessionChangeDetails)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (tipSessionChangeDetails != null)
            {
                telemetryContext.AddContext("tipSessionChangeType", tipSessionChangeDetails.ChangeType)
                    .AddContext("tipSessionChangeErrorMessage", tipSessionChangeDetails.ErrorMessage)
                    .AddContext("tipSessionChangeLastUpdatedTime", tipSessionChangeDetails.LastUpdatedTimeUtc)
                    .AddContext("tipSessionChangeNote", tipSessionChangeDetails.Note)
                    .AddContext("tipSessionChangeRequestedTime", tipSessionChangeDetails.RequestedTimeUtc)
                    .AddContext("tipSessionChangeResult", tipSessionChangeDetails.Result.ToString())
                    .AddContext("tipSessionChangeId", tipSessionChangeDetails.TipNodeSessionChangeId)
                    .AddContext("tipSessionId", tipSessionChangeDetails.TipNodeSessionId);
            }

            return telemetryContext;
        }
    }
}
