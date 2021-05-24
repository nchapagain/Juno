using System;
using System.Threading.Tasks;

namespace Juno.Execution.AgentRuntime
{
    /// <summary>
    /// Provides monitors to run in the Juno Host agent process for capturing information
    /// about the physical node/system in operation during experiments.
    /// </summary>
    public interface ISystemManager
    {
        /// <summary>
        /// Get the properties associated with host or guest.
        /// </summary>
        ISystemPropertyReader PropertyReader { get; }

        /// <summary>
        /// Returns true if running as Administrator (Windows), root (Linux).
        /// </summary>
        /// <returns>True if running as elevated user</returns>
        bool IsRunningAsAdministrator();

        /// <summary>
        /// Returns time since system boot
        /// </summary>
        /// <returns>Time elapsed since system boot</returns>
        TimeSpan GetUptime();

        /// <summary>
        /// Install the executable as a service.
        /// </summary>
        /// <param name="serviceName">Service name.</param>
        /// <param name="serviceDescription">A description of the service and its purpose.</param>
        /// <param name="executablePath">Executable path.</param>
        /// <param name="workingDirectory">Working directory of service.</param>
        /// <param name="execArguments">Command-line arguments to be passed to the executing process.</param>
        /// <param name="userName">Optional parameter defines the username for the account in which the service will run.</param>
        /// <param name="pwd">Optional parameter defines the password for the account in which the service will run.</param>
        /// <returns></returns>
        Task InstallServiceAsync(string serviceName, string serviceDescription, string executablePath, string workingDirectory, string execArguments, string userName = null, string pwd = null);
    }
}
