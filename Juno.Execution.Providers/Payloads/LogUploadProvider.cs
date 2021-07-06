namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.IO;
    using System.IO.Abstractions;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Uploads log files
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
    [SupportedParameter(Name = Parameters.Payload, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.FileName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.UploadToKusto, Type = typeof(bool), Required = false)]
    public class LogUploadProvider : ExperimentProvider
    {
        private IFileSystem fileSystem;
        private AgentIdentification agentId;
        private AgentClient apiClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogUploadProvider"/> class.
        /// </summary>
        /// <param name="services"></param>
        public LogUploadProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            if (!this.Services.HasService<IFileSystem>())
            {
                this.Services.AddTransient<IFileSystem>((provider) => new FileSystem());
            }

            return base.ConfigureServicesAsync(context, component);
        }

        /// <inheritdoc/>
        protected async override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            // Cancelled state, accept state.
            if (cancellationToken.IsCancellationRequested)
            {
                return new ExecutionResult(ExecutionStatus.Cancelled);
            }

            this.fileSystem = this.Services.GetService<IFileSystem>();
            this.agentId = this.Services.GetService<AgentIdentification>();
            ClientPool<AgentClient> apiPool = this.Services.GetService<ClientPool<AgentClient>>();
            this.apiClient = apiPool.GetClient(ApiClientType.AgentFileUploadApi);
            string payload = component.Parameters.GetValue<string>(Parameters.Payload);
            string fileName = component.Parameters.GetValue<string>(Parameters.FileName);
            bool isUploadToKusto = component.Parameters.GetValue<bool>(Parameters.UploadToKusto, false);
            string filePath;

            try
            {
                filePath = this.fileSystem.Directory.GetFile(payload, fileName);
                if (isUploadToKusto)
                {
                    string logFileContents = await this.fileSystem.File.ReadAllTextAsync(filePath)
                        .ConfigureDefaults();
                    telemetryContext.AddContext(nameof(logFileContents), logFileContents);
                }
                else
                {
                    this.UploadLogFileToStorage(filePath, fileName, context, cancellationToken);
                }
            }
            catch (Exception e)
            {
                // Log exception for debugging.
                // We don't want this provider to fail the experiment.
                telemetryContext.AddContext(nameof(Exception), e);
            }

            return new ExecutionResult(ExecutionStatus.Succeeded);
        }

        private void UploadLogFileToStorage(string filePath, string fileName, ExperimentContext context, CancellationToken cancellationToken)
        {
            using (Stream fs = this.fileSystem.File.Open(filePath, FileMode.Open))
            {
                HttpResponseMessage uploadResponse = this.apiClient.UploadFileAsync(
                    context.ExperimentId,
                    "Host",
                    this.agentId.ToString(),
                    fileName,
                    "text/plain",
                    Encoding.UTF8,
                    fs,
                    DateTime.UtcNow,
                    cancellationToken).GetAwaiter().GetResult();

                uploadResponse.ThrowOnError<ExperimentException>();
            }
        }

        internal static class Parameters
        {
            internal const string Payload = "payload";
            internal const string FileName = "fileName";
            internal const string UploadToKusto = "uploadToKusto";
        }
    }
}
