namespace Juno.Execution.Api
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Extensions.AspNetCore;
    using Juno.Extensions.Telemetry;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Juno execution notifications REST API controller for adding or reading message from azure queue
    /// </summary>
    /// <remarks>
    /// Introduction to ASP.NET Core
    /// https://docs.microsoft.com/en-us/aspnet/core/?view=aspnetcore-2.1
    ///
    /// ASP.NET Core MVC Controllers
    /// https://docs.microsoft.com/en-us/aspnet/core/mvc/controllers/actions?view=aspnetcore-2.1
    ///
    /// Kestrel Web Server (Self-Hosting)
    /// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-2.1
    ///
    /// Async/Await/ConfigureAwait Overview
    /// https://www.skylinetechnologies.com/Blog/Skyline-Blog/December_2018/async-await-configureawait
    /// </remarks>
    [ApiController]
    [Produces("application/json")]
    [Route("/api/notifications")]
    public partial class NotificationsController : ControllerBase
    {
        private const string V1 = "v1";

        /// <summary>
        /// Create new instance of <see cref="NotificationsController"/>
        /// </summary>
        /// <param name="notificationManager">Notification manager</param>
        /// <param name="logger">The trace/telemetry logger for the controller.</param>
        public NotificationsController(IExperimentNotificationManager notificationManager, ILogger logger = null)
        {
            notificationManager.ThrowIfNull(nameof(notificationManager));

            this.NotificationManager = notificationManager;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets the notification manager that handles the data access requirements
        /// for the controller.
        /// </summary>
        protected IExperimentNotificationManager NotificationManager { get; }

        /// <summary>
        /// Gets the trace/telemetry logger for the controller.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Creates a new experiment/work notice.
        /// </summary>
        /// <param name="workQueue">The name of the queue on which the notification should be placed.</param>
        /// <param name="notice">Context/metadata properties to include in the notice.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="visibilityDelay">
        /// The time in seconds (from current UTC time) to delay the visibility of the message. If not defined the message will be visible immediately.
        /// </param>
        /// <response code="201">Created. The experiment notice was created successfully.</response>
        /// <response code="400">Bad Request. The experiment notice is invalid.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        /// <returns></returns>
        [HttpPost]
        [Description("Creates a new experiment/work notice.")]
        [ApiExplorerSettings(GroupName = NotificationsController.V1)]
        [ProducesResponseType(typeof(ExperimentMetadataInstance), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateNoticeAsync([FromQuery] string workQueue, ExperimentMetadata notice, CancellationToken cancellationToken, [FromQuery] int? visibilityDelay = null)
        {
            // Persist the activity ID so that it can be used down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(notice, nameof(notice))
                .AddContext(nameof(workQueue), workQueue)
                .AddContext(nameof(visibilityDelay), visibilityDelay);

            return await this.ExecuteApiOperationAsync(EventNames.CreateExperimentNotice, telemetryContext, this.Logger, async () =>
            {
                TimeSpan? initialVisibilityDelay = null;
                if (visibilityDelay.HasValue)
                {
                    initialVisibilityDelay = TimeSpan.FromSeconds((int)visibilityDelay);
                }

                var instance = await this.NotificationManager.CreateNoticeAsync(workQueue, notice, cancellationToken, initialVisibilityDelay)
                    .ConfigureDefaults();

                telemetryContext.AddContext(instance, "noticen");

                return this.CreatedAtAction(nameof(this.GetNoticeAsync), new { }, instance);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Delete a notice in queue.
        /// </summary>
        /// <param name="workQueue">The name of the queue on which the notification should be deleted from.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="messageId">MessageId of the notice.</param>
        /// <param name="popReceipt">PopReceipt of the notice.</param>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        /// <returns></returns>
        [HttpDelete]
        [Description("Deletes a notice in queue.")]
        [ApiExplorerSettings(GroupName = NotificationsController.V1)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteNoticeAsync([FromQuery] string workQueue, [FromQuery] string messageId, [FromQuery] string popReceipt, CancellationToken cancellationToken)
        {
            // Persist the activity ID so that it can be used down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(workQueue), workQueue)
                .AddContext(nameof(messageId), messageId)
                .AddContext(nameof(popReceipt), popReceipt);

            return await this.ExecuteApiOperationAsync(EventNames.DeleteWorkNotice, telemetryContext, this.Logger, async () =>
            {
                await this.NotificationManager.DeleteNoticeAsync(workQueue, messageId, popReceipt, cancellationToken)
                    .ConfigureDefaults();

                return this.NoContent();
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Returns the priority notice in queue and hide the notice in the queue if defined.
        /// </summary>
        /// <param name="workQueue">The name of the queue on which the notification should be placed.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="visibilityDelay">
        /// The time in seconds (from current UTC time) to hide the notice. If not defined the message will still be visible.
        /// </param>
        /// <response code="200">Ok. And experiment notice is found.</response>
        /// <response code="204">No Content. No experiment notices were found.</response>
        /// <response code="404">The experiment notice is not found.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        /// <returns></returns>
        [HttpGet]
        [Description("Returns the priority notice in queue.")]
        [ApiExplorerSettings(GroupName = NotificationsController.V1)]
        [ProducesResponseType(typeof(ExperimentMetadataInstance), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetNoticeAsync([FromQuery] string workQueue, CancellationToken cancellationToken, [FromQuery] int? visibilityDelay = null)
        {
            // Persist the activity ID so that it can be used down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(workQueue), workQueue)
                .AddContext(nameof(visibilityDelay), visibilityDelay);

            return await this.ExecuteApiOperationAsync(EventNames.GetExperimentNotice, telemetryContext, this.Logger, async () =>
            {
                TimeSpan? noticeVisibilityDelay = null;
                if (visibilityDelay.HasValue)
                {
                    noticeVisibilityDelay = TimeSpan.FromSeconds((int)visibilityDelay);
                }

                var notice = await this.NotificationManager.PeekNoticeAsync(workQueue, cancellationToken, noticeVisibilityDelay)
                    .ConfigureDefaults();

                telemetryContext.AddContext(notice, "noticen");

                return this.Ok(notice);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Set the delay for which the notice will be visible.
        /// </summary>
        /// <param name="workQueue">The name of the queue on which the notification should be placed.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="messageId">MessageId of the notice.</param>
        /// <param name="popReceipt">PopReceipt of the notice.</param>
        /// <param name="visibilityDelay">
        /// The time in seconds (from current UTC time) to hide the notice. If not defined the message will still be visible.
        /// </param>
        /// <response code="200">Ok. And experiment notice is found.</response>
        /// <response code="204">No Content. No experiment notices were found.</response>
        /// <response code="404">The experiment notice is not found.</response>
        /// <response code="500">Internal Server Error. An unexpected error occurred on the server.</response>
        /// <returns></returns>
        [HttpPut]
        [Description("Updates a work notice to make it visible.")]
        [ApiExplorerSettings(GroupName = NotificationsController.V1)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SetNoticeVisibilityAsync([FromQuery] string workQueue, [FromQuery] string messageId, [FromQuery] string popReceipt, [FromQuery] int visibilityDelay, CancellationToken cancellationToken)
        {
            // Persist the activity ID so that it can be used down the callstack.
            EventContext telemetryContext = EventContext.Persist(activityId: Guid.NewGuid())
                .AddContext(nameof(workQueue), workQueue)
                .AddContext(nameof(messageId), messageId)
                .AddContext(nameof(popReceipt), popReceipt)
                .AddContext(nameof(visibilityDelay), visibilityDelay);

            return await this.ExecuteApiOperationAsync(EventNames.SetNoticeVisibility, telemetryContext, this.Logger, async () =>
            {
                await this.NotificationManager.SetNoticeVisibilityAsync(workQueue, messageId, popReceipt, TimeSpan.FromSeconds(visibilityDelay), cancellationToken)
                    .ConfigureDefaults();

                return this.Ok(messageId);
            }).ConfigureDefaults();
        }

        /// <summary>
        /// Provides the names of telemetry events emitted by the controller.
        /// </summary>
        private static class EventNames
        {
            public static readonly string CreateExperimentNotice = EventContext.GetEventName(ExperimentsController.ApiName, "CreateWorkNotice");
            public static readonly string GetExperimentNotice = EventContext.GetEventName(ExperimentsController.ApiName, "GetWorkNotice");
            public static readonly string DeleteWorkNotice = EventContext.GetEventName(ExperimentsController.ApiName, "DeleteWorkNotice");
            public static readonly string SetNoticeVisibility = EventContext.GetEventName(ExperimentsController.ApiName, "SetNoticeVisibility");
        }
    }
}