namespace Juno.Execution.AgentRuntime.Windows
{
    /// <summary>
    /// Provides an interface to read and write to MSRs.
    /// </summary>
    public interface IMsr
    {
        /// <summary>
        /// Read MSR.
        /// </summary>
        /// <param name="registerId">The model-specific register</param>
        /// <param name="processorIndex">The processor index associated with the register</param>
        /// <returns></returns>
        public string Read(string registerId, string processorIndex = "0");

        /// <summary>
        /// Write to MSR.
        /// </summary>
        /// <param name="registerId">The model-specific register</param>
        /// <param name="processorIndex">The processor index associated with the register</param>
        /// <param name="value">The value to write to the register</param>
        public void Write(string registerId, string processorIndex, string value);
    }
}
