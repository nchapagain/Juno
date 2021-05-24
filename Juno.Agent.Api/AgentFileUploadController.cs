namespace Juno.Agent.Api
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.DataManagement;
    using Juno.Extensions.AspNetCore;
    using Juno.Extensions.Telemetry;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.Storage;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Juno execution REST API controller for managing agent file/content data.
    /// </summary>
    /// <remarks>
    /// Uploading Files in ASP.NET Core (streaming)
    /// https://dotnetcoretutorials.com/2017/03/12/uploading-files-asp-net-core/
    /// 
    /// https://blog.stephencleary.com/2016/11/streaming-zip-on-aspnet-core.html
    /// </remarks>
    [ApiController]
    [Route("/api/experiments")]
    [Produces("application/json")]
    public class AgentFileUploadController : ControllerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentFileUploadController"/>.
        /// </summary>
        /// <param name="fileManager">Manages the details of the file data storage.</param>
        /// <param name="logger">A logger that can be used to capture telemetry.</param>
        public AgentFileUploadController(IExperimentFileManager fileManager, ILogger logger = null)
        {
            fileManager.ThrowIfNull(nameof(fileManager));

            this.FileManager = fileManager;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the data layer component that provides data management functionality
        /// to the controller.
        /// </summary>
        protected IExperimentFileManager FileManager { get; }

        /// <summary>
        /// Gets the trace/telemetry logger for the controller.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// API method uploads a file from the HTTP request to backing storage.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment in which the agent and file are associated.</param>
        /// <param name="fileName">The name of the file (e.g. sellog).</param>
        /// <param name="agentType">The agent type (e.g. Host, Guest).</param>
        /// <param name="agentId">The ID of the agent submitting the file.</param>
        /// <param name="timestamp">The timestamp when the file was produced.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns></returns>
        [HttpPost("agent-files")]
        [Consumes("text/plain", "text/csv")]
        public async Task<IActionResult> UploadFileAsync(
            [FromQuery] string experimentId,
            [FromQuery] string fileName,
            [FromQuery] string agentType,
            [FromQuery] string agentId,
            [FromQuery] DateTime timestamp,
            CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            fileName.ThrowIfNullOrWhiteSpace(nameof(fileName));
            agentType.ThrowIfNullOrWhiteSpace(nameof(agentType));
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));

            EventContext telemetryContext = EventContext.Persist(Guid.NewGuid())
               .AddContext(nameof(experimentId), experimentId)
               .AddContext(nameof(agentType), agentType)
               .AddContext(nameof(agentId), agentId)
               .AddContext(nameof(fileName), fileName)
               .AddContext("contentType", this.Request.ContentType)
               .AddContext(nameof(timestamp), timestamp);

            return await this.ExecuteApiOperationAsync(EventNames.CreateFile, telemetryContext, this.Logger, async () =>
            {
                if (this.Request.Body != null)
                {
                    string contentType;
                    Encoding contentEncoding;
                    if (!AgentFileUploadController.TryParseContentType(this.Request, out contentType, out contentEncoding))
                    {
                        throw new SchemaException(
                            $"Missing content type header. The file content type and encoding must be provided in the headers for the " +
                            $"request (e.g. Content-Type: text/plain; charset=utf-8");
                    }

                    BlobStream file = new BlobStream(this.Request.Body, contentType, contentEncoding);
                    await this.FileManager.CreateFileAsync(experimentId, fileName, file, timestamp, cancellationToken, agentType, agentId)
                        .ConfigureDefaults();
                }

                return this.Ok();
            }).ConfigureDefaults();
        }

        private static bool TryParseContentType(HttpRequest request, out string contentType, out Encoding contentEncoding)
        {
            contentEncoding = null;
            contentType = null;
            bool contentTypeDefined = false;

            if (!string.IsNullOrWhiteSpace(request.ContentType))
            {
                // Format:
                // text/plain; charset=utf-8
                string[] contentTypeParts = request.ContentType.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                if (contentTypeParts.Length > 1)
                {
                    contentTypeDefined = true;
                    contentType = contentTypeParts[0].Trim();

                    int charsetIndex = contentTypeParts[1].IndexOf("=", StringComparison.OrdinalIgnoreCase);
                    if (charsetIndex >= 0)
                    {
                        contentEncoding = Encoding.GetEncoding(contentTypeParts[1].Substring(charsetIndex + 1).Trim());
                    }
                    else
                    {
                        contentEncoding = Encoding.GetEncoding(contentTypeParts[1].Trim());
                    }
                }
            }

            return contentTypeDefined;
        }

        /// <summary>
        /// Provides the names of telemetry events emitted by the file manager.
        /// </summary>
        private static class EventNames
        {
            public static readonly string CreateFile = EventContext.GetEventName(AgentExperimentsController.ApiName, "UploadFile");
        }
    }
}
