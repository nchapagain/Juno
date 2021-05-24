namespace Juno.Execution.Providers.AutoTriage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Execution.TipIntegration;
    using Juno.Hosting.Common;
    using Juno.Providers;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provider enabled to autotriage experiment failures
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Diagnostics, SupportedStepTarget.ExecuteRemotely)]
    [ProviderInfo(Name = "Run auto-triage on failed experiment", Description = "Performs auto-triage diagnostics on experiment failures", FullDescription = "Step to determine the root cause of an experiment failure.")]
    public class AutoTriageProvider : ExperimentProvider
    {
        // The timeout for the entirety of the auto triage process.
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromHours(2);

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoTriageProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public AutoTriageProvider(IServiceCollection services)
           : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            base.ConfigureServicesAsync(context, component);

            EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);

            if (!this.Services.TryGetService<IKustoQueryIssuer>(out IKustoQueryIssuer issuer))
            {
                KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
                kustoSettings.ThrowIfNull(nameof(kustoSettings));
                AadPrincipalSettings principalSettings = kustoSettings.AadPrincipals.Get(Setting.Default);
                this.Services.AddSingleton<IKustoQueryIssuer>(new KustoQueryIssuer(
                    principalSettings.PrincipalId,
                    principalSettings.PrincipalCertificateThumbprint,
                    principalSettings.TenantId));
            }

            if (!this.Services.TryGetService<ITipClient>(out ITipClient tipClient))
            {
                this.Services.AddSingleton<ITipClient>(new TipClient(context.Configuration));
            }

            if (!this.Services.TryGetService<IArmClient>(out IArmClient armClient))
            {
                // Create arm client
                IRestClient restClient = new RestClientBuilder()
                    .WithAutoRefreshToken(
                        settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc).AuthorityUri,
                        settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc).PrincipalId,
                        "https://management.core.windows.net/",
                        settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc).PrincipalCertificateThumbprint)
                    .AddAcceptedMediaType(MediaType.Json)
                    .Build();

                this.Services.AddSingleton<IArmClient>(new ArmClient(restClient));
            }

            if (!this.Services.TryGetService<IProviderDataClient>(out IProviderDataClient client))
            {
                AadPrincipalSettings executionSvcPrincipal = settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc);
                AadPrincipalSettings executionApiPrincipal = settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionApi);
                ExecutionClient apiClient = HostDependencies.CreateExecutionApiClient(
                    executionSvcPrincipal,
                    executionApiPrincipal,
                    settings.ExecutionSettings.ExecutionApiUri);
                this.Services.AddSingleton<IProviderDataClient>((provider) => new ProviderDataClient(apiClient));
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Executes auto-triage diagnostics by orchestrating diagnostic providers to query for requested data.
        /// </summary>
        /// <returns> The execution result of the attempt to automatically fetch the requested diagnostics.
        /// </returns>
        /// <remarks>
        /// Workflow:
        /// 1) Get all diagnostics request for the current experiment
        /// 2) If a timeout occurs, mark capture the timeout exception and mark the provider step as failed
        /// 3) For each request, check whether or not diagnostics needs to be performed from the diagnostic handler
        /// 4) Get the overall result status of the diagnostic handlers
        /// 5) Otherwise the experiment succeeded and/or an exception was caught prior to being requested, return succeeded
        /// </remarks>
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            if (!cancellationToken.IsCancellationRequested)
            {
                State state = await this.GetStateAsync<State>(context, cancellationToken).ConfigureDefaults()
                    ?? new State()
                    {
                        StepTimeout = DateTime.UtcNow.Add(component.Parameters.GetTimeSpanValue(StepParameters.Timeout, AutoTriageProvider.DefaultTimeout))
                    };

                IEnumerable<DiagnosticsRequest> diagnosticRequests = await this.GetDiagnosticsRequestsAsync(context, cancellationToken).ConfigureDefaults();

                try
                {
                    if (diagnosticRequests?.Any() == true)
                    {
                        if (state.IsTimeoutExpired)
                        {
                            throw new TimeoutException($"Auto-triage diagnostic process timed out (timeout = '{state.StepTimeout}').");
                        }

                        IEnumerable<DiagnosticsProvider> diagnosticProvider = this.CreateDiagnosticsProviders();
                        List<Task<ExecutionResult>> diagnosticResults = new List<Task<ExecutionResult>>();
                        diagnosticRequests.ForEach(request =>
                        {
                            diagnosticProvider.ForEach(handler =>
                             {
                                 if (!handler.IsHandled(request))
                                 {
                                     diagnosticResults.Add(Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded)));
                                 }
                                 else
                                 {
                                     diagnosticResults.Add(Task.Run(() => handler.DiagnoseAsync(request, this.Logger, cancellationToken)));
                                 }
                             });
                            
                        });

                        IEnumerable<ExecutionResult> results = await Task.WhenAll(diagnosticResults).ConfigureDefaults();

                        result = results.GetExecutionResult();
                    }
                    else
                    {
                        result = new ExecutionResult(ExecutionStatus.Succeeded);
                    }
                }
                finally
                {
                    await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                }
            }

            return result;
        }

        /// <summary>
        /// Creates the diagnostic handlers to perform the auto triage diagnostics.
        /// </summary>
        protected IEnumerable<DiagnosticsProvider> CreateDiagnosticsProviders()
        {
            List<DiagnosticsProvider> diagnosticHandlers = new List<DiagnosticsProvider>()
            {
                new MicrocodeUpdateFailureKustoDiagnostics(this.Services),
                new TipDeploymentFailureKustoDiagnostics(this.Services),
                new ArmVmDeploymentFailureKustoDiagnostics(this.Services),
                new ArmVmDeploymentFailureActivityLogDiagnostics(this.Services),
                new TipApiServiceFailureDiagnostics(this.Services)
            };

            return diagnosticHandlers;
        }

        internal class State
        {
            public DateTime StepTimeout { get; set; }

            [JsonIgnore]
            public bool IsTimeoutExpired => DateTime.UtcNow > this.StepTimeout;
        }
    }
}