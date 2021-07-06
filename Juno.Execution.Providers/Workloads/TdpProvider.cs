namespace Juno.Execution.Providers.Workloads
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Kusto.Data;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provider for creating virtual machines repetitively as TDP workload.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Parameters.DataDiskCount, Type = typeof(int), Required = false)]
    [SupportedParameter(Name = Parameters.DataDiskSizeInGB, Type = typeof(int), Required = false)]
    [SupportedParameter(Name = Parameters.DataDiskSku, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.DataDiskStorageAccountType, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.OsDiskStorageAccountType, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.OsOffer, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.OsPublisher, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.OsSku, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.OsVersion, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.SigImageReference, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.Platform, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.Regions, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.SubscriptionId, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.UseTipSession, Type = typeof(bool), Required = false)]
    [SupportedParameter(Name = Parameters.PinnedCluster, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Parameters.Iterations, Type = typeof(int), Required = true)]
    [SupportedParameter(Name = Parameters.VmSize, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.EnableAcceleratedNetworking, Type = typeof(bool), Required = false)]
    [SupportedParameter(Name = Parameters.MaxConsecutiveFailure, Type = typeof(int), Required = false)]
    [ProviderInfo(Name = "TDP workload", Description = "Tenant deployment performance workload.", FullDescription = "Step to deploy virtual machines on physical nodes/blades in the environment repetitively to get measurement of how fast Azure can deploy a Virtual machine.")]
    public partial class TdpProvider : ExperimentProvider
    {
        internal const string UnknownEntity = "unknown";
        private const int DefaultMaxConsecutiveFailure = 5;
        private const int DefaultDataDiskCount = 0;
        private const string DefaultDiskSku = "Standard_LRS";
        private const int DefaultDataDiskSizeInGB = 1024;
        private const string DefaultOsVersion = "latest";

        /// <summary>
        /// Initializes a new instance of the <see cref="TdpProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public TdpProvider(IServiceCollection services)
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
                TdpState state = await this.GetStateAsync<TdpState>(context, cancellationToken).ConfigureAwait(false);

                if (state == null)
                {
                    // Code path for first time entry with new state.
                    state = new TdpState();
                    state.ActiveIterationResourceGroup = await this.CreateResourceGroupDefinitionAsync(context, component, telemetryContext, cancellationToken).ConfigureAwait(false);
                    int maxConsecutiveFailure = component.Parameters.GetValue<int>(Parameters.MaxConsecutiveFailure, TdpProvider.DefaultMaxConsecutiveFailure);
                    state.MaximumConsecutiveFailure = maxConsecutiveFailure;
                    telemetryContext.AddContext(state.ActiveIterationResourceGroup);
                    await this.DeployIterationAsync(state, context, telemetryContext, cancellationToken).ConfigureDefaults();
                }
                else if (state.ActiveIterationState == TdpState.TdpStatus.Creating)
                {
                    // If the previous state is creating. This will refresh the state and set the state to Created if the iteration is finished.
                    await this.DeployIterationAsync(state, context, telemetryContext, cancellationToken).ConfigureDefaults();
                }
                else if (state.ActiveIterationState == TdpState.TdpStatus.Deleting)
                {
                    // If the previous state is deleting. This will refresh the state and set the state to Deleting if the iteration is deleted.
                    await this.DeleteIterationAsync(state, context, telemetryContext, cancellationToken).ConfigureDefaults();
                }

                if (state.ActiveIterationState == TdpState.TdpStatus.Created)
                {
                    // Once the iteration is deployed. Proceed to deletion.
                    await this.DeleteIterationAsync(state, context, telemetryContext, cancellationToken).ConfigureDefaults();
                }
                else if (state.ActiveIterationState == TdpState.TdpStatus.Deleted || state.ActiveIterationState == TdpState.TdpStatus.CreationFailed)
                {
                    // Once the iteration is deleted. Return success if reached defined iterations, or deploy new iteration.
                    int interations = component.Parameters.GetValue<int>(Parameters.Iterations);
                    if (state.SuccessfulVirtualMachines.Count >= interations)
                    {
                        result = new ExecutionResult(ExecutionStatus.Succeeded);
                    }
                    else
                    {
                        state.ActiveIterationResourceGroup = await this.CreateResourceGroupDefinitionAsync(context, component, telemetryContext, cancellationToken).ConfigureAwait(false);
                        await this.DeployIterationAsync(state, context, telemetryContext, cancellationToken).ConfigureDefaults();
                    }
                }

                await this.SaveStateAsync<TdpState>(context, state, cancellationToken).ConfigureDefaults();
            }

            return result;
        }

        /// <summary>
        /// Returns the target/valid OS disk SKU for the VM SKU provided.
        /// </summary>
        /// <param name="proposedDiskSku">The disk SKU proposed/defined in the experiment workflow step definition.</param>
        /// <param name="vmSku">The VM SKU for which the OS disk is associated.</param>
        /// <returns>
        /// A valid OS disk SKU for the VM SKU defined.
        /// </returns>
        protected static string GetTargetDiskSku(string proposedDiskSku, string vmSku)
        {
            proposedDiskSku.ThrowIfNullOrWhiteSpace(nameof(proposedDiskSku));
            vmSku.ThrowIfNullOrWhiteSpace(nameof(vmSku));

            string effectiveDiskSku = proposedDiskSku;
            string premiumDiskSupportingVmSkuExpression = "Standard_[A-M][0-9]+a*(?=s)"; // These VM SKUs support premium disks
            if (!Regex.IsMatch(vmSku, premiumDiskSupportingVmSkuExpression, RegexOptions.IgnoreCase)
                && !string.Equals(proposedDiskSku, TdpProvider.DefaultDiskSku, StringComparison.OrdinalIgnoreCase))
            {
                // The disk SKU defined is not valid for the VM SKU provided. Use the default/safe disk SKU instead.
                effectiveDiskSku = TdpProvider.DefaultDiskSku;
            }

            return effectiveDiskSku;
        }

        /// <summary>
        /// Returns the target VM SKU selected from the supported VM SKUs defined for the TiP session/node matching the
        /// one of the VM SKUs defined in the component definition.
        /// </summary>
        /// <param name="component">The experiment workflow step definition/component.</param>
        /// <param name="telemetryContext">The telemetry event context.</param>
        /// <returns>
        /// A VM SKU from the set as defined by the workflow step/component definition.
        /// </returns>
        protected static string GetTargetVmSku(ExperimentComponent component, EventContext telemetryContext)
        {
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            string targetVmSku = null;
            string[] explicitVmSkus = component.Parameters.GetValue<string>(Parameters.VmSize).Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Trim()).ToArray();

            telemetryContext.AddContext("vmSkusAllowed", string.Join(",", explicitVmSkus));
            targetVmSku = explicitVmSkus.Shuffle().FirstOrDefault();

            if (string.IsNullOrWhiteSpace(targetVmSku))
            {
                throw new ProviderException("The target VM SKU could not be determined from the experiment step definition.");
            }

            return targetVmSku;
        }

        /// <summary>
        /// Returns the target VM SKU selected from the supported VM SKUs defined for the TiP session/node matching the
        /// one of the VM SKUs defined in the component definition.
        /// </summary>
        /// <param name="component">The experiment workflow step definition/component.</param>
        /// <param name="tipSessionEntity">The TiP session/node environment entity on which the VM(s) will be deployed.</param>
        /// <param name="telemetryContext">The telemetry event context.</param>
        /// <returns>
        /// A VM SKU that matches the set of available VM SKUs that can be deployed to the node and the set of VM SKUs
        /// allowable as defined by the workflow step/component definition.
        /// </returns>
        protected static string GetTargetVmSku(ExperimentComponent component, EnvironmentEntity tipSessionEntity, EventContext telemetryContext)
        {
            component.ThrowIfNull(nameof(component));
            tipSessionEntity.ThrowIfNull(nameof(tipSessionEntity));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            string targetVmSku = null;
            string[] explicitVmSkus = component.Parameters.GetValue<string>(Parameters.VmSize).Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Trim()).ToArray();

            telemetryContext.AddContext("vmSkusAllowed", string.Join(",", explicitVmSkus));

            if (explicitVmSkus.Length == 1)
            {
                return explicitVmSkus.FirstOrDefault();
            }

            targetVmSku = explicitVmSkus.FirstOrDefault();
            if (tipSessionEntity.Metadata.TryGetValue(nameof(TipSession.PreferredVmSku), out IConvertible preferredVmSku)
                && preferredVmSku != null)
            {
                targetVmSku = preferredVmSku.ToString().Trim();
                telemetryContext.AddContext("vmSkusSupported", string.Join(",", targetVmSku));
            }
            else if (explicitVmSkus.Length > 1
                && tipSessionEntity.Metadata.TryGetValue(nameof(TipSession.SupportedVmSkus), out IConvertible allVmSkusAvailable)
                && allVmSkusAvailable != null)
            {
                string[] availableVmSkus = allVmSkusAvailable.ToString().Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(entry => entry.Trim()).ToArray();

                telemetryContext.AddContext("vmSkusSupported", string.Join(",", availableVmSkus));
                targetVmSku = explicitVmSkus.Intersect(availableVmSkus).Shuffle().FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(targetVmSku))
            {
                throw new ProviderException("The target VM SKU(s) defined in the experiment step do not match with the VM SKUs available for the TiP session/entity.");
            }

            return targetVmSku;
        }

        private async Task DeployIterationAsync(TdpState state, ExperimentContext context, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            using (IArmResourceManager resourceManager = this.CreateArmResourceManager(context))
            {
                // This method itself is idempotent, which means it will start deployment if not already started, and refresh state if already started. 
                string errorMessage = string.Empty;
                try
                {
                    await resourceManager.DeployResourceGroupAndVirtualMachinesAsync(state.ActiveIterationResourceGroup, cancellationToken).ConfigureDefaults();
                }
                catch (Exception exc)
                {
                    state.ActiveIterationResourceGroup.State = ProvisioningState.Failed;
                    errorMessage = exc.ToString(withCallStack: true, withErrorTypes: true);
                }

                if (state.ActiveIterationResourceGroup.IsSuccessful() && state.ActiveIterationResourceGroup.VirtualMachines.First().IsSuccessful())
                {
                    state.ActiveIterationState = TdpState.TdpStatus.Created;
                    state.SuccessfulVirtualMachines.Add(state.ActiveIterationResourceGroup.VirtualMachines.First().Name);
                    state.ConsecutiveFailures = 0;

                    this.Logger.LogTelemetry($"{nameof(TdpProvider)}.VirtualMachineCreated", LogLevel.Information, telemetryContext
                                .AddContext("vmName", state.ActiveIterationResourceGroup.VirtualMachines.First().VirtualMachineName));
                }
                else if (state.ActiveIterationResourceGroup.State == ProvisioningState.Failed || state.ActiveIterationResourceGroup.VirtualMachines.First().State == ProvisioningState.Failed)
                {
                    state.ActiveIterationState = TdpState.TdpStatus.CreationFailed;
                    this.Logger.LogTelemetry($"{nameof(TdpProvider)}.DeploymentFailed", LogLevel.Information, telemetryContext
                                .AddContext("vmName", state.ActiveIterationResourceGroup.VirtualMachines.First().VirtualMachineName)
                                .AddContext("errorMessage", errorMessage));

                    state.ConsecutiveFailures += 1;
                    if (state.ConsecutiveFailures > state.MaximumConsecutiveFailure)
                    {
                        throw new ProviderException(
                            $"Reached maximum consecutive failures for deploying resource group '{state.ActiveIterationResourceGroup.Name}' at '{state.MaximumConsecutiveFailure}' times.",
                            ErrorReason.MaximumFailureReached);
                    }
                }
                else
                {
                    state.ActiveIterationState = TdpState.TdpStatus.Creating;
                    this.Logger.LogTelemetry($"{nameof(TdpProvider)}.DeploymentInProgress", LogLevel.Information, telemetryContext
                                .AddContext("vmName", state.ActiveIterationResourceGroup.VirtualMachines.First().VirtualMachineName));
                }
            }
        }

        private async Task DeleteIterationAsync(TdpState state, ExperimentContext context, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            using (IArmResourceManager resourceManager = this.CreateArmResourceManager(context))
            {
                await resourceManager.DeleteResourceGroupAsync(state.ActiveIterationResourceGroup, cancellationToken).ConfigureDefaults();

                if (state.ActiveIterationResourceGroup.DeletionState == CleanupState.Succeeded)
                {
                    state.ActiveIterationState = TdpState.TdpStatus.Deleted;
                    state.ConsecutiveFailures = 0;
                    this.Logger.LogTelemetry($"{nameof(TdpProvider)}.VirtualMachineDeleted", LogLevel.Information, telemetryContext
                                .AddContext("vmName", state.ActiveIterationResourceGroup.VirtualMachines.First().VirtualMachineName));
                }
                else if (state.ActiveIterationResourceGroup.DeletionState == CleanupState.Accepted || state.ActiveIterationResourceGroup.DeletionState == CleanupState.Deleting)
                {
                    state.ActiveIterationState = TdpState.TdpStatus.Deleting;
                    this.Logger.LogTelemetry($"{nameof(TdpProvider)}.DeletionInProgress", LogLevel.Information, telemetryContext
                                .AddContext("vmName", state.ActiveIterationResourceGroup.VirtualMachines.First().VirtualMachineName));
                }
                else
                {
                    state.ActiveIterationState = TdpState.TdpStatus.DeletionFailed;
                    this.Logger.LogTelemetry($"{nameof(TdpProvider)}.DeletionFailed", LogLevel.Information, telemetryContext
                                .AddContext("vmName", state.ActiveIterationResourceGroup.VirtualMachines.First().VirtualMachineName));

                    state.ConsecutiveFailures += 1;
                    if (state.ConsecutiveFailures > state.MaximumConsecutiveFailure)
                    {
                        throw new ProviderException(
                            $"Reached maximum consecutive failures for deleting resource group '{state.ActiveIterationResourceGroup.Name}' at '{state.ConsecutiveFailures}' times.",
                            ErrorReason.DependencyFailure);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a <see cref="VmResourceGroupDefinition"/> defining the requirements of the set of VMs to 
        /// request via the ARM service.
        /// </summary>
        /// <param name="context">Provides context for the experiment in which the provider is running.</param>
        /// <param name="component">Defines the specifics of the experiment step (including parameters).</param>
        /// <param name="telemetryContext">Telemetry context.</param>
        /// <param name="cancellationToken">Cancellation token to cancel creation of resource group definition.</param>
        /// <returns>
        /// A <see cref="VmResourceGroupDefinition"/> that defines 1 or more VMs to create on a set
        /// of nodes in a target Azure data center region.
        /// </returns>
        private async Task<VmResourceGroupDefinition> CreateResourceGroupDefinitionAsync(
            ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            TipSession tipSession = null;
            string targetRegion = null;
            IConvertible useTipSession = true;
            string targetVmSku = null;
            string clusterName = null;

            if (!component.Parameters.TryGetValue(Parameters.UseTipSession, out useTipSession) || (bool)useTipSession == true)
            {
                // The provider supports the ability for the author to define 1 or more VM SKUs that can
                // be used. When the original TiP session is created, the supported VM SKUs are included in the 
                // metadata for the TiP session and node entities. If the workflow step definition/component defines
                // more than 1 VM SKU, the set of VM SKUs defined will be compared with the set of VM SKUs available (on the
                // TiP node). A final VM SKU will be selected from the intersection of the 2 sets.
                EnvironmentEntity tipSessionEntity = await this.GetTargetTipSessionAsync(context, component, cancellationToken).ConfigureAwait(false);
                tipSession = TipSession.FromEnvironmentEntity(tipSessionEntity);
                targetRegion = tipSession.Region;
                targetVmSku = TdpProvider.GetTargetVmSku(component, tipSessionEntity, telemetryContext);
                clusterName = tipSession.ClusterName;
            }
            else
            {
                // The parameter requirements are already validated beforehand.
                // Parameter Format
                // regions: East US,East US 2,West US,West US 2
                // vmSize: Standard_D2s_v3, vmSize: Standard_D2_v3,Standard_D2s_v3
                string regions = component.Parameters.GetValue<string>(Parameters.Regions);
                string[] allRegions = regions.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(entry => entry.Trim()).ToArray();

                targetRegion = allRegions.Length == 1
                    ? allRegions[0]
                    : allRegions.Shuffle().First();

                targetVmSku = TdpProvider.GetTargetVmSku(component, telemetryContext);
                clusterName = component.Parameters.GetValue<string>(Parameters.PinnedCluster, string.Empty);
            }

            if (string.IsNullOrWhiteSpace(targetRegion))
            {
                throw new ProviderException("The target region is not defined or cannot be determined from the context available.");
            }

            if (string.IsNullOrWhiteSpace(targetVmSku))
            {
                throw new ProviderException("The target VM SKU is not defined or cannot be determined from the context available.");
            }

            telemetryContext.AddContext("region", targetRegion);
            telemetryContext.AddContext("vmSku", targetVmSku);

            IDictionary<string, string> resourceGroupTags = new Dictionary<string, string>
            {
                { "experimentId", context.Experiment.Id },
                { "experimentGroup", component.Group },
                { "experimentStepId", context.ExperimentStep.Id },
                { "tipSessionId", tipSession?.TipSessionId ?? TdpProvider.UnknownEntity },
                { "nodeId", tipSession?.NodeId ?? TdpProvider.UnknownEntity },
                { "createdDate", DateTime.UtcNow.ToString("o") },
                { "expirationDate", DateTime.UtcNow.AddDays(2).ToString("o") }
            };

            if (component.Tags?.Any() == true)
            {
                resourceGroupTags.AddRange(component.Tags.Select(t => new KeyValuePair<string, string>(t.Key, t.Value.ToString())));
            }

            EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
            List<AzureVmSpecification> vmSpecifications = new List<AzureVmSpecification>();

            int iterations = component.Parameters.GetValue<int>(Parameters.Iterations, 1);
            string osDiskStorageAccountType = TdpProvider.GetTargetDiskSku(
                component.Parameters.GetValue<string>(Parameters.OsDiskStorageAccountType, TdpProvider.DefaultDiskSku),
                targetVmSku);

            string dataDiskStorageAccountType = TdpProvider.GetTargetDiskSku(
                component.Parameters.GetValue<string>(Parameters.DataDiskStorageAccountType, TdpProvider.DefaultDiskSku),
                targetVmSku);

            string dataDiskSku = TdpProvider.GetTargetDiskSku(
               component.Parameters.GetValue<string>(Parameters.DataDiskSku, TdpProvider.DefaultDiskSku),
               targetVmSku);

            vmSpecifications.Add(new AzureVmSpecification(
                    osDiskStorageAccountType,
                    targetVmSku,
                    component.Parameters.GetValue<string>(Parameters.OsPublisher, string.Empty),
                    component.Parameters.GetValue<string>(Parameters.OsOffer, string.Empty),
                    component.Parameters.GetValue<string>(Parameters.OsSku, string.Empty),
                    component.Parameters.GetValue<string>(Parameters.OsVersion, TdpProvider.DefaultOsVersion),
                    component.Parameters.GetValue<int>(Parameters.DataDiskCount, TdpProvider.DefaultDataDiskCount),
                    dataDiskSku,
                    component.Parameters.GetValue<int>(Parameters.DataDiskSizeInGB, TdpProvider.DefaultDataDiskSizeInGB),
                    dataDiskStorageAccountType,
                    component.Parameters.GetValue<bool>(Parameters.EnableAcceleratedNetworking, false),
                    sigImageReference: component.Parameters.GetValue<string>(Parameters.SigImageReference, string.Empty),
                    nodeId: tipSession?.NodeId,
                    tipSessionId: tipSession?.TipSessionId,
                    clusterId: clusterName));

            return new VmResourceGroupDefinition(
                settings.Environment,
                component.Parameters[Parameters.SubscriptionId].ToString(),
                context.Experiment.Id,
                Guid.NewGuid().ToString(), // This is a special case we are using a new Guid as the step id, because this step will have multiple resource groups.
                vmSpecifications,
                targetRegion,
                resourceGroupTags,
                component.Parameters.GetValue<string>(Parameters.Platform, VmPlatform.WinX64));
        }

        /// <summary>
        /// Creates an ARM resource manager to handle interactions with the Azure Resource Manager (ARM) endpoint to create 
        /// virtual machines and supporting resource within a target resource group.
        /// </summary>
        /// <param name="context">Provides context for the experiment in which the provider is running.</param>
        /// <returns>
        /// An <see cref="IArmResourceManager"/> for interaction with the ARM service.
        /// </returns>
        protected virtual IArmResourceManager CreateArmResourceManager(ExperimentContext context)
        {
            context.ThrowIfNull(nameof(context));
            return new ArmTemplateManager(context.Configuration, this.Logger);
        }

        /// <summary>
        /// Gets the TiP session defined in the context of the experiment that represents the isolated node for
        /// the environment on which the VMs will be created.
        /// </summary>
        /// <param name="context">Provides context for the experiment in which the provider is running.</param>
        /// <param name="component">Defines the specifics of the experiment step (including parameters).</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// The <see cref="TipSession"/> representing the TiP session/node on which the VMs will be created.
        /// </returns>
        protected async Task<EnvironmentEntity> GetTargetTipSessionAsync(ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            EnvironmentEntity tipSessionEntity = null;
            IEnumerable<EnvironmentEntity> entitiesProvisioned = await this.GetEntitiesProvisionedAsync(context, cancellationToken).ConfigureAwait(false);

            if (entitiesProvisioned != null)
            {
                tipSessionEntity = entitiesProvisioned.GetEntities(EntityType.TipSession, context.ExperimentStep.ExperimentGroup).FirstOrDefault();
            }

            if (tipSessionEntity == null)
            {
                throw new ProviderException(
                    $"A TiP session/entity for experiment group '{context.ExperimentStep.ExperimentGroup}' was not found.",
                    ErrorReason.ExpectedEnvironmentEntitiesNotFound);
            }

            return tipSessionEntity;
        }

        /// <summary>
        /// Validate required parameters of the experiment component that defines the requirements
        /// for the provider.
        /// </summary>
        /// <param name="component">The experiment component to validate.</param>
        protected override void ValidateParameters(ExperimentComponent component)
        {
            component.ThrowIfNull(nameof(component));
            base.ValidateParameters(component);

            IConvertible regions;
            IConvertible useTipSession;
            component.Parameters.TryGetValue(Parameters.UseTipSession, out useTipSession);
            component.Parameters.TryGetValue(Parameters.Regions, out regions);

            if (regions != null && (useTipSession == null || (bool)useTipSession == true))
            {
                throw new SchemaException(
                    $"Invalid step definition. When region(s) are explicitly defined, the '{Parameters.UseTipSession}' parameter must be defined and set to false.");
            }

            if (regions == null && useTipSession != null && (bool)useTipSession == false)
            {
                throw new SchemaException(
                    $"Invalid step definition. When TiP sessions are not used, the '{Parameters.Regions}' must be defined.");
            }
        }

        private class Parameters
        {
            internal const string DataDiskCount = nameof(Parameters.DataDiskCount);
            internal const string DataDiskSizeInGB = nameof(Parameters.DataDiskSizeInGB);
            internal const string DataDiskSku = nameof(Parameters.DataDiskSku);
            internal const string DataDiskStorageAccountType = nameof(Parameters.DataDiskStorageAccountType);
            internal const string Iterations = nameof(Parameters.Iterations);
            internal const string OsDiskStorageAccountType = nameof(Parameters.OsDiskStorageAccountType);
            internal const string OsOffer = nameof(Parameters.OsOffer);
            internal const string OsPublisher = nameof(Parameters.OsPublisher);
            internal const string OsSku = nameof(Parameters.OsSku);
            internal const string OsVersion = nameof(Parameters.OsVersion);
            internal const string SigImageReference = nameof(Parameters.SigImageReference);
            internal const string Platform = nameof(Parameters.Platform);
            internal const string Regions = nameof(Parameters.Regions);
            internal const string SubscriptionId = nameof(Parameters.SubscriptionId);
            internal const string UseTipSession = nameof(Parameters.UseTipSession);
            internal const string PinnedCluster = nameof(Parameters.PinnedCluster);
            internal const string VmSize = nameof(Parameters.VmSize);
            internal const string EnableAcceleratedNetworking = nameof(Parameters.EnableAcceleratedNetworking);
            internal const string MaxConsecutiveFailure = nameof(Parameters.MaxConsecutiveFailure);
        }

        internal class TdpState
        {
            internal enum TdpStatus
            {
                Creating,

                Created,

                Deleting,

                Deleted,
                
                CreationFailed,

                DeletionFailed
            }

            /// <summary>
            /// Counts the consecutive failures in deploying the VM. The Provider will abort trying if failures reaches maximum.
            /// </summary>
            public int ConsecutiveFailures { get; set; } = 0;

            /// <summary>
            /// Defines the maximum consecutive failures the ARM deployments and deletions can have before aborting.
            /// </summary>
            public int MaximumConsecutiveFailure { get; set; }

            /// <summary>
            /// Resource group definition for the active resource group being deployed.
            /// </summary>
            public VmResourceGroupDefinition ActiveIterationResourceGroup { get; set; }

            /// <summary>
            /// The state of the TDP provider for the active iteration.
            /// </summary>
            public TdpStatus ActiveIterationState { get; set; }

            /// <summary>
            /// Saves the successful virtual machines.
            /// </summary>
            public List<string> SuccessfulVirtualMachines { get; set; } = new List<string>();
        }
    }
}