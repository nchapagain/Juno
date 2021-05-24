namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.AgentRuntime.Tasks;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Polly;

    /// <summary>
    /// Provides monitors to run in the Juno Host agent process for capturing information
    /// about the physical node/system in operation during experiments.
    /// </summary>
    public class AgentSystemMonitoring
    {
        private CancellationToken cancellationToken;
        private ExperimentInstance currentExperiment;
        private AgentType agentType;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentSystemMonitoring"/> class.
        /// </summary>
        /// <param name="services">Services/dependencies required by the system monitors.</param>
        /// <param name="settings">Configuration settings for the environment.</param>
        /// <param name="agentId">The ID of the Juno Host agent.</param>
        /// <param name="agentType">Type of the agent</param>
        /// <param name="retryPolicy">A retry policy to use for API calls.</param>
        public AgentSystemMonitoring(IServiceCollection services, EnvironmentSettings settings, AgentIdentification agentId, AgentType agentType, IAsyncPolicy retryPolicy = null)
        {
            services.ThrowIfNull(nameof(services));
            settings.ThrowIfNull(nameof(settings));
            agentId.ThrowIfNull(nameof(agentId));

            this.Services = services;
            this.Settings = settings;
            this.RetryPolicy = retryPolicy ?? Policy.NoOpAsync();
            this.AgentId = agentId;
            this.agentType = agentType;
        }

        /// <summary>
        /// Gets the ID of the Juno Host agent.
        /// </summary>
        protected AgentIdentification AgentId { get; }

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
        /// Gets the retry policy to apply to API calls to handle transient failures.
        /// </summary>
        protected IAsyncPolicy RetryPolicy { get; }

        /// <summary>
        /// Gets the set of services/dependencies required by the monitoring tasks.
        /// </summary>
        protected IServiceCollection Services { get; }

        /// <summary>
        /// Gets the configuration settings for the environment.
        /// </summary>
        protected EnvironmentSettings Settings { get; }

        /// <summary>
        /// Starts each of the monitors registered returning a set of tasks that can be
        /// used to track them.
        /// </summary>
        public IEnumerable<Task> StartMonitors(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            EventContext.Persist(Guid.NewGuid());
            AgentMonitoringSettings monitoringSettings = this.Settings.AgentSettings.AgentMonitoringSettings;
            List<Task> result = new List<Task>();
            switch (this.agentType)
            {
                case AgentType.HostAgent:
                    SelLogMonitorTask selLogTask = new SelLogMonitorTask(
                        this.Services,
                        this.Settings,
                        Policy.Handle<ExperimentException>().WaitAndRetryAsync(retryCount: 3, (retries) => TimeSpan.FromSeconds(retries)));
                    selLogTask.Results += this.OnSelLogMonitorResult;
                    result.Add(selLogTask.ExecuteAsync(
                        monitoringSettings.SelLogMonitorSettings.MonitorInterval,
                        cancellationToken,
                        runImmediately: true));

                    SystemInformationMonitorTask siMonitorTask = new SystemInformationMonitorTask(this.Services, this.Settings);
                    result.Add(siMonitorTask.ExecuteAsync(
                        monitoringSettings.SystemInfoMonitorSettings.MonitorInterval,
                        cancellationToken,
                        runImmediately: true));

                    FpgaHealthMonitorTask fpgaHealthMonitorTask = new FpgaHealthMonitorTask(this.Services, this.Settings);
                    result.Add(fpgaHealthMonitorTask.ExecuteAsync(
                        monitoringSettings.FpgaHealthMonitorSettings.MonitorInterval,
                        cancellationToken,
                        runImmediately: true));

                    VirtualClientMonitorTask virtualClientLiteMonitorTask = new VirtualClientMonitorTask(this.Services, this.Settings);
                    result.Add(virtualClientLiteMonitorTask.ExecuteAsync(
                        monitoringSettings.VCMonitorSettings.MonitorInterval,
                        cancellationToken,
                        runImmediately: true));
                    break;

                case AgentType.GuestAgent:
                    VmUptimeMonitorTask vmUptimeTask = new VmUptimeMonitorTask(this.Services, this.Settings);
                    result.Add(vmUptimeTask.ExecuteAsync(
                        monitoringSettings.VmUptimeMonitorSettings.MonitorInterval,
                        cancellationToken,
                        runImmediately: true));
                    break;
            }

            return result;
        }

        /// <summary>
        /// Event handler handles the processing of SEL log results.
        /// </summary>
        [SuppressMessage("Naming", "AZCA1004:AsyncResultProhibited Rule", Justification = "Preferred model for event handler.")]
        protected void OnSelLogMonitorResult(object sender, ExecutionEventArgs<string> e)
        {
            if (e?.Results != null)
            {
                if (!this.cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        EventContext telemetryContext = EventContext.Persisted()
                            .AddContext("agentId", this.AgentId.ToString());

                        int attempts = 0;
                        List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

                        if (this.currentExperiment == null)
                        {
                            this.Logger.LogTelemetry($"{nameof(SelLogMonitorTask)}.GetExperiment", telemetryContext, () =>
                            {
                                try
                                {
                                    ClientPool<AgentClient> apiPool = this.Services.GetService<ClientPool<AgentClient>>();
                                    AgentClient apiClient = apiPool.GetClient(ApiClientType.AgentApi);

                                    this.RetryPolicy.ExecuteAsync(() =>
                                    {
                                        attempts++;
                                        HttpResponseMessage response = apiClient.GetAgentExperimentAsync(this.AgentId.ToString(), this.cancellationToken)
                                            .GetAwaiter().GetResult()
                                            .Handle(rsp =>
                                            {
                                                responses.Add(rsp);
                                                rsp.ThrowOnError<ExperimentException>();
                                            });

                                        this.currentExperiment = response.Content.ReadAsJsonAsync<ExperimentInstance>()
                                            .GetAwaiter().GetResult();

                                        return Task.CompletedTask;
                                    }).GetAwaiter().GetResult();
                                }
                                finally
                                {
                                    telemetryContext.AddContext(this.currentExperiment);
                                    telemetryContext.AddContext(responses);
                                    telemetryContext.AddContext(nameof(attempts), attempts);
                                }
                            });
                        }

                        telemetryContext.AddContext("experimentId", this.currentExperiment.Id);

                        this.Logger.LogTelemetry($"{nameof(SelLogMonitorTask)}.UploadFile", telemetryContext, () =>
                        {
                            attempts = 0;
                            responses.Clear();

                            try
                            {
                                ClientPool<AgentClient> apiPool = this.Services.GetService<ClientPool<AgentClient>>();
                                AgentClient apiClient = apiPool.GetClient(ApiClientType.AgentFileUploadApi);

                                this.RetryPolicy.ExecuteAsync(() =>
                                {
                                    attempts++;
                                    using (Stream logFileStream = new MemoryStream(Encoding.UTF8.GetBytes(e.Results)))
                                    {
                                        HttpResponseMessage uploadResponse = apiClient.UploadFileAsync(
                                            this.currentExperiment.Id,
                                            "Host",
                                            this.AgentId.ToString(),
                                            "sellog",
                                            "text/plain",
                                            Encoding.UTF8,
                                            logFileStream,
                                            DateTime.UtcNow,
                                            this.cancellationToken).GetAwaiter().GetResult();

                                        uploadResponse.ThrowOnError<ExperimentException>();
                                    }

                                    return Task.CompletedTask;
                                });
                            }
                            finally
                            {
                                telemetryContext.AddContext(this.currentExperiment);
                                telemetryContext.AddContext(responses);
                                telemetryContext.AddContext(nameof(attempts), attempts);
                            }
                        });
                    }
                    catch
                    {
                        // We do not want to crash the task thread nor the agent on an exception.
                        // We are capturing the exception in telemetry.
                    }
                }
            }
        }
    }
}
