namespace Juno.Execution.Providers.Payloads
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides base methods required to activate
    /// or vaildate the Payload.
    /// </summary>
    public interface IPayloadActivator
    {
        /// <summary>
        /// Execute the logic to check Payload
        /// is activated on Azure Host.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<ActivationResult> ActivateAsync(CancellationToken cancellationToken);
    }
}
