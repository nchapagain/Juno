namespace Juno.Execution.Providers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Execution.TipIntegration;
    using Microsoft.Azure.CRC.Extensions;
    using Moq;
    using Moq.Language.Flow;
    using TipGateway.Entities;

    internal static class FixtureMockExtensions
    {
        public static ISetup<ITipClient, Task<TipNodeSessionChange>> OnCreateTipSession(this Mock<ITipClient> tipClient)
        {
            tipClient.ThrowIfNull(nameof(tipClient));
            return tipClient.Setup(c => c.CreateTipSessionAsync(It.IsAny<TipParameters>(), It.IsAny<CancellationToken>()));
        }

        public static ISetup<ITipClient, Task<TipNodeSessionChange>> OnDeleteTipSession(this Mock<ITipClient> tipClient)
        {
            tipClient.ThrowIfNull(nameof(tipClient));
            return tipClient.Setup(c => c.DeleteTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()));
        }

        public static ISetup<ITipClient, Task<TipNodeSession>> OnGetTipSession(this Mock<ITipClient> tipClient)
        {
            tipClient.ThrowIfNull(nameof(tipClient));
            return tipClient.Setup(c => c.GetTipSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()));
        }
    }
}
