namespace Juno.Execution.AgentRuntime.Linux
{
    using System;
    using System.IO;
    using System.IO.Abstractions;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Provides monitors to run in the Juno Host agent process for capturing information
    /// about the physical node/system in operation during experiments.
    /// </summary>
    public class LinuxSystemManager : ISystemManager
    {
        private const string ServicePath = "/etc/systemd/system/";
        private const string ServiceExtension = ".service";
        private const string AdminUser = "root";
        private const string ServiceTemplate = @"
[Unit]
Description={0} 
DefaultDependencies=no
After=network.target remote-fs.target nss-lookup.target
 
[Service]
Type=simple
RemainAfterExit=no
ExecStart={1}
WorkingDirectory={2}
User={3}
Group={3}
Restart=no

[Install]
WantedBy=default.target
";

        private readonly IFileSystem fileSystem;
        private readonly string procUptimeFileLocation = "/proc/uptime";

        private LinuxSystemManager(IFileSystem fileSystem = null, IProcessExecution processExecutor = null, ISystemPropertyReader propertyReader = null)
        {
            this.PropertyReader = propertyReader ?? new LinuxPropertyReader();
            this.ProcessExecution = processExecutor ?? new ProcessExecution();
            this.fileSystem = fileSystem ?? new FileSystem();
        }

        /// <summary>
        /// Return the singleton instance of <see cref="LinuxSystemManager"/>
        /// </summary>
        public static ISystemManager Instance { get; } = new LinuxSystemManager();

        /// <inheritdoc/>
        public IProcessExecution ProcessExecution { get; }

        /// <inheritdoc />
        public ISystemPropertyReader PropertyReader { get; }

        /// <inheritdoc />
        public TimeSpan GetUptime()
        {
            // TODO: find appropriate POSIX function. ALternate option is to parse from File.ReadAllText( "/proc/uptime" );
            // TODO: If we switch to .NET Core 3.0, use System.Environment.TickCount64 instead
            double seconds = 0.0;

            using (StreamReader reader = new StreamReader(this.procUptimeFileLocation))
            {
                string line = reader.ReadLine();

                if (!string.IsNullOrEmpty(line))
                {
                    seconds = double.Parse(line.Split(' ')[0]);
                }
            }

            return TimeSpan.FromSeconds(seconds);
        }

        /// <inheritdoc />
        public async Task InstallServiceAsync(string serviceName, string serviceDescription, string executablePath, string workingDirectory, string execArguments, string userName = null, string pwd = null)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new Exception("Cannot install service with a null/empty service name!");
            }

            if (serviceDescription == null)
            {
                throw new Exception("Cannot install service with a null service description!");
            }

