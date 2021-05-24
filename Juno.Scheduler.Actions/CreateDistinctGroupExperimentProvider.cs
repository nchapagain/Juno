namespace Juno.Scheduler.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Polly;

    /// <summary>
    /// Create an experiment with seperate groups containing different nodes.
    /// </summary>
    [SupportedParameter(Name = Constants.NodeListPrefix, Required = false, Type = typeof(JunoParameter))]
    [SupportedParameter(Name = Constants.MergeLists, Required = false, Type = typeof(bool))]
    public class CreateDistinctGroupExperimentProvider : CreateExperimentProvider
    {
        /// <summary>
        /// Initializes <see cref="CreateDistinctGroupExperimentProvider"/>
        /// </summary>
        /// <param name="services">Collection of services used for dependency injection.</param>
        public CreateDistinctGroupExperimentProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        protected async override Task<ExecutionResult> ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));

            if (cancellationToken.IsCancellationRequested)
            {
                return new ExecutionResult(ExecutionStatus.Cancelled);
            }

            // Translate from IConvertible to EnvironmentQuery
            IDictionary<string, EnvironmentQuery> queries = new Dictionary<string, EnvironmentQuery>();
            IEnumerable<string> queryParameters = component.Parameters.Keys.Where(key => key.StartsWith(Constants.NodeListPrefix, StringComparison.OrdinalIgnoreCase));
            foreach (string queryParameter in queryParameters)
            {
                JunoParameter container = component.Parameters.GetValue<JunoParameter>(queryParameter);
                queries.Add(queryParameter, container.Definition as EnvironmentQuery);
            }

            // Translate from EnvironmentQuery to Tasks
            IExperimentClient client = this.Services.GetService<IExperimentClient>();
            IDictionary<string, Task<HttpResponseMessage>> apiResponses = new Dictionary<string, Task<HttpResponseMessage>>();
            foreach (KeyValuePair<string, EnvironmentQuery> query in queries)
            {
                apiResponses.Add(query.Key, client.ReserveEnvironmentsAsync(query.Value, cancellationToken));
            }

            _ = await Task.WhenAll(apiResponses.Values).ConfigureDefaults();

            // Translate from Tasks to List of Candidates
            IEnumerable<Task<KeyValuePair<string, IEnumerable<EnvironmentCandidate>>>> candidateTasks = apiResponses.Select(async response =>
            {
                HttpResponseMessage message = await response.Value.ConfigureDefaults();
                if (!message.IsSuccessStatusCode)
                {
                    string details;
                    try
                    {
                        ProblemDetails problemDetails = await message.Content.ReadAsJsonAsync<ProblemDetails>().ConfigureDefaults();
                        telemetryContext.AddContext(nameof(problemDetails), problemDetails);
                        details = problemDetails.Detail;
                    }
                    catch (ArgumentException)
                    {
                        details = $"{nameof(HttpResponseMessage)} had no content";
                    }
                    catch (NullReferenceException)
                    {
                        details = $"{nameof(HttpResponseMessage)} had no content";
                    }
                    catch (JsonReaderException)
                    {
                        details = $"Unable to parse {nameof(ProblemDetails)}";
                    }

                    throw new SchedulerException($"Response from {nameof(IExperimentClient.ReserveEnvironmentsAsync)} was {message.StatusCode} with details: {details}");
                }

                IEnumerable<EnvironmentCandidate> values = await message.Content.ReadAsJsonAsync<IEnumerable<EnvironmentCandidate>>()
                    .ConfigureDefaults();

                return new KeyValuePair<string, IEnumerable<EnvironmentCandidate>>(response.Key, values);
            });

            IEnumerable<KeyValuePair<string, IEnumerable<EnvironmentCandidate>>> candidates = await Task.WhenAll(candidateTasks).ConfigureDefaults();

            telemetryContext.AddContext(nameof(candidates), candidates);

            IEnumerable<string> supportedVmSkus = new List<string>();

            // Translate into parameter that can be stored back into the dictionary
            IList<string> nodeList = new List<string>();
            foreach (KeyValuePair<string, IEnumerable<EnvironmentCandidate>> candidate in candidates)
            {
                IEnumerable<string> currentNodeList = candidate.Value.Select(c => c.NodeId);
                foreach (string node in currentNodeList)
                {
                    nodeList.Add(node);
                }

                string nodeValue = string.Join(",", currentNodeList);
                component.Parameters[candidate.Key] = nodeValue;

                if (!component.Parameters.ContainsKey(Constants.VmSku))
                {
                    IEnumerable<string> currentSupportedVmSku = this.GetSupportedVmSkus(candidate.Value);

                    // Make sure that the Vm Sku list is supported by all nodes across all groups.
                    supportedVmSkus = supportedVmSkus.Any()
                        ? supportedVmSkus.Intersect(currentSupportedVmSku)
                        : currentSupportedVmSku;
                }
            }

            // Add in the subscription. Although the queries may have returned different subscriptions
            // we are accepting this unintentional feature.
            component.Parameters.Add(Constants.Subscription, candidates.First().Value.First().Subscription);

            if (!component.Parameters.ContainsKey(Constants.VmSku))
            {
                component.Parameters.Add(Constants.VmSku, string.Join(",", supportedVmSkus));
            }

            bool mergeLists = component.Parameters.GetValue<bool>(Constants.MergeLists, false);
            if (mergeLists)
            {
                component.Parameters.Add(Constants.NodeListPrefix, string.Join(",", nodeList));
            }

            return await base.ExecuteActionAsync(component, scheduleContext, telemetryContext, cancellationToken).ConfigureDefaults();
        }

        /// <inheritdoc/>
        protected override void ValidateParameters(GoalComponent component)
        {
            component.ThrowIfNull(nameof(component));
            bool nodeListPresent = component.Parameters.Any(param => param.Key.StartsWith(Constants.NodeListPrefix, StringComparison.OrdinalIgnoreCase));
            if (nodeListPresent)
            {
                IEnumerable<bool> validQueryParameters = component.Parameters.Select(param =>
                {
                    bool isQuery = param.Key.StartsWith(Constants.NodeListPrefix, StringComparison.OrdinalIgnoreCase);
                    if (isQuery)
                    {
                        JunoParameter currentParam = param.Value as JunoParameter;
                        return currentParam.ParameterType == typeof(EnvironmentQuery).FullName;
                    }

                    return true;
                });

                if (!validQueryParameters.All(param => param))
                {
                    throw new SchemaException($"Parameters starting with: {Constants.NodeListPrefix} " +
                        $"must be of type: {typeof(JunoParameter)} with {nameof(JunoParameter.ParameterType)}: {typeof(EnvironmentQuery)}");
                }
            }

            if (!nodeListPresent)
            {
                throw new SchemaException($"Must be atleast one parameter with prefix {Constants.NodeListPrefix} with " +
                    $"type: {typeof(JunoParameter)} with {nameof(JunoParameter.ParameterType)}: {typeof(EnvironmentQuery)}");
            }

            base.ValidateParameters(component);
        }

        private IEnumerable<string> GetSupportedVmSkus(IEnumerable<EnvironmentCandidate> candidates)
        {
            IList<IList<string>> vmSkuLists = candidates.Select(entity => entity.VmSku).ToList();
            IEnumerable<string> supportedVms = vmSkuLists[0];
            foreach (IList<string> vmSkuList in vmSkuLists)
            {
                supportedVms = supportedVms.Intersect(vmSkuList);
            }

            if (!supportedVms.Any())
            {
                throw new SchedulerException("There are no VMs that all nodes in candidate node list support.");
            }

            return supportedVms;
        }

        internal class Constants
        {
            internal const string NodeListPrefix = "nodeList";
            internal const string MergeLists = "mergeLists";
            internal const string Subscription = "subscription";
            internal const string VmSku = "vmSku";
        }
    }
}
