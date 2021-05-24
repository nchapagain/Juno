namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using TipGateway.Entities;

    /// <summary>
    /// Provider enables the deployment of microcode updates/payloads to physical nodes
    /// as part of a Juno experiment.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Parameters.MicrocodeProvider, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.MicrocodeVersion, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.PFServiceName, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.PFServicePath, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.RequestTimeout, Type = typeof(TimeSpan))]
    [SupportedParameter(Name = Parameters.VerificationTimeout, Type = typeof(TimeSpan))]
    [ProviderInfo(Name = "Apply Microcode Update", Description = "Applies the microcode update to the physical nodes/blades in the experiment group", FullDescription = "Step to deploy an Intel microcode update (IPU) to physical nodes in the Azure fleet as part of a Juno experiment. This experiment step uses the PilotFish agent running on the physical node to handle the deployment of the microcode update. PilotFish is an agent of the AutoPilot system that enables the deployment of applications to Azure physical nodes/infrastructure. PilotFish meets Azure security and compliance requirements. The Juno system uses PilotFish to deploy different types of payloads to Azure physical nodes to evaluate net changes to the performance or reliability of the node for hosting customer VM workloads caused by firmware or hardware changes.")]
    public class MicrocodeUpdateProvider : ExperimentProvider
    {
        private const int DefaultMaxInstallationAttempts = 5;
        private static readonly TimeSpan RetryTime = TimeSpan.FromSeconds(180);

        // Default timeout for validating that the TiP Gateway handed off the microcode
        // update request to the PilotFish service.
        internal static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(20);

        // Default timeout for validating the microcode update was successfully applied on
        // the physical node.
        internal static readonly TimeSpan DefaultVerificationTimeout = TimeSpan.FromMinutes(20);

        private IEnumerable<EnvironmentEntity> entitiesProvisioned;
        private IEnumerable<EnvironmentEntity> tipSessions;

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrocodeUpdateProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public MicrocodeUpdateProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        public override Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            ITipClient tipClient;
            if (!this.Services.TryGetService<ITipClient>(out tipClient))
            {
                this.Services.AddTransient<ITipClient>((provider) => new TipClient(context.Configuration));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);

            if (!cancellationToken.IsCancellationRequested)
            {
                MicrocodeUpdateProviderState state = await this.GetStateAsync<MicrocodeUpdateProviderState>(context, cancellationToken, sharedState: false).ConfigureDefaults()
                    ?? new MicrocodeUpdateProviderState();

                this.ValidateState(state);
                await this.GetTipSessionsAsync(context, cancellationToken).ConfigureDefaults();

                // Execution Flow:
                // 1) Send request to TiP Gateway service to request the deployment of the microcode update to the physical node(s)
                //    in the experiment group.
                // 2) Confirm that the TiP Gateway service successfully hands-off the request to the PilotFish agent on the physical
                //    node(s).
                // 3) Create agent steps that will run in the Host Agent process on the physical nodes to explicitly verify the
                //    microcode update was successfully applied.
                // 4) Check the execution status of the agent steps to confirm whether they complete successfully indicating the
                //    microcode update was verified to be applied on the physical node(s). In case of failure updating Microcode,
                //    run diagnostics.

                if (!state.DeploymentRequestsCompleted)
                {
                    if (state.DeploymentRequested)
                    {
                        await this.ConfirmMicrocodeUpdateInProgressAsync(context, state, telemetryContext, cancellationToken)
                            .ConfigureDefaults();
                    }

                    await this.RequestMicrocodeUpdateDeploymentAsync(context, component, state, telemetryContext, cancellationToken)
                        .ConfigureDefaults();

                    await this.SaveStateAsync(context, state, cancellationToken, sharedState: false)
                        .ConfigureDefaults();
                }

                if (state.DeploymentRequested && state.DeploymentRequestsCompleted)
                {
                    if (!state.AgentStepsCreated)
                    {
                        await this.CreateAgentStepsAsync(context, component, state, telemetryContext, cancellationToken).ConfigureDefaults();
                        await this.SaveStateAsync(context, state, cancellationToken, sharedState: false).ConfigureDefaults();
                    }
                    else
                    {
                        ExecutionResult microcodeUpdateResult = await this.GetAgentStepResultAsync(context, state, telemetryContext, cancellationToken)
                            .ConfigureDefaults();

                        if (ExecutionResult.CompletedStatuses.Contains(microcodeUpdateResult.Status))
                        {
                            result = microcodeUpdateResult;
                        }

                        bool diagnosticsEnabled = context.Experiment.IsDiagnosticsEnabled();
                        if (diagnosticsEnabled && microcodeUpdateResult.Status == ExecutionStatus.Failed)
                        {
                            await this.RequestDiagnosticsAsync(context, state.TipRequests, telemetryContext, cancellationToken).ConfigureDefaults();
                        }
                    }
                }
            }

            return result;
        }

        private static List<KeyValuePair<string, string>> CreateDeploymentParameters(ExperimentComponent component)
        {
            component.ThrowIfNull(nameof(component));

            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(
                    component.Parameters.GetValue<string>(Parameters.PFServiceName),
                    component.Parameters.GetValue<string>(Parameters.PFServicePath))
            };
        }

        private static void SaveChangeId(EnvironmentEntity tipSession, string tipNodeChangeId)
        {
            // Add the TiP session change ID to the list of IDs preserved on the environment entity.
            if (tipSession.Metadata.ContainsKey(nameof(TipSession.ChangeIdList)))
            {
                char[] delimiters = new char[] { ',' };
                List<string> tipNodeChangeIds = tipSession.Metadata[nameof(TipSession.ChangeIdList)]
                    .ToString().Split(delimiters, StringSplitOptions.RemoveEmptyEntries).ToList();

                tipNodeChangeIds.Add(tipNodeChangeId);
                tipSession.Metadata[nameof(TipSession.ChangeIdList)] = string.Join(",", tipNodeChangeIds);
            }
        }

        [SuppressMessage("Naming", "AZCA1004:AsyncResultProhibited Rule", Justification = "The analyzer mistakenly thought 'tipChange.Result' is task.Result but it's not.")]
        private async Task ConfirmMicrocodeUpdateInProgressAsync(ExperimentContext context, MicrocodeUpdateProviderState state, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                ITipClient tipClient = this.Services.GetService<ITipClient>();
                List<Task> verificationTasks = new List<Task>();

                foreach (TipRequestDescription tipRequest in state.TipRequests)
                {
                    if (!(tipRequest.RequestTime == DateTime.MinValue))
                    {
                        verificationTasks.Add(Task.Run(async () =>
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                EventContext relatedContext = telemetryContext.Clone()
                                    .AddContext(nameof(tipRequest), tipRequest);

                                await this.Logger.LogTelemetryAsync($"{nameof(MicrocodeUpdateProvider)}.VerifyRequest", relatedContext, async () =>
                                {
                                    TipNodeSessionChangeDetails tipSessionChangeResponse = await tipClient.GetTipSessionChangeAsync(
                                        tipRequest.TipNodeSessionId,
                                        tipRequest.TipNodeSessionChangeId,
                                        cancellationToken).ConfigureDefaults();

                                    telemetryContext.AddContext(nameof(tipSessionChangeResponse), tipSessionChangeResponse);

                                    if (tipSessionChangeResponse.Status == TipNodeSessionChangeStatus.Finished && tipSessionChangeResponse.Result == TipNodeSessionChangeResult.Succeeded)
                                    {
                                        tipRequest.DeploymentVerified = true;
                                    }
                                    else if (tipSessionChangeResponse.Result == TipNodeSessionChangeResult.Failed &&
                                            tipSessionChangeResponse.IsRetryableScenario() &&
                                            state.InstallationRetries++ <= MicrocodeUpdateProvider.DefaultMaxInstallationAttempts)
                                    {
                                        if (tipRequest.RequestTime != null && tipRequest.RequestTime.Add(MicrocodeUpdateProvider.RetryTime) < DateTime.UtcNow)
                                        {
                                            state.TipRequests.Remove(tipRequest);
                                        }
                                    }
                                }).ConfigureDefaults();
                            }
                        }));
                    }
                }

                if (state.TipRequests.All(request => request.DeploymentVerified))
                {
                    state.DeploymentRequestsCompleted = true;
                }
                else
                {
                    TipRequestDescription expiredRequest = state.TipRequests.FirstOrDefault(request => request.IsTimeoutExpired);
                    if (expiredRequest != null)
                    {
                        if (context.Experiment.IsDiagnosticsEnabled())
                        {
                            await this.RequestDiagnosticsAsync(context, expiredRequest.TipNodeId, expiredRequest.TipNodeSessionId, telemetryContext, cancellationToken, expiredRequest.TipNodeSessionChangeId).ConfigureDefaults();
                        }

                        throw new ProviderException(
                            $"Timeout expired. The TiP Gateway request to deploy the microcode update did not complete " +
                            $"within the time range allowed (timeout = '{expiredRequest.RequestTimeout}')",
                            ErrorReason.Timeout);
                    }
                }

                await Task.WhenAll(verificationTasks).ConfigureDefaults();
            }
        }

        private async Task CreateAgentStepsAsync(
            ExperimentContext context,
            ExperimentComponent component,
            MicrocodeUpdateProviderState state,
            EventContext telemetryContext,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await this.Logger.LogTelemetryAsync($"{nameof(MicrocodeUpdateProvider)}.CreateAgentSteps", telemetryContext, async () =>
                {
                    List<Task> stepCreationTasks = new List<Task>();

                    foreach (TipRequestDescription tipRequest in state.TipRequests)
                    {
                        stepCreationTasks.Add(Task.Run(async () =>
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                string microcodeProvider = component.Parameters.GetValue<string>(Parameters.MicrocodeProvider);
                                string microcodeVersion = component.Parameters.GetValue<string>(Parameters.MicrocodeVersion);

                                // The agent step will run in the Juno Host agent process and is responsible for explicitly
                                // verifying that the microcode update version deployed is actually applied/installed on the
                                // system.
                                ExperimentComponent agentStep = new ExperimentComponent(
                                    typeof(MicrocodeActivationProvider).FullName,
                                    "Verify Microcode Activation",
                                    $"Verify '{microcodeProvider}' microcode update version '{microcodeVersion}' is applied to the node.",
                                    component.Group,
                                    parameters: new Dictionary<string, IConvertible>
                                    {
                                        [Parameters.MicrocodeVersion] = component.Parameters.GetValue<string>(Parameters.MicrocodeVersion),
                                        [StepParameters.Timeout] = component.Parameters.GetTimeSpanValue(
                                            Parameters.VerificationTimeout,
                                            MicrocodeUpdateProvider.DefaultVerificationTimeout).ToString()
                                    },
                                    tags: component.Tags);

                                IProviderDataClient dataClient = this.Services.GetService<IProviderDataClient>();
                                await dataClient.CreateAgentStepsAsync(context.ExperimentStep, agentStep, tipRequest.AgentId, cancellationToken)
                                    .ConfigureDefaults();
                            }
                        }));
                    }

                    state.AgentStepsCreated = true;
                    state.AgentStepCreationTime = DateTime.UtcNow;
                    state.UpdateVerificationTimeout = component.Parameters.GetTimeSpanValue(
                        Parameters.VerificationTimeout,
                        MicrocodeUpdateProvider.DefaultVerificationTimeout);

                    await Task.WhenAll(stepCreationTasks).ConfigureDefaults();
                }).ConfigureDefaults();
            }
        }

        private async Task<ExecutionResult> GetAgentStepResultAsync(
            ExperimentContext context,
            MicrocodeUpdateProviderState state,
            EventContext telemetryContext,
            CancellationToken cancellationToken)
        {
            ExecutionResult result = new ExecutionResult(ExecutionStatus.InProgress);
            if (!cancellationToken.IsCancellationRequested)
            {
                if (state.IsVerificationTimeoutExpired)
                {
                    throw new ProviderException(
                        $"Timeout expired. The microcode update did not complete within the time range allowed " +
                        $"(timeout = '{state.UpdateVerificationTimeout}')",
                        ErrorReason.Timeout);
                }

                await this.Logger.LogTelemetryAsync($"{nameof(MicrocodeUpdateProvider)}.VerifyUpdateApplied", telemetryContext, async () =>
                {
                    IProviderDataClient dataClient = this.Services.GetService<IProviderDataClient>();
                    IEnumerable<ExperimentStepInstance> agentSteps = await dataClient.GetAgentStepsAsync(context.ExperimentStep, cancellationToken)
                        .ConfigureDefaults();

                    if (agentSteps?.Any() != true)
                    {
                        throw new ProviderException("Unexpected data scenario. The agent steps expected for the provider do not exist in the Juno system.");
                    }

                    ExecutionStatus status = agentSteps.GetExecutionStatus();
                    IEnumerable<Exception> errors = agentSteps.GetErrors();

                    result = new ExecutionResult(status, error: errors?.Any() == true ? new AggregateException(errors) : null);
                }).ConfigureDefaults();
            }

            return result;
        }

        private async Task GetTipSessionsAsync(
            ExperimentContext context,
            CancellationToken cancellationToken)
        {
            this.entitiesProvisioned = await this.GetEntitiesProvisionedAsync(context, cancellationToken).ConfigureDefaults();
            this.tipSessions = this.entitiesProvisioned?.GetEntities(EntityType.TipSession, context.ExperimentStep.ExperimentGroup);

            if (this.tipSessions?.Any() != true)
            {
                throw new ProviderException(
                    $"Expected environment entities not found. There are no TiP session/node entities registered in the experiment environment " +
                    $"for experiment group '{context.ExperimentStep.ExperimentGroup}' ",
                    ErrorReason.ExpectedEnvironmentEntitiesNotFound);
            }
        }

        private async Task RequestMicrocodeUpdateDeploymentAsync(
            ExperimentContext context,
            ExperimentComponent component,
            MicrocodeUpdateProviderState state,
            EventContext telemetryContext,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                if (state.TipRequests == null)
                {
                    state.TipRequests = new List<TipRequestDescription>();
                }

                List<Task> deploymentRequestTasks = new List<Task>();

                foreach (EnvironmentEntity tipSession in this.tipSessions)
                {
                    TipRequestDescription previousRequest = state.TipRequests.Where(s => s.TipNodeSessionId == tipSession.Id)?.FirstOrDefault();
                    if (previousRequest == null)
                    {
                        deploymentRequestTasks.Add(Task.Run(async () =>
                        {
                            EventContext relatedContext = telemetryContext.Clone()
                                .AddContext(nameof(tipSession), tipSession);

                            await this.Logger.LogTelemetryAsync($"{nameof(MicrocodeUpdateProvider)}.RequestUpdate", relatedContext, async () =>
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    ITipClient tipClient = this.Services.GetService<ITipClient>();
                                    List<KeyValuePair<string, string>> serviceDeploymentParameters = MicrocodeUpdateProvider.CreateDeploymentParameters(component);
                                    TipNodeSessionChange tipResult = await tipClient.ApplyPilotFishServicesAsync(tipSession.Id, serviceDeploymentParameters, cancellationToken)
                                        .ConfigureDefaults();

                                    relatedContext.AddContext("tipSessionChangeResponse", tipResult);

                                    state.DeploymentRequested = true;
                                    state.TipRequests.Add(new TipRequestDescription
                                    {
                                        AgentId = new AgentIdentification(tipSession.ClusterName(), tipSession.NodeId(), tipSession.TipSessionId()).ToString(),
                                        RequestTime = DateTime.UtcNow,
                                        RequestTimeout = component.Parameters.GetTimeSpanValue(Parameters.RequestTimeout, MicrocodeUpdateProvider.DefaultRequestTimeout),
                                        TipNodeId = tipSession.Metadata.GetValue<string>(nameof(TipSession.NodeId)),
                                        TipNodeSessionChangeId = tipResult.TipNodeSessionChangeId,
                                        TipNodeSessionId = tipResult.TipNodeSessionId
                                    });

                                    MicrocodeUpdateProvider.SaveChangeId(tipSession, tipResult.TipNodeSessionChangeId);
                                }
                            }).ConfigureDefaults();
                        }));
                    }
                }

                await Task.WhenAll(deploymentRequestTasks).ConfigureDefaults();
                await this.UpdateEntitiesProvisionedAsync(context, this.entitiesProvisioned, cancellationToken).ConfigureDefaults();
            }
        }

        private void ValidateState(MicrocodeUpdateProviderState state)
        {
            if (state.DeploymentRequested && state.TipRequests?.Any() != true)
            {
                throw new ProviderException(
                    $"Invalid provider state. The provider state asserts that deployment requests were sent, but the requests are not tracked.",
                    ErrorReason.ProviderStateInvalid);
            }
        }

        private async Task RequestDiagnosticsAsync(ExperimentContext context, List<TipRequestDescription> tipRequests, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            tipRequests.ThrowIfNull(nameof(tipRequests));
            List<Task> tipSessionDiagnosticsTasks = new List<Task>();

            foreach (var tipRequest in tipRequests)
            {
                tipSessionDiagnosticsTasks.Add(Task.Run(async () => await this.RequestDiagnosticsAsync(context, tipRequest.TipNodeId, tipRequest.TipNodeSessionId, telemetryContext, cancellationToken).ConfigureDefaults()));
            }

            await Task.WhenAll(tipSessionDiagnosticsTasks).ConfigureDefaults();
        }

        private async Task RequestDiagnosticsAsync(ExperimentContext context, string tipNodeId, string tipNodeSessionId, EventContext telemetryContext, CancellationToken cancellationToken, string tipNodeSessionChangeId = null)
        {
            context.ThrowIfNull(nameof(context));
            tipNodeId.ThrowIfNullOrEmpty(nameof(tipNodeId));
            tipNodeSessionId.ThrowIfNullOrEmpty(nameof(tipNodeSessionId));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (!cancellationToken.IsCancellationRequested)
            {
                EventContext relatedContext = telemetryContext.Clone(withProperties: true);
                try
                {
                    await this.Logger.LogTelemetryAsync($"{nameof(MicrocodeUpdateProvider)}.RequestDiagnostics", relatedContext, async () =>
                    {
                        IDictionary<string, IConvertible> metadata = new Dictionary<string, IConvertible>()
                        {
                            { DiagnosticsParameter.TipNodeId, tipNodeId },
                            { DiagnosticsParameter.TipSessionId, tipNodeSessionId },
                            { DiagnosticsParameter.ExperimentId, context.Experiment.Id },
                            { DiagnosticsParameter.ProviderName, nameof(MicrocodeUpdateProvider) }
                        };

                        if (!string.IsNullOrEmpty(nameof(tipNodeSessionChangeId)))
                        {
                            metadata.Add(DiagnosticsParameter.TipSessionChangeId, tipNodeSessionChangeId);
                        }

                        List<DiagnosticsRequest> diagnosticsRequests = new List<DiagnosticsRequest>()
                        {
                            new DiagnosticsRequest(
                                context.Experiment.Id,
                                Guid.NewGuid().ToString(),
                                DiagnosticsIssueType.MicrocodeUpdateFailure,
                                DateTime.UtcNow.AddHours(-2),
                                DateTime.UtcNow,
                                metadata),

                            new DiagnosticsRequest(
                                context.Experiment.Id,
                                Guid.NewGuid().ToString(),
                                DiagnosticsIssueType.TipPilotfishPackageDeploymentFailure,
                                DateTime.UtcNow.AddHours(-2),
                                DateTime.UtcNow,
                                metadata)
                        };

                        relatedContext.AddContext("requests", diagnosticsRequests);

                        await this.AddDiagnosticsRequestAsync(context, diagnosticsRequests, cancellationToken).ConfigureDefaults();
                    }).ConfigureDefaults();
                }
                catch
                {
                    // The logging framework already captures the details of any exceptions thrown by the logic within.
                    // The exception should not surface beyond this as it may create confusion around the reason for the provider's failure.
                }
            }
        }

        internal static class Parameters
        {
            public const string MicrocodeProvider = "microcodeProvider";
            public const string MicrocodeVersion = "microcodeVersion";
            public const string PFServiceName = "pfServiceName";
            public const string PFServicePath = "pfServicePath";
            public const string RequestTimeout = "requestTimeout";
            public const string VerificationTimeout = "verificationTimeout";
        }

        internal class MicrocodeUpdateProviderState
        {
            /// <summary>
            /// True if the provider has successfully created the agent steps required
            /// to run on the target nodes in order to verify the actual microcode update
            /// was applied.
            /// </summary>
            public bool AgentStepsCreated { get; set; }

            /// <summary>
            /// The time at which the microcode update verification process began
            /// (e.g. when the agent steps responsible for verification were deployed).
            /// </summary>
            public DateTime AgentStepCreationTime { get; set; }

            /// <summary>
            /// True if the provider has successfully requested deployment of the microcode
            /// PF service (via the TiP Gateway service).
            /// </summary>
            public bool DeploymentRequested { get; set; }

            /// <summary>
            /// True of the provider has successfully verified the deployment of the microcode
            /// update. This is not the verification that the update is applied but that it was
            /// handed off to the PilotFish service. Final verification is performed by an agent
            /// step running on the physical node.
            /// </summary>
            public bool DeploymentRequestsCompleted { get; set; }

            /// <summary>
            /// Notes how many times the provider retries on the installation of Micorcode Pilotfish.
            /// </summary>
            public int InstallationRetries { get; set; }

            /// <summary>
            /// True if the allowable time to verify the microcode update has expired.
            /// </summary>
            [JsonIgnore]
            public bool IsVerificationTimeoutExpired
            {
                get
                {
                    return DateTime.UtcNow >= this.AgentStepCreationTime.Add(this.UpdateVerificationTimeout);
                }
            }

            /// <summary>
            /// The timeout for validation that the expected microcode update was
            /// actually applied on the target node(s).
            /// </summary>
            public TimeSpan UpdateVerificationTimeout { get; set; }

            /// <summary>
            /// The set of TiP request descriptions that have details for the status of individual
            /// microcode update requests per TiP session.
            /// </summary>
            public List<TipRequestDescription> TipRequests { get; set; }
        }

        internal class TipRequestDescription
        {
            /// <summary>
            /// The unique ID of the Host agent.
            /// </summary>
            public string AgentId { get; set; }

            /// <summary>
            /// True if the provider has successfully verified that the PF service was
            /// deployed on the physical node(s). Note that this is a confirmation that
            /// the TiP Gateway service handed off to the PilotFish agent on the node(s).
            /// A final verification is required on the actual node to explicitly verify
            /// that the microcode update was actually applied to the physical node(s) by
            /// the PilotFish agent.
            /// </summary>
            public bool DeploymentVerified { get; set; }

            /// <summary>
            /// True if the allowable time to verify the deployment hand-off of the microcode
            /// update (to PilotFish) has expired.
            /// </summary>
            [JsonIgnore]
            public bool IsTimeoutExpired
            {
                get
                {
                    return DateTime.UtcNow >= this.RequestTime.Add(this.RequestTimeout);
                }
            }

            /// <summary>
            /// The time at which the request(s) were made to the TiP Gateway to deploy
            /// the microcode update.
            /// </summary>
            public DateTime RequestTime { get; set; }

            /// <summary>
            /// The timeout for validation that the TiP Gateway successfully handed
            /// off the microcode update request to the PilotFish agent.
            /// </summary>
            public TimeSpan RequestTimeout { get; set; }

            /// <summary>
            /// The name of the physical node for which the TiP session is established.
            /// </summary>
            public string TipNodeId { get; set; }

            /// <summary>
            /// The TiP session ID associated with the physical node(s) isolated through
            /// the TiP service.
            /// </summary>
            public string TipNodeSessionId { get; set; }

            /// <summary>
            /// The TipNode request ID associated with the change request to deploy
            /// the microcode update. This is used to track the status of the request
            /// as the TiP service attempts to hand-off to the PilotFish service.
            /// </summary>
            public string TipNodeSessionChangeId { get; set; }
        }
    }
}