namespace Juno.Execution.Providers.Payloads
{
    using System;

    /// <summary>
    /// reperesents the activation result and approximate time of activation.
    /// </summary>
    public class ActivationResult
    {
        /// <summary>
        /// Initializes a new instance of ActivationResult.
        /// </summary>
        /// <param name="activationTime"></param>
        /// <param name="isActivated"></param>
        public ActivationResult(bool isActivated, DateTime? activationTime = null)
        {
            this.ActivationTime = activationTime ?? null;
            this.IsActivated = isActivated;
        }

        /// <summary>
        /// Gets/sets the activation time.
        /// </summary>
        public DateTime? ActivationTime { get; }

        /// <summary>
        /// Gets/sets the activation result.
        /// </summary>
        public bool IsActivated { get; }
    }
}
