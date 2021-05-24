# Juno Diagnostic Providers

#### Preliminaries
It is important to understand the basic schema of an experiment before attempting to write or reference Juno experiment providers.

[Juno Authoring Experiment Schema](./Authoring-ExperimentSchema.md)  
[Juno Experiment Providers](./ExperimentProviders.md)

### Defining Diagnostics Providers
All diagnostic providers in the Juno system derive from the base provider ```DiagnosticsProvider```.

``` csharp
public abstract class DiagnosticsProvider : IDiagnosticsProvider
{
    public DiagnosticsProvider(IServiceCollection services, IAsyncPolicy retryPolicy = null)
    {
        services.ThrowIfNull(nameof(services));

        this.Services = services;
        this.RetryPolicy = retryPolicy ?? DiagnosticsProvider.DefaultRetryPolicy;
    }

    // Provides a set of one or more dependencies required by the provider.
    public IServiceProvider Services { get; }

    // The entry-point method for a provider. The Autotriage provider will call this method to execute 
    // the diagnostics if the request has the required data.
    public async Task<ExecutionResult> DiagnoseAsync(DiagnosticsRequest request, ILogger logger, CancellationToken cancellationToken)

    // Single method required to be implemented by ALL diagnostic providers.
    protected abstract Task<ExecutionResult> DiagnoseAsync(DiagnosticsRequest request, EventContext telemetryContext, ILogger logger, CancellationToken cancellationToken);

    // Enables the provider to perform special validation against the parameters defined in the
    // diagnostic request.
    public bool IsHandled(DiagnosticsRequest request)

    // Single method required to be implemented by ALL providers based on data needed to execute the query
    protected abstract bool IsHandled(IDictionary<string, IConvertible> metadata, DiagnosticsIssueType issueType);
}
```

The ```DiagnosticsProvider``` base class requires a single method to be implemented ```DiagnoseAsync``` that is responsible for handling the runtime 
specifics/requirements defined by the ```DiagnosticsRequest``` that is passed into the method. Additionally, event context for the experiment is passed to the 
method in the ```EventContext``` class.  The experiment for which the provider is related/running is provided along with the exact experiment step.

All experiment provider implementation must abide by the following requirements:
* Providers must derive from the base ```DiagnosticsProvider``` class.
* Provider classes must have a second constructor that takes in a single ```IServiceProvider``` parameter.
* Provider classes can also implement a customized retry policy by adding in an optional ```IAsyncPolicy``` parameter.

### Types of Diagnostics Providers
Although the specifics of a provider implementation are up to the developer, there are 3 types of experiment provider implementations in the 
Juno system:

* **MicrocodeUpdateFailure**  
Diagnostics providers of this type perform queries from Kusto to diagnose CSI microcode update failures from the relevant Kusto telemetry data stores.

* **TipPilotfishPackageDeploymentFailure**  
Diagnostics providers of this type perform queries the TIP session status activity log and the Kusto log node snapshot to determine where the
TIP deployment failure occured.

* **ArmVmCreationFailure**  
Diagnostics providers of this type perform queries the subscription activity log, the Kusto deployment logs, and the Kusto QoS Events to determine 
where the ARM VM deployment failure occured.

``` csharp
public enum DiagnosticsIssueType
{
    /// <summary>
    /// Default issue type.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Microcode update failure issue.
    /// </summary>
    MicrocodeUpdateFailure,

    /// <summary>
    /// TIP pilotfish package deployment failure issue.
    /// </summary>
    TipPilotfishPackageDeploymentFailure,

    /// <summary>
    /// ARM VM creation failure issue.
    /// </summary>
    ArmVmCreationFailure
}
```
As each of these queries require certain data to execute, each diagnostic provider must also define what data is required for the query to execute
successfully. 

### Required Diagnostic Provider Services
To perform queries from Kusto or the subscription activity logs, there are services that are required by the diagnostic providers. Currently the auto-triage provider is responsible for passing the required services to the diagnostic providers. The following are core services that the current diagnostic providers rely on:
* **IKustoQueryIssuer**  
This service allows the diagnostic provider to issue a query against a cluster at the database level.
* **IArmClient**  
This service allows the diagnostic provider to retrieve diagnostic data from the Azure subscriptions with the Azure Resource Manager (ARM) Service.
* **ITipClient**  
This service allows the diagnostic provider to retrieve diagnostic data with the TIP service.
* **IProviderDataClient**  
This service allows the diagnostic provider to retrieve diagnostic data from within the Juno system including saving the provider state and data obtained from the queries.

