namespace Juno.Api.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Contracts.OData;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Polly;

    /// <summary>
    /// Juno Execution REST API client.
    /// </summary>
    public class ExecutionClient
    {
        private const string ExperimentsApiRoute = "/api/experiments";
        private const string HeartbeatsApiRoute = "/api/heartbeats";
        private const string NotificationApiRoute = "/api/notifications";
        private const string ExecutionGoalsApiRoute = "/api/executionGoals";
        private const string ExecutionGoalTemplateApiRoute = "/api/executionGoalTemplates";
        private const string ExperimentStatusApiRoute = "/api/experimentstatus";
        private const string ExperimentTemplatesApiRoute = "/api/experimentTemplates";

        /// <summary>
        /// Defines the default retry policy for Api operations on timeout only
        /// </summary>
        private static IAsyncPolicy defaultRetryPolicy = Policy.Handle<WebException>((exc) =>
        {
            return exc.Status == WebExceptionStatus.Timeout;
        })
        .WaitAndRetryAsync(1, (retries) => TimeSpan.FromMilliseconds(retries * 100));

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionClient"/> class.
        /// </summary>
        /// <param name="restClient">
        /// The REST client that handles HTTP communications with the API
        /// service.
        /// </param>
        /// <param name="baseUri">
        /// The base URI to the server hosting the API (e.g. https://juno-prod01.westUS2.webapps.net).
        /// </param>
        /// <param name="retryPolicy">A retry policy to apply to api call operations that experience transient issues.</param>
        public ExecutionClient(IRestClient restClient, Uri baseUri, IAsyncPolicy retryPolicy = null)
        {
            restClient.ThrowIfNull(nameof(restClient));
            baseUri.ThrowIfNull(nameof(baseUri));

            this.RestClient = restClient;
            this.BaseUri = baseUri;
            this.RetryPolicy = retryPolicy ?? ExecutionClient.defaultRetryPolicy;
        }

        /// <summary>
        /// Gets the base URI to the server hosting the API.
        /// </summary>
        protected Uri BaseUri { get; }

        /// <summary>
        /// Gets or sets the REST client that handles HTTP communications
        /// with the API service.
        /// </summary>
        protected IRestClient RestClient { get; }

        /// <summary>
        /// Gets the retry policy to apply when experiencing transient issues.
        /// </summary>
        protected IAsyncPolicy RetryPolicy { get; }

        /// <summary>
        /// Makes an API request to create a new step for the agent associated with the parent step.
        /// </summary>
        /// <param name="definition">The agent/child step definition.</param>
        /// <param name="agentId">The unique ID of the agent within the environment.</param>
        /// <param name="parentStep">The unique ID of the parent step of the agent step.</param>
        /// <param name="cancellationToken">A token that can be used bo cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing an array of <see cref="ExperimentStepInstance"/> instance
        /// created.
        /// </returns>
        public async Task<HttpResponseMessage> CreateExperimentAgentStepsAsync(ExperimentStepInstance parentStep, ExperimentComponent definition, string agentId, CancellationToken cancellationToken)
        {
            parentStep.ThrowIfNull(nameof(parentStep));
            definition.ThrowIfNull(nameof(definition));
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));

            // Format: /api/experiments/{experimentId}/agent-steps?agentId={agentId}&parentStepId={stepId}
            string route = $"{ExecutionClient.ExperimentsApiRoute}/{WebUtility.UrlEncode(parentStep.ExperimentId)}/agent-steps";
            string queryString = $"?agentId={WebUtility.UrlEncode(agentId)}&parentStepId={WebUtility.UrlEncode(parentStep.Id)}";

            Uri requestUri = new Uri(this.BaseUri, route + queryString);

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(definition.ToJson()))
            {
                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to create a new experiment instance.
        /// </summary>
        /// <param name="experiment">The experiment definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentInstance"/> created.
        /// </returns>
        public async Task<HttpResponseMessage> CreateExperimentAsync(Experiment experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(experiment.ToJson()))
            {
                // Format: /api/experiments
                string route = ExecutionClient.ExperimentsApiRoute;
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to create a context/metadata instance for an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment for which the context/metadata is related.</param>
        /// <param name="context">The experiment context/metadata instance.</param>
        /// <param name="cancellationToken">A token that can be used bo cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentMetadataInstance"/> created.
        /// </returns>
        public async Task<HttpResponseMessage> CreateExperimentContextAsync(string experimentId, ExperimentMetadata context, CancellationToken cancellationToken, string contextId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            context.ThrowIfNull(nameof(context));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(context.ToJson()))
            {
                // Format: /api/experiments/{experimentId}/context
                // Format: /api/experiments/{experimentId}/context?contextId={contextId}
                string route = contextId == null ?
                    $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}/context" :
                    $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}/context?contextId={contextId}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to create steps associated with an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment for which the context/metadata is related.</param>
        /// <param name="cancellationToken">A token that can be used bo cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing an array of <see cref="ExperimentStepInstance"/> instances
        /// created.
        /// </returns>
        public async Task<HttpResponseMessage> CreateExperimentStepsAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(string.Empty))
            {
                // Format: /api/experiments/{experimentId}/steps
                string route = $"{ExecutionClient.ExperimentsApiRoute}/{WebUtility.UrlEncode(experimentId)}/steps";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                    .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to create steps associated with an experiment given a definition.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment for which the context/metadata is related.</param>
        /// <param name="sequence">Defines the the sequence for the step.</param>
        /// <param name="definition">The workflow step definition to create for the experiment in the system.</param>
        /// <param name="cancellationToken">A token that can be used bo cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing an array of <see cref="ExperimentStepInstance"/> instances
        /// created.
        /// </returns>
        public async Task<HttpResponseMessage> CreateExperimentStepsAsync(string experimentId, int sequence, ExperimentComponent definition, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            definition.ThrowIfNull(nameof(definition));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(definition.ToJson()))
            {
                // Format: /api/experiments/{experimentId}/steps?sequence=150
                // BODY: ExperimentComponent
                string route = $"{ExecutionClient.ExperimentsApiRoute}/{WebUtility.UrlEncode(experimentId)}/steps?sequence={sequence}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                    .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to create a new experiment definition instance.
        /// </summary>
        /// <param name="experiment">The experiment definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentInstance"/> created.
        /// </returns>
        public async Task<HttpResponseMessage> CreateExperimentTemplateAsync(Experiment experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(experiment.ToJson()))
            {
                string route = ExecutionClient.ExperimentTemplatesApiRoute;
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to creates a new experiment/work notice.
        /// </summary>
        /// <param name="workQueue">The name of the queue on which the notification should be placed.</param>
        /// <param name="context">Context/metadata properties to include in the notice.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="visibilityDelay">
        /// The interval of time from now during which the message will be invisible in timespan. If null then the message will be visible immediately.
        /// </param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentMetadataInstance"/> created.
        /// </returns>
        public async Task<HttpResponseMessage> CreateNoticeAsync(string workQueue, ExperimentMetadata context, CancellationToken cancellationToken, TimeSpan? visibilityDelay = null)
        {
            context.ThrowIfNull(nameof(context));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(context.ToJson()))
            {
                // Format: /api/notifications?workQueue=experimentnotices
                //         /api/notifications?workQueue=experimentnotices&visibilityDelay=10
                string route = visibilityDelay == null
                    ? $"{ExecutionClient.NotificationApiRoute}?workQueue={workQueue}"
                    : $"{ExecutionClient.NotificationApiRoute}?workQueue={workQueue}&visibilityDelay={visibilityDelay.Value.TotalSeconds}";

                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Create an Execution Goal and post to the system
        /// </summary>
        /// <param name="executionGoal">The Execution Goal to create in the system</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="Item{GoalBasedSchedule}"/> created.
        /// </returns>        
        public async Task<HttpResponseMessage> CreateExecutionGoalAsync(Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(executionGoal.ToJson()))
            {
                // Format: /api/executionGoals
                string route = $"{ExecutionClient.ExecutionGoalsApiRoute}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Create an Execution Goal Template and post to the system
        /// </summary>
        /// <param name="executionGoalTemplate">The Execution Goal to create in the system</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="GoalBasedSchedule"/> created.
        /// </returns>        
        public async Task<HttpResponseMessage> CreateExecutionGoalTemplateAsync(Item<GoalBasedSchedule> executionGoalTemplate, CancellationToken cancellationToken)
        {
            executionGoalTemplate.ThrowIfNull(nameof(executionGoalTemplate));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(executionGoalTemplate.ToJson()))
            {
                // Format: /api/executionGoalTemplates
                string route = $"{ExecutionClient.ExecutionGoalTemplateApiRoute}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Create an execution goal from template and post to system
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="templateId"></param>
        /// <param name="teamName">Team that owns the template</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> CreateExecutionGoalFromTemplateAsync(ExecutionGoalParameter parameters, string templateId, string teamName, CancellationToken cancellationToken)
        {
            parameters.ThrowIfNull(nameof(parameters));
            templateId.ThrowIfNullOrWhiteSpace(nameof(templateId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(parameters.ToJson()))
            {
                // Format: /api/executionGoals/templateId/
                string route = $"{ExecutionClient.ExecutionGoalsApiRoute}/{Uri.EscapeUriString(templateId)}?teamName={Uri.EscapeUriString(teamName)}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Create an execution goal from template and post to system
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="templateId"></param>
        /// <param name="teamName">Team that owns the template</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> UpdateExecutionGoalFromTemplateAsync(ExecutionGoalParameter parameters, string templateId, string teamName, CancellationToken cancellationToken)
        {
            parameters.ThrowIfNull(nameof(parameters));
            templateId.ThrowIfNullOrWhiteSpace(nameof(templateId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(parameters.ToJson()))
            {
                // Format: /api/executionGoals/templateId/
                string route = $"{ExecutionClient.ExecutionGoalsApiRoute}/{Uri.EscapeUriString(templateId)}?teamName={Uri.EscapeUriString(teamName)}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to delete steps associated with an agent associated with an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment whose agent steps will be deleted.</param>
        /// <param name="cancellationToken">A token that can be used bo cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the result.
        /// </returns>
        public async Task<HttpResponseMessage> DeleteExperimentAgentStepsAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Format: /api/experiments/{experimentId}/agent-steps
            string route = $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}/agent-steps";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.DeleteAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to delete an experiment instance.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment to delete.</param>
        /// <param name="cancellationToken">A token that can be used bo cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the result.
        /// </returns>
        public async Task<HttpResponseMessage> DeleteExperimentAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Format: /api/experiments/{experimentId}
            string route = $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.DeleteAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to delete the context/metadata instance associated with an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment whose context/metadata will be deleted.</param>
        /// <param name="cancellationToken">A token that can be used bo cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the result.
        /// </returns>
        public async Task<HttpResponseMessage> DeleteExperimentContextAsync(string experimentId, CancellationToken cancellationToken, string contextId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Format: /api/experiments/{experimentId}/context
            // Format: /api/experiments/{experimentId}/context?contextId={contextId}
            string route = contextId == null ?
                    $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}/context" :
                    $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}/context?contextId={contextId}";

            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.DeleteAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to delete steps associated with an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment whose steps will be deleted.</param>
        /// <param name="cancellationToken">A token that can be used bo cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the result.
        /// </returns>
        public async Task<HttpResponseMessage> DeleteExperimentStepsAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Format: /api/experiments/{experimentId}/steps
            string route = $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}/steps";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.DeleteAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to delete a experiment/work notice.
        /// </summary>
        /// <param name="workQueue">The name of the queue on which the notification should be placed.</param>
        /// <param name="messageId">MessageId in the queue.</param>
        /// <param name="popReceipt">PopReceipt for the queue message.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentMetadataInstance"/> created.
        /// </returns>
        public async Task<HttpResponseMessage> DeleteNoticeAsync(string workQueue, string messageId, string popReceipt, CancellationToken cancellationToken)
        {
            workQueue.ThrowIfNull(nameof(workQueue));
            messageId.ThrowIfNull(nameof(messageId));
            popReceipt.ThrowIfNull(nameof(popReceipt));

            // Format: /api/notifications?workQueue=experimentnotices&messageId=xxx&popReceipt=xxxxxxx
            string route = $"{ExecutionClient.NotificationApiRoute}?workQueue={workQueue}&messageId={messageId}&popReceipt={HttpUtility.UrlEncode(popReceipt)}";

            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.DeleteAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete an Execution Goal from the System
        /// </summary>
        /// <param name="executionGoalId">Id of the Execution Goal</param>
        /// <param name="teamName">The team that owns the Execution Goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> DeleteExecutionGoalAsync(string executionGoalId, string teamName, CancellationToken cancellationToken)
        {
            executionGoalId.ThrowIfNullOrWhiteSpace(nameof(executionGoalId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            string route = $"{ExecutionClient.ExecutionGoalsApiRoute}/{Uri.EscapeUriString(executionGoalId)}?teamName={WebUtility.UrlEncode(teamName)}";

            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.DeleteAsync(requestUri, cancellationToken)
                .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete an Execution Goal Template from the System
        /// </summary>
        /// <param name="executionGoalTemplateId">Id of the Execution Goal</param>
        /// <param name="teamName">The team that owns the Execution Goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> DeleteExecutionGoalTemplateAsync(string executionGoalTemplateId, string teamName, CancellationToken cancellationToken)
        {
            executionGoalTemplateId.ThrowIfNullOrWhiteSpace(nameof(executionGoalTemplateId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            // Format: /api/executionGoalTemplates/executionGoalId/?teamName={CRC AIR}
            string route = $"{ExecutionClient.ExecutionGoalTemplateApiRoute}/{executionGoalTemplateId}/?teamName={teamName}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.DeleteAsync(requestUri, cancellationToken)
                .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to delete an experiment definition instance.
        /// </summary>
        /// <param name="templateId">The ID of the experiment to delete.</param>
        /// <param name="teamName">Team to which template belongs</param>        
        /// <param name="cancellationToken">A token that can be used bo cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the result.
        /// </returns>
        public async Task<HttpResponseMessage> DeleteExperimentTemplateAsync(string templateId, string teamName, CancellationToken cancellationToken)
        {
            templateId.ThrowIfNullOrWhiteSpace(nameof(templateId));

            // Format: /api/experiments/{experimentId}
            ////string route = $"{ExecutionClient.ExperimentTemplatesApiRoute}?teamName={teamName}&templateId={templateId}";
            string route = $"{ExecutionClient.ExperimentTemplatesApiRoute}/{Uri.EscapeUriString(teamName)}/{Uri.EscapeUriString(templateId)}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.DeleteAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to get the steps associated with an agent running in an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="parentStepId">Optional parameter defines the unique ID of the parent step for which the agent steps are related.</param>
        /// <param name="status">Optional parameter defines the set of statuses on which to filter the results.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the set of <see cref="ExperimentStepInstance"/>
        /// definitions.
        /// </returns>
        public async Task<HttpResponseMessage> GetExperimentAgentStepsAsync(string experimentId, CancellationToken cancellationToken, string parentStepId = null, IEnumerable<ExecutionStatus> status = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Format: /api/experiments/{experimentId}/agent-steps
            //         /api/experiments/{experimentId}/agent-steps?filter=(ParentStepId={parentStepId})
            //         /api/experiments/{experimentId}/agent-steps?filter=((Status eq 'InProgress') or (Status eq 'InProgressContinue'))
            string route = $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}/agent-steps";

            QueryFilter filter = new QueryFilter();
            if (!string.IsNullOrWhiteSpace(parentStepId))
            {
                filter.And(nameof(ExperimentStepInstance.ParentStepId), ComparisonType.Equal, parentStepId);
            }

            if (status?.Any() == true)
            {
                QueryFilter statusFilter = new QueryFilter();
                status.ToList().ForEach(individualStatus => statusFilter.Or(nameof(ExperimentStepInstance.Status), ComparisonType.Equal, individualStatus.ToString()));
                filter.And(statusFilter);
            }

            if (!string.IsNullOrWhiteSpace(filter.Filter))
            {
                route += $"?filter={HttpUtility.UrlEncode(filter.CreateExpression())}";
            }

            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to gets an experiment instance.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> GetExperimentAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Format: /api/experiments/{experimentId}
            string route = $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to get the context/metadata instance for an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentMetadataInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> GetExperimentContextAsync(string experimentId, CancellationToken cancellationToken, string contextId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Format: /api/experiments/{experimentId}/context
            string route = contextId == null ?
                $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}/context" :
                $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}/context?contextId={contextId}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to get the steps for an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="status">Optional parameter defines the set of statuses on which to filter the results.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the set of <see cref="ExperimentStepInstance"/>
        /// definitions.
        /// </returns>
        public async Task<HttpResponseMessage> GetExperimentStepsAsync(string experimentId, CancellationToken cancellationToken, IEnumerable<ExecutionStatus> status = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Format: /api/experiments/{experimentId}/steps
            //         /api/experiments/{experimentId}/steps?filter=((Status eq 'InProgress') or (Status eq 'InProgressContinue'))
            string route = $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}/steps";

            QueryFilter filter = new QueryFilter();
            if (status?.Any() == true)
            {
                status.ToList().ForEach(individualStatus => filter.Or(nameof(ExperimentStepInstance.Status), ComparisonType.Equal, individualStatus.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(filter.Filter))
            {
                route += $"?filter={HttpUtility.UrlEncode(filter.CreateExpression())}";
            }

            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to get experiment instance statuses for end-to-end experiments.
        /// </summary>
        /// <param name="experimentName">The name of the experiment/experiment instances.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the individual experiment status objects.
        /// </returns>
        public async Task<HttpResponseMessage> GetExperimentInstanceStatusesAsync(string experimentName, CancellationToken cancellationToken)
        {
            experimentName.ThrowIfNullOrWhiteSpace(nameof(experimentName));

            // Format: /api/experimentstatus/{experimentName}
            string route = $"{ExecutionClient.ExperimentStatusApiRoute}/{experimentName}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to gets an agent heartbeat instance.
        /// </summary>
        /// <param name="agentId">The ID of the agent.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="AgentHeartbeatInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> GetHeartbeatAsync(string agentId, CancellationToken cancellationToken)
        {
            agentId.ThrowIfNullOrWhiteSpace(nameof(agentId));

            // Format: /api/heartbeats/{agentId}
            string route = $"{ExecutionClient.HeartbeatsApiRoute}?agentId={HttpUtility.UrlEncode(agentId)}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to gets the priority notice in queue and also hide it for 5 minutes.
        /// </summary>
        /// <param name="workQueue">The name of the queue on which the notification should be placed.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="visibilityDelay">Optional parameter defines a period of time in which the message will be hidden.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentMetadataInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> GetNoticeAsync(string workQueue, CancellationToken cancellationToken, TimeSpan? visibilityDelay = null)
        {
            int visibilityDelaySeconds = (int)((visibilityDelay?.TotalSeconds) ?? 300);

            // Format: /api/notifications?queueName=experimentnotices
            string route = $"{ExecutionClient.NotificationApiRoute}?workQueue={workQueue}&visibilityDelay={visibilityDelaySeconds}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve execution goals from the system
        /// </summary>
        /// <param name="executionGoalId">The id of the execution goal</param>
        /// <param name="teamName">Name of the team that owns the execution goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <param name= "view">Resource Type of the execution goal (optional)</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="GoalBasedSchedule"/>.
        /// </returns>        
        public async Task<HttpResponseMessage> GetExecutionGoalsAsync(CancellationToken cancellationToken, string teamName = null, string executionGoalId = null, ExecutionGoalView view = ExecutionGoalView.Full)
        {
            string route = $"{ExecutionClient.ExecutionGoalsApiRoute}";

            if (teamName != null)
            {
                route = executionGoalId == null
                ? $"{route}?teamName={Uri.EscapeUriString(teamName)}&view={view}"
                : $"{route}?teamName={Uri.EscapeUriString(teamName)}&executionGoalId={WebUtility.UrlEncode(executionGoalId)}&view={view}";
            }
            else
            {
                route = executionGoalId == null
                ? $"{route}?view={view}"
                : $"{route}?executionGoalId={WebUtility.UrlEncode(executionGoalId)}&view={view}";
            }

            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to gets a list of experiment defintion instances.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> GetExperimentTemplatesListAsync(CancellationToken cancellationToken)
        {
            string route = $"{ExecutionClient.ExperimentTemplatesApiRoute}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a list of execution goal template metadata 
        /// </summary>
        /// <param name="teamName">Name of the team that owns the templates</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <param name="view"></param>
        /// <param name="templateId"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> GetExecutionGoalTemplatesAsync(CancellationToken cancellationToken, string teamName = null, string templateId = null, View view = View.Full)
        {
            string route = string.Empty;

            if (teamName != null)
            {
                route = templateId == null
                    ? $"{ExecutionClient.ExecutionGoalTemplateApiRoute}/?teamName={teamName}&view={view}"
                    : $"{ExecutionClient.ExecutionGoalTemplateApiRoute}/?teamName={teamName}&templateId={templateId}&view={view}";
            }
            else
            {
                route = templateId == null
                    ? $"{ExecutionClient.ExecutionGoalTemplateApiRoute}/?view={view}"
                    : $"{ExecutionClient.ExecutionGoalTemplateApiRoute}/?templateId={templateId}&view={view}";
            }

            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to gets an experiment defintion instance.
        /// </summary>
        /// <param name="experimentTemplateId">The ID of the experiment.</param>
        /// <param name="teamName">Name of the team to which the template belongs.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> GetExperimentTemplateAsync(string teamName, string experimentTemplateId, CancellationToken cancellationToken)
        {
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            // Format: /api/experiments/{experimentId}
            string route = $"{ExecutionClient.ExperimentTemplatesApiRoute}/{teamName}/{experimentTemplateId}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to gets a list of experiment defintion instances.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="teamName">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> GetExperimentTemplatesListAsync(string teamName, CancellationToken cancellationToken)
        {
            string route = $"{ExecutionClient.ExperimentTemplatesApiRoute}/{teamName}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to set the visibility of a notice.
        /// </summary>
        /// <param name="workQueue">The name of the queue on which the notification should be found.</param>
        /// <param name="messageId">MessageId in the queue.</param>
        /// <param name="popReceipt">PopReceipt for the queue message.</param>
        /// <param name="visibilityDelay">
        /// The interval of time from now during which the message will be invisible in timespan.
        /// </param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentMetadataInstance"/> created.
        /// </returns>
        public async Task<HttpResponseMessage> SetNoticeVisibilityAsync(string workQueue, string messageId, string popReceipt, TimeSpan visibilityDelay, CancellationToken cancellationToken)
        {
            workQueue.ThrowIfNull(nameof(workQueue));
            messageId.ThrowIfNull(nameof(messageId));
            popReceipt.ThrowIfNull(nameof(popReceipt));
            visibilityDelay.ThrowIfNull(nameof(visibilityDelay));

            // Format: /api/notifications?workQueue=experimentnotices
            string route = $"{ExecutionClient.NotificationApiRoute}" +
                $"?workQueue={workQueue}&messageId={messageId}&popReceipt={HttpUtility.UrlEncode(popReceipt)}&visibilityDelay={visibilityDelay.TotalSeconds}";

            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.PutAsync(requestUri, new StringContent(string.Empty), cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes an API request to update an existing experiment agent step instance.
        /// </summary>
        /// <param name="step">The updated experiment agent step definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the updated <see cref="ExperimentStepInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> UpdateExperimentAgentStepAsync(ExperimentStepInstance step, CancellationToken cancellationToken)
        {
            step.ThrowIfNull(nameof(step));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(step.ToJson()))
            {
                // Format: /api/experiments/{experimentId}/agent-steps/{stepId}
                string route = $"{ExecutionClient.ExperimentsApiRoute}/{step.ExperimentId}/agent-steps/{step.Id}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to update an existing experiment instance.
        /// </summary>
        /// <param name="experiment">The updated experiment definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the updated <see cref="ExperimentInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> UpdateExperimentAsync(ExperimentInstance experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(experiment.ToJson()))
            {
                // Format: /api/experiments/{experimentId}
                string route = $"{ExecutionClient.ExperimentsApiRoute}/{experiment.Id}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to update an existing experiment context/metadata instance.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="context">The updated experiment context/metadata definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="contextId">Optionally defines the specific ID of the context to retrieve. When not defined, returns the global context for the experiment.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the updated <see cref="ExperimentMetadataInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> UpdateExperimentContextAsync(string experimentId, ExperimentMetadataInstance context, CancellationToken cancellationToken, string contextId = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            context.ThrowIfNull(nameof(context));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(context.ToJson()))
            {
                // Format: /api/experiments/{experimentId}/context
                // Format: /api/experiments/{experimentId}/context?contextId={contextId}
                string route = contextId == null ?
                    $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}/context" :
                    $"{ExecutionClient.ExperimentsApiRoute}/{experimentId}/context?contextId={contextId}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to update an existing experiment step instance.
        /// </summary>
        /// <param name="step">The updated experiment step definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the updated <see cref="ExperimentStepInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> UpdateExperimentStepAsync(ExperimentStepInstance step, CancellationToken cancellationToken)
        {
            step.ThrowIfNull(nameof(step));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(step.ToJson()))
            {
                // Format: /api/experiments/{experimentId}/steps/{stepId}
                string route = $"{ExecutionClient.ExperimentsApiRoute}/{step.ExperimentId}/steps/{step.Id}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Updates an execution goal in the system
        /// </summary>
        /// <param name="executionGoal">The execution goal to update</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>A <see cref="HttpResponseMessage"/> containing a <see cref="GoalBasedSchedule"/></returns>
        public async Task<HttpResponseMessage> UpdateExecutionGoalAsync(Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(executionGoal.ToJson()))
            {
                string route = $"{ExecutionClient.ExecutionGoalsApiRoute}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                    .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes an API request to update an existing experiment definition instance.
        /// </summary>
        /// <param name="experiment">The updated experiment definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the updated <see cref="ExperimentInstance"/>.
        /// </returns>
        public async Task<HttpResponseMessage> UpdateExperimentTemplateAsync(ExperimentItem experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(experiment.ToJson()))
            {
                // Format: /api/experiments/{experimentId}
                string route = $"{ExecutionClient.ExperimentTemplatesApiRoute}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Updates an execution goal template in the system
        /// </summary>
        /// <param name="executionGoal">The execution goal to update</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>A <see cref="HttpResponseMessage"/> containing <see cref="Item{GoalBasedSchedule}"/> </returns>
        public async Task<HttpResponseMessage> UpdateExecutionGoalTemplateAsync(Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            using (StringContent requestBody = ExecutionClient.CreateJsonContent(executionGoal.ToJson()))
            {
                string route = $"{ExecutionClient.ExecutionGoalTemplateApiRoute}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                    .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        private static StringContent CreateJsonContent(string content)
        {
            return new StringContent(content, Encoding.UTF8, "application/json");
        }
    }
}