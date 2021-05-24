namespace Juno.Execution.TipIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.CRC.Extensions;
    using TipGateway.Entities;

    /// <summary>
    /// Extension methods for TiP Service integration scenarios.
    /// </summary>
    public static class TipIntegrationExtensions
    {
        private static List<Func<TipNodeSessionChangeDetails, bool>> retryableTipScenarios = new List<Func<TipNodeSessionChangeDetails, bool>>()
        {
            (details) =>
            {
                bool isRetryable = false;
                if (!string.IsNullOrWhiteSpace(details.ErrorMessage))
                {
                    // The following text were parts of error messages returned by the TiP service
                    // when trying to deploy the agent. Whereas, these are not transient errors in the
                    // traditional sense, they do represent errors that the TiP team has confirmed are relatively
                    // transient. As such, we are going to retry on these errors.
                    List<string> retryableErrors = new List<string>
                    {
                        "semaphore timeout period has expired",
                        "Failed to upload chunk",
                        "network name is no longer available",
                        "Failed to add image Image",
                        "Failed to send DeliveryPath to dynamic storage",
                        "Error querying for",
                        "The media is write protected",
                        "remoteJob failed"
                    };

                    foreach (string error in retryableErrors)
                    {
                        if (Regex.IsMatch(details.ErrorMessage.RemoveWhitespaces(), error.RemoveWhitespaces(), RegexOptions.IgnoreCase))
                        {
                            isRetryable = true;
                            break;
                        }
                    }
                }

                return isRetryable;
            }
        };

        /// <summary>
        /// Returns true/false whether the request associated with the failure response indicated in the TiP session change
        /// details/response can be retried.
        /// </summary>
        /// <param name="tipResponse">Provides the reason for the failure of the TiP session change.</param>
        /// <returns>
        /// True if the initial request associated with the failure can be retried, false if not.
        /// </returns>
        public static bool IsRetryableScenario(this TipNodeSessionChangeDetails tipResponse)
        {
            tipResponse.ThrowIfNull(nameof(tipResponse));

            bool isRetryable = false;
            if (tipResponse.Result == TipNodeSessionChangeResult.Failed)
            {
                // There are certain scenarios where the TiP Service has failed to deploy the agent PF service for which
                // we want to retry on. The TiP team confirms the acceptability of some amount of these failures; however,
                // the Juno team does not consider them acceptable. Hence we are going to retry with the hope of surviving 
                // these failures and avoiding a failed experiment.
                isRetryable = TipIntegrationExtensions.retryableTipScenarios.Any(scenario => scenario.Invoke(tipResponse));
            }

            return isRetryable;
        }
    }
}
