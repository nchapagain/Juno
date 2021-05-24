namespace Juno.PowerShellModule
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension methods for the base experiment cmdlet class
    /// </summary>
    public static class ExperimentCmdletBaseExtensions
    {
        /// <summary>
        /// Method to get the complete list of Experiment templates available in COSMOS for this environment
        /// </summary>
        /// <param name="commandlet">The base experiment cmdlet executing this method.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<HttpResponseMessage> GetExperimentTemplateListAsync(this ExperimentCmdletBase commandlet)
        {
            commandlet.ThrowIfNull(nameof(commandlet));

            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                HttpResponseMessage responseMessage = null;
                await commandlet.RetryPolicy.ExecuteAsync(async () =>
                {
                    responseMessage = await commandlet.ExperimentsClient.GetExperimentTemplateListAsync(source.Token).ConfigureAwait(false);
                }).ConfigureAwait(false);

                return responseMessage;
            }
        }

        /// <summary>
        /// Method to get an Experiment template available in COSMOS for this environment
        /// </summary>
        /// <param name="commandlet">The base experiment cmdlet executing this method.</param>
        /// <param name="experimentTemplateId">An experiment template Id.</param>
        /// <param name="teamName">The team name associated with the experiment template.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<HttpResponseMessage> GetExperimentTemplateAsync(this ExperimentCmdletBase commandlet, string experimentTemplateId, string teamName)
        {
            commandlet.ThrowIfNull(nameof(commandlet));
            experimentTemplateId.ThrowIfNullOrWhiteSpace(nameof(experimentTemplateId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                HttpResponseMessage responseMessage = null;
                await commandlet.RetryPolicy.ExecuteAsync(async () =>
                {
                    responseMessage = await commandlet.ExperimentsClient.GetExperimentTemplateAsync(experimentTemplateId, teamName, source.Token).ConfigureAwait(false);
                }).ConfigureAwait(false);

                return responseMessage;
            }
        }

        /// <summary>
        /// Method to get the complete list of Execution Goal templates available in COSMOS for this environment
        /// </summary>
        /// <param name="commandlet">The base experiment cmdlet executing this method.</param>
        /// <param name="templateId">An execution goal template Id.</param>
        /// <param name="teamName">The team name associated with the execution goal template.</param>
        /// <param name="view">View info like summary, full etc.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<HttpResponseMessage> GetExecutionGoalTemplateListAsync(this ExperimentCmdletBase commandlet, string teamName = null, string templateId = null, Contracts.View view = Contracts.View.Summary)
        {
            commandlet.ThrowIfNull(nameof(commandlet));

            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                HttpResponseMessage responseMessage = null;
                await commandlet.RetryPolicy.ExecuteAsync(async () =>
                {
                    responseMessage = await commandlet.ExperimentsClient.GetTemplatesAsync(source.Token, teamName: teamName, view, templateId: templateId).ConfigureAwait(false);
                }).ConfigureAwait(false);

                return responseMessage;
            }
        }

        /// <summary>
        /// Method to get the complete list of Execution Goal templates available in COSMOS for this environment
        /// </summary>
        /// <param name="commandlet">The base experiment cmdlet executing this method.</param>
        /// <param name="executionGoalId">An execution goal template Id.</param>
        /// <param name="teamName">The team name associated with the execution goal template.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<HttpResponseMessage> GetExecutionGoalAsync(this ExperimentCmdletBase commandlet, string executionGoalId, string teamName)
        {
            commandlet.ThrowIfNull(nameof(commandlet));
            ////executionGoalId.ThrowIfNull(nameof(executionGoalId));
            ////teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                HttpResponseMessage responseMessage = null;
                await commandlet.RetryPolicy.ExecuteAsync(async () =>
                {
                    responseMessage = await commandlet.ExperimentsClient.GetExecutionGoalsAsync(source.Token, teamName, executionGoalId: executionGoalId).ConfigureAwait(false);
                }).ConfigureAwait(false);

                return responseMessage;
            }
        }

        /// <summary>
        /// Method to remove ExecutionGoal
        /// </summary>
        /// <param name="commandlet">The base experiment cmdlet executing this method.</param>
        /// <param name="executionGoalId">An execution goal template Id.</param>
        /// <param name="teamName">The team name associated with the execution goal template.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<HttpResponseMessage> RemoveExecutionGoalAsync(this ExperimentCmdletBase commandlet, string executionGoalId, string teamName)
        {
            commandlet.ThrowIfNull(nameof(commandlet));
            executionGoalId.ThrowIfNull(nameof(executionGoalId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                HttpResponseMessage responseMessage = null;
                await commandlet.RetryPolicy.ExecuteAsync(async () =>
                {
                    responseMessage = await commandlet.ExperimentsClient.DeleteExecutionGoalAsync(executionGoalId: executionGoalId, teamName, source.Token).ConfigureAwait(false);
                }).ConfigureAwait(false);

                return responseMessage;
            }
        }

        /// <summary>
        /// Method to remove ExecutionGoalTemplate
        /// </summary>
        /// <param name="commandlet">The base experiment cmdlet executing this method.</param>
        /// <param name="executionGoalTemplateId">An execution goal template Id.</param>
        /// <param name="teamName">The team name associated with the execution goal template.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<HttpResponseMessage> RemoveExecutionGoalTemplateAsync(this ExperimentCmdletBase commandlet, string executionGoalTemplateId, string teamName)
        {
            commandlet.ThrowIfNull(nameof(commandlet));
            executionGoalTemplateId.ThrowIfNull(nameof(executionGoalTemplateId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                HttpResponseMessage responseMessage = null;
                await commandlet.RetryPolicy.ExecuteAsync(async () =>
                {
                    responseMessage = await commandlet.ExperimentsClient.DeleteExecutionGoalTemplateAsync(executionGoalTemplateId: executionGoalTemplateId, teamName, source.Token).ConfigureAwait(false);
                }).ConfigureAwait(false);

                return responseMessage;
            }
        }
    }
}
