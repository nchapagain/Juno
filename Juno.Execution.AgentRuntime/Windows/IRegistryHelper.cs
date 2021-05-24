namespace Juno.Execution.AgentRuntime.Windows
{
    using System;

    /// <summary>
    /// Helper class for interacting with the Windows registry
    /// </summary>
    public interface IRegistryHelper
    {
        /// <summary>
        /// Reads a Windows registry key value and returns a string.
        /// </summary>
        /// <param name="registryKeyFullPath">Full path of the registry key</param>
        /// <param name="valueName">Name of the registry value</param>
        /// <param name="type">Type of the registry value</param>
        /// <returns>Contents of the registry key as string.</returns>
        string ReadRegistryKeyByType(string registryKeyFullPath, string valueName, Type type);

        /// <summary>
        /// Write value to a Windows registry key.
        /// </summary>
        /// <param name="registryKeyFullPath">Full path of the registry key</param>
        /// <param name="valueName">Name of the registry value</param>
        /// <param name="value">Contents to write to the registry.</param>
        /// <param name="type">Type of the registry value</param>
        void WriteToRegistryKeyByType(string registryKeyFullPath, string valueName, string value, Type type);

        /// <summary>
        /// Append value to a Windows registry key.
        /// </summary>
        /// <param name="registryKeyFullPath">Full path of the registry key</param>
        /// <param name="valueName">Name of the registry value</param>
        /// <param name="value">Contents to append to the registry.</param>
        void AppendToRegistryKey(string registryKeyFullPath, string valueName, string value);
    }
}
