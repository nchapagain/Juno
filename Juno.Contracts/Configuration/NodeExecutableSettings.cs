namespace Juno.Contracts.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Offers configuration for Node executables.
    /// </summary>
    public class NodeExecutableSettings : SettingsBase
    {
        /// <summary>
        /// The minimum execution time an executable must 
        /// run for to be marked successful.
        /// </summary>
        public TimeSpan? MinExecutionTime { get; set; }

        /// <summary>
        /// The maximum allowed time an executable can
        /// run for.
        /// </summary>
        public TimeSpan? MaxExecutionTime { get; set; }

        /// <summary>
        /// The name of the executable.
        /// </summary>
        public string ExecutableName { get; set; }

        /// <summary>
        /// Name of the log file to print out to.
        /// </summary>
        public string LogFileName { get; set; }

        /// <summary>
        /// Arguments to give to the execute command.
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// Name of the payload the executable is in.
        /// </summary>
        public string Payload { get; set; }

        /// <summary>
        /// The sub-directory in the payload where
        /// the executable resides.
        /// </summary>
        public string Scenario { get; set; }

        /// <summary>
        /// The number of retries a provider can attempt if the 
        /// process fails. Only applicable if there are retry strings available.
        /// </summary>
        public int? Retries { get; set; }

        /// <summary>
        /// List of strings that the log file must contain at 
        /// termination.
        /// </summary>
        public IEnumerable<string> SuccessStrings { get; set; }

        /// <summary>
        /// List of strings that if the log file contains ANY one of the strings
        /// allow for a retry of the process.
        /// </summary>
        public IEnumerable<string> RetryableString { get; set; }
    }
}
