namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.TipIntegration;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Polly;

    /// <summary>
    /// Common dependencies and services required for diagnostics tests.
    /// </summary>
    public class DiagnosticsProviderFixture : FixtureDependencies
    {
        /// <summary>
        ///  The default retry policy found in each of the diagnostic handlers
        /// </summary>
        protected static readonly IAsyncPolicy DefaultRetryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(5, (retries) => TimeSpan.FromSeconds(retries + 3));

        /// <summary>
        /// Initializes a <see cref="DiagnosticsProviderFixture"/> for running mock Diagnostics
        /// </summary>
        public DiagnosticsProviderFixture(IAsyncPolicy retryPolicy = null)
        {
            this.Initialize(retryPolicy);
        }

        /// <summary>
        /// Common experiment ID for testing
        /// </summary>
        public string ExperimentId { get; set; }

        /// <summary>
        /// A common event context to all providers
        /// </summary>
        public EventContext EventContext { get; set; }

        /// <summary>
        /// A common cancellation token context to all providers
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Mock diagnostic provider retry policy.
        /// </summary>
        public IAsyncPolicy RetryPolicy { get; set; }

        /// <summary>
        /// Mock diagnostic data client.
        /// </summary>
        public Mock<IProviderDataClient> DataClient { get; set; }

        /// <summary>
        /// A list of valid diagnostic requests
        /// </summary>
        public IEnumerable<DiagnosticsRequest> DiagnosticRequestsList { get; set; }

        /// <summary>
        /// Mock diagnostic arm client
        /// </summary>
        public Mock<IArmClient> ArmClient { get; set; }

        /// <summary>
        /// Mock diagnostic tip client
        /// </summary>
        public Mock<ITipClient> TipClient { get; set; }

        /// <summary>
        /// Mock diagnostic query issuer
        /// </summary>
        public Mock<IKustoQueryIssuer> QueryIssuer { get; set; }

        /// <summary>
        /// Creates columns for the various kusto tables returned
        /// </summary>
        public static DataColumn CreateColumn(string columnName)
        {
            DataColumn column = new DataColumn();
            column.DataType = Type.GetType("System.String");
            column.ColumnName = columnName;
            column.AutoIncrement = false;
            column.Caption = columnName;
            column.ReadOnly = false;
            column.Unique = false;
            return column;
        }

        /// <summary>
        /// Generic method to create a valid diagnostic request for the providers
        /// </summary>
        public DiagnosticsRequest CreateValidDiagnosticRequest(DiagnosticsIssueType issueType, Dictionary<string, IConvertible> queryContext)
        {
            return new DiagnosticsRequest(
                experimentId: this.ExperimentId,
                id: Guid.NewGuid().ToString(),
                issueType: issueType,
                timeRangeBegin: DateTime.UtcNow.AddMinutes(-30),
                timeRangeEnd: DateTime.UtcNow,
                context: queryContext);
        }

        /// <summary>
        /// Creates a list of valid Diagnostic Requests
        /// </summary>
        private List<DiagnosticsRequest> CreateDiagnosticRequestList()
        {
            return new List<DiagnosticsRequest>()
            {
                {
                    // Creates the Arm Vm Deployment Failure Activity Log Diagnostic Request
                    this.CreateValidDiagnosticRequest(
                        DiagnosticsIssueType.ArmVmCreationFailure,
                        new Dictionary<string, IConvertible>
                        {
                            { DiagnosticsParameter.SubscriptionId, Guid.NewGuid().ToString() },
                            { DiagnosticsParameter.ResourceGroupName, Guid.NewGuid().ToString() }
                        })
                },
                {
                    // Creates the Arm Vm Deployment Failure Kusto Diagnostic Request
                    this.CreateValidDiagnosticRequest(
                        issueType: DiagnosticsIssueType.ArmVmCreationFailure,
                        queryContext: new Dictionary<string, IConvertible>
                        {
                            { DiagnosticsParameter.TipSessionId, Guid.NewGuid().ToString() },
                            { DiagnosticsParameter.ResourceGroupName, Guid.NewGuid().ToString() }
                        })
                },
                {
                    // Creates the Microcode Update Failure Kusto Diagnostic Request
                    this.CreateValidDiagnosticRequest(
                        issueType: DiagnosticsIssueType.MicrocodeUpdateFailure,
                        queryContext: new Dictionary<string, IConvertible>
                        {
                            { DiagnosticsParameter.TipNodeId, Guid.NewGuid().ToString() }
                        })
                },
                {
                    // Creates the Tip Api Service Failure Diagnostic Request
                    this.CreateValidDiagnosticRequest(
                        issueType: DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure,
                        queryContext: new Dictionary<string, IConvertible>
                        {
                            { DiagnosticsParameter.TipSessionId, Guid.NewGuid().ToString() },
                            { DiagnosticsParameter.TipSessionChangeId, Guid.NewGuid().ToString() }
                        })
                },
                {
                    // Creates the Tip Failure Kusto Diagnostic Request
                     this.CreateValidDiagnosticRequest(
                        issueType: DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure,
                        queryContext: new Dictionary<string, IConvertible>
                        {
                            { DiagnosticsParameter.TipSessionId, Guid.NewGuid().ToString() }
                        })
                }
            };
        }

        private void Initialize(IAsyncPolicy retryPolicy = null)
        {
            // set up the common attributes
            this.ExperimentId = Guid.NewGuid().ToString();
            this.CancellationToken = CancellationToken.None;
            this.EventContext = new EventContext(Guid.NewGuid());
            // set up the common diagnostic provider services
            this.RetryPolicy = this.RetryPolicy != null ? retryPolicy : DiagnosticsProviderFixture.DefaultRetryPolicy;
            this.ArmClient = new Mock<IArmClient>();
            this.QueryIssuer = new Mock<IKustoQueryIssuer>();
            this.DataClient = new Mock<IProviderDataClient>();
            this.TipClient = new Mock<ITipClient>();
            this.Services = new ServiceCollection();
            this.Services
                .AddSingleton<IKustoQueryIssuer>(this.QueryIssuer.Object)
                .AddSingleton<IArmClient>(this.ArmClient.Object)
                .AddSingleton<IProviderDataClient>(this.DataClient.Object)
                .AddSingleton<ITipClient>(this.TipClient.Object);
            this.Logger = new Mock<ILogger>();

            // each diagnostic provider gets and saves a common diagnostic state
            var state = new DiagnosticState();
            this.DataClient.OnGetState<DiagnosticState>().ReturnsAsync(new DiagnosticState());
            this.DataClient.OnSaveState<DiagnosticState>().Returns(Task.CompletedTask);

            // ensure the successful path defaults to http success codes
            using (HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK))
            {
                this.RestClient
                    .Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .ReturnsAsync(response);
            }

            this.DiagnosticRequestsList = this.CreateDiagnosticRequestList();
        }
    }
}