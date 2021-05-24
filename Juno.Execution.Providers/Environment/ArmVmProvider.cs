namespace Juno.Execution.Providers.Environment
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
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provider for creating virtual machines (and supporting resources) using the Azure Resource
    /// Manager (ARM) service in a target subscription.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
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
    [SupportedParameter(Name = Parameters.VmCount, Type = typeof(int), Required = true)]
    [SupportedParameter(Name = Parameters.VmSize, Type = typeof(string), Required = true)]
    [SupportedParameter(Name = Parameters.EnableAcceleratedNetworking, Type = typeof(bool), Required = false)]
    [ProviderInfo(Name = "Create Virtual Machines", Description = "Create VMs to run workloads in the specified experiment group", FullDescription = "Step to deploy virtual machines on physical nodes/blades in the environment using the Azure Resource Manager (ARM) service. Virtual machines are used to run workloads as part of a Juno experiment. Workloads are often ran on virtual machines versus the physical node because this represents the real scenario experienced by Azure customers.")]
    public partial class ArmVmProvider : ExperimentProvider
    {
        private const int DefaultDataDiskCount = 1;
        private const string DefaultDiskSku = "Standard_LRS";
        private const int DefaultDataDiskSizeInGB = 32;
        private const string DefaultOsVersion = "latest";

        /// <summary>
        /// Initializes a new instance of the <see cref="ArmVmProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ArmVmProvider(IServiceCollection services)
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
                string vmResourceGroupStateKey = string.Format(ContractExtension.ResourceGroup, context.ExperimentStep.ExperimentGroup);

                VmResourceGroupDefinition resourceGroupDefinition = await this.GetStateAsync<VmResourceGroupDefinition>(
                        context,
                        vmResourceGroupStateKey,
                        cancellationToken).ConfigureDefaults();

                if (resourceGroupDefinition == null)
                {
                    EnvironmentEntity tipSession = null;
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
                        tipSession = await this.GetTargetTipSessionAsync(context, component, cancellationToken).ConfigureDefaults();

                        targetRegion = tipSession.Region();
                        targetVmSku = ArmVmProvider.GetTargetVmSku(component, tipSession, telemetryContext);
                        clusterName = tipSession.ClusterName();
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

                        targetVmSku = ArmVmProvider.GetTargetVmSku(component, telemetryContext);
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

                    resourceGroupDefinition = ArmVmProvider.CreateResourceGroupDefinition(
                        context,
                        component,
                        targetRegion,
                        targetVmSku,
                        clusterName,
                        tipSession?.TipSessionId(),
                        tipSession?.NodeId());
                }

                telemetryContext.AddContext(resourceGroupDefinition);
                result = ArmVmProvider.CreateExecutionResult(resourceGroupDefinition);

                if (!result.IsCompleted())
                {
                    using (IArmResourceManager resourceManager = this.CreateArmResourceManager(context))
                    {
                        await resourceManager.DeployResourceGroupAndVirtualMachinesAsync(resourceGroupDefinition, cancellationToken).ConfigureDefaults();
                        result = ArmVmProvider.CreateExecutionResult(resourceGroupDefinition);
                    }
                }

                if (result.Status == ExecutionStatus.Succeeded)
                {
                    IEnumerable<EnvironmentEntity> vmEntities = ArmVmProvider.ConvertToEntities(
                        resourceGroupDefinition,
                        context.ExperimentStep.ExperimentGroup);

                    await this.UpdateEntitiesProvisionedAsync(context, vmEntities, cancellationToken).ConfigureDefaults();
                }
                else if (result.Status == ExecutionStatus.Failed && context.Experiment.IsDiagnosticsEnabled())
                {
                    await this.RequestDiagnosticsAsync(context, resourceGroupDefinition, telemetryContext, cancellationToken).ConfigureDefaults();
                }

                await this.SaveStateAsync<VmResourceGroupDefinition>(context, vmResourceGroupStateKey, resourceGroupDefinition, cancellationToken)
                    .ConfigureDefaults();
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
                && !string.Equals(proposedDiskSku, ArmVmProvider.DefaultDiskSku, StringComparison.OrdinalIgnoreCase))
            {
                // The disk SKU defined is not valid for the VM SKU provided. Use the default/safe disk SKU instead.
                effectiveDiskSku = ArmVmProvider.DefaultDiskSku;
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

        /// <summary>
        /// Creates a <see cref="VmResourceGroupDefinition"/> defining the requirements of the set of VMs to
        /// request via the ARM service.
        /// </summary>
        /// <param name="context">Provides context for the experiment in which the provider is running.</param>
        /// <param name="component">Defines the specifics of the experiment step (including parameters).</param>
        /// <param name="region">The Azure data center region in which the VMs should be created.</param>
        /// <param name="clusterName">The Azure data center cluster in which the VMs should be created.</param>
        /// <param name="tipSessionId">The ID of the TiP session for the isolated node/blade on which the VMs should be created.</param>
        /// <param name="nodeId">Optional parameter defines the ID of the node/blade on which the VMs should be created.</param>
        /// <param name="vmSku">Optional parameter defines the VM SKU to target for the creation of the VM (e.g. Standard_D2s_v3).</param>
        /// <returns>
        /// A <see cref="VmResourceGroupDefinition"/> that defines 1 or more VMs to create on a set
        /// of nodes in a target Azure data center region.
        /// </returns>
        protected static VmResourceGroupDefinition CreateResourceGroupDefinition(
            ExperimentContext context, ExperimentComponent component, string region, string vmSku, string clusterName = null, string tipSessionId = null, string nodeId = null)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            region.ThrowIfNullOrWhiteSpace(nameof(region));

            IDictionary<string, string> resourceGroupTags = new Dictionary<string, string>
            {
                { "experimentId", context.Experiment.Id },
                { "experimentGroup", component.Group },
                { "experimentStepId", context.ExperimentStep.Id },
                { "tipSessionId", tipSessionId ?? AgentIdentification.UnknownEntity },
                { "nodeId", nodeId ?? AgentIdentification.UnknownEntity },
                { "createdDate", DateTime.UtcNow.ToString("o") },
                { "expirationDate", DateTime.UtcNow.AddDays(2).ToString("o") }
            };

            if (component.Tags?.Any() == true)
            {
                resourceGroupTags.AddRange(component.Tags.Select(t => new KeyValuePair<string, string>(t.Key, t.Value.ToString())));
            }

            EnvironmentSettings settings = EnvironmentSettings.Initialize(context.Configuration);
            List<AzureVmSpecification> vmSpecifications = new List<AzureVmSpecification>();

            int vmCount = component.Parameters.GetValue<int>(Parameters.VmCount, 1);
            string osDiskStorageAccountType = ArmVmProvider.GetTargetDiskSku(
                component.Parameters.GetValue<string>(Parameters.OsDiskStorageAccountType, ArmVmProvider.DefaultDiskSku),
                vmSku);

            string dataDiskStorageAccountType = ArmVmProvider.GetTargetDiskSku(
                component.Parameters.GetValue<string>(Parameters.DataDiskStorageAccountType, ArmVmProvider.DefaultDiskSku),
                vmSku);

            string dataDiskSku = ArmVmProvider.GetTargetDiskSku(
               component.Parameters.GetValue<string>(Parameters.DataDiskSku, ArmVmProvider.DefaultDiskSku),
               vmSku);

            for (int i = 0; i < vmCount; i++)
            {
                vmSpecifications.Add(new AzureVmSpecification(
                    osDiskStorageAccountType,
                    vmSku,
                    component.Parameters.GetValue<string>(Parameters.OsPublisher, string.Empty),
                    component.Parameters.GetValue<string>(Parameters.OsOffer, string.Empty),
                    component.Parameters.GetValue<string>(Parameters.OsSku, string.Empty),
                    component.Parameters.GetValue<string>(Parameters.OsVersion, ArmVmProvider.DefaultOsVersion),
                    component.Parameters.GetValue<int>(Parameters.DataDiskCount, ArmVmProvider.DefaultDataDiskCount),
                    dataDiskSku,
                    component.Parameters.GetValue<int>(Parameters.DataDiskSizeInGB, ArmVmProvider.DefaultDataDiskSizeInGB),
                    dataDiskStorageAccountType,
                    component.Parameters.GetValue<bool>(Parameters.EnableAcceleratedNetworking, false),
                    component.Parameters.GetValue<string>(Parameters.SigImageReference, string.Empty)));
            }

            return new VmResourceGroupDefinition(
                settings.Environment,
                component.Parameters[Parameters.SubscriptionId].ToString(),
                context.Experiment.Id,
                context.ExperimentStep.Id,
                vmSpecifications,
                region,
                clusterName,
                tipSessionId,
                nodeId,
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
            IEnumerable<EnvironmentEntity> entitiesProvisioned = await this.GetEntitiesProvisionedAsync(context, cancellationToken).ConfigureDefaults();

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

        private async Task RequestDiagnosticsAsync(ExperimentContext context, VmResourceGroupDefinition resourceGroupDefinition, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            resourceGroupDefinition.ThrowIfNull(nameof(resourceGroupDefinition));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (!cancellationToken.IsCancellationRequested)
            {
                EventContext relatedContext = telemetryContext.Clone(withProperties: true);
                try
                {
                    await this.Logger.LogTelemetryAsync($"{nameof(ArmVmProvider)}.RequestDiagnostics", relatedContext, async () =>
                    {
                        List<DiagnosticsRequest> requests = new List<DiagnosticsRequest>()
                        {
                            new DiagnosticsRequest(
                                context.ExperimentId,
                                Guid.NewGuid().ToString(),
                                DiagnosticsIssueType.ArmVmCreationFailure,
                                DateTime.UtcNow.AddHours(-2),
                                DateTime.UtcNow,
                                new Dictionary<string, IConvertible>()
                                {
                                    { DiagnosticsParameter.TipSessionId, resourceGroupDefinition.TipSessionId },
                                    { DiagnosticsParameter.SubscriptionId, resourceGroupDefinition.SubscriptionId },
                                    { DiagnosticsParameter.ResourceGroupName, resourceGroupDefinition.Name },
                                    { DiagnosticsParameter.ExperimentId, context.ExperimentId },
                                    { DiagnosticsParameter.ProviderName, nameof(ArmVmProvider) }
                                })
                        };

                        relatedContext.AddContext("requests", requests);
                        await this.AddDiagnosticsRequestAsync(context, requests, cancellationToken).ConfigureDefaults();
                    }).ConfigureDefaults();
                }
                catch
                {
                    // The logging framework already captures the details of any exceptions thrown by the logic within.
                    // The exception should not surface beyond this as it may create confusion around the reason for the provider's failure.                
                }
            }
        }

        private static IList<EnvironmentEntity> ConvertToEntities(VmResourceGroupDefinition definition, string group)
        {
            var entities = new List<EnvironmentEntity>();

            foreach (var vm in definition.VirtualMachines)
            {
                AgentIdentification agentId = AgentIdentification.CreateVirtualMachineId(definition.ClusterId, definition.NodeId, vm.Name, definition.TipSessionId);

                var metadata = new Dictionary<string, IConvertible>
                {
                    { "tipSessionId", agentId.Context },
                    { "cluster", agentId.ClusterName },
                    { "region", definition.Region },
                    { "nodeId", agentId.NodeName },
                    { "agentId", agentId.ToString() },
                    { "vmName", vm.Name },
                    { "vmSku", vm.VirtualMachineSize },
                    { "deploymentId", vm.DeploymentId },
                    { "osDiskSku", vm.OsDiskStorageAccountType },
                    { "dataDiskCount", vm.VirtualDisks?.Count ?? 0 },

                    // Format:
                    // dataDisks: sku=Standard_LRS,lun=0,sizeInGB=32|sku=Standard_LRS,lun=1,sizeInGB=32
                    { "dataDisks", vm.VirtualDisks?.Any() == true ? VmDisk.ToString(vm.VirtualDisks) : null }
                };

                EnvironmentEntity vmEntity = EnvironmentEntity.VirtualMachine(vm.Name, agentId.NodeName, group, metadata);
                vmEntity.AgentId(agentId.ToString());
                entities.Add(vmEntity);
            }

            return entities;
        }

        private static ExecutionResult CreateExecutionResult(VmResourceGroupDefinition resourceGroupDefinition)
        {
            var executionResult = new ExecutionResult(ExecutionStatus.InProgress);
            if (ArmVmProvider.IsSuccessful(resourceGroupDefinition))
            {
                executionResult = new ExecutionResult(ExecutionStatus.Succeeded);
            }
            else if (ArmVmProvider.IsFailedState(resourceGroupDefinition))
            {
                executionResult = new ExecutionResult(ExecutionStatus.Failed);
            }

            return executionResult;
        }

        private static bool IsSuccessful(VmResourceGroupDefinition resourceGroup)
        {
            return resourceGroup.IsSuccessful() && resourceGroup.VirtualMachines.All(vm => vm.IsSuccessful());
        }

        private static bool IsFailedState(VmResourceGroupDefinition resourceGroup)
        {
            return resourceGroup.State == ProvisioningState.Failed || resourceGroup.VirtualMachines.Any(vm => vm.State == ProvisioningState.Failed);
        }

        private class Parameters
        {
            internal const string DataDiskCount = nameof(Parameters.DataDiskCount);
            internal const string DataDiskSizeInGB = nameof(Parameters.DataDiskSizeInGB);
            internal const string DataDiskSku = nameof(Parameters.DataDiskSku);
            internal const string DataDiskStorageAccountType = nameof(Parameters.DataDiskStorageAccountType);
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
            internal const string VmCount = nameof(Parameters.VmCount);
            internal const string VmSize = nameof(Parameters.VmSize);
            internal const string EnableAcceleratedNetworking = nameof(Parameters.EnableAcceleratedNetworking);
        }
    }
}