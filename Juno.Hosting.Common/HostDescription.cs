namespace Juno.Hosting.Common
{
    using System;

    /// <summary>
    /// Provides properties that describes the details of the host/agent
    /// executable.
    /// </summary>
    public class HostDescription
    {
        /// <summary>
        /// Gets the host name (e.g. Juno.HostAgent.exe).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a more descriptive name for the host (e.g. Juno Host Agent).
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// Gets or sets a description of the host.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets the assembly version of the host.
        /// </summary>
        public Version Version { get; set; }
    }
}
