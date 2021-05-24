namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Text;

    [SuppressMessage("Design", "CA1060:Move pinvokes to native methods class", Justification = "Not preferable here.")]
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1121:Use built-in type alias", Justification = "Interop")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Interop")]
    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1214:Readonly fields should appear before non-readonly fields", Justification = "Interop")]
    internal class WindowsPolicy
    {
        private WindowsPolicy()
        {
        }

        private enum LSA_AccessPolicy : long
        {
            POLICY_VIEW_LOCAL_INFORMATION = 0x00000001L,
            POLICY_VIEW_AUDIT_INFORMATION = 0x00000002L,
            POLICY_GET_PRIVATE_INFORMATION = 0x00000004L,
            POLICY_TRUST_ADMIN = 0x00000008L,
            POLICY_CREATE_ACCOUNT = 0x00000010L,
            POLICY_CREATE_SECRET = 0x00000020L,
            POLICY_CREATE_PRIVILEGE = 0x00000040L,
            POLICY_SET_DEFAULT_QUOTA_LIMITS = 0x00000080L,
            POLICY_SET_AUDIT_REQUIREMENTS = 0x00000100L,
            POLICY_AUDIT_LOG_ADMIN = 0x00000200L,
            POLICY_SERVER_ADMIN = 0x00000400L,
            POLICY_LOOKUP_NAMES = 0x00000800L,
            POLICY_NOTIFICATION = 0x00001000L
        }

        [DllImport("advapi32")]
        public static extern void FreeSid(IntPtr pSid);

        // Import the LSA functions
        [DllImport("advapi32.dll", PreserveSig = true)]
        private static extern UInt32 LsaOpenPolicy(
            ref LSA_UNICODE_STRING SystemName,
            ref LSA_OBJECT_ATTRIBUTES ObjectAttributes,
            Int32 DesiredAccess,
            out IntPtr PolicyHandle);

        [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
        private static extern long LsaAddAccountRights(
            IntPtr PolicyHandle,
            IntPtr AccountSid,
            LSA_UNICODE_STRING[] UserRights,
            long CountOfRights);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, PreserveSig = true)]
        private static extern bool LookupAccountName(
            string lpSystemName, string lpAccountName,
            IntPtr psid,
            ref int cbsid,
            StringBuilder domainName, ref int cbdomainLength, ref int use);

        [DllImport("advapi32.dll")]
        private static extern bool IsValidSid(IntPtr pSid);

        [DllImport("advapi32.dll")]
        private static extern long LsaClose(IntPtr ObjectHandle);

        [DllImport("kernel32.dll")]
        private static extern int GetLastError();

        [DllImport("advapi32.dll")]
        private static extern long LsaNtStatusToWinError(long status);

        // define the structures

        [StructLayout(LayoutKind.Sequential)]
        private struct LSA_OBJECT_ATTRIBUTES
        {
            public int Length;
            public IntPtr RootDirectory;
            public readonly LSA_UNICODE_STRING ObjectName;
            public UInt32 Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LSA_UNICODE_STRING
        {
            public UInt16 Length;
            public UInt16 MaximumLength;
            public IntPtr Buffer;
        }

        /// <summary>
        /// The singleton instance of the <see cref="WindowsPolicy"/> class.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "Interop")]
        public static WindowsPolicy Instance { get; } = new WindowsPolicy();

        /// <summary>
        /// Sets a specific privileges/rights on the Windows account.
        /// </summary>
        /// <example>
        /// SetAccountRights("domain\username", "SeServiceLogonRight")
        /// </example>
        [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "Interop")]
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1129:Do not use default value type constructor", Justification = "Interop")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1312:Variable names should begin with lower-case letter", Justification = "Interop")]
        public long SetAccountRights(string accountName, string privilegeName)
        {
            long winErrorCode = 0; // contains the last error

            // initialize a pointer for the account SID (security identifier).
            IntPtr sid = IntPtr.Zero;

            // initialize a pointer for the policy handle
            IntPtr policyHandle = IntPtr.Zero;

            try
            {
                // pointer an size for the SID
                int sidSize = 0;

                // StringBuilder and size for the domain name
                var domainName = new StringBuilder();
                int nameSize = 0;

                // account-type variable for lookup
                int accountType = 0;

                // get required buffer size
                WindowsPolicy.LookupAccountName(string.Empty, accountName, sid, ref sidSize, domainName, ref nameSize, ref accountType);

                // allocate buffers
                domainName = new StringBuilder(nameSize);
                sid = Marshal.AllocHGlobal(sidSize);

                // lookup the SID for the account
                bool result = WindowsPolicy.LookupAccountName(string.Empty, accountName, sid, ref sidSize, domainName, ref nameSize, ref accountType);

                if (!result)
                {
                    winErrorCode = WindowsPolicy.GetLastError();
                    throw new SecurityException($"A SID for the account '{domainName}\\{accountName}' does not exist on the system. Error code = {winErrorCode}.");
                }

                // initialize an empty unicode-string
                var systemName = new LSA_UNICODE_STRING();

                // combine all policies
                var access = (int)(
                    LSA_AccessPolicy.POLICY_AUDIT_LOG_ADMIN |
                    LSA_AccessPolicy.POLICY_CREATE_ACCOUNT |
                    LSA_AccessPolicy.POLICY_CREATE_PRIVILEGE |
                    LSA_AccessPolicy.POLICY_CREATE_SECRET |
                    LSA_AccessPolicy.POLICY_GET_PRIVATE_INFORMATION |
                    LSA_AccessPolicy.POLICY_LOOKUP_NAMES |
                    LSA_AccessPolicy.POLICY_NOTIFICATION |
                    LSA_AccessPolicy.POLICY_SERVER_ADMIN |
                    LSA_AccessPolicy.POLICY_SET_AUDIT_REQUIREMENTS |
                    LSA_AccessPolicy.POLICY_SET_DEFAULT_QUOTA_LIMITS |
                    LSA_AccessPolicy.POLICY_TRUST_ADMIN |
                    LSA_AccessPolicy.POLICY_VIEW_AUDIT_INFORMATION |
                    LSA_AccessPolicy.POLICY_VIEW_LOCAL_INFORMATION);

                // these attributes are not used, but LsaOpenPolicy wants them to exists
                var ObjectAttributes = new LSA_OBJECT_ATTRIBUTES();
                ObjectAttributes.Length = 0;
                ObjectAttributes.RootDirectory = IntPtr.Zero;
                ObjectAttributes.Attributes = 0;
                ObjectAttributes.SecurityDescriptor = IntPtr.Zero;
                ObjectAttributes.SecurityQualityOfService = IntPtr.Zero;

                // get a policy handle
                uint resultPolicy = WindowsPolicy.LsaOpenPolicy(ref systemName, ref ObjectAttributes, access, out policyHandle);
                winErrorCode = WindowsPolicy.LsaNtStatusToWinError(resultPolicy);

                if (winErrorCode != 0)
                {
                    throw new SecurityException($"Unable to open LSA policy for account '{domainName}\\{accountName}'. Error code = {winErrorCode}.");
                }

                // Now that we have the SID and the policy, we can add rights to the account.

                // initialize an unicode-string for the privilege name
                var userRights = new LSA_UNICODE_STRING[1];
                userRights[0] = new LSA_UNICODE_STRING();
                userRights[0].Buffer = Marshal.StringToHGlobalUni(privilegeName);
                userRights[0].Length = (UInt16)(privilegeName.Length * UnicodeEncoding.CharSize);
                userRights[0].MaximumLength = (UInt16)((privilegeName.Length + 1) * UnicodeEncoding.CharSize);

                // add the rights to the account
                long res = WindowsPolicy.LsaAddAccountRights(policyHandle, sid, userRights, 1);
                winErrorCode = WindowsPolicy.LsaNtStatusToWinError(res);

                if (winErrorCode != 0)
                {
                    throw new SecurityException($"Unable to add privilege '{privilegeName}' to LSA policy for account '{domainName}\\{accountName}'. Error code = {winErrorCode}.");
                }
            }
            finally
            {
                WindowsPolicy.LsaClose(policyHandle);
                WindowsPolicy.FreeSid(sid);
            }

            return winErrorCode;
        }
    }
}
