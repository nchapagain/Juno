namespace Juno.Execution.AgentRuntime.Windows
{
    using Microsoft.Win32;

    /// <summary>
    /// Provides interface for reading windows registry.
    /// </summary>
    public class WindowsRegistry : IRegistry
    {
        /// <inheritdoc/>
        public T Read<T>(string registryKeyFullPath, string valueName, T defaultValue)
        {
            return (T)Registry.GetValue(registryKeyFullPath, valueName, defaultValue);
        }

        /// <inheritdoc/>
        public T Read<T>(RegistryHive hive, string subkeyPath, string valueName, T defaultValue)
        {
            RegistryView view = System.Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;
            RegistryKey regKey = string.IsNullOrEmpty(subkeyPath)
                ? RegistryKey.OpenBaseKey(hive, view)
                : RegistryKey.OpenBaseKey(hive, view).OpenSubKey(subkeyPath);

            return regKey != null ? (T)regKey.GetValue(valueName, defaultValue) : defaultValue;
        }

        /// <inheritdoc/>
        public void Write(string registryKeyFullPath, string valueName, object value)
        {
            Registry.SetValue(registryKeyFullPath, valueName, value);
        }
    }
}
