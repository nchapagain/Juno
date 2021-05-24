using Microsoft.Extensions.Logging;

namespace Juno.Providers
{
    /// <summary>
    /// Provides methods used by providers to monitor the status of related/child
    /// agent steps.
    /// </summary>
    public interface IExperimentStepMonitoringProvider : IExperimentProvider
    {
        /// <summary>
        /// The logger to use to capture telemetry data.
        /// </summary>
        ILogger Logger { get; }
    }
}
