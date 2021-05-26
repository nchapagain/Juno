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
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

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
        protected async override Task ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            component.ThrowIfNull(nameof(component));

            if (!cancellationToken.IsCancellationRequested)
            {
                // Translate from IConvertible to EnvironmentQuery
                IDictionary<string, EnvironmentQuery> queries = new Dictionary<string, EnvironmentQuery>();
                IEnumerable<string> queryParameters = component.Parameters.Keys.Where(key => key.StartsWith(Constants.NodeListPrefix, StringComparison.OrdinalIgnoreCase));
                foreach (string queryParameter in queryParameters)
                {
                    JunoParameter container = component.Parameters.GetValue<JunoParameter>(queryParameter);
                    queries.Add(queryParameter, container.Definition as EnvironmentQuery);
                }

                IExperimentClient client = this.Services.GetService<IExperimentClient>();
                IDictionary<string, Task<HttpResponseMessage>> apiResponses = new Dictionary<string, Task<HttpResponseMessage>>();
                foreach (KeyValuePair<string, EnvironmentQuery> query in queries)
                {
                    apiResponses.Add(query.Key, client.ReserveEnvironmentsAsync(query.Value, cancellationToken));
                }

                _ = await Task.WhenAll(apiResponses.Values).ConfigureDefaults();

                IEnumerable<Task<KeyValuePair<string, IEnumerable<EnvironmentCandidate>>>> candidateTasks = apiResponses.Select(async response =>
                {
                    HttpResponseMessage message = await response.Value.ConfigureDefaults();
                    message.ThrowOnError<SchedulerException>();

                    IEnumerable<EnvironmentCandidate> values = await message.Content.ReadAsJsonAsync<IEnumerable<EnvironmentCandidate>>()
                        .ConfigureDefaults();

                    return new KeyValuePair<string, IEnumerable<EnvironmentCandidate>>(response.Key, values);
                });

                IEnumerable<KeyValuePair<string, IEnumerable<EnvironmentCandidate>>> candidates = await Task.WhenAll(candidateTasks).ConfigureDefaults();

                telemetryContext.AddContext(nameof(candidates), candidates);

                IEnumerable<string> supportedVmSkus = new HashSet<string>(candidates.SelectMany(c => c.Value).SelectMany(candidate => candidate.VmSku));
                foreach (KeyValuePair<string, IEnumerable<EnvironmentCandidate>> candidate in candidates)
                {
                    component.Parameters[candidate.Key] = string.Join(",", candidate.Value.Select(c => c.NodeId));
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
                    component.Parameters.Add(Constants.NodeListPrefix, string.Join(",", candidates.SelectMany(c => c.Value).Select(c => c.NodeId).Distinct()));
                }
            }

            await base.ExecuteActionAsync(component, scheduleContext, telemetryContext, cancellationToken)
                .ConfigureDefaults();
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

        private class Constants
        {
            public const string NodeListPrefix = "nodeList";
            public const string MergeLists = "mergeLists";
            public const string Subscription = "subscription";
            public const string VmSku = "vmSku";
        }
    }
}
