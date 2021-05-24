namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using Microsoft.Win32;

    /// <summary>
    /// Provides interface for reading windows registry.
    /// </summary>
    public interface IRegistry
    {
        /// <summary>
        /// Read registry value, this method uses default RegistryView64.
        /// </summary>
        /// <typeparam name="T">Type for registry value, e.g. string, byte[].</typeparam>
        /// <param name="registryKeyFullPath">RegistryKey full path.</param>
        /// <param name="valueName">Registry value name.</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns></returns>
        T Read<T>(string registryKeyFullPath, string valueName, T defaultValue);

        /// <summary>
        /// Read registry value, this method picks corresponding RegistryView (Registry32,Registry64).
        /// </summary>
        /// <typeparam name="T">Type for registry value, e.g. string, byte[].</typeparam>
        /// <param name="hive">RegistryHive of the registry key.</param>
        /// <param name="valueName">Registry value name</param>
        /// <param name="subkeyPath">Key path after the hive path, if any.</param>
        /// <param name="defaultValue">Default return value.</param>
        /// <returns></returns>
        T Read<T>(RegistryHive hive, string subkeyPath, string valueName, T defaultValue);

        /// <summary>
        /// Write registry value.
        /// </summary>
        /// <param name="registryKeyFullPath">RegistryKey full path.</param>
        /// <param name="valueName">Registry value name.</param>
        /// <param name="value">Contents to write to the registry.</param>
        void Write(string registryKeyFullPath, string valueName, object value);
    }
}
