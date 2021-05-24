namespace Juno.GuestAgent
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Execution.AgentRuntime;
    using static Juno.Execution.AgentRuntime.NativeMethods;

    /// <summary>
    /// Provides all the hooks needs to run this process as
    /// a Windows service. This wraps the Juno Guest Agent
    /// </summary>
    public class WindowsServiceBase : ServiceBase
    {
        private readonly CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsServiceBase"/> class.
        /// </summary>
        /// <param name="cancellationTokenSource">Cancellation token to notify cancel/stop events</param>
        public WindowsServiceBase(CancellationTokenSource cancellationTokenSource)
        {
            this.cancellationTokenSource = cancellationTokenSource;
        }

        /// <inheritdoc/>
        protected override void OnStart(string[] args)
        {
            this.SetStatus(ServiceState.Started);
        }

        /// <inheritdoc />
        protected override void OnStop()
        {
            this.SetStatus(ServiceState.StopPending);
            this.cancellationTokenSource.Cancel();
            this.SetStatus(ServiceState.Stopped);
        }

        /// <inheritdoc />
        protected override void OnShutdown()
        {
            this.SetStatus(ServiceState.StopPending);
            this.cancellationTokenSource.Cancel();
            this.SetStatus(ServiceState.Stopped);
        }

        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "This class only runs on Windows.")]
        private void SetStatus(ServiceState state)
        {
            ServiceStatus serviceStatus = new ServiceStatus
            {
                CurrentState = state,
                WaitHint = (long)TimeSpan.FromMinutes(2).TotalMilliseconds,
            };

            NativeMethods.SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }
    }
}
