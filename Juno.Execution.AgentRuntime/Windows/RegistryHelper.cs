namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Text;

    /// <inheritdoc/>
    public class RegistryHelper : IRegistryHelper
    {
        private readonly IRegistry registry;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryHelper"/> class.
        /// </summary>
        /// <param name="registry"></param>
        public RegistryHelper(IRegistry registry)
        {
            this.registry = registry;
        }

        /// <inheritdoc/>
        public void AppendToRegistryKey(string registryKeyFullPath, string valueName, string value)
        {
            var previousValue = this.registry.Read<string>(registryKeyFullPath, valueName, string.Empty);
            if (!string.IsNullOrWhiteSpace(previousValue))
            {
                this.registry.Write(registryKeyFullPath, valueName, $"{previousValue} {value}");
            }
            else
            {
                this.registry.Write(registryKeyFullPath, valueName, value);
            }
        }

        /// <inheritdoc/>
        public string ReadRegistryKeyByType(string registryKeyFullPath, string valueName, Type type)
        {
            return this.ReadRegistryKeyByType(registryKeyFullPath, valueName, type, string.Empty);
        }

        /// <summary>
        /// Reads a Windows registry key value and returns a string.
        /// </summary>
        /// <param name="registryKeyFullPath">Full path of the registry key</param>
        /// <param name="valueName">Name of the registry value</param>
        /// <param name="type">Type of the registry value</param>
        /// <param name="defaultValue">Default value when none is found</param>
        /// <returns>Contents of the registry key as string.</returns>
        public string ReadRegistryKeyByType(string registryKeyFullPath, string valueName, Type type, string defaultValue)
        {
            string registryStringValue = defaultValue;
            if (type != null)
            {
                var registryValue = this.registry.Read<dynamic>(registryKeyFullPath, valueName, defaultValue);
                if (registryValue == null)
                {
                    return registryStringValue;
                }

                switch (type.Name)
                {
                    case nameof(Array):
                        registryStringValue = BitConverter.ToString(registryValue);
                        break;
                    case nameof(Int32):
                    case nameof(String):
                    default:
                        registryStringValue = registryValue.ToString();
                        break;
                }
            }

            return registryStringValue;
        }

        /// <inheritdoc/>
        public void WriteToRegistryKeyByType(string registryKeyFullPath, string valueName, string value, Type type)
        {
            if (type != null)
            {
                switch (type.Name)
                {
                    case nameof(Array):
                        this.registry.Write(registryKeyFullPath, valueName, Encoding.Unicode.GetBytes(value));
                        break;
                    case nameof(Int32):
                        this.registry.Write(registryKeyFullPath, valueName, int.Parse(value));
                        break;
                    case nameof(String):
                    default:
                        this.registry.Write(registryKeyFullPath, valueName, value);
                        break;
                }
            }            
        }
    }
}
