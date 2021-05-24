namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.ArmIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A provider for installing guest agent on virtual machines using arm templates
    /// </summary>
    /// 
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = StepParameters.PackageVersion, Type = typeof(string))]
    [SupportedParameter(Name = StepParameters.Platform, Type = typeof(string))]
    [ProviderInfo(Name = "Install Juno Guest Agent", Description = "Installs the Juno Guest agent on the VMs in the specified experiment group", FullDescription = "Step to install the Juno Guest agent on virtual machines in the environment as part of an experiment.  The Juno Guest agent runs on virtual machines associated with the experiment and is responsible for handling the application of the workloads as part of this experiment. The Juno Guest agent additionally monitors certain aspects of the system as it is running producing data that can be analyzed after the experiment runs.")]
    public partial class InstallGuestAgentProvider : ExperimentProvider
    {
        /// <summary>
        /// Default timeout for agents to send heartbeat after bootstrap deployment is succeeded for all virtual machines
        /// </summary>
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallGuestAgentProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public InstallGuestAgentProvider(IServiceCollection services)
            : base(services)
        {
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
                InstallGuestAgentProviderState state = await this.GetStateAsync<InstallGuestAgentProviderState>(context, cancellationToken).ConfigureDefaults()
                    ?? new InstallGuestAgentProviderState();

                string key = string.Format(ContractExtension.ResourceGroup, context.ExperimentStep.ExperimentGroup);
                VmResourceGroupDefinition resourceGroupDefinition = await this.GetStateAsync<VmResourceGroupDefinition>(context, key, cancellationToken)
                    .ConfigureDefaults();

                telemetryContext.AddContext(resourceGroupDefinition);

                if (resourceGroupDefinition == null)
                {
                    throw new ProviderException(
                        $"Juno Guest agent installation failed. Expected virtual machines not found. The Juno Guest agent cannot be installed until virtual machines have " +
                        $"been established for the experiment in the target subscription.",
                        ErrorReason.VirtualMachinesNotFound);
                }

                TimeSpan timeout = component.Parameters.GetTimeSpanValue(StepParameters.Timeout, InstallGuestAgentProvider.DefaultTimeout);

                if (!cancellationToken.IsCancellationRequested)
                {
                    if (InstallGuestAgentProvider.IsDeploymentSucceeded(resourceGroupDefinition))
                    {
                        if (await this.CheckHeartbeatAsync(resourceGroupDefinition, telemetryContext, cancellationToken).ConfigureDefaults())
                        {
                            result = new ExecutionResult(ExecutionStatus.Succeeded);
                        }
                        else if (await this.CheckTimeoutAsync(context, telemetryContext, timeout, cancellationToken).ConfigureDefaults())
                        {
                            throw new ProviderException(
                                $"Juno Guest agent installation failed. An agent heartbeat could not be confirmed within the time range allowed " +
                                $"(timeout = {timeout.ToString()}).",
                                ErrorReason.Timeout);
                        }
                    }
                    else if (InstallGuestAgentProvider.IsTerminalState(resourceGroupDefinition))
                    {
                        throw new ProviderException(
                            $"Juno Guest agent installation failed. The ARM service indicates the deployment/bootstrapping of the agent is in a terminal state." +
                            $"(timeout = {timeout.ToString()}).",
                            ErrorReason.ArmDeploymentFailure);
                    }
                    else
                    {
                        if (!state.AgentInstallationRequested)
                        {
                            await this.RegisterAgentsWithExperimentAsync(context, resourceGroupDefinition, telemetryContext, cancellationToken)
                                .ConfigureDefaults();
                        }

                        Uri installerUri = InstallGuestAgentProvider.GetInstallerUri(context, component, telemetryContext);
                        component.Parameters.TryGetValue(StepParameters.PackageVersion, out IConvertible packageVersion);

                        await this.InstallGuestAgentAsync(context, resourceGroupDefinition, telemetryContext, installerUri, cancellationToken, packageVersion?.ToString())
                            .ConfigureDefaults();

                        state.AgentInstallationRequested = true;
                        await this.SaveStateAsync(context, state, cancellationToken).ConfigureDefaults();
                    }
                }

                telemetryContext.AddContext(resourceGroupDefinition);
                await this.SaveStateAsync<VmResourceGroupDefinition>(context, key, resourceGroupDefinition, cancellationToken)
                    .ConfigureDefaults();
            }

            return result;
        }

        private static AgentIdentification GetAgentIdentification(VmResourceGroupDefinition resourceGroupDefinition, VmDefinition vmDefinition)
        {
            return AgentIdentification.CreateVirtualMachineId(
                resourceGroupDefinition.ClusterId,
                resourceGroupDefinition.NodeId,
                vmDefinition.Name,
                resourceGroupDefinition.TipSessionId);
        }

        private static Uri GetInstallerUri(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
            Uri installerUri = settings.ExecutionSettings.Installers.Get(Setting.Default).Uri; // default = win-x64 installer
            if (component.Parameters.TryGetValue(StepParameters.Platform, out IConvertible platform))
            {
                telemetryContext.AddContext("platform", platform.ToString());

                // Ex: GuestAgent-win-x64, GuestAgent-linux-x64
                string installer = $"{Setting.GuestAgent}-{platform}";
                InstallerSettings installerSettings = settings.ExecutionSettings.Installers.Get(installer);
                installerUri = installerSettings.Uri;
            }

            return installerUri;
        }

        private static bool IsDeploymentSucceeded(VmResourceGroupDefinition resourceGroupDefinition)
        {
            resourceGroupDefinition.ThrowIfNull(nameof(resourceGroupDefinition));
            return resourceGroupDefinition.VirtualMachines.All(vm => vm.BootstrapState.IsSuccessful());
        }

        private static bool IsTerminalState(VmResourceGroupDefinition resourceGroupDefinition)
        {
            resourceGroupDefinition.ThrowIfNull(nameof(resourceGroupDefinition));
            return resourceGroupDefinition.VirtualMachines.All(vm => vm.BootstrapState.IsDeploymentFinished());
        }

        private async Task<bool> CheckTimeoutAsync(ExperimentContext context, EventContext telemetryContext, TimeSpan timeout, CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);
            return await this.Logger.LogTelemetryAsync($"{nameof(InstallGuestAgentProvider)}.CheckTimeout", relatedContext, async () =>
            {
                bool result = false;
                try
                {
                    var timeoutKey = $"heartbeatTimeout_{context.ExperimentStep.Id}";
                    var startTime = await this.GetStateAsync<DateTime>(context, timeoutKey, cancellationToken).ConfigureAwait(false);
                    // This means it is first time, save current time utc as start time
                    if (startTime == default)
                    {
                        await this.SaveStateAsync(context, timeoutKey, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
                    }
                    else if (DateTime.UtcNow > startTime.Add(timeout))
                    {
                        result = true;
                    }
                }
                catch (Exception exception)
                {
                    relatedContext.AddError(exception);
                }

                return result;
            }).ConfigureAwait(false);
        }

        private async Task<bool> CheckHeartbeatAsync(VmResourceGroupDefinition resourceGroupDefinition, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);
            return await this.Logger.LogTelemetryAsync($"{nameof(InstallGuestAgentProvider)}.CheckHeartbeat", relatedContext, async () =>
            {
                bool heartbeatExists = false;

                try
                {
                    resourceGroupDefinition.ThrowIfNull(nameof(resourceGroupDefinition));
                    IProviderDataClient apiClient = this.Services.GetService<IProviderDataClient>();
                    foreach (var vm in resourceGroupDefinition.VirtualMachines)
                    {
                        string agentId = InstallGuestAgentProvider.GetAgentIdentification(resourceGroupDefinition, vm).ToString();
                        AgentHeartbeatInstance heartbeat = await apiClient.GetAgentHeartbeatAsync(agentId, cancellationToken).ConfigureDefaults();
                        heartbeatExists = heartbeat != null;
                    }
                }
                catch (Exception exception)
                {
                    relatedContext.AddError(exception);
                }

                return heartbeatExists;
            }).ConfigureAwait(false);
        }

        private async Task InstallGuestAgentAsync(
            ExperimentContext context, VmResourceGroupDefinition resourceGroupDefinition, EventContext telemetryContext, Uri installerUri, CancellationToken cancellationToken, string packageVersion = null)
        {
            EventContext relatedContext = telemetryContext.Clone()
                .AddContext("agents", resourceGroupDefinition.VirtualMachines.Select(vm => vm.VirtualMachineName));

            await this.Logger.LogTelemetryAsync($"{nameof(InstallGuestAgentProvider)}.RequestInstallation", relatedContext, async () =>
            {
                using (var armTemplateManager = new ArmTemplateManager(context.Configuration, this.Logger))
                {
                    await armTemplateManager.BootstrapVirtualMachinesAsync(resourceGroupDefinition, installerUri, cancellationToken, packageVersion)
                        .ConfigureDefaults();
                }
            }).ConfigureDefaults();
        }

        private async Task RegisterAgentsWithExperimentAsync(ExperimentContext context, VmResourceGroupDefinition resourceGroupDefinition, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            if (resourceGroupDefinition.VirtualMachines?.Any() == true)
            {
                IProviderDataClient dataClient = this.Services.GetService<IProviderDataClient>();
                foreach (VmDefinition vmEntity in resourceGroupDefinition.VirtualMachines)
                {
                    string agentId = InstallGuestAgentProvider.GetAgentIdentification(resourceGroupDefinition, vmEntity).ToString();
                    EventContext relatedContext = telemetryContext.Clone()
                        .AddContext(nameof(agentId), agentId);

                    await this.Logger.LogTelemetryAsync($"{nameof(InstallGuestAgentProvider)}.RegisterAgent", relatedContext, async () =>
                    {
                        try
                        {
                            ExperimentComponent agentRegistrationStep = new ExperimentComponent(
                            typeof(GuestAgentRegistrationProvider).FullName,
                            "Register Agent",
                            "Registers/associates the Guest Agent with the experiment",
                            group: context.ExperimentStep.ExperimentGroup);

                            await dataClient.CreateAgentStepsAsync(context.ExperimentStep, agentRegistrationStep, agentId, cancellationToken)
                                .ConfigureDefaults();
                        }
                        catch (ProviderException exc) when (exc.Reason == ErrorReason.DataAlreadyExists)
                        {
                            // The agent/experiment mapping already exists. This is ok.
                        }
                    }).ConfigureDefaults();
                }
            }
        }

        internal class InstallGuestAgentProviderState
        {
            public bool AgentInstallationRequested { get; set; }

            public TimeSpan Timeout { get; set; }

            public DateTime StepTimeout { get; set; }
        }
    }
}