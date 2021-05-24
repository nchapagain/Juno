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
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Contracts.OData;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Newtonsoft.Json;
    using Polly;

    /// <summary>
    /// Juno Experiment REST API client.
    /// </summary>
    public class ExperimentsClient : IExperimentClient
    {
        private const string ExperimentsApiRoute = "/api/experiments";
        private const string ExperimentContextApiRoute = "/api/experiments/{0}/resources";
        private const string ExperimentProvidersApiRoute = "/api/experiments/providers";
        private const string ExperimentStatusApiRoute = "/api/experimentstatus";
        private const string ExecutionGoalsApiRoute = "/api/executionGoals";
        private const string ExecutionGoalTemplatesApiRoute = "/api/executionGoalTemplates";
        private const string ExperimentTemplatesApiRoute = "/api/experimentTemplates";
        private const string EnvironmentsApiRoute = "/api/environments";

        /// <summary>
        /// Defines the default retry policy for Api operations on timeout only
        /// </summary>
        private static IAsyncPolicy defaultRetryPolicy = Policy.Handle<WebException>((exc) =>
        {
            return exc.Status == WebExceptionStatus.Timeout;
        })
        .WaitAndRetryAsync(1, (retries) => TimeSpan.FromMilliseconds(retries * 100));

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentsClient"/> class.
        /// </summary>
        /// <param name="restClient">
        /// The REST client that handles HTTP communications with the API
        /// service.
        /// </param>
        /// <param name="baseUri">
        /// The base URI to the server hosting the API (e.g. https://juno-prod01.westUS2.webapps.net).
        /// </param>
        /// <param name="retryPolicy">A retry policy to apply to api call operations that experience transient issues.</param>
        public ExperimentsClient(IRestClient restClient, Uri baseUri, IAsyncPolicy retryPolicy = null)
        {
            restClient.ThrowIfNull(nameof(restClient));
            baseUri.ThrowIfNull(nameof(baseUri));

            this.RestClient = restClient;
            this.BaseUri = baseUri;
            this.RetryPolicy = retryPolicy ?? ExperimentsClient.defaultRetryPolicy;
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

        /// <see cref="IExperimentClient.CancelExperimentAsync"/>
        public async Task<HttpResponseMessage> CancelExperimentAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Format: /api/experiments/{experimentId}
            string route = $"{ExperimentsClient.ExperimentsApiRoute}/{experimentId}?cancel=true";
            Uri requestUri = new Uri(this.BaseUri, route);

            using (StringContent requestBody = new StringContent(" "))
            {
                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        /// <see cref="IExperimentClient.CreateExperimentAsync"/>
        public async Task<HttpResponseMessage> CreateExperimentAsync(Experiment experiment, CancellationToken cancellationToken, string workQueue = null)
        {
            experiment.ThrowIfNull(nameof(experiment));

            using (StringContent requestBody = ExperimentsClient.CreateJsonContent(experiment.ToJson()))
            {
                // Format: /api/experiments
                //         /api/experiments?workQueue=experimentnotices-other
                string route = ExperimentsClient.ExperimentsApiRoute;
                if (!string.IsNullOrWhiteSpace(workQueue))
                {
                    route += $"?workQueue={workQueue}";
                }

                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        /// <see cref="IExperimentClient.CreateExperimentTemplateAsync"/>
        public async Task<HttpResponseMessage> CreateExperimentTemplateAsync(Experiment experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));

            using (StringContent requestBody = ExperimentsClient.CreateJsonContent(experiment.ToJson()))
            {
                // Format: /api/experiments
                string route = ExperimentsClient.ExperimentTemplatesApiRoute;
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        /// <see cref="IExperimentClient.CreateExperimentFromTemplateAsync"/>
        public async Task<HttpResponseMessage> CreateExperimentFromTemplateAsync(ExperimentTemplate experimentTemplate, CancellationToken cancellationToken, string workQueue = null)
        {
            experimentTemplate.ThrowIfNull(nameof(experimentTemplate));

            using (StringContent requestBody = (StringContent)(RequestResponseExtensions.ToJsonContent(experimentTemplate.ToJson())))
            {
                // Format: /api/experiments/template
                //         /api/experiments/template?workQueue=experimentWorkQueueName
                string route = $"{ExperimentsClient.ExperimentsApiRoute}/template";
                if (!string.IsNullOrWhiteSpace(workQueue))
                {
                    route = $"{route}?workQueue={workQueue}";
                }

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

            using (StringContent requestBody = ExperimentsClient.CreateJsonContent(executionGoalTemplate.ToJson()))
            {
                // Format: /api/executionGoalTemplates
                string route = $"{ExperimentsClient.ExecutionGoalTemplatesApiRoute}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <see cref="IExperimentClient.CreateExecutionGoalFromTemplateAsync"/>
        public async Task<HttpResponseMessage> CreateExecutionGoalFromTemplateAsync(ExecutionGoalParameter parameters, string templateId, string teamName, CancellationToken cancellationToken)
        {
            parameters.ThrowIfNull(nameof(parameters));
            templateId.ThrowIfNullOrWhiteSpace(nameof(templateId));
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));

            using (StringContent requestBody = (StringContent)(RequestResponseExtensions.ToJsonContent(parameters.ToJson())))
            {
                // Format: /api/executionGoals
                string route = $"{ExperimentsClient.ExecutionGoalsApiRoute}/{Uri.EscapeUriString(templateId)}?teamName={Uri.EscapeUriString(teamName)}";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <see cref="IExperimentClient.GetExperimentTemplateAsync"/>
        public async Task<HttpResponseMessage> GetExperimentTemplateAsync(string definitionId, string teamName, CancellationToken cancellationToken)
        {
            definitionId.ThrowIfNull(nameof(definitionId));
            teamName.ThrowIfNull(nameof(teamName));

            // Format: /api/experiments
            string route = $"{ExperimentsClient.ExperimentTemplatesApiRoute}/{Uri.EscapeUriString(teamName)}/{Uri.EscapeUriString(definitionId)}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);

            }).ConfigureAwait(false);
        }

        /// <see cref="IExperimentClient.GetExperimentTemplateListAsync"/>
        public async Task<HttpResponseMessage> GetExperimentTemplateListAsync(CancellationToken cancellationToken)
        {
            // Format: /api/experiments
            string route = ExperimentsClient.ExperimentTemplatesApiRoute;
            Uri requestUri = new Uri(this.BaseUri, string.Format(route));

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);

            }).ConfigureAwait(false);
        }

        /// <see cref="IExperimentClient.GetExperimentProviderListAsync"/>
        public async Task<HttpResponseMessage> GetExperimentProviderListAsync(CancellationToken cancellationToken)
        {
            // Format: /api/experiments
            string route = string.Concat(ExperimentsClient.ExperimentsApiRoute, "/providers");
            Uri requestUri = new Uri(this.BaseUri, string.Format(route));

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);

            }).ConfigureAwait(false);
        }

        /// <see cref="IExperimentClient.GetExperimentStepsAsync"/>
        public async Task<HttpResponseMessage> GetExperimentStepsAsync(string experimentId, CancellationToken cancellationToken, View view = View.Summary, IEnumerable<ExecutionStatus> status = null)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            // Format: /api/experiments/{experimentId}/steps
            //         /api/experiments/{experimentId}/steps?filter=((Status eq 'InProgress') or (Status eq 'InProgressContinue'))
            string route = $"{ExperimentsClient.ExperimentsApiRoute}/{experimentId}/steps?view=" + view;

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

        /// <see cref="IExperimentClient.GetExperimentInstanceStatusesAsync"/>
        public async Task<HttpResponseMessage> GetExperimentInstanceStatusesAsync(string experimentName, CancellationToken cancellationToken)
        {
            experimentName.ThrowIfNullOrWhiteSpace(nameof(experimentName));

            // Format: /api/experimentstatus/{experimentName}
            string route = $"{ExperimentsClient.ExperimentStatusApiRoute}/{experimentName}";
            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <see cref="IExperimentClient.DeleteExecutionGoalAsync"/>
        public async Task<HttpResponseMessage> DeleteExecutionGoalAsync(string executionGoalId, string teamName, CancellationToken cancellationToken)
        {
            string route = $"{ExperimentsClient.ExecutionGoalsApiRoute}";
            route = $"{route}/{WebUtility.UrlEncode(executionGoalId)}?teamName={Uri.EscapeUriString(teamName)}";

            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.DeleteAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <see cref="IExperimentClient.DeleteExecutionGoalTemplateAsync"/>
        public async Task<HttpResponseMessage> DeleteExecutionGoalTemplateAsync(string executionGoalTemplateId, string teamName, CancellationToken cancellationToken)
        {
            string route = $"{ExperimentsClient.ExecutionGoalTemplatesApiRoute}";
            route = $"{route}/{WebUtility.UrlEncode(executionGoalTemplateId)}?teamName={Uri.EscapeUriString(teamName)}";

            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.DeleteAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <see cref="IExperimentClient.GetExecutionGoalsAsync"/>       
        public async Task<HttpResponseMessage> GetExecutionGoalsAsync(CancellationToken cancellationToken, string teamName = null, string executionGoalId = null, ExecutionGoalView view = ExecutionGoalView.Full)
        {
            string route = $"{ExperimentsClient.ExecutionGoalsApiRoute}";

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
                    : $"{route}?&executionGoalId={WebUtility.UrlEncode(executionGoalId)}&view={view}";
            }

            Uri requestUri = new Uri(this.BaseUri, route);

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <see cref="IExperimentClient.ReserveEnvironmentsAsync"/>
        public async Task<HttpResponseMessage> ReserveEnvironmentsAsync(EnvironmentQuery query, CancellationToken cancellationToken)
        {
            query.ThrowIfNull(nameof(query));

            // Format: /api/envrionments
            string route = $"{ExperimentsClient.EnvironmentsApiRoute}";
            Uri requestUri = new Uri(this.BaseUri, route);

            using (StringContent requestBody = ExperimentsClient.CreateJsonContent(query.ToJson()))
            {
                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        /// <see cref="IExperimentClient.UpdateExperimentTemplateAsync"/>
        public async Task<HttpResponseMessage> UpdateExperimentTemplateAsync(ExperimentItem experimentTemplate, CancellationToken cancellationToken)
        {
            experimentTemplate.ThrowIfNull(nameof(experimentTemplate));

            using (StringContent requestBody = ExperimentsClient.CreateJsonContent(experimentTemplate.ToJson()))
            {
                // Format: /api/experiments
                string route = ExperimentsClient.ExperimentTemplatesApiRoute;
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        /// <see cref="IExperimentClient.ValidateExperimentAsync"/>
        public async Task<HttpResponseMessage> ValidateExperimentAsync(Experiment experiment, CancellationToken cancellationToken)
        {
            experiment.ThrowIfNull(nameof(experiment));

            using (StringContent requestBody = ExperimentsClient.CreateJsonContent(experiment.ToJson()))
            {
                // Format: /api/experiments?validate=true
                string route = $"{ExperimentsClient.ExperimentsApiRoute}?validate=true";
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PostAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        /// <see cref="IExperimentClient.GetExperimentResourcesAsync"/>
        public async Task<IEnumerable<EnvironmentEntity>> GetExperimentResourcesAsync(string experimentId, CancellationToken cancellationToken)
        {
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));

            string route = ExperimentsClient.ExperimentContextApiRoute;
            Uri requestUri = new Uri(this.BaseUri, string.Format(route, experimentId));

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                HttpResponseMessage response = await this.RestClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
                return JsonConvert.DeserializeObject<IEnumerable<EnvironmentEntity>>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }).ConfigureAwait(false);
        }

        /// <see cref="IExperimentClient.GetProvidersListAsync"/>
        public async Task<HttpResponseMessage> GetProvidersListAsync(CancellationToken cancellationToken)
        {
            string route = ExperimentsClient.ExperimentProvidersApiRoute;
            Uri requestUri = new Uri(this.BaseUri, string.Format(route));

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

            }).ConfigureAwait(false);
        }

        /// <see cref="IExperimentClient.GetTemplatesAsync"/>
        public async Task<HttpResponseMessage> GetTemplatesAsync(CancellationToken cancellationToken, string teamName = null, View view = View.Full, string templateId = null)
        {
            string route = string.Empty;
            if (teamName != null)
            {
                route = templateId == null
                    ? $"{ExperimentsClient.ExecutionGoalTemplatesApiRoute}?teamName={teamName}&view={view}"
                    : $"{ExperimentsClient.ExecutionGoalTemplatesApiRoute}?teamName={teamName}&view={view}&templateId={templateId}";
            }
            else
            {
                route = templateId == null
                    ? $"{ExperimentsClient.ExecutionGoalTemplatesApiRoute}?view={view}"
                    : $"{ExperimentsClient.ExecutionGoalTemplatesApiRoute}?view={view}&templateId={templateId}";
            }

            Uri requestUri = new Uri(this.BaseUri, string.Format(route));

            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

            }).ConfigureAwait(false);
        }

        /// <see cref="IExperimentClient.UpdateExecutionGoalAsync"/>
        public async Task<HttpResponseMessage> UpdateExecutionGoalAsync(Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            using (StringContent requestBody = ExperimentsClient.CreateJsonContent(executionGoal.ToJson()))
            {
                // Format: /api/executionGoals
                string route = ExperimentsClient.ExecutionGoalsApiRoute;
                Uri requestUri = new Uri(this.BaseUri, route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                        .ConfigureAwait(false);

                }).ConfigureAwait(false);
            }
        }

        /// <see cref="IExperimentClient.UpdateExecutionGoalTemplateAsync"/>
        public async Task<HttpResponseMessage> UpdateExecutionGoalTemplateAsync(Item<GoalBasedSchedule> executionGoalTemplate, CancellationToken cancellationToken)
        {
            executionGoalTemplate.ThrowIfNull(nameof(executionGoalTemplate));

            using (StringContent requestBody = ExperimentsClient.CreateJsonContent(executionGoalTemplate.ToJson()))
            {
                // Format: /api/executionGoals
                string route = ExperimentsClient.ExecutionGoalTemplatesApiRoute;
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
