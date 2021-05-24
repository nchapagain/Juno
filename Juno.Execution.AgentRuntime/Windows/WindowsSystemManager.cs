namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Security.Principal;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Polly;

    /// <summary>
    /// Provides monitors to run in the Juno Host agent process for capturing information
    /// about the physical node/system in operation during experiments.
    /// </summary>
    public class WindowsSystemManager : ISystemManager
    {
        private WindowsSystemManager()
        {
            this.PropertyReader = new WindowsPropertyReader();
            this.ProcessExecution = new ProcessExecution();
        }

        /// <summary>
        /// Return the singleton instance of <see cref="WindowsSystemManager"/>
        /// </summary>
        public static ISystemManager Instance { get; } = new WindowsSystemManager();

        /// <inheritdoc/>
        public ISystemPropertyReader PropertyReader { get; }

        /// <inheritdoc/>
        public IProcessExecution ProcessExecution { get; }

        /// <inheritdoc/>
        public TimeSpan GetUptime()
        {
            // This cast from ulong is OK because overflow would happen at
            // Int64.MaxValue milliseconds, which is 292 million years.
            long milliseconds = (long)NativeMethods.GetTickCount64();
            return TimeSpan.FromMilliseconds(milliseconds);
        }

        /// <inheritdoc/>
        public async Task InstallServiceAsync(string serviceName, string serviceDescription, string executablePath, string workingDirectory, string execArguments, string userName = null, string pwd = null)
        {
            const string scExe = "sc";
            TimeSpan executeTimeout = TimeSpan.FromMinutes(5);

            IAsyncPolicy retryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(3, (retries) => TimeSpan.FromSeconds(retries * 2));

            // SC.exe Documentation
            // https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2012-R2-and-2012/cc754599(v=ws.11)

            // Stop previous service
            await retryPolicy.ExecuteAsync(async () =>
            {
                await this.ProcessExecution.ExecuteProcessAsync(scExe, $"stop {serviceName}", executeTimeout)
                    .ConfigureDefaults();

            }).ConfigureDefaults();

            // Allow time for the previous command to have completed. Found this to be an issue when working through
            // issues on a local dev box.
            await Task.Delay(3000).ConfigureDefaults();

            // Delete previous service registration
            await retryPolicy.ExecuteAsync(async () =>
            {
                ProcessExecutionResult deleteResult = await this.ProcessExecution.ExecuteProcessAsync(scExe, $"delete {serviceName}", executeTimeout)
                    .ConfigureDefaults();

                // 1060 = Service Does Not Exist
                if (deleteResult.ExitCode != 1060)
                {
                    deleteResult.ThrowIfErrored<ProcessExecutionException>(
                        $"Service delete failed. Service='{serviceName}', Executable Path='{executablePath}', Working Directory='{workingDirectory}'. " +
                        $"{Environment.NewLine}" +
                        $"{string.Join(Environment.NewLine, deleteResult.Output)}");
                }
            }).ConfigureDefaults();

            await Task.Delay(3000).ConfigureDefaults();

            // Create new service registration
            await retryPolicy.ExecuteAsync(async () =>
            {
                string serviceStartupArguments = string.IsNullOrEmpty(execArguments) ? string.Empty : execArguments;
                string serviceCreateCommand = $@"create {serviceName} binpath= ""{executablePath} """"{serviceStartupArguments}"""""" start= delayed-auto obj= LocalSystem DisplayName= {serviceName}";
                string serviceFailureCommand = $@"failure {serviceName} reset= 0 actions= restart/60000/restart/60000/restart/60000";

                if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(pwd))
                {
                    // Any account that is going to be used to run a service must have the
                    // "logon as service" privilege.
                    this.SetLogonAsServicePrivileges(userName);

                    serviceCreateCommand = $@"create {serviceName} binpath= ""{executablePath} """"{serviceStartupArguments}"""""" start= delayed-auto obj= ""{userName}"" password= ""{pwd}"" DisplayName= {serviceName}";
                }

                ProcessExecutionResult createResult = await this.ProcessExecution.ExecuteProcessAsync(scExe, serviceCreateCommand, executeTimeout)
                    .ConfigureDefaults();

                createResult.ThrowIfErrored<ProcessExecutionException>(
                    $"Service installation failed. Service='{serviceName}', Executable Path='{executablePath}', Working Directory='{workingDirectory}'. " +
                    $"{Environment.NewLine}" +
                    $"{string.Join(Environment.NewLine, createResult.Output)}");

                ProcessExecutionResult failureResult = await this.ProcessExecution.ExecuteProcessAsync(scExe, serviceFailureCommand, executeTimeout)
                    .ConfigureDefaults();

                failureResult.ThrowIfErrored<ProcessExecutionException>(
                    $"Service recovery action failed. Service='{serviceName}', Executable Path='{executablePath}', Working Directory='{workingDirectory}'. " +
                    $"{Environment.NewLine}" +
                    $"{string.Join(Environment.NewLine, failureResult.Output)}");

            }).ConfigureDefaults();

            // Add service description
            await retryPolicy.ExecuteAsync(async () =>
            {
                await this.ProcessExecution.ExecuteProcessAsync(scExe, $@" description {serviceName} ""{serviceDescription}""", executeTimeout)
                    .ConfigureDefaults();

            }).ConfigureDefaults();

            // Start new instance
            await retryPolicy.ExecuteAsync(async () =>
            {
                ProcessExecutionResult startResult = await this.ProcessExecution.ExecuteProcessAsync(scExe, $@"start {serviceName}", executeTimeout)
                    .ConfigureDefaults();

                startResult.ThrowIfErrored<ProcessExecutionException>(
                    $"Service startup failed. Service='{serviceName}', Executable Path='{executablePath}', Working Directory='{workingDirectory}'. " +
                    $"{Environment.NewLine}" +
                    $"{string.Join(Environment.NewLine, startResult.Output)}");
            }).ConfigureDefaults();
        }

        /// <inheritdoc/>
        public bool IsRunningAsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>
        /// Sets the account (e.g. 8ade375d1de-1\junovmadmin) to have "logon as service" privileges
        /// on the Windows system.
        /// </summary>
        /// <param name="account">
        /// The account to provide "logon as service" privileges (e.g. 8ade375d1de-1\junovmadmin).
        /// </param>
        protected virtual void SetLogonAsServicePrivileges(string account)
        {
            // https://stackoverflow.com/questions/14408973/using-sc-exe-to-set-service-credentials-password-fails
            // https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2008-R2-and-2008/cc794944(v=ws.10)?redirectedfrom=MSDN
            // https://morgantechspace.com/2013/11/set-or-grant-logon-as-a-service-right-to-user.html

            WindowsPolicy.Instance.SetAccountRights(account, "SeServiceLogonRight");
        }
    }
}