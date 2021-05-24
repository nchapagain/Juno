namespace Juno.Execution.AgentRuntime.Tasks
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts.Configuration;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Polly;

    /// <summary>
    /// Base class for background tasks that run as part of a host/agent
    /// process.
    /// </summary>
    public abstract class AgentTask
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentTask"/> class.
        /// </summary>
        /// <param name="services">
        /// Services collection provides required dependencies to the host task/operation.
        /// </param>
        /// <param name="settings">Configuration settings for the environment.</param>
        /// <param name="retryPolicy">A retry policy to apply to the execution of the task operation.</param>
        protected AgentTask(IServiceCollection services, EnvironmentSettings settings, IAsyncPolicy retryPolicy = null)
        {
            services.ThrowIfNull(nameof(services));
            settings.ThrowIfNull(nameof(settings));

            this.Services = services;
            this.Settings = settings;
            this.RetryPolicy = retryPolicy ?? Policy.NoOpAsync();
            this.Timer = new Stopwatch();
        }

        /// <summary>
        /// Gets the logger from the services that is used to capture telemetry.
        /// </summary>
        protected ILogger Logger
        {
            get
            {
                return this.Services.HasService<ILogger>()
                    ? this.Services.GetService<ILogger>()
                    : NullLogger.Instance;
            }
        }

        /// <summary>
        /// Gets the configuration settings for the environment.
        /// </summary>
        protected EnvironmentSettings Settings { get; }

        /// <summary>
        /// Gets the retry policy to apply to the execution of the task
        /// operation.
        /// </summary>
        protected IAsyncPolicy RetryPolicy { get; }

        /// <summary>
        /// Gets the services collection provides required dependencies to 
        /// the host task/operation.
        /// </summary>
        protected IServiceCollection Services { get; }

        /// <summary>
        /// A stopwatch that can be used to capture the elapsed time of an operation.
        /// </summary>
        protected Stopwatch Timer { get; }

        /// <summary>
        /// When implemented executes the host/agent operation task logic.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// A task that can be used to execute the host/agent logic asynchronously.
        /// </returns>
        public abstract Task ExecuteAsync(CancellationToken cancellationToken);

        /// <summary>
        /// When implemented executes the monitoring operation.
        /// </summary>
        /// <param name="interval">A constant interval at which to execute the agent task.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="runImmediately">
        /// True if the task should execute its first run immediately without delay or false if it should wait until the 
        /// first interval passes before beginning execution.
        /// </param>
        /// <returns>
        /// A task that can be used to execute the monitoring operation asynchronously.
        /// </returns>
        public async Task ExecuteAsync(TimeSpan interval, CancellationToken cancellationToken, bool runImmediately = true)
        {
            await Task.Run(async () =>
            {
                bool delayExecution = !runImmediately;
                while (!cancellationToken.IsCancellationRequested)
                {
                    this.Timer.Restart();
                    if (delayExecution)
                    {
                        // Delay the first execution.
                        delayExecution = false;
                    }
                    else
                    {
                        try
                        {
                            await this.ExecuteAsync(cancellationToken).ConfigureDefaults();
                        }
                        catch (Exception exc)
                        {
                            EventContext context = EventContext.Persisted()
                                .AddError(exc);

                            this.Logger.LogTelemetry($"{this.GetType().Name}Error", LogLevel.Error, context);
                        }
                    }

                    this.Timer.Stop();
                    await AgentTask.WaitAsync(this.Timer, interval).ConfigureDefaults();
                }
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Waits for the amount of time remaining between the time that has elapsed
        /// and the execution interval. This is used to ensure execution on consistent
        /// intervals.
        /// </summary>
        /// <param name="timer">A stopwatch timer that contains the elapsed time for a given operation.</param>
        /// <param name="executionInterval">The interval at which the logic should continuously execute.</param>
        /// <returns>
        /// A task that can be used to wait for the remaining time between execution intervals.
        /// </returns>
        protected static async Task WaitAsync(Stopwatch timer, TimeSpan executionInterval)
        {
            timer.ThrowIfNull(nameof(timer));
            executionInterval.ThrowIfNull(nameof(executionInterval));

            if (timer.Elapsed < executionInterval)
            {
                TimeSpan waitInterval = executionInterval.Subtract(timer.Elapsed);
                await Task.Delay(waitInterval).ConfigureDefaults();
            }
        }
    }
}