namespace Juno.Execution.ArmIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.Management.TrafficManager.Fluent.TrafficManagerEndpoint.Definition;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// ARM Resource group definition.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Need to modify VMs.")]
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class VmResourceGroupDefinition : ResourceState
    {
        private string resourceNamePrefix;

        /// <summary>
        /// Initializes a new instance of the <see cref="VmResourceGroupDefinition"/> class.
        /// </summary>
        /// <param name="environment">The environment in which the VMs will run (e.g. juno-dev01, juno-prod01).</param>
        /// <param name="subscriptionId">Subscription id</param>
        /// <param name="experimentId">experiment Id</param>
        /// <param name="stepId">Step Id</param>
        /// <param name="vmSpecs">Virtual machine specifications</param>
        /// <param name="region">Region aka. location</param>
        /// <param name="clusterId">Cluster Id</param>
        /// <param name="tipSessionId">Tip session id</param>
        /// <param name="nodeId">Node id</param>
        /// <param name="tags">Tag for VM resource group</param>
        /// <param name="platform">OS platform the VM uses</param>
        public VmResourceGroupDefinition(
            string environment,
            string subscriptionId,
            string experimentId,
            string stepId,
            IList<AzureVmSpecification> vmSpecs,
            string region,
            string clusterId = null,
            string tipSessionId = null,
            string nodeId = null,
            IDictionary<string, string> tags = null,
            string platform = VmPlatform.WinX64)
        {
            this.Initialize(environment, subscriptionId, experimentId, stepId, vmSpecs, region, clusterId, tipSessionId, nodeId, tags, platform);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VmResourceGroupDefinition"/> class.
        /// </summary>
        [JsonConstructor]
        public VmResourceGroupDefinition()
        {
        }

        /// <summary>
        /// Gets the environment in which the VMs will run (e.g. juno-dev01, juno-prod01).
        /// </summary>
        [JsonProperty]
        public string Environment { get; set; }

        /// <summary>
        /// Get or set subscriptionId
        /// </summary>
        [JsonProperty]
        public string SubscriptionId { get; set; }

        /// <summary>
        /// Get or set step Id.
        /// </summary>
        [JsonProperty]
        public string StepId { get; set; }

        /// <summary>
        /// Get or set experiment Id.
        /// </summary>
        [JsonProperty]
        public string ExperimentId { get; set; }

        /// <summary>
        /// Get or set tip session Id.
        /// </summary>
        [JsonProperty]
        public string TipSessionId { get; set; }

        /// <summary>
        /// Get or set cluster Id.
        /// </summary>
        [JsonProperty]
        public string ClusterId { get; set; }

        /// <summary>
        /// Get or set node Id.
        /// </summary>
        [JsonProperty]
        public string NodeId { get; set; }

        /// <summary>
        /// Get or set region
        /// </summary>
        [JsonProperty]
        public string Region { get; set; }

        /// <summary>
        /// Get or set azure key vault name
        /// </summary>
        [JsonProperty]
        public string KeyVaultName { get; set; }

        /// <summary>
        /// Get or set virtual network name.
        /// </summary>
        [JsonProperty]
        public string VirtualNetworkName { get; set; }

        /// <summary>
        ///  Get or set netwok security group name.
        /// </summary>
        [JsonProperty]
        public string NetworkSecurityGroupName { get; set; }

        /// <summary>
        /// Get or set the VM platform
        /// </summary>
        [JsonProperty]
        public string Platform { get; set; }

        /// <summary>
        /// Get or set subnet name
        /// </summary>
        [JsonProperty]
        public string SubnetName { get; set; }

        /// <summary>
        /// Get or set virtual Machines in the resource group.
        /// </summary>
        [JsonProperty]
        public IList<VmDefinition> VirtualMachines { get; set; }

        /// <summary>
        /// Get or set tags for the resource group.
        /// </summary>
        [JsonProperty]
        public IDictionary<string, string> Tags { get; set; }

        /// <summary>
        /// Get or set the deletion state of resource group
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(StringEnumConverter))]
        public CleanupState DeletionState { get; set; }

        /// <summary>
        /// Add a Virtual machine definition to a resource group definition.
        /// </summary>
        /// <param name="vmSpec"></param>
        internal void AddVirtualMachine(AzureVmSpecification vmSpec)
        {
            vmSpec.ThrowIfNull(nameof(vmSpec));

            if (this.VirtualMachines == null)
            {
                throw new InvalidOperationException("The VmResourceGroupDefinition class needs to be initiated before appending VM.");
            }

            // The VM starts at XXXXXX-0 so we don't need to add 1 to this.
            int vmCount = this.VirtualMachines.Count;
            JObject imageReference;
            if (!string.IsNullOrEmpty(vmSpec.SigImageReference))
            {
                imageReference = JObject.FromObject(new SigImageReference(vmSpec.SigImageReference));
            }
            else
            {
                imageReference = JObject.FromObject(new ImageReference(vmSpec.OsPublisher, vmSpec.OsOffer, vmSpec.OsSku, vmSpec.OsVersion));
            }

            var vmDefinition = new VmDefinition
            {
                Name = $"{this.resourceNamePrefix}-{vmCount}",
                AdminUserName = VmAdminAccounts.Default,
                AdminPasswordSecretName = $"{this.resourceNamePrefix}-{vmCount}-pw",
                AdminSshPublicKeySecretName = $"{this.resourceNamePrefix}-{vmCount}-pub",
                AdminSshPrivateKeySecretName = $"{this.resourceNamePrefix}-{vmCount}-pem",
                ImageReference = imageReference,
                OsDiskStorageAccountType = vmSpec.OsDiskStorageAccountType,
                VirtualMachineSize = vmSpec.VmSize,
                DeploymentName = $"deployment-{this.resourceNamePrefix}-{vmCount}",
                VirtualDisks = VmDisk.CreateVirtualMachineDisk(vmSpec.DataDiskCount, vmSpec.DataDiskSku, vmSpec.DataDiskSizeInGB, vmSpec.DataDiskStorageAccountType),
                BootstrapState = new ResourceState()
                {
                    DeploymentName = $"bootstrap-{this.resourceNamePrefix}-{vmCount}"
                },
                EnableAcceleratedNetworking = vmSpec.EnableAcceleratedNetworking,
            };
            this.VirtualMachines.Add(vmDefinition);
        }

        private void Initialize(
            string environment,
            string subscriptionId,
            string experimentId,
            string stepId,
            IList<AzureVmSpecification> vmSpecs,
            string region,
            string clusterId,
            string tipSessionId,
            string nodeId,
            IDictionary<string, string> tags,
            string platform)
        {
            environment.ThrowIfNullOrWhiteSpace(nameof(environment));
            subscriptionId.ThrowIfNullOrWhiteSpace(nameof(subscriptionId));
            experimentId.ThrowIfNullOrWhiteSpace(nameof(experimentId));
            stepId.ThrowIfNullOrWhiteSpace(nameof(stepId));
            vmSpecs.ThrowIfNull(nameof(vmSpecs));

            this.Environment = environment;
            this.SubscriptionId = subscriptionId;
            this.StepId = stepId;
            this.ExperimentId = experimentId;
            this.ClusterId = clusterId;
            this.TipSessionId = tipSessionId;
            this.Region = region;
            this.NodeId = nodeId;
            this.Tags = tags;
            this.Platform = platform;
            this.DeletionState = CleanupState.NotStarted;

            // ARM naming restrictions.
            // Resource group: ^([A-Za-z0-9_().]{0,89}[A-Za-z0-9_()]{1})$
            // Avset: ^([A-Za-z0-9]{1}[A-Za-z0-9._-]{0,78}[A-Za-z0-9_]{1})$
            // NetworkSecurityGroup: ^([A-Za-z0-9]{1}[A-Za-z0-9._-]{0,78}[A-Za-z0-9_]{1})$
            // Network: ^([A-Za-z0-9]{1}[A-Za-z0-9._-]{0,78}[A-Za-z0-9_]{1})$
            // Network Interface: ^([A-Za-z0-9]{1}[A-Za-z0-9._-]{0,78}[A-Za-z0-9_]{1})$
            // Public Ip Address: ^([A-Za-z0-9]{1}[A-Za-z0-9._-]{0,78}[A-Za-z0-9_]{1})$
            // Virtual Machine: ^(?![0-9]{1,15}$)[a-zA-Z0-9-]{1,15}$

            // Naming best practices
            // https://docs.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/naming-and-tagging#naming-and-tagging-resources
            this.resourceNamePrefix = this.StepId.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase).Substring(0, 11);
            this.Name = $"rg-{this.resourceNamePrefix}";
            this.NetworkSecurityGroupName = $"nsg-{this.resourceNamePrefix}";
            this.VirtualNetworkName = $"vnet-{this.resourceNamePrefix}";
            this.SubnetName = $"subnet-{this.resourceNamePrefix}";
            this.KeyVaultName = $"kv-{this.resourceNamePrefix}";
            this.DeploymentName = $"deployment-{this.resourceNamePrefix}";
            this.VirtualMachines = new List<VmDefinition>();

            for (int vmCount = 0; vmCount < vmSpecs.Count; vmCount++)
            {
                this.AddVirtualMachine(vmSpecs[vmCount]);
            }
        }
    }
}