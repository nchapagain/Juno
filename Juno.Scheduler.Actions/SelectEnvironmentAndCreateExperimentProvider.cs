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
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Schedule Action that Queries the ESS for Environment candidates
    /// and then submits the experiment to the system.
    /// </summary>
    [SupportedParameter(Name = Constants.EnvironmentQuery, Required = true, Type = typeof(JunoParameter))]
    public class SelectEnvironmentAndCreateExperimentProvider : CreateExperimentProvider
    {
        /// <summary>
        /// Instantiates a <see cref="SelectEnvironmentAndCreateExperimentProvider"/>
        /// </summary>
        /// <param name="services"></param>
        public SelectEnvironmentAndCreateExperimentProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <summary>
        /// Retrieves an eligible envrionment to be used for an experiment. And then 
        /// executes an experiment.
        /// </summary>
        /// <param name="scheduleAction">The action component to execute</param>
        /// <param name="scheduleContext">Context in which the provider is executing in</param>
        /// <param name="telemetryContext">Event context object used for capturing telemetry</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        protected async override Task<ExecutionResult> ExecuteActionAsync(ScheduleAction scheduleAction, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            // Do the environment selection and add to the parameters. 
            scheduleAction.ThrowIfNull(nameof(scheduleAction));
            scheduleContext.ThrowIfNull(nameof(scheduleContext));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (!cancellationToken.IsCancellationRequested)
            {
                JunoParameter container = scheduleAction.Parameters.GetValue<JunoParameter>(Constants.EnvironmentQuery);
                EnvironmentQuery query = container.Definition as EnvironmentQuery;

                scheduleAction.Parameters.Remove(Constants.EnvironmentQuery);

                this.ProviderContext.Add(nameof(query), query.Name);

                // Allow the parameterization of the values in the query.
                this.OverwriteQueryparameters(query, scheduleAction.Parameters);

                // Change regions from * to subscription
                this.OverwriteSearchValues(query);

                telemetryContext.AddContext(nameof(query), query);

                IExperimentClient client = this.Services.GetService<IExperimentClient>();

                HttpResponseMessage response = await client.ReserveEnvironmentsAsync(query, cancellationToken).ConfigureDefaults();
                response.ThrowOnError<SchedulerException>();
                telemetryContext.AddContext(response);

                IEnumerable<EnvironmentCandidate> candidates = await response.Content.ReadAsJsonAsync<IEnumerable<EnvironmentCandidate>>()
                    .ConfigureDefaults();
                
                string nodeList = string.Join(",", candidates.Select(entity => entity.NodeId));
                string subscription = candidates.Select(entity => entity.Subscription).First();
                IEnumerable<string> vmSkus = new HashSet<string>(candidates.SelectMany(entity => entity.VmSku));

                telemetryContext.AddContext(SchedulerEventProperty.NodeList, nodeList);
                telemetryContext.AddContext(EventProperty.SubscriptionId, subscription);
                telemetryContext.AddContext(SchedulerEventProperty.VmSkuList, vmSkus);

                if (!scheduleAction.Parameters.ContainsKey(Constants.Nodes))
                {
                    scheduleAction.Parameters.Add(Constants.Nodes, nodeList);
                }

                if (!scheduleAction.Parameters.ContainsKey(Constants.Subscription))
                {
                    scheduleAction.Parameters.Add(Constants.Subscription, subscription);
                }

                if (!scheduleAction.Parameters.ContainsKey(Constants.VmSku))
                {
                    scheduleAction.Parameters.Add(Constants.VmSku, string.Join(",", vmSkus));
                }

                return await base.ExecuteActionAsync(scheduleAction, scheduleContext, telemetryContext, cancellationToken)
                    .ConfigureDefaults();
            }

            return new ExecutionResult(ExecutionStatus.Cancelled);
        }

        private void OverwriteQueryparameters(EnvironmentQuery query, IDictionary<string, IConvertible> parameters)
        {
            const string queryTag = "query.";
            foreach (KeyValuePair<string, IConvertible> parameter in parameters)
            {
                if (parameter.Key.StartsWith(queryTag, StringComparison.OrdinalIgnoreCase))
                {
                    string parameterName = parameter.Key.Substring(queryTag.Length);
                    query.Parameters.Add(parameterName, parameter.Value);
                }
            }
        }

        private void OverwriteSearchValues(EnvironmentQuery query)
        {
            string star = "*";
            foreach (EnvironmentFilter filter in query.Filters)
            {
                if (filter.Parameters.ContainsKey(Constants.IncludeRegion) && filter.Parameters[Constants.IncludeRegion].Equals(star))
                {
                    filter.Parameters[Constants.IncludeRegion] = Constants.InternalRegionSearch;
                }
            }
        }

        internal class Constants
        {
            internal const string EnvironmentQuery = "environmentQuery";
            internal const string Nodes = "nodes";
            internal const string Subscription = "subscription";
            internal const string VmSku = "vmSku";
            internal const string QueryId = "queryId";
            internal const string IncludeRegion = "includeRegion";
            internal const string InternalRegionSearch = "$.subscription.regionSearchSpace";
        }
    }
}
