namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.Runtime.InteropServices;

    // Native support methods and enum for window service. More at https://docs.microsoft.com/en-us/dotnet/framework/windows-services/walkthrough-creating-a-windows-service-application-in-the-component-designer 

    /// <summary>
    /// Native wrappers for Windows APIs
    /// </summary>
    public static class NativeMethods
    {
        /// <summary>
        /// 
        /// </summary>
        [Flags]
        public enum REGNOTIFYCHANGES
        {
            /// <summary>
            /// Notify the caller if a subkey is added or deleted
            /// </summary>
            Name = 0x1,

            /// <summary>
            /// Notify the caller of changes to the attributes of the key,
            /// such as the security descriptor information
            /// </summary>
            Atrributes = 0x2,

            /// <summary>
            /// Notify the caller of changes to a value of the key. This can
            /// include adding or deleting a value, or changing an existing value
            /// </summary>
            LastSet = 0x4,

            /// <summary>
            /// Notify the caller of changes to the security descriptor of the key
            /// </summary>
            Security = 0x8
        }

        internal enum ServiceState
        {
            Stopped = 0x00000001,
            StartPending = 0x00000002,
            StopPending = 0x00000003,
            Started = 0x00000004,
            ContinuePending = 0x00000005,
            PausePending = 0x00000006,
            Paused = 0x00000007
        }

        /*
         * Retrieves the number of milliseconds that have elapsed since the system was started.
         * https://msdn.microsoft.com/en-us/library/windows/desktop/ms724411(v=vs.85).aspx
         */
        [DllImport("kernel32.dll")]
        internal static extern ulong GetTickCount64(); // returns milliseconds

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int handle);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);

        [DllImport("Advapi32.dll")]
        internal static extern int RegNotifyChangeKeyValue(
           IntPtr hKey,
           bool watchSubtree,
           REGNOTIFYCHANGES notifyFilter,
           IntPtr hEvent,
           bool asynchronous);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);

        /// <summary>
        /// Struct required by service functions
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct ServiceStatus
        {
            public long ServiceType;
            public ServiceState CurrentState;
            public long ControlsAccepted;
            public long Win32ExitCode;
            public long ServiceSpecificExitCode;
            public long CheckPoint;
            public long WaitHint;
        }
    }
}
