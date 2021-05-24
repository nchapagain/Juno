namespace Juno.EnvironmentSelection.SubscriptionFilters
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Subscriptions;
    using Microsoft.Azure.CRC.Telemetry;

    /// <summary>
    /// Interface for derived class to implement a strategy on how to interpert
    /// each subscription.
    /// </summary>
    public interface ISelectionStrategy
    {
        /// <summary>
        /// Returns Environment Candidates that contain
        /// elligible subscriptions.
        /// </summary>
        /// <param name="limits">The details about each subscription</param>
        /// <param name="threshold">The minimum quota a vm + region combo must have.</param>
        /// <param name="telemetryContext">Context object used to capture telemetry.</param>
        /// <returns></returns>
        IDictionary<string, EnvironmentCandidate> GetEnvironmentCandidates(IList<ServiceLimitAvailibility> limits, int threshold, EventContext telemetryContext);
    }
}
