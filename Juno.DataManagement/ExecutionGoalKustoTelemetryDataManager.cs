namespace Juno.DataManagement
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Kusto.Data.Exceptions;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Polly;

    /// <summary>
    /// <see cref="IExperimentKustoTelemetryDataManager"/>
    /// </summary>
    public class ExecutionGoalKustoTelemetryDataManager : IExperimentKustoTelemetryDataManager
    {
        private readonly int maxRetryCount = 3;
        private IAsyncPolicy retryPolicy;

        private string absoluteUri;
        private string clusterDatabase;
        private string environment;

        /// <summary>
        /// <see cref="IExperimentKustoTelemetryDataManager"/>
        /// </summary>
        public ExecutionGoalKustoTelemetryDataManager(IKustoQueryIssuer kustoQueryIssuer, IConfiguration configuration, ILogger logger = null)
        {
            kustoQueryIssuer.ThrowIfNull(nameof(kustoQueryIssuer));

            this.KustoQueryIssuer = kustoQueryIssuer;
            this.Logger = logger ?? NullLogger.Instance;
            this.Configuration = configuration;

            this.SetKustoSettings();
            this.retryPolicy = Policy.Handle<KustoException>(e => ((HttpStatusCode)e.FailureCode).IsTransientError())
                .WaitAndRetryAsync(retryCount: this.maxRetryCount, (retries) => TimeSpan.FromSeconds(Math.Pow(2, this.maxRetryCount)));
        }

        /// <summary>
        /// Gets the logger for capturing operation telemetry.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the experiment document store data provider.
        /// </summary>
        protected IKustoQueryIssuer KustoQueryIssuer { get; }

        /// <summary>
        /// Gets the experiment document store data provider.
        /// </summary>
        protected IConfiguration Configuration { get; }

        /// <see cref="IExperimentKustoTelemetryDataManager.GetExecutionGoalStatusAsync(string, CancellationToken, string)"/>
        public async Task<IList<ExperimentInstanceStatus>> GetExecutionGoalStatusAsync(string executionGoal, CancellationToken cancellationToken, string teamName = null)
        {
            teamName.ThrowIfNullOrWhiteSpace(nameof(teamName));
            executionGoal.ThrowIfNullOrWhiteSpace(nameof(executionGoal));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(executionGoal), executionGoal);

            return await this.Logger.LogTelemetryAsync($"{nameof(ExecutionGoalKustoTelemetryDataManager)}.GetExecutionGoalStatus", telemetryContext, async () =>
            {
                string query = Kusto.Resources.ExecutionGoalStatus;
                query = query.Replace(Constants.ExecutionGoalFilter, executionGoal, StringComparison.Ordinal);
                telemetryContext.AddContext(nameof(query), query);

                DataTable kustoResponse = await this.ExecuteQueryAsync(query).ConfigureAwait(false);
                
                return kustoResponse.ParseExperimentInstanceStatus();
            }).ConfigureDefaults();
        }

        /// <see cref="IExperimentKustoTelemetryDataManager.GetExecutionGoalTimelineAsync(GoalBasedSchedule, CancellationToken)"/>
        public Task<IList<TargetGoalTimeline>> GetExecutionGoalTimelineAsync(GoalBasedSchedule executionGoal, CancellationToken cancellationToken)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(executionGoal.ExecutionGoalId), executionGoal.ExecutionGoalId)
                .AddContext(nameof(executionGoal.ExperimentName), executionGoal.ExperimentName);

            return this.Logger.LogTelemetryAsync($"{nameof(ExecutionGoalKustoTelemetryDataManager)}.GetExecutionGoalTimeline", telemetryContext, async () =>
            {
                IList<TargetGoalTimeline> results = new List<TargetGoalTimeline>();
                Dictionary<Goal, Task<DataTable>> dataTableTaskDictionary = new Dictionary<Goal, Task<DataTable>>();

                foreach (Goal targetGoal in executionGoal.TargetGoals)
                {
                    string query = Kusto.Resources.TargetGoalStatus;
                    string targetGoalFilter = string.Join("-", targetGoal.Name, executionGoal.TeamName);
                    query = query.Replace(Constants.TargetGoalFilter, targetGoalFilter, StringComparison.Ordinal);

                    telemetryContext.AddContext(nameof(query), query);

                    dataTableTaskDictionary.Add(targetGoal, this.ExecuteQueryAsync(query));
                }

                // All kusto tasks will complete here.
                await Task.WhenAll(dataTableTaskDictionary.Values).ConfigureAwait(false);
                foreach (KeyValuePair<Goal, Task<DataTable>> pair in dataTableTaskDictionary)
                {
                    DataTable kustoResponse = await pair.Value.ConfigureAwait(false);
                    List<TargetGoalTimeline> targetGoalTimeLines = kustoResponse.ParseExecutionGoalTimeline(pair.Key, executionGoal.ExecutionGoalId, executionGoal.ExperimentName, executionGoal.TeamName, this.environment).ToList();
                    results = results.Concat(targetGoalTimeLines).ToList();
                }

                return results;
            });
        }

        private async Task<DataTable> ExecuteQueryAsync(string query)
        {
            query = query.Replace(Constants.Environment, this.environment, StringComparison.Ordinal);
            return await this.retryPolicy.ExecuteAsync(async () =>
            {
                return await this.KustoQueryIssuer.IssueAsync(this.absoluteUri, this.clusterDatabase, query)
                .ConfigureAwait(false);
            }).ConfigureDefaults();
        }

        private void SetKustoSettings()
        {
            EnvironmentSettings settings = EnvironmentSettings.Initialize(this.Configuration);
            this.environment = settings.Environment;
            KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
            this.absoluteUri = kustoSettings.ClusterUri.AbsoluteUri;
            this.clusterDatabase = kustoSettings.ClusterDatabase;
        }

        internal class Constants
        {
            internal const string Environment = "$environment$";
            internal const string ExecutionGoalFilter = "$executionGoalFilter$";
            internal const string TargetGoalFilter = "$targetGoalFilter$";
        }
    }
}
