namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.AgentRuntime;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;

    /// <summary>
    /// Provider that allows for pre-defined commands to be executed on node.
    /// </summary>
    [SupportedParameter(Name = Parameters.Scenario, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.ConfigurationId, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.ExecutableName, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.Payload, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.LogFileName, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.MaxExecutionTime, Type = typeof(TimeSpan), Required = false)]
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    public class AgentExecuteCommandProvider : ExperimentProvider
    {
        private static readonly TimeSpan DefaultTimeOut = TimeSpan.FromHours(1);
        private NodeExecutableSettings settings;

        /// <summary>
        /// Initialize a new instance of <see cref="AgentExecuteCommandProvider"/>
        /// </summary>
        /// <param name="services">Collection of services utilized for dependency injection.</param>
        public AgentExecuteCommandProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            if (!this.Services.HasService<IProcessExecution>())
            {
                this.Services.AddTransient<IProcessExecution>((provider) => new ProcessExecution());
            }

            if (!this.Services.HasService<IFileSystem>())
            {
                this.Services.AddTransient<IFileSystem>((provider) => new FileSystem());
            }

            string configurationId = component.Parameters.GetValue<string>(Parameters.ConfigurationId, string.Empty);
            // Allow for injecting parameters under the circumstance
            // iff the payload is set to JunoCustomPayload
            if (string.IsNullOrEmpty(configurationId)) 
            {
                string payload = component.Parameters.GetValue<string>(Parameters.Payload, string.Empty);
                if (!payload.Equals(Parameters.JunoCustomPayload, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ProviderException($"{nameof(AgentExecuteCommandProvider)} only supports debug mode " +
                        $"when executing commands from: {Parameters.JunoCustomPayload}. The payload given was: {payload}");
                }

                this.settings = new NodeExecutableSettings()
                {
                    ExecutableName = component.Parameters.GetValue<string>(Parameters.ExecutableName),
                    Payload = component.Parameters.GetValue<string>(Parameters.Payload),
                    LogFileName = component.Parameters.GetValue<string>(Parameters.LogFileName),
                    MaxExecutionTime = component.Parameters.GetTimeSpanValue(Parameters.MaxExecutionTime, AgentExecuteCommandProvider.DefaultTimeOut)
                };

                return base.ConfigureServicesAsync(context, component);
            }

            if (!this.Services.TryGetService<NodeExecutableSettings>(out NodeExecutableSettings exeSettings))
            {
                IConfiguration configuration = context.Configuration;
                EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
                IEnumerable<NodeExecutableSettings> executableSettings = settings.NodeExecutableSettings;
                exeSettings = executableSettings.Get(configurationId);                    
            }

            this.settings = exeSettings;
            return base.ConfigureServicesAsync(context, component);
        }

        /// <summary>
        /// Executes a command on the machine running on behalf of the agent.
        /// </summary>
        protected async override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            // Cancelled state, accept state.
            if (cancellationToken.IsCancellationRequested)
            {
                return new ExecutionResult(ExecutionStatus.Cancelled);
            }

            State state = await this.GetStateAsync<State>(context, cancellationToken).ConfigureDefaults();

            telemetryContext.AddContext(nameof(state), state);
            string scenario = component.Parameters.TryGetValue(Parameters.Scenario, out IConvertible value) ? value.ToString() : null;

            // Start state
            if (state == null)
            {
                DateTime timeout = DateTime.UtcNow.Add(this.settings.MaxExecutionTime ?? AgentExecuteCommandProvider.DefaultTimeOut);
                DateTime stepLowerBound = DateTime.UtcNow.Add(this.settings.MinExecutionTime ?? TimeSpan.Zero);
                state = new State(timeout, stepLowerBound, 0);
                this.ActivateExecutable(context, state, scenario, cancellationToken);
                await this.SaveStateAsync<State>(context, state, cancellationToken).ConfigureDefaults();

                return new ExecutionResult(ExecutionStatus.InProgress);
            }

            // Process has exited, evaluate whether to enter success, retry, or failed state.
            if (state.ProcessExitCode != null)
            {
                // If the process is short lived, enter failed state.
                AgentExecuteCommandProvider.ThrowOnShortLivedProcess(state);
                string logFileContents = await this.ReadLogFileAsync(scenario).ConfigureAwait(false);
                telemetryContext.AddContext(nameof(logFileContents), logFileContents);

                // If the process has exited with a non-zero exit code, state can enter failed state, or retry state.
                if (state.ProcessExitCode != 0)
                {
                    // If the retry strings are present and the state has not reached the max retry count, enter retry state (start state').
                    if (!this.IsRetryable(logFileContents) || state.RetryCount >= this.settings.Retries)
                    {
                        throw new ProviderException($"The strings necessary to allow for a retry could not be found: {this.settings.RetryableString} in the file: {this.settings.LogFileName} " +
                            $"the process attempted has reached a failed-unretryable state. Exit Code: {state.ProcessExitCode}");
                    }

                    this.ActivateExecutable(context, state, scenario, cancellationToken);
                    await this.SaveStateAsync<State>(context, new State(state.StepTimeout, state.StepLowerBound, state.RetryCount + 1), cancellationToken).ConfigureDefaults();
                    return new ExecutionResult(ExecutionStatus.InProgress);
                }

                // Check if state is successful, if yes move to terminal success state.
                if (!this.IsSuccess(logFileContents))
                {
                    throw new ProviderException($"The required success strings could not be found: {this.settings.SuccessStrings} in the file: {this.settings.LogFileName}");
                }

                return new ExecutionResult(ExecutionStatus.Succeeded);
            }

            AgentExecuteCommandProvider.ThrowOnTimeout(state);

            return new ExecutionResult(ExecutionStatus.InProgress);
        }

        /// <summary>
        /// Evaluates if the current state establishes the success terminal state.
        /// </summary>
        /// <param name="logFileContents">Contents of the log file.</param>
        /// <returns>True/False if the process execution was a success.</returns>
        protected virtual bool IsSuccess(string logFileContents)
        {
            logFileContents.ThrowIfNullOrWhiteSpace(nameof(logFileContents));
            // If its not required just set to true
            return (this.settings.SuccessStrings == null || !this.settings.SuccessStrings.Any()) ||
                this.settings.SuccessStrings?.All(s => logFileContents.Contains(s, StringComparison.OrdinalIgnoreCase)) == true;
        }

        /// <summary>
        /// Evaluates if the current state contains the signal to allow for retry.
        /// </summary>
        /// <param name="logFileContents">Contents of the log file.</param>
        protected virtual bool IsRetryable(string logFileContents)
        {
            logFileContents.ThrowIfNullOrWhiteSpace(nameof(logFileContents));
            // If there are no retry strings, automatically fail.
            return this.settings.RetryableString?.Any() == true &&
                this.settings.RetryableString.Any(s => logFileContents.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Format and then execute the command on the machine.
        /// </summary>
        /// <param name="context">Context of the experiment.</param>
        /// <param name="state">State of the provider.</param>
        /// <param name="scenario">The optional scenario used to locate correct .exe</param>
        /// <param name="cancellationToken">Token used for cancelling a thread of execution.</param>
        protected virtual void ActivateExecutable(ExperimentContext context, State state, string scenario, CancellationToken cancellationToken)
        {
            EventContext telemetryContext = EventContext.Persisted();
            IProcessExecution processExecution = this.Services.GetService<IProcessExecution>();
            IFileSystem fileSystem = this.Services.GetService<IFileSystem>();

            this.Logger.LogTelemetry($"{nameof(AgentExecuteCommandProvider)}.Activate", telemetryContext, () =>
            {
                // Create new action for process to take on exit.
                async void ExitAction(object sender, EventArgs args)
                {
                    await this.Logger.LogTelemetryAsync($"{nameof(AgentExecuteCommandProvider)}.CompleteProcess", telemetryContext, async () =>
                    {
                        Process currentProcess = sender as Process;
                        telemetryContext.AddContext(nameof(currentProcess.StartInfo), currentProcess.StartInfo);
                        telemetryContext.AddContext(nameof(currentProcess.ExitCode), currentProcess.ExitCode);
                        telemetryContext.AddContext(nameof(currentProcess.ExitTime), currentProcess.ExitTime);
                        await this.SaveStateAsync<State>(context, new State(state.StepTimeout, state.StepLowerBound, state.RetryCount, currentProcess.ExitCode), cancellationToken).ConfigureDefaults();
                    }).ConfigureDefaults();
                }

                string workingDirectory = fileSystem.Directory.GetParentDirectory(this.settings.Payload, this.settings.ExecutableName, scenario);
                string arguments = $"/c {this.settings.ExecutableName} {this.settings.Arguments} 1>{this.settings.LogFileName} 2>&1 `& exit";

                telemetryContext.AddContext(nameof(workingDirectory), workingDirectory);
                telemetryContext.AddContext(nameof(arguments), arguments);
                int processId = processExecution.CreateProcess("cmd.exe", arguments, workingDirectory, ExitAction);
                telemetryContext.AddContext(nameof(processId), processId);
            });
        }

        private Task<string> ReadLogFileAsync(string scenario = null)
        {
            IFileSystem fileSystem = this.Services.GetService<IFileSystem>();
            if (!fileSystem.Directory.FileExists(this.settings.Payload, this.settings.LogFileName, scenario))
            {
                throw new ProviderException($"The log file: {this.settings.LogFileName} could not be found.");
            }

            string filePath = fileSystem.Directory.GetFile(this.settings.Payload, this.settings.LogFileName);
            return fileSystem.File.ReadAllTextAsync(filePath);
        }

        private static void ThrowOnTimeout(State state)
        {
            if (state.IsTimeoutExpired)
            {
                throw new TimeoutException(
                    $"{nameof(AgentExecuteCommandProvider)} attempt timed out. The time allowed has expired)");
            }
        }

        private static void ThrowOnShortLivedProcess(State state)
        {
            if (state.IsShortLived)
            {
                throw new TimeoutException(
                    $"{nameof(AgentExecuteCommandProvider)} attempt executed too fast. Failing step.");
            }
        }

        /// <summary>
        /// State of the Agent Execute Command.
        /// </summary>
        protected internal class State
        {
            /// <summary>
            /// Initializes a new instance of <see cref="State"/>
            /// </summary>
            /// <param name="stepLowerBound">The Datetime at which the process must reach before exiting.</param>
            /// <param name="retryCount">The count of retries executed.</param>
            /// <param name="processExitCode">The exit code that the process returns.</param>
            /// <param name="stepTimeout">The maximum time alloted for the step to finish.</param>
            [JsonConstructor]
            public State(DateTime stepTimeout, DateTime stepLowerBound, int retryCount, int? processExitCode = null)
            {
                this.StepTimeout = stepTimeout;
                this.StepLowerBound = stepLowerBound;
                this.ProcessExitCode = processExitCode;
                this.RetryCount = retryCount;
            }

            /// <summary>
            /// Evaluates if the timeout has currently expired.
            /// </summary>
            [JsonIgnore]
            public bool IsTimeoutExpired => DateTime.UtcNow > this.StepTimeout;

            /// <summary>
            /// Evaluates if the process has not reached min execution time.
            /// </summary>
            [JsonIgnore]
            public bool IsShortLived => DateTime.UtcNow < this.StepLowerBound;

            /// <summary>
            /// The exit code the process exited with.
            /// </summary>
            public int? ProcessExitCode { get; }

            /// <summary>
            /// The number of retries executed.
            /// </summary>
            public int RetryCount { get; }

            /// <summary>
            /// The step timeout
            /// </summary>
            public DateTime StepTimeout { get; }

            /// <summary>
            /// The lower bound of step execution time.
            /// </summary>
            public DateTime StepLowerBound { get; }
        }

        private class Parameters
        {
            public const string Scenario = nameof(Parameters.Scenario);
            public const string ConfigurationId = nameof(Parameters.ConfigurationId);
            public const string ExecutableName = nameof(Parameters.ExecutableName);
            public const string Payload = nameof(Parameters.Payload);
            public const string LogFileName = nameof(Parameters.LogFileName);
            public const string MaxExecutionTime = nameof(Parameters.MaxExecutionTime);
            public const string JunoCustomPayload = nameof(Parameters.JunoCustomPayload);
        }
    }
}