``` csharp
public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
{
    context.ThrowIfNull(nameof(context));
    component.ThrowIfNull(nameof(component));

    base.ConfigureServicesAsync(context, component);

    EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
    // Adds in the KustoQueryIssuer based on the current environment settings
    if (!this.Services.TryGetService<IKustoQueryIssuer>(out IKustoQueryIssuer issuer))
    {
        KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
        kustoSettings.ThrowIfNull(nameof(kustoSettings));
        AadPrincipalSettings principalSettings = kustoSettings.AadPrincipals.Get(Setting.Default);
        this.Services.AddSingleton<IKustoQueryIssuer>(new KustoQueryIssuer(
            principalSettings.PrincipalId,
            principalSettings.PrincipalCertificateThumbprint,
            principalSettings.AuthorityUri.AbsoluteUri));
    }
    // Adds in the TIP Client
    if (!this.Services.TryGetService<ITipClient>(out ITipClient tipClient))
    {
        this.Services.AddSingleton<ITipClient>(new TipClient(context.Configuration));
    }
    // Adds in the ARM Client to access subscription activity logs from Azure Resource Manager
    if (!this.Services.TryGetService<IArmClient>(out IArmClient armClient))
    {
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
    // Adds in the DataClient to facilitate API calls within the Juno System
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
```
##### Execution Statuses

| Status             | Description     |
| ------------------ | --------------- |
| InProgress         | The provider/diagnostics execution is in-progress and the system should wait for it to complete before continuing to the next pending step(s). The runtime execution engine will continue to re-evaluate providers in this status until a final/terminal status is provided (e.g. Succeeded, Failed, Cancelled).
| Succeeded          | The provider/diagnostics execution completed successfully.
| Failed             | The provider/diagnostics execution failed.
| Cancelled          | The provider/diagnostics execution was cancelled.

##### Example DiagnosticProvider
The following is the MicrocodeUpdateFailureKustoDiagnostics provider.
``` csharp
/// <summary>
///  Diagnoses CSI microcode update failures by searching relevant Kusto telemetry data stores.
/// </summary>
public class MicrocodeUpdateFailureKustoDiagnostics : DiagnosticsProvider
{
    private readonly IKustoQueryIssuer queryIssuer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MicrocodeUpdateFailureKustoDiagnostics"/> class.
    /// </summary>
    /// <param name="services">The services/dependencies collection for the provider.</param>
    /// <param name="retryPolicy"></param>
    public MicrocodeUpdateFailureKustoDiagnostics(IServiceCollection services, IAsyncPolicy retryPolicy = null)
        : base(services, retryPolicy)
    {
        this.queryIssuer = services.GetService<IKustoQueryIssuer>();
    }

    /// <inheritdoc/>
    public override bool IsHandled(DiagnosticsRequest request)
    {
        request.ThrowIfNull(nameof(request));

        return request.Context.ContainsKey(DiagnosticsParameter.TipNodeId)
            && request.IssueType.Equals(DiagnosticsIssueType.MicrocodeUpdateFailure);
    }

    /// <inheritdoc/>
    protected override async Task<ExecutionResult> DiagnoseAsync(DiagnosticsRequest request, EventContext telemetryContext, ILogger logger, CancellationToken cancellationToken)
    {
        request.ThrowIfNull(nameof(request));
        telemetryContext.ThrowIfNull(nameof(telemetryContext));

        ExecutionResult executionResult = new ExecutionResult(ExecutionStatus.Succeeded);

        State state = await this.GetStateAsync<State>(request, cancellationToken).ConfigureDefaults()
            ?? new State()
            {
                Status = ExecutionStatus.InProgress
            };

        if (!state.IsTerminal && this.IsHandled(request))
        {
            EventContext relatedContext = telemetryContext.Clone();
            try
            {
                await logger.LogTelemetryAsync($"{nameof(MicrocodeUpdateFailureKustoDiagnostics)}.Diagnose", relatedContext, async () =>
                {
                    string tipNodeId = request.Context.GetValue<string>(DiagnosticsParameter.TipNodeId);
                    IEnumerable<DiagnosticsEntry> entries = await this.queryIssuer.GetMicrocodeUpdateDiagnosticsAsync(
                        tipNodeId, request.TimeRangeBegin, request.TimeRangeEnd, this.RetryPolicy).ConfigureDefaults();
                    if (entries?.Any() == true)
                    {
                        await logger.LogTelemetryAsync($"{nameof(MicrocodeUpdateFailureKustoDiagnostics)}.DiagnosticsResults", relatedContext, entries).ConfigureDefaults();
                    }
                }).ConfigureDefaults();
            }
            catch (Exception ex)
            {
                // Failure exception from handler should not crash the calling provider. Hence swallow the exception by logging the details.
                relatedContext.AddError(ex);
                logger.LogTelemetry($"{nameof(MicrocodeUpdateFailureKustoDiagnostics)}.Failure", LogLevel.Error, relatedContext);
                executionResult = new ExecutionResult(ExecutionStatus.Failed, ex);
            }
            finally
            {
                state.Status = executionResult.Status;
                await this.SaveStateAsync(request, state, cancellationToken).ConfigureDefaults();
            }
        }

        return executionResult;
    }

    internal class State
    {
        public ExecutionStatus Status { get; set; }

        public bool IsTerminal => ExecutionResult.IsTerminalStatus(this.Status);
    }
}
```
[Source Code](https://msazure.visualstudio.com/One/_git/CSI-CRC-AIR?path=%2Fsrc%2FJuno%2FJuno.Execution.Providers%2FAutoTriage%2FMicrocodeUpdateFailureKustoDiagnostics.cs&version=GBmaster)
