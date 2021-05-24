namespace Juno.Extensions.AspNetCore
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.CRC.AspNetCore;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Extension methods for Juno ASP.NET Core API controllers.
    /// </summary>
    public static class ApiControllerExtensions
    {
        /// <summary>
        /// Extension executes an API operation action/delegate, captures telemetry and returns
        /// a result.
        /// </summary>
        /// <param name="controller">The ASP.NET Core controller.</param>
        /// <param name="eventName">The name of the telemetry event (e.g. CreateExperiment, GetExperimentSteps).</param>
        /// <param name="telemetryContext">The telemetry event context object providing information to include with the telemetry events.</param>
        /// <param name="operation">The operation action/delegate to execute.</param>
        /// <param name="logger">The logger/telemetry logger.</param>
        /// <returns>
        /// An <see cref="IActionResult"/> from the execution of the operation.
        /// </returns>
        public static async Task<IActionResult> ExecuteApiOperationAsync(
            this ControllerBase controller,
            string eventName,
            EventContext telemetryContext,
            ILogger logger,
            Func<Task<IActionResult>> operation)
        {
            controller.ThrowIfNull(nameof(controller));
            eventName.ThrowIfNullOrWhiteSpace(nameof(eventName));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            logger.ThrowIfNull(nameof(logger));
            operation.ThrowIfNull(nameof(operation));

            return await logger.LogTelemetryAsync(eventName, telemetryContext, async () =>
            {
                IActionResult result = null;
                Exception apiError = null;
                LogLevel apiErrorLevel = LogLevel.Warning;

                try
                {
                    result = await operation.Invoke().ConfigureAwait(false);
                }
                catch (SchemaException exc)
                {
                    result = controller.DataSchemaInvalid(exc.Message);
                    apiError = exc;
                }
                catch (DataStoreException exc) when (exc.Reason == DataErrorReason.DataAlreadyExists)
                {
                    result = controller.DataConflict(exc.Message);
                    apiError = exc;
                }
                catch (DataStoreException exc) when (exc.Reason == DataErrorReason.DataNotFound)
                {
                    result = controller.DataNotFound(exc.Message);
                    apiError = exc;
                }
                catch (DataStoreException exc) when (exc.Reason == DataErrorReason.ETagMismatch)
                {
                    result = controller.DataETagConflict(exc.Message);
                    apiError = exc;
                }
                catch (DataStoreException exc) when (exc.Reason == DataErrorReason.PartitionKeyMismatch)
                {
                    result = controller.DataPartitionConflict(exc.Message);
                    apiError = exc;
                }
                catch (CosmosException exc)
                {
                    result = controller.Error(exc, (int)exc.StatusCode);
                }
                catch (Microsoft.Azure.Cosmos.Table.StorageException exc)
                {
                    result = controller.Error(
                        exc,
                        exc.RequestInformation != null ? (int)exc.RequestInformation.HttpStatusCode : StatusCodes.Status500InternalServerError);

                    apiError = exc;
                    apiErrorLevel = LogLevel.Error;
                }
                catch (Microsoft.Azure.Storage.StorageException exc)
                {
                    result = controller.Error(
                        exc,
                        exc.RequestInformation != null ? (int)exc.RequestInformation.HttpStatusCode : StatusCodes.Status500InternalServerError);

                    apiError = exc;
                    apiErrorLevel = LogLevel.Error;
                }
                catch (Exception exc)
                {
                    result = controller.Error(exc);
                }
                finally
                {
                    telemetryContext.Properties["statusCode"] = (result as IStatusCodeActionResult)?.StatusCode;
                    if (apiError != null)
                    {
                        EventContext errorContext = telemetryContext.Clone()
                            .AddError(apiError, withCallStack: true);

                        await logger.LogTelemetryAsync($"{eventName}Error", apiErrorLevel, errorContext).ConfigureAwait(false);
                    }
                }

                return result;
            }).ConfigureAwait(false);
        }
    }
}
