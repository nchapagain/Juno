namespace Juno.Execution.AgentRuntime.Linux
{
    using System;
    using System.Collections.Generic;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC.AspNetCore;

    /// <summary>
    /// Properties that represents an Azure Virtual machine.
    /// </summary>
    public class LinuxPropertyReader : ISystemPropertyReader
    {
        /*
         * See "Hyper-V Data Exchange - Linux Guests"
         * https://technet.microsoft.com/en-us/library/dn798287(v=ws.11).aspx#BKMK_LINUX
         * and
         * https://technet.microsoft.com/windows-server-docs/compute/hyper-v/supported-ubuntu-virtual-machines-on-hyper-v
         */

        // Linux VM config Work in progress !

        private Dictionary<string, string> kvpPool3Data;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinuxPropertyReader"/> class.
        /// </summary>
        public LinuxPropertyReader()
        {
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
        public string ReadHostName()
        {
            string value = HostingContext.GetEnvironmentVariableValue(AgentEnvironmentVariables.NodeName);
            if (string.IsNullOrWhiteSpace(value))
            {
                value = this.GetKvpPoolContent()[Constants.HostName];
            }

            return value;
        }

        /// <inheritdoc cref="ISystemPropertyReader"/>
        public string ReadContainerId()
        {
            return this.GetKvpPoolContent()?[Constants.ContainerId];
        }
        
        /// <inheritdoc cref="ISystemPropertyReader"/>
        public string ReadVirtualMachineId()
        {
            return this.GetKvpPoolContent()?[Constants.VirtualMachineId];
        }

        /// <inheritdoc cref="ISystemPropertyReader"/>
        public string ReadVirtualMachineName()
        {
            return Environment.MachineName;
        }

        /// <inheritdoc/>
        public string Read(AzureHostProperty hostProperty)
        {
            switch (hostProperty)
            {
                case AzureHostProperty.ClusterName:
                    return this.GetKvpPoolContent()[Constants.ClusterName];
                case AzureHostProperty.NodeId:
                    throw new NotImplementedException("Cannot read NodeId, unknown how to get this yet.");
                case AzureHostProperty.CpuMicrocodeVersion:
                    throw new NotImplementedException("Cannot read MicrocodeVersion, unknown how to get this yet.");
                case AzureHostProperty.CpuMicrocodeUpdateStatus:
                    throw new NotImplementedException("Cannot read MicrocodeUpdateStatus, unknown how to get this yet.");
                case AzureHostProperty.TipSessionId:
                    throw new NotImplementedException("Cannot read TipSessionId, unknown how to get this yet.");

                default:
                    throw new NotImplementedException($"HostProperty '{hostProperty}' is not implemented in {nameof(LinuxPropertyReader)}.");
            }
        }

        /// <inheritdoc/>
        public List<string> ReadDevices(string deviceClass)
        {
            throw new NotImplementedException();
        }

        private Dictionary<string, string> GetKvpPoolContent()
        {
            if (this.kvpPool3Data != null)
            {
                return this.kvpPool3Data;
            }

            return this.kvpPool3Data = LinuxKVPReader.ReadKeyValuePairs(LinuxKVPReader.Pool3);
        }

        private static class Constants
        {
            internal const string HostName = "HostName";
            internal const string ClusterName = "ClusterName";
            internal const string ContainerId = "VirtualMachineName";
            internal const string VirtualMachineId = "VirtualMachineId";
        }
    }
}
