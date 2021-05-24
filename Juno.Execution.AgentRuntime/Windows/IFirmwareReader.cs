namespace Juno.Execution.AgentRuntime.Windows
{
    using System;

    /// <summary>
    /// Interface for firmware readers.
    /// </summary>
    /// <typeparam name="TInfo">The type of info to read from the machine.</typeparam>
    public interface IFirmwareReader<TInfo>
    {
        /// <summary>
        /// Reads the TInfo from the machine.
        /// </summary>
        /// <returns>The object containing the information.</returns>
        TInfo Read();

        /// <summary>
        /// Evaluates if the machine can gather the requested info.
        /// </summary>
        /// <param name="type">The type of info to assess.</param>
        /// <returns>True/False if the reader can read the info.</returns>
        bool CanRead(Type type);
    }
}
