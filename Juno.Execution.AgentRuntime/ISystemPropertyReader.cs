namespace Juno.Execution.AgentRuntime
{
    using System.Collections.Generic;
    using Juno.Execution.AgentRuntime.Contract;

    /// <summary>
    /// Provides methods for capturing property about the physical node or VM.
    /// </summary>
    public interface ISystemPropertyReader
    {
        /// <summary>
        /// Reads the guest's cluster name.
        /// </summary>
        /// <returns>clusterName</returns>
        string ReadGuestClusterName();

        /// <summary>
        /// Reads the context id.
        /// </summary>
        /// <returns>contextId</returns>
        string ReadContextId();

        /// <summary>
        /// Reads the guest container id. (Guest property)
        /// </summary>
        /// <returns>containerId</returns>
        string ReadContainerId();

        /// <summary>
        /// Reads the guest virtual machine id. (Guest property)
        /// </summary>
        /// <returns>virtualMachineId</returns>
        string ReadVirtualMachineId();

        /// <summary>
        /// Reads the host name on the guest. (Guest property)
        /// </summary>
        /// <returns>hostName</returns>
        string ReadHostName();

        /// <summary>
        /// Reads the VM's name. (Guest property)
        /// </summary>
        /// <returns></returns>
        string ReadVirtualMachineName();

        /// <summary>
        /// Get the properties associated with a device setting
        /// </summary>
        List<string> ReadDevices(string deviceClass);

        /// <summary>
        /// Get the properties associated with an Azure VM. (Host property)
        /// </summary>
        string Read(AzureHostProperty hostProperty);
    }
}