            if (!this.IsRunningAsAdministrator())
            {
                throw new Exception("The program must run as root to install a service.");
            }

            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                throw new Exception($"Tried to install a service with a non-existant executable! \"{executablePath}\"");
            }

            if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            {
                throw new Exception($"Tried to install a service using a non-existant working directory! \"{workingDirectory}\"");
            }

            string serviceFileName = serviceName + LinuxSystemManager.ServiceExtension; // Extension marks this as a service, instead of for instance, a socket or a device.
            TimeSpan executeTimeout = TimeSpan.FromMinutes(5);

            // Determine the user which the service should run as.
            // Due to certificate issues regarding sudo vs running directly as a given user, getting this wrong may cause GuestAgent to not be able to find the certificate.
            string runAsUser = Mono.Unix.Native.Syscall.getlogin();
            if (string.IsNullOrEmpty(runAsUser))
            {
                runAsUser = Environment.UserName;
            }

            // Make sure the target directory is owned by the user. Unnecessary, but good practice.
            ProcessExecutionResult ownershipResult = await this.ProcessExecution.ExecuteProcessAsync("sudo", $"chown -R {runAsUser}:{runAsUser} {workingDirectory}")
                .ConfigureAwait(false);

            ownershipResult.ThrowIfErrored<ProcessExecutionException>(
                $"Failed to grant ownership of service directory. User='{runAsUser}', Directory='{workingDirectory}'. " +
                $"{Environment.NewLine}" +
                $"{string.Join(Environment.NewLine, ownershipResult.Output)}");

            // Stop previous run.
            await this.ProcessExecution.ExecuteProcessAsync("sudo", $"systemctl stop {serviceFileName}", executeTimeout)
                .ConfigureAwait(false);

            // Disables it from starting after a reboot.
            await this.ProcessExecution.ExecuteProcessAsync("sudo", $"systemctl disable {serviceFileName}", executeTimeout)
                .ConfigureAwait(false);

            // Delete previous service registration
            await this.ProcessExecution.ExecuteProcessAsync("sudo", $"systemctl delete {serviceFileName}", executeTimeout)
                .ConfigureAwait(false);

            // Mark the service binary as executable.
            ProcessExecutionResult markExecutableResult = await this.ProcessExecution.ExecuteProcessAsync("sudo", $"chmod +x {executablePath}", executeTimeout)
                .ConfigureAwait(false);

            markExecutableResult.ThrowIfErrored<ProcessExecutionException>(
                $"Failed to mark service binary as executable. Executable Path='{executablePath}'. " +
                $"{Environment.NewLine}" +
                $"{string.Join(Environment.NewLine, markExecutableResult.Output)}");

            // Manual Reference: https://www.man7.org/linux/man-pages/man5/systemd.service.5.html

            // Note: For some reason, we can't directly specify the user running it as root.
            // It's entirely unclear to me why this is.
            // If we do, .NET Core is unable to find certificates installed by the installer when GuestAgent runs.
            // It only detects it if it's another user which runs GuestAgent via sudo.
            // I have no idea why this might be, it seems like an issue in the .NET Core Runtime itself.

            string runPrefix = (LinuxSystemManager.AdminUser.Equals(runAsUser, StringComparison.InvariantCulture) ? string.Empty : "/usr/bin/sudo -S ");
            string executionCmd = runPrefix + executablePath + (string.IsNullOrEmpty(execArguments) ? string.Empty : $" \"{execArguments}\"");
            string serviceDefinition = string.Format(LinuxSystemManager.ServiceTemplate, serviceDescription, executionCmd, workingDirectory, runAsUser);
            await this.fileSystem.File.WriteAllTextAsync(Path.Combine(LinuxSystemManager.ServicePath, serviceFileName), serviceDefinition).ConfigureDefaults();

            // Reload services.
            ProcessExecutionResult reloadResult = await this.ProcessExecution.ExecuteProcessAsync("sudo", "systemctl daemon-reload", executeTimeout).ConfigureAwait(false);
            reloadResult.ThrowIfErrored<ProcessExecutionException>(
                $"Failed to reload daemons." +
                $"{Environment.NewLine}" +
                $"{string.Join(Environment.NewLine, reloadResult.Output)}");

            // Enable the service to start after a reboot.
            ProcessExecutionResult enableResult = await this.ProcessExecution.ExecuteProcessAsync("sudo", $"systemctl enable {serviceFileName}", executeTimeout)
                .ConfigureAwait(false);
            // This can run successfully with exit code 0 and still give an error message, even on a successful execution. So we only check the exit code.
            if (enableResult.ExitCode != 0)
            {
                throw new ProcessExecutionException(
                    $"Failed to enable service. Service='{serviceFileName}'" +
                    $"{Environment.NewLine}" +
                    $"{string.Join(Environment.NewLine, enableResult.Output)}");
            }

            // Start new instance
            ProcessExecutionResult startResult = await this.ProcessExecution.ExecuteProcessAsync("sudo", $"systemctl start {serviceFileName}", executeTimeout)
                .ConfigureAwait(false);

            startResult.ThrowIfErrored<ProcessExecutionException>(
                $"Service startup failed. Service='{serviceFileName}', Executable Path='{executablePath}', Working Directory='{workingDirectory}'. " +
                $"{Environment.NewLine}" +
                $"{string.Join(Environment.NewLine, startResult.Output)}");
        }

        /// <inheritdoc />
        public bool IsRunningAsAdministrator()
        {
            return Mono.Unix.Native.Syscall.geteuid() == 0;
        }
    }
}
