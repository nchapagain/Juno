namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Management;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Win32;

    /// <summary>
    /// Properties that represents an Azure Windows Virtual machine.
    /// </summary>
    public class WindowsPropertyReader : ISystemPropertyReader
    {
        /*
         * See "Hyper-V Data Exchange - Windows Guests"
         * https://technet.microsoft.com/en-us/library/dn798287(v=ws.11).aspx
         * and
         * https://docs.microsoft.com/en-us/windows-server/virtualization/hyper-v/supported-windows-guest-operating-systems-for-hyper-v-on-windows
         */

        // Guest paths
        private const string GuestParametersRegistryPath = @"SOFTWARE\Microsoft\Virtual Machine\Guest\Parameters";

        private static Dictionary<AzureHostProperty, ReaderParameters> registryConfigurations = new Dictionary<AzureHostProperty, ReaderParameters>()
        {
            {
                AzureHostProperty.ClusterName,
                ReaderParameters.CreateNewReaderParameters<string>(
                    NodeConstants.AzNodeKey,
                    Constants.ClusterName,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.ClusterName)}")
            },
            {
                AzureHostProperty.NodeId,
                ReaderParameters.CreateNewReaderParameters<string>(
                    NodeConstants.AzNodeKey,
                    Constants.NodeId,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.NodeId)}")
            },
            {
                AzureHostProperty.TipSessionId,
                ReaderParameters.CreateNewReaderParameters<string>(
                    NodeConstants.TipSessionKey,
                    Constants.TipSessionId,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.TipSessionId)}")
            },
            {
                AzureHostProperty.CpuMicrocodeVersion,
                ReaderParameters.CreateNewReaderParameters<byte[]>(
                    CpuConstants.CpuKey,
                    Constants.MicrocodeVersion,
                    new byte[] { byte.MinValue })
            },
            {
                AzureHostProperty.CpuMicrocodeUpdateStatus,
                ReaderParameters.CreateNewReaderParameters<int>(
                    CpuConstants.CpuKey,
                    Constants.MicrocodeUpdateStatus,
                    int.MinValue)
            },
            {
                AzureHostProperty.PreviousCpuMicrocodeVersion,
                ReaderParameters.CreateNewReaderParameters<byte[]>(
                    CpuConstants.CpuKey,
                    Constants.PreviousMicrocodeVersion,
                    new byte[] { byte.MinValue })
            },
            {
                AzureHostProperty.UpdatedCpuMicrocodeVersion,
                ReaderParameters.CreateNewReaderParameters<byte[]>(
                    CpuConstants.CpuKey,
                    Constants.UpdatedMicrocodeVersion,
                    new byte[] { byte.MinValue })
            },
            {
                AzureHostProperty.CpuIdentifier,
                ReaderParameters.CreateNewReaderParameters<string>(
                    CpuConstants.CpuKey,
                    Constants.CpuIdentifier,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.CpuIdentifier)}")
            },
            {
                AzureHostProperty.CpuManufacturer,
                ReaderParameters.CreateNewReaderParameters<string>(
                    CpuConstants.CpuKey,
                    Constants.CpuManufacturer,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.CpuManufacturer)}")
            },
            {
                AzureHostProperty.CpuProcessorNameString,
                ReaderParameters.CreateNewReaderParameters<string>(
                    CpuConstants.CpuKey,
                    Constants.CpuProcessorNameString,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.CpuProcessorNameString)}")
            },
            {
                AzureHostProperty.BiosVersion,
                ReaderParameters.CreateNewReaderParameters<string>(
                    BiosConstants.BiosKey,
                    Constants.BiosVersion,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.BiosVersion)}")
            },
            {
                AzureHostProperty.BiosVendor,
                ReaderParameters.CreateNewReaderParameters<string>(
                    BiosConstants.BiosKey,
                    Constants.BiosVendor,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.BiosVendor)}")
            },
            {
                AzureHostProperty.OsWinNtBuildLabEx,
                ReaderParameters.CreateNewReaderParameters<string>(
                    OSConstants.WinNtCurrentVersionKey,
                    Constants.BuildLabEx,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.OsWinNtBuildLabEx)}")
            },
            {
                AzureHostProperty.OsWinAzBuildLabEx,
                ReaderParameters.CreateNewReaderParameters<string>(
                    OSConstants.WinAzCurrentVersionKey,
                    Constants.BuildLabEx,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.OsWinAzBuildLabEx)}")
            },
            {
                AzureHostProperty.OsWinNtCurrentBuildNumber,
                ReaderParameters.CreateNewReaderParameters<string>(
                    OSConstants.WinNtCurrentVersionKey,
                    Constants.CurrentBuildNumber,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.OsWinNtCurrentBuildNumber)}")
            },
            {
                AzureHostProperty.OsWinNtProductName,
                ReaderParameters.CreateNewReaderParameters<string>(
                    OSConstants.WinNtCurrentVersionKey,
                    Constants.ProductName,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.OsWinNtProductName)}")
            },
            {
                AzureHostProperty.OsWinNtReleaseId,
                ReaderParameters.CreateNewReaderParameters<string>(
                    OSConstants.WinNtCurrentVersionKey,
                    Constants.ReleaseId,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.OsWinNtReleaseId)}")
            },
            {
                AzureHostProperty.OSWinNtUBR,
                ReaderParameters.CreateNewReaderParameters<int>(
                    OSConstants.WinNtCurrentVersionKey,
                    Constants.Ubr,
                    int.MinValue)
            },
            {
                AzureHostProperty.CloudCoreBuild,
                ReaderParameters.CreateNewReaderParameters<string>(
                    OSConstants.CloudCoreKey,
                    Constants.BuildEx,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.CloudCoreBuild)}")

            },
            {
                AzureHostProperty.CloudCoreSupportBuild,
                ReaderParameters.CreateNewReaderParameters<string>(
                    OSConstants.CloudCoreSupprotKey,
                    Constants.BuildEx,
                    $"{Constants.Unknown}{nameof(AzureHostProperty.CloudCoreSupportBuild)}")
            },
        };

        private IRegistry registry;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsPropertyReader"/> class.
        /// </summary>
        /// <param name="registry">Registry</param>
        public WindowsPropertyReader(IRegistry registry = null)
        {
            this.registry = registry ?? new WindowsRegistry();
        }

        /// <inheritdoc cref="ISystemPropertyReader"/>
        public string ReadGuestClusterName()
        {
            return HostingContext.GetEnvironmentVariableValue(AgentEnvironmentVariables.ClusterName);
        }

        /// <inheritdoc cref="ISystemPropertyReader"/>
        public string ReadContextId()
        {
            return HostingContext.GetEnvironmentVariableValue(AgentEnvironmentVariables.ContextId);
        }

        /// <inheritdoc cref="ISystemPropertyReader"/>
        public string ReadVirtualMachineName()
        {
            return Environment.MachineName;
        }

        /// <inheritdoc cref="ISystemPropertyReader"/>
        public string ReadContainerId()
        {
            return this.registry.Read<string>(
               Constants.DefaultHive,
               WindowsPropertyReader.GuestParametersRegistryPath,
               Constants.VirtualMachineName,
               $"{Constants.Unknown}{Constants.RegistryContainerId}");

        }

        /// <inheritdoc cref="ISystemPropertyReader"/>
        public string ReadVirtualMachineId()
        {
            return this.registry.Read<string>(
                Constants.DefaultHive, WindowsPropertyReader.GuestParametersRegistryPath, Constants.VirtualMachineId, $"{Constants.Unknown}{nameof(Constants.VirtualMachineId)}");
        }

        /// <inheritdoc cref="ISystemPropertyReader"/>
        public string ReadHostName()
        {
            string value = HostingContext.GetEnvironmentVariableValue(AgentEnvironmentVariables.NodeName);
            if (string.IsNullOrWhiteSpace(value))
            {
                value = this.registry.Read<string>(
                    Constants.DefaultHive, WindowsPropertyReader.GuestParametersRegistryPath, Constants.RegistryHostName, $"{Constants.Unknown}{Constants.RegistryHostName}");
            }

            return value;
        }

        /// <summary>
        /// Method to read CPU Microcode Version.
        /// </summary>
        /// <returns></returns>
        public string ReadCpuMicrocodeVersion()
        {
            RegistryHelper registryHelper = new RegistryHelper(this.registry);
            ReaderParameters parameters = null;

            string value = null;

            if (!WindowsPropertyReader.registryConfigurations.TryGetValue(AzureHostProperty.CpuMicrocodeVersion, out parameters))
            {
                throw new NotImplementedException($"HostProperty '{AzureHostProperty.CpuMicrocodeVersion}' is not supported in {nameof(WindowsPropertyReader)}.");
            }

            byte[] uCodeBytes = this.registry.Read<byte[]>(
                parameters.SubKeyPath,
                parameters.ValueName,
                (byte[])parameters.DefaultValue);

            // Raw registry value will be in Least Significant Bit(LSB) order
            // Raw value : 000000003900000b , required format to compare: b000039
            // convert raw registry value to Most Significant Bit(MSB) format for comparison
            Array.Reverse(uCodeBytes, 0, uCodeBytes.Length);
            value = BitConverter.ToString(uCodeBytes).Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);

            return value;
        }

        /// <summary>
        /// Reades the previous CPU microcode version deployed.
        /// </summary>
        /// <returns></returns>
        public string ReadPreviousCpuMicrocodeVersion()
        {
            RegistryHelper registryHelper = new RegistryHelper(this.registry);
            ReaderParameters parameters = null;

            string value = null;

            if (!WindowsPropertyReader.registryConfigurations.TryGetValue(AzureHostProperty.PreviousCpuMicrocodeVersion, out parameters))
            {
                throw new NotImplementedException($"HostProperty '{AzureHostProperty.PreviousCpuMicrocodeVersion}' is not supported in {nameof(WindowsPropertyReader)}.");
            }

            byte[] ucodeBytes = this.registry.Read<byte[]>(
                parameters.SubKeyPath,
                parameters.ValueName,
                (byte[])parameters.DefaultValue);

            // Raw registry value will be in Least Significant Bit(LSB) order
            // Raw value : 000000003900000b , required format to compare: b000039
            // convert raw registry value to Most Significant Bit(MSB) format for comparison
            Array.Reverse(ucodeBytes, 0, ucodeBytes.Length);
            value = BitConverter.ToString(ucodeBytes).Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);

            return value;
        }

        /// <summary>
        /// Reades the update CPU microcode version deployed.
        /// </summary>
        /// <returns></returns>
        public string ReadUpdatedCpuMicrocodeVersion()
        {
            RegistryHelper registryHelper = new RegistryHelper(this.registry);
            ReaderParameters parameters = null;

            string value = null;

            if (!WindowsPropertyReader.registryConfigurations.TryGetValue(AzureHostProperty.UpdatedCpuMicrocodeVersion, out parameters))
            {
                throw new NotImplementedException($"HostProperty '{AzureHostProperty.UpdatedCpuMicrocodeVersion}' is not supported in {nameof(WindowsPropertyReader)}.");
            }

            byte[] ucodeBytes = this.registry.Read<byte[]>(
                parameters.SubKeyPath,
                parameters.ValueName,
                (byte[])parameters.DefaultValue);

            // Raw registry value will be in Least Significant Bit(LSB) order
            // Raw value : 000000003900000b , required format to compare: b000039
            // convert raw registry value to Most Significant Bit(MSB) format for comparison
            Array.Reverse(ucodeBytes, 0, ucodeBytes.Length);
            value = BitConverter.ToString(ucodeBytes).Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);

            return value;
        }

        /// <inheritdoc/>
        public string Read(AzureHostProperty hostProperty)
        {
            if (hostProperty == AzureHostProperty.CpuMicrocodeVersion)
            {
                return this.ReadCpuMicrocodeVersion();
            }
            else if (hostProperty == AzureHostProperty.PreviousCpuMicrocodeVersion)
            {
                return this.ReadPreviousCpuMicrocodeVersion();
            }
            else if (hostProperty == AzureHostProperty.UpdatedCpuMicrocodeVersion)
            {
                return this.ReadUpdatedCpuMicrocodeVersion();
            }

            RegistryHelper registryHelper = new RegistryHelper(this.registry);
            ReaderParameters parameters = null;

            if (!WindowsPropertyReader.registryConfigurations.TryGetValue(hostProperty, out parameters))
            {
                throw new NotImplementedException($"HostProperty '{hostProperty}' is not supported in {nameof(WindowsPropertyReader)}.");
            }

            string value = registryHelper.ReadRegistryKeyByType(parameters.SubKeyPath, parameters.ValueName, typeof(string), parameters.DefaultValue.ToString());

            return value;
        }

        /// <inheritdoc/>
        public List<string> ReadDevices(string deviceClass)
        {
            var devices = new List<string>();
            ManagementObjectSearcher searcher = new ManagementObjectSearcher($"SELECT * FROM {deviceClass}");
            var managementObjCollection = searcher.Get();
            foreach (var queryObj in managementObjCollection)
            {
                devices.Add((string)queryObj.Properties["Name"].Value);
            }

            return devices;
        }

        // constants used to access specific registry value in registry path.
        // need these string to reach exact registry value
        // e.g. @"HKLM\SOFTWARE\Microsoft\AzureTipNode" is registry path and TipNodeSessionId is actual variable in registry path.
        private static class Constants
        {
            internal const string BiosVendor = "BIOSVendor";
            internal const string BiosVersion = "BIOSVersion";
            internal const string BuildEx = "BuildEx";
            internal const string BuildLabEx = "BuildLabEx";
            internal const string ClusterName = "ClusterName";
            internal const string CpuIdentifier = "Identifier";
            internal const string CpuManufacturer = "VendorIdentifier";
            internal const string CpuProcessorNameString = "ProcessorNameString";
            internal const string CurrentBuildNumber = "CurrentBuildNumber";
            internal const string MicrocodeUpdateStatus = "Update Status";
            internal const string MicrocodeVersion = "Update Revision";
            internal const string UpdatedMicrocodeVersion = "Update Revision";
            internal const string PreviousMicrocodeVersion = "Previous Update Revision";
            internal const string NodeId = "NodeId";
            internal const string ProductName = "ProductName";
            internal const string TipSessionId = "TipNodeSessionId";
            internal const string Ubr = "UBR";
            internal const string Unknown = "Unknown";
            internal const string VirtualMachineId = "VirtualMachineId";
            internal const string VirtualMachineName = "VirtualMachineName";
            internal const string ReleaseId = "ReleaseId";
            internal const string RegistryHostName = "HostName";
            internal const string RegistryContainerId = "ContainerId";
            internal const RegistryHive DefaultHive = RegistryHive.LocalMachine;
        }

        private class ReaderParameters
        {
            private static string localHive = "HKEY_LOCAL_MACHINE\\";

            /// <summary>
            /// Initializes a new instance of the <see cref="ReaderParameters"/> class.
            /// </summary>
            public ReaderParameters()
            {
            }

            /// <summary>
            /// Key path after the hive path, if any.
            /// </summary>
            public string SubKeyPath { get; set; }

            /// <summary>
            /// Registry value name
            /// </summary>
            public string ValueName { get; set; }

            /// <summary>
            /// Default return value.
            /// </summary>
            public object DefaultValue { get; set; }

            public static ReaderParameters CreateNewReaderParameters<T>(string subkeyPath, string valueName, T defaultValue)
            {
                return new ReaderParameters()
                {
                    SubKeyPath = ReaderParameters.localHive + subkeyPath,
                    ValueName = valueName,
                    DefaultValue = defaultValue
                };
            }
        }
    }
}