namespace Juno.Execution.ArmIntegration
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
    using Juno.Execution.ArmIntegration.Parameters;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Polly;

    /// <summary>
    /// ARM template client to make http call for deploying and checking deployment states.
    /// https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/deploy-rest
    /// </summary>
    public class ArmClient : IArmClient
    {
        private const string ResourceGroupScopeApiVersion = "2019-10-01";
        private const string SubscriptionScopeApiVersion = "2019-05-01";
        private const string SubscriptionActivityLogApiVersion = "2017-03-01-preview";

        private const string BaseUri = "https://management.azure.com";
        private const string VirtualMachineProviderNamespace = "Microsoft.Compute";
        private const string VirtualMachineResourceType = "virtualMachines";

        /// <summary>
        /// Uri for accessing Activity Logs on an Azure subscription.
        /// </summary>
        private const string ActivityLogsUri = "https://management.azure.com/subscriptions/{0}/providers/microsoft.insights/eventtypes/management/values?api-version={1}&$filter={2}";

        /// <summary>
        /// Create or update resource group uri placeholder
        /// </summary>
        private const string ResourceGroupCreateOrUpdateUri = "https://management.azure.com/subscriptions/{0}/resourcegroups/{1}?api-version=2019-10-01";

        /// <summary>
        /// Resource group level Uri placeholder
        /// </summary>
        private const string ResourceGroupScopeUri = "https://management.azure.com/subscriptions/{0}/resourcegroups/{1}/providers/Microsoft.Resources/deployments/{2}?api-version=2019-10-01";

        /// <summary>
        /// Subscriptionlevel Uri placeholder
        /// </summary>
        private const string SubscriptionScopeUri = "https://management.azure.com/subscriptions/{0}/providers/Microsoft.Resources/deployments/{1}?api-version=2019-05-01";

        /// <summary>
        /// Delete resource group api uri
        /// </summary>
        private const string ResourceGroupUri = "https://management.azure.com/subscriptions/{0}/resourcegroups/{1}?api-version=2020-06-01";

        /// <summary>
        /// Delete resource group api uri
        /// </summary>
        private const string ResourceScopeUri = "https://management.azure.com/subscriptions/{0}/resourcegroups/{1}/providers/{2}/{3}/{4}?api-version=2020-06-01";

        /// <summary>
        /// Using custom JsonSerializationSettings because ARM template validation seems to have issue when
        /// optional parameters are missing. Therefore, we want to keep the parameters though the value can be null/default.
        /// </summary>
        private static readonly JsonSerializerSettings JsonSerializationSettings = new JsonSerializerSettings
        {
            // Format: 2012-03-21T05:40:12.340Z
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            Formatting = Formatting.None,

            // We tried using PreserveReferenceHandling.All and Object, but ran into issues
            // when deserializing string arrays and read only dictionaries
            ReferenceLoopHandling = ReferenceLoopHandling.Error,

            // This is the default setting, but to avoid remote code execution bugs do NOT change
            // this to any other setting.
            TypeNameHandling = TypeNameHandling.None
        };

        /// <summary>
        /// Defines the default retry policy for Api operations on timeout only
        /// </summary>
        private static readonly IAsyncPolicy DefaultRetryPolicy = Policy.Handle<WebException>((exc) =>
        {
            return exc.Status == WebExceptionStatus.Timeout;
        })
        .WaitAndRetryAsync(1, (retries) => TimeSpan.FromMilliseconds(retries * 100));

        /// <summary>
        /// Initializes a new instance of the <see cref="ArmClient"/> class.
        /// </summary>
        /// <param name="restClient"></param>
        /// <param name="retryPolicy"></param>
        public ArmClient(IRestClient restClient, IAsyncPolicy retryPolicy = null)
        {
            restClient.ThrowIfNull(nameof(restClient));

            this.RestClient = restClient;
            this.RetryPolicy = retryPolicy ?? ArmClient.DefaultRetryPolicy;
        }

        /// <summary>
        /// Gets the retry policy to apply when experiencing transient issues.
        /// </summary>
        private IAsyncPolicy RetryPolicy { get; }

        /// <summary>
        /// Gets or sets the REST client that handles HTTP communications
        /// with the API service.
        /// </summary>
        private IRestClient RestClient { get; }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> CreateResourceGroupAsync(
         string subscriptionId,
         string resourceGroupName,
         string location,
         CancellationToken cancellationToken,
         IDictionary<string, string> tags = null)
        {
            subscriptionId.ThrowIfNullOrWhiteSpace(nameof(subscriptionId));
            resourceGroupName.ThrowIfNullOrWhiteSpace(nameof(resourceGroupName));
            location.ThrowIfNullOrWhiteSpace(nameof(location));

            var jsonObject = new JObject
            {
                ["location"] = location,
            };

            if (tags?.Any() == true)
            {
                jsonObject.Add("tags", JObject.Parse(tags.ToJson()));
            }

            using (StringContent requestBody = ArmClient.CreateJsonContent(jsonObject.ToString()))
            {
                string route = string.Format(ArmClient.ResourceGroupCreateOrUpdateUri, subscriptionId, resourceGroupName);
                Uri requestUri = new Uri(route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                    .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> DeleteResourceGroupAsync(string subscriptionId, string resourceGroupName, CancellationToken cancellationToken)
        {
            subscriptionId.ThrowIfNullOrWhiteSpace(nameof(subscriptionId));
            resourceGroupName.ThrowIfNullOrWhiteSpace(nameof(resourceGroupName));
            Uri requestUri = new Uri(string.Format(ArmClient.ResourceGroupUri, subscriptionId, resourceGroupName));
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.DeleteAsync(requestUri, cancellationToken)
                .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> DeleteVirtualMachineAsync(string subscriptionId, string resourceGroupName, string virtualMachineName, CancellationToken cancellationToken)
        {
            // parameters: {subscriptionId} {resourceGroupName} {resourceProviderNamespace} {resourceType} {resourceName}

            subscriptionId.ThrowIfNullOrWhiteSpace(nameof(subscriptionId));
            resourceGroupName.ThrowIfNullOrWhiteSpace(nameof(resourceGroupName));
            Uri requestUri = new Uri(string.Format(
                ArmClient.ResourceScopeUri, subscriptionId, resourceGroupName, ArmClient.VirtualMachineProviderNamespace, ArmClient.VirtualMachineResourceType, virtualMachineName));
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.DeleteAsync(requestUri, cancellationToken)
                .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> DeployAtSubscriptionScopeAsync(
            string subscriptionId,
            string deploymentName,
            string template,
            VmResourceGroupTemplateParameters parameters,
            CancellationToken cancellationToken)
        {
            subscriptionId.ThrowIfNullOrWhiteSpace(nameof(subscriptionId));
            deploymentName.ThrowIfNullOrWhiteSpace(nameof(deploymentName));
            template.ThrowIfNullOrWhiteSpace(nameof(template));
            parameters.ThrowIfNull(nameof(parameters));

            var stringBuilder = ArmClient.BuildJsonString(template, parameters, parameters.Location.Value);
            using (StringContent requestBody = ArmClient.CreateJsonContent(stringBuilder.ToString()))
            {
                string route = string.Format(ArmClient.SubscriptionScopeUri, subscriptionId, deploymentName);
                Uri requestUri = new Uri(route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                    .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> DeployAtResourceGroupScopeAsync(
            string subscriptionId,
            string resourceGroupName,
            string deploymentName,
            string template,
            TemplateParameters parameters,
            CancellationToken cancellationToken)
        {
            subscriptionId.ThrowIfNullOrWhiteSpace(nameof(subscriptionId));
            deploymentName.ThrowIfNullOrWhiteSpace(nameof(deploymentName));
            template.ThrowIfNullOrWhiteSpace(nameof(template));
            parameters.ThrowIfNull(nameof(parameters));
            var stringBuilder = ArmClient.BuildJsonString(template, parameters);
            using (StringContent requestBody = ArmClient.CreateJsonContent(stringBuilder.ToString()))
            {
                string route = string.Format(ArmClient.ResourceGroupScopeUri, subscriptionId, resourceGroupName, deploymentName);
                Uri requestUri = new Uri(route);

                return await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.PutAsync(requestUri, requestBody, cancellationToken)
                    .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> GetSubscriptionActivityLogsAsync(string subscriptionId, string filter, CancellationToken cancellationToken, IEnumerable<string> fields = null)
        {
            subscriptionId.ThrowIfNullOrWhiteSpace(nameof(subscriptionId));
            filter.ThrowIfNullOrWhiteSpace(nameof(filter));
            ArmActivityLogEntry activityLog = new ArmActivityLogEntry();
            HttpResponseMessage response;

            Uri requestUri = ArmClient.GetActivityLogUri(subscriptionId, filter, fields);
            string nextUri = requestUri.ToString();

            do
            {
                response = await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    return await this.RestClient.GetAsync(new Uri(nextUri), cancellationToken).ConfigureDefaults();
                }).ConfigureDefaults();

                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    string jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    ArmActivityLogEntry logs = jsonString.FromJson<ArmActivityLogEntry>();
                    if (logs?.Value?.Any() == true)
                    {
                        activityLog.Value = (activityLog.Value != null) ? activityLog.Value.Concat(logs.Value) : logs.Value;
                        // Get the URL for the next page
                        nextUri = (logs.NextLink != null) ? logs.NextLink : string.Empty;
                    }
                    else
                    {
                        nextUri = string.Empty;
                    }
                }
                else
                {
                    nextUri = string.Empty;
                }
            }
            while (!string.IsNullOrWhiteSpace(nextUri));

            if (activityLog.Value?.Any() == true)
            {
                string result = activityLog.ToJson();
                return ArmClient.CreateResponseMessage(response.StatusCode, result);
            }

            return response;
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> GetSubscriptionScopeDeploymentStateAsync(string deploymentId, CancellationToken cancellationToken)
        {
            deploymentId.ThrowIfNullOrWhiteSpace(nameof(deploymentId));
            Uri requestUri = ArmClient.GetDeploymentUri(deploymentId, ArmClient.SubscriptionScopeApiVersion);
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> GetResourceGroupScopeDeploymentStateAsync(string deploymentId, CancellationToken cancellationToken)
        {
            deploymentId.ThrowIfNullOrWhiteSpace(nameof(deploymentId));
            Uri requestUri = ArmClient.GetDeploymentUri(deploymentId, ArmClient.ResourceGroupScopeApiVersion);
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> GetVirtualMachineAsync(string subscriptionId, string resourceGroupName, string virtualMachineName, CancellationToken cancellationToken)
        {
            // parameters: {subscriptionId} {resourceGroupName} {resourceProviderNamespace} {resourceType} {resourceName}

            subscriptionId.ThrowIfNullOrWhiteSpace(nameof(subscriptionId));
            resourceGroupName.ThrowIfNullOrWhiteSpace(nameof(resourceGroupName));
            Uri requestUri = new Uri(string.Format(
                ArmClient.ResourceScopeUri, subscriptionId, resourceGroupName, ArmClient.VirtualMachineProviderNamespace, ArmClient.VirtualMachineResourceType, virtualMachineName));
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.GetAsync(requestUri, cancellationToken)
                .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> GetResourceGroupAsync(string subscriptionId, string resourceGroupName, CancellationToken cancellationToken)
        {
            subscriptionId.ThrowIfNullOrWhiteSpace(nameof(subscriptionId));
            resourceGroupName.ThrowIfNullOrWhiteSpace(nameof(resourceGroupName));
            Uri requestUri = new Uri(string.Format(ArmClient.ResourceGroupUri, subscriptionId, resourceGroupName));
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.HeadAsync(requestUri, cancellationToken)
                .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<HttpResponseMessage> HeadResourceGroupAsync(string subscriptionId, string resourceGroupName, CancellationToken cancellationToken)
        {
            subscriptionId.ThrowIfNullOrWhiteSpace(nameof(subscriptionId));
            resourceGroupName.ThrowIfNullOrWhiteSpace(nameof(resourceGroupName));
            Uri requestUri = new Uri(string.Format(ArmClient.ResourceGroupUri, subscriptionId, resourceGroupName));
            return await this.RetryPolicy.ExecuteAsync(async () =>
            {
                return await this.RestClient.HeadAsync(requestUri, cancellationToken)
                .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns the Uri for the subscription-level activity logs by appending the filter and fields provided.
        /// </summary>
        /// <param name="subscriptionId">The subscription in which to get the activity logs.</param>
        /// <param name="filter">
        /// The filter to apply (e.g. eventTimestamp ge '2020-06-01T00:00:00.000Z' and resourceGroupName eq 'resourceGroupName')
        /// </param>
        /// <param name="fields">Optional string parameter of comma separated values allows specific fields to be returned by the API (default is null).</param>
        /// <returns>
        /// A customized Uri for the activity logs for a specific resource group and subscription.
        /// </returns>
        protected static Uri GetActivityLogUri(string subscriptionId, string filter, IEnumerable<string> fields = null)
        {
            subscriptionId.ThrowIfNullOrWhiteSpace(nameof(subscriptionId));
            filter.ThrowIfNullOrWhiteSpace(nameof(filter));

            string effectiveFilter = HttpUtility.UrlEncode(filter);

            string requestUri = string.Format(ArmClient.ActivityLogsUri, subscriptionId, ArmClient.SubscriptionActivityLogApiVersion, effectiveFilter);

            if (fields?.Any() == true)
            {
                string s = string.Join(",", fields.ToArray());
                string fieldsPrefix = "$select=";
                string effectiveFields = fieldsPrefix + (s.StartsWith(fieldsPrefix, StringComparison.OrdinalIgnoreCase)
                    ? HttpUtility.UrlEncode(s.Substring(fieldsPrefix.Length))
                    : HttpUtility.UrlEncode(s));

                requestUri += $"&{effectiveFields}";
            }

            return new Uri(requestUri);
        }

        /// <summary>
        /// Build JSON string from parameters and template.
        /// There is no massaging of strings such as find and replace. Based on ARM template REST API best practice, we are serializing parameters object to JSON string
        /// and putting on the same level with template. The template file is the same all time.
        /// You can either include your template in the request body or link to a file.
        /// When using a file, it can be a local file or an external file that is available through a URI.
        /// https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/deploy-rest
        /// </summary>
        /// <param name="template">Template json string which is loaded into memory from resource file</param>
        /// <param name="parameters">Template parameters object</param>
        /// <param name="location">Location aka. region.</param>
        /// <returns>Request body json string</returns>
        private static string BuildJsonString(string template, TemplateParameters parameters, string location = null)
        {
            var jsonObject = new JObject();
            if (!string.IsNullOrWhiteSpace(location))
            {
                jsonObject["location"] = location;
            }

            var properties = new JObject
            {
                ["template"] = JObject.Parse(template),
                ["mode"] = "Incremental",
                ["parameters"] = JObject.Parse(parameters.ToJson(ArmClient.JsonSerializationSettings))
            };

            jsonObject["properties"] = properties;
            return jsonObject.ToString();
        }

        private static StringContent CreateJsonContent(string content)
        {
            return new StringContent(content, Encoding.UTF8, "application/json");
        }

        private static Uri GetDeploymentUri(string deploymentId, string apiVersion)
        {
            return new Uri(string.Concat(ArmClient.BaseUri, deploymentId, "?api-version=", apiVersion));
        }

        private static HttpResponseMessage CreateResponseMessage(HttpStatusCode expectedStatusCode, string content)
        {
            HttpResponseMessage response = new HttpResponseMessage(expectedStatusCode);
            response.Content = ArmClient.CreateJsonContent(content);

            return response;
        }
    }
}