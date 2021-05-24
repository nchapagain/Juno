namespace Juno.Execution.ArmIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.ArmIntegration.Parameters;
    using Juno.Execution.ArmIntegration.Properties;
    using Juno.Extensions.Telemetry;
    using Juno.Hosting.Common;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Rest;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Common = Microsoft.Azure.CRC;

    /// <summary>
    /// Manage deploying and monitoring of ARM template deployments
    /// </summary>
    public class ArmTemplateManager : IArmResourceManager
    {
        private const int SshKeySize = 2048;
        private const string ArmResourceId = "https://management.core.windows.net/";

        private const string KeyVaultAuthenticationResourceId = "https://vault.azure.net";

        /// <summary>
        /// Key vault placeholder to store password
        /// </summary>
        private const string KeyVaultBaseUrl = "https://{0}.vault.azure.net/";

        /// <summary>
        /// Key vault resources id/relative url to retrive password during VM deployment
        /// </summary>
        private const string KeyVaultResourceId = "/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.KeyVault/vaults/{2}";

        private EnvironmentSettings settings;
        private AadPrincipalSettings executionSvcPrincipal;
        private AadPrincipalSettings guestAgentPrincipal;
        private AgentSettings agentSettings;
        private ExecutionSettings executionSettings;
        private ILogger logger;
        private IArmClient armClient;
        private IAuthenticationProvider<AuthenticationResult> authProvider;
        private List<IDisposable> disposables;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArmTemplateManager"/> class.
        /// </summary>
        /// <param name="configuration">ARM template configurations</param>
        /// <param name="logger">Logger for capturing telemetry.</param>
        public ArmTemplateManager(IConfiguration configuration, ILogger logger = null)
        {
            configuration.ThrowIfNull(nameof(configuration));
            this.Initialize(configuration, logger);
        }

        /// <summary>
        /// Delete all VMs defined in the resource group.
        /// </summary>
        /// <param name="resourceGroup">Resource group definition <see cref="VmResourceGroupDefinition"/></param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Deployment state</returns>
        public async Task DeleteVirtualMachinesAsync(VmResourceGroupDefinition resourceGroup, CancellationToken cancellationToken)
        {
            resourceGroup.ThrowIfNull(nameof(resourceGroup));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(resourceGroup)
                .AddContext("experimentId", resourceGroup.ExperimentId);

            await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.DeleteVirtualMachines", telemetryContext, async () =>
            {
                foreach (VmDefinition vm in resourceGroup.VirtualMachines.Where(vm => (vm.State != ProvisioningState.Deleted)))
                {
                    HttpResponseMessage getVMResponse = await this.armClient.GetVirtualMachineAsync(
                            resourceGroup.SubscriptionId, resourceGroup.Name, vm.Name, cancellationToken).ConfigureAwait(false);
                    if (getVMResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        vm.State = ProvisioningState.Deleted;
                    }
                    else if (vm.State != ProvisioningState.Deleting)
                    {
                        HttpResponseMessage deleteVMResponse = await this.armClient.DeleteVirtualMachineAsync(
                            resourceGroup.SubscriptionId, resourceGroup.Name, vm.Name, cancellationToken).ConfigureAwait(false);
                        if (deleteVMResponse.IsSuccessStatusCode)
                        {
                            vm.State = ProvisioningState.Deleting;
                        }
                        else if (deleteVMResponse.StatusCode == HttpStatusCode.NotFound)
                        {
                            vm.State = ProvisioningState.Deleted;
                        }
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete resource group 
        /// </summary>
        /// <param name="resourceGroup">Resource group definition <see cref="VmResourceGroupDefinition"/></param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <param name="forceDelete">Should we force a delete even if previous delete has not completed?</param>
        /// <returns>Deployment state</returns>
        public async Task DeleteResourceGroupAsync(VmResourceGroupDefinition resourceGroup, CancellationToken cancellationToken, bool forceDelete = false)
        {
            resourceGroup.ThrowIfNull(nameof(resourceGroup));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(resourceGroup)
                .AddContext("experimentId", resourceGroup.ExperimentId);

            await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.DeleteResourceGroup", telemetryContext, async () =>
            {
                if (resourceGroup.DeletionState == CleanupState.NotStarted)
                {
                    resourceGroup.DeletionState = await this.DeleteResourceGroupAsync(resourceGroup, telemetryContext, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var headResponse = await this.armClient.HeadResourceGroupAsync(resourceGroup.SubscriptionId, resourceGroup.Name, cancellationToken).ConfigureAwait(false);
                    if (headResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        resourceGroup.DeletionState = CleanupState.Succeeded;
                    }
                    else if (!headResponse.IsSuccessStatusCode)
                    {
                        var headResponseError = await ReadFromJsonAsync<CloudError>(headResponse.Content).ConfigureAwait(false);
                        telemetryContext.AddContext(headResponseError, nameof(headResponse));
                        resourceGroup.DeletionState = CleanupState.Failed;
                        ArmTemplateManager.ThrowArmException(headResponseError);
                    }
                    else if (headResponse.StatusCode == HttpStatusCode.NoContent)
                    {
                        if (forceDelete)
                        {
                            // Resource group exist. only retry delete if force delete is specified
                            resourceGroup.DeletionState = await this.DeleteResourceGroupAsync(resourceGroup, telemetryContext, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            resourceGroup.DeletionState = CleanupState.Deleting;
                        }
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Deploy resource group and shared resources such as subnet, virtual network, key vault and network security group.
        /// It also deploy virtual machines with all necessary resources once shared resources are deployed successfully.
        /// </summary>
        /// <param name="resourceGroup">Resource group definition</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Updated <see cref="VmResourceGroupDefinition"/> with new state and deployment id for each shared resources and virtual machines</returns>
        public async Task<VmResourceGroupDefinition> DeployResourceGroupAndVirtualMachinesAsync(VmResourceGroupDefinition resourceGroup, CancellationToken cancellationToken)
        {
            resourceGroup.ThrowIfNull(nameof(resourceGroup));
            resourceGroup.ThrowIfInvalid(
                nameof(resourceGroup),
                defn => !string.IsNullOrWhiteSpace(resourceGroup.Environment),
                $"The environment must be defined.");

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(resourceGroup)
                .AddContext("experimentId", resourceGroup.ExperimentId);

            return await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.DeployResourceGroupAndVirtualMachines", telemetryContext, async () =>
            {
                if (resourceGroup.IsSuccessful())
                {
                    await this.DeployVirtualMachineResourcesAsync((VmResourceGroupDefinition)resourceGroup, (CancellationToken)cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    if (resourceGroup.State == ProvisioningState.Pending)
                    {
                        resourceGroup = await this.DeploySharedResourceAsync(resourceGroup, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        var resourceState = await this.GetSharedResourceDeploymentStateAsync(resourceGroup.DeploymentId, cancellationToken).ConfigureAwait(false);
                        resourceGroup.State = resourceState.State;
                    }
                }

                return resourceGroup;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Refresh the state of the virtual machines in the resource group.
        /// </summary>
        /// <param name="resourceGroup">Resource group definition</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <param name="refreshAll">Whether to force refresh all the virtual machines in the resource group. Will skip terminal state VM if set to false.</param>
        /// <returns>Updated <see cref="VmResourceGroupDefinition"/> with new state and deployment id for each shared resources and virtual machines</returns>
        public async Task<VmResourceGroupDefinition> RefreshVirtualMachinesStateAsync(VmResourceGroupDefinition resourceGroup, CancellationToken cancellationToken, bool refreshAll = true)
        {
            resourceGroup.ThrowIfNull(nameof(resourceGroup));
            resourceGroup.ThrowIfInvalid(
                nameof(resourceGroup),
                defn => !string.IsNullOrWhiteSpace(resourceGroup.Environment),
                $"The environment must be defined.");

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(resourceGroup)
                .AddContext("experimentId", resourceGroup.ExperimentId);

            return await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.RefreshVirtualMachinesState", telemetryContext, async () =>
            {
                if (refreshAll)
                {
                    foreach (VmDefinition vm in resourceGroup.VirtualMachines)
                    {
                        HttpResponseMessage response = await this.armClient.GetVirtualMachineAsync(resourceGroup.SubscriptionId, resourceGroup.Name, vm.Name, cancellationToken).ConfigureAwait(false);
                        ResourceState resourceState = await this.ParseArmResponseAsync(telemetryContext, response).ConfigureAwait(false);
                        vm.State = resourceState.State;
                    }
                }
                else
                {
                    foreach (VmDefinition vm in resourceGroup.VirtualMachines.Where(vm => !vm.FailedOrDeleted()))
                    {
                        HttpResponseMessage response = await this.armClient.GetVirtualMachineAsync(resourceGroup.SubscriptionId, resourceGroup.Name, vm.Name, cancellationToken).ConfigureAwait(false);
                        ResourceState resourceState = await this.ParseArmResponseAsync(telemetryContext, response).ConfigureAwait(false);
                        vm.State = resourceState.State;
                    }
                }

                return resourceGroup;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Refresh the state of the resource group.
        /// </summary>
        /// <param name="resourceGroup">Resource group definition</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Updated <see cref="VmResourceGroupDefinition"/> with new state and deployment id for each shared resources and virtual machines</returns>
        public async Task<VmResourceGroupDefinition> RefreshResourceGroupStateAsync(VmResourceGroupDefinition resourceGroup, CancellationToken cancellationToken)
        {
            resourceGroup.ThrowIfNull(nameof(resourceGroup));
            resourceGroup.ThrowIfInvalid(
                nameof(resourceGroup),
                defn => !string.IsNullOrWhiteSpace(resourceGroup.Environment),
                $"The environment must be defined.");

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(resourceGroup)
                .AddContext("experimentId", resourceGroup.ExperimentId);

            return await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.RefreshResourceGroup", telemetryContext, async () =>
            {
                HttpResponseMessage response = await this.armClient.GetResourceGroupAsync(resourceGroup.SubscriptionId, resourceGroup.Name, cancellationToken).ConfigureAwait(false);
                ResourceState resourceGroupState = await this.ParseArmResponseAsync(telemetryContext, response).ConfigureAwait(false);
                resourceGroup.State = resourceGroupState.State;

                return resourceGroup;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Install guest agent on virtual machines within the resource group
        /// </summary>
        /// <param name="resourceGroup">Resource group definition</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <param name="packageVersion">Guest agent package version</param>
        /// <param name="installerUri">Optional parameter defines the URI to the Guest Agent installer.</param>
        /// <returns>Updated <see cref="VmResourceGroupDefinition"/> with new state and deployment id of bootstrap deployment for each virtual machine</returns>
        public async Task<VmResourceGroupDefinition> BootstrapVirtualMachinesAsync(VmResourceGroupDefinition resourceGroup, Uri installerUri, CancellationToken cancellationToken, string packageVersion = null)
        {
            resourceGroup.ThrowIfNull(nameof(resourceGroup));
            resourceGroup.ThrowIfInvalid(
                nameof(resourceGroup),
                defn => !string.IsNullOrWhiteSpace(resourceGroup.Environment),
                $"The environment must be defined.");

            installerUri.ThrowIfNull(nameof(installerUri));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(resourceGroup)
                .AddContext("experimentId", resourceGroup.ExperimentId)
                .AddContext("installerUri", installerUri)
                .AddContext("packageVersion", packageVersion);

            return await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.BootstrapVirtualMachines", telemetryContext, async () =>
            {
                if (resourceGroup.IsSuccessful())
                {
                    foreach (var virtualMachine in resourceGroup.VirtualMachines.Where(v => !v.BootstrapState.IsDeploymentFinished()))
                    {
                        if (virtualMachine.BootstrapState.State == ProvisioningState.Pending)
                        {
                            await this.BootstrapVirtualMachineAsync(resourceGroup, virtualMachine, installerUri, cancellationToken, packageVersion)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            var resourceState = await this.GetResourceGroupScopeDeploymentStateAsync(virtualMachine.BootstrapState.DeploymentId, cancellationToken).ConfigureAwait(false);
                            virtualMachine.BootstrapState.State = resourceState.State;
                        }
                    }
                }

                return resourceGroup;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Disposes of resources used by the instance.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Creates a Key Vault client using the AAD principal settings defined.
        /// </summary>
        protected virtual IAzureKeyVault CreateKeyVaultClient(Uri keyVaultUri)
        {
            IKeyVaultClient client = KeyVaultClientFactory.CreateClient(this.executionSvcPrincipal.PrincipalId, this.executionSvcPrincipal.PrincipalCertificateThumbprint);
            this.disposables.Add(client);

            return new AzureKeyVault(client, keyVaultUri);
        }

        /// <summary>
        /// Creates a REST client for communications with an ARM endpoint.
        /// </summary>
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Disposed where used.")]
        protected virtual IRestClient CreateRestClient()
        {
            return new RestClientBuilder()
                .WithAutoRefreshToken(
                    this.executionSvcPrincipal.AuthorityUri,
                    this.executionSvcPrincipal.PrincipalId,
                    ArmTemplateManager.ArmResourceId,
                    this.executionSvcPrincipal.PrincipalCertificateThumbprint)
                .AddAcceptedMediaType(MediaType.Json)
                .Build();
        }

        /// <summary>
        /// Disposes of resources used by the instance.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.disposables.ForEach(resource => resource.Dispose());
                }

                this.disposed = true;
            }
        }

        private static void ThrowArmException(CloudError headResponseError)
        {
            if (headResponseError?.Error != null)
            {
                throw new ArmException(headResponseError.Error.Message, headResponseError.Error);
            }
            else
            {
                // I haven't seen this scenario happening
                throw new ArmException("Unknown ARM error occurred.");
            }
        }

        private static async Task<TData> ReadFromJsonAsync<TData>(HttpContent content)
        {
            content.ThrowIfNull(nameof(content));

            try
            {
                string contentJson = await content.ReadAsStringAsync().ConfigureAwait(false);
                return contentJson.FromJson<TData>();
            }
            catch (JsonException exc)
            {
                throw new JsonReaderException(
                    $"Invalid HTTP content format.  The contents of the HTTP response cannot be JSON-deserialized into an object of type '{typeof(TData).FullName}'.",
                    exc);
            }
        }

        private async Task<CleanupState> DeleteResourceGroupAsync(VmResourceGroupDefinition resourceGroup, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            HttpResponseMessage deleteResponse = await this.armClient.DeleteResourceGroupAsync(resourceGroup.SubscriptionId, resourceGroup.Name, cancellationToken).ConfigureAwait(false);
            CleanupState resourceState;
            if (deleteResponse.IsSuccessStatusCode)
            {
                resourceState = CleanupState.Accepted;
            }
            else
            {
                CloudError deleteResponseError = await ReadFromJsonAsync<CloudError>(deleteResponse.Content).ConfigureAwait(false);
                telemetryContext.AddContext(deleteResponseError, nameof(deleteResponseError));
                resourceState = CleanupState.Failed;
                ArmTemplateManager.ThrowArmException(deleteResponseError);
            }

            return resourceState;
        }

        private async Task<ResourceState> ParseArmResponseAsync(EventContext telemetryContext, HttpResponseMessage responseMessage)
        {
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);
            return await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.ParseArmResponse", relatedContext, async () =>
            {
                ResourceState resourceState = new ResourceState();
                relatedContext.AddContext(responseMessage);
                if (responseMessage.IsSuccessStatusCode)
                {
                    var response = await ReadFromJsonAsync<ArmDeploymentResponse>(responseMessage.Content).ConfigureAwait(false);
                    relatedContext.AddContext(response);
                    resourceState.DeploymentId = response.Id;
                    resourceState.DeploymentName = response.Name;
                    resourceState.CorrelationId = response.Properties.CorrelationId;

                    if (response?.Properties?.ProvisioningState != null
                        && Enum.TryParse(response?.Properties?.ProvisioningState, true, out ProvisioningState provisionState))
                    {
                        resourceState.State = provisionState;
                    }
                    else
                    {
                        // If ARM returns unknown state, we will try to get the deployment status until the step timeout.
                        // Resubmitting the deployment doesn’t make sense because we are getting success status code.
                        // Therefore changing the state from pending to unknown. 
                        resourceState.State = ProvisioningState.Unknown;
                    }

                    if (response?.Properties?.Error != null)
                    {
                        resourceState.Error = response?.Properties?.Error;
                    }
                }
                else
                {
                    var response = await ReadFromJsonAsync<CloudError>(responseMessage.Content).ConfigureAwait(false);
                    relatedContext.AddContext(response);
                    resourceState.State = ProvisioningState.Failed;
                    ArmTemplateManager.ThrowArmException(response);
                }

                relatedContext.AddContext("resourceState", resourceState);
                return resourceState;
            }).ConfigureAwait(false);
        }

        private async Task<VmResourceGroupDefinition> DeploySharedResourceAsync(VmResourceGroupDefinition resourceGroup, CancellationToken cancellationToken)
        {
            resourceGroup.ThrowIfNull(nameof(resourceGroup));
            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(resourceGroup)
                .AddContext("experimentId", resourceGroup.ExperimentId);

            return await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.DeploySharedResource", telemetryContext, async () =>
            {
                var template = Encoding.Default.GetString(Resources.ResourceGroupTemplate);
                var resourceGroupTemplateParameters = new VmResourceGroupTemplateParameters(
                     new ParameterValue<string>(resourceGroup.Region),
                     new ParameterValue<string>(resourceGroup.Name),
                     new ParameterValue<string>(resourceGroup.KeyVaultName),
                     new ParameterValue<string>(resourceGroup.SubnetName),
                     new ParameterValue<string>(resourceGroup.NetworkSecurityGroupName),
                     new ParameterValue<string>(resourceGroup.VirtualNetworkName),
                     new ParameterValue<string>(this.executionSvcPrincipal.EnterpriseObjectId),
                     new ParameterValue<string>(this.guestAgentPrincipal.EnterpriseObjectId));

                telemetryContext.AddContext(nameof(resourceGroupTemplateParameters), resourceGroupTemplateParameters);
                var createResourceGroupResponse = await this.armClient.CreateResourceGroupAsync(
                    resourceGroup.SubscriptionId,
                    resourceGroup.Name,
                    resourceGroup.Region,
                    cancellationToken,
                    resourceGroup.Tags).ConfigureAwait(false);

                if (createResourceGroupResponse.IsSuccessStatusCode)
                {
                    var parsedResponse = await ReadFromJsonAsync<CreateResourceGroupResponse>(createResourceGroupResponse.Content).ConfigureAwait(false);

                    telemetryContext.AddContext("createResourceGroupResponse", parsedResponse);

                    var response = await this.armClient.DeployAtResourceGroupScopeAsync(
                        resourceGroup.SubscriptionId,
                        resourceGroup.Name,
                        resourceGroup.DeploymentName,
                        template,
                        resourceGroupTemplateParameters,
                        cancellationToken).ConfigureAwait(false);

                    var resourceState = await this.ParseArmResponseAsync(telemetryContext, response).ConfigureAwait(false);
                    resourceGroup.State = resourceState.State;
                    resourceGroup.DeploymentId = resourceState.DeploymentId;
                }
                else
                {
                    resourceGroup.State = ProvisioningState.Failed;
                    telemetryContext.AddContext(createResourceGroupResponse);
                    await this.ParseArmResponseAsync(telemetryContext, createResourceGroupResponse).ConfigureAwait(false);
                }

                return resourceGroup;
            }).ConfigureAwait(false);
        }

        private async Task<VmResourceGroupDefinition> DeployVirtualMachineResourcesAsync(VmResourceGroupDefinition resourceGroup, CancellationToken cancellationToken)
        {
            resourceGroup.ThrowIfNull(nameof(resourceGroup));
            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(resourceGroup)
               .AddContext("experimentId", resourceGroup.ExperimentId);

            return await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.DeployVirtualMachines", telemetryContext, async () =>
            {
                foreach (var virtualMachine in resourceGroup.VirtualMachines.Where(v => !v.IsDeploymentFinished()))
                {
                    if (virtualMachine.State == ProvisioningState.Pending)
                    {
                        await this.DeployVirtualMachineResourceAsync(resourceGroup, virtualMachine, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        var resourceState = await this.GetResourceGroupScopeDeploymentStateAsync(virtualMachine.DeploymentId, cancellationToken).ConfigureAwait(false);
                        virtualMachine.State = resourceState.State;

                        if (virtualMachine.DeploymentRequestStartTime == null)
                        {
                            virtualMachine.DeploymentRequestStartTime = DateTime.UtcNow;
                        }

                        if (virtualMachine.State == ProvisioningState.Failed)
                        {
                            if (virtualMachine.IsDeploymentRequestTimedOut)
                            {
                                throw new ProviderException(
                                    $"Failed to create the VM within deployment request timeout period (timeout={virtualMachine.DeploymentRequestTimeout}). " + 
                                    $"(Name={resourceState.Name}, State={resourceState.State.ToString()}, Correlation ID={resourceState.CorrelationId}, " +
                                    $"Deployment ID={resourceState.DeploymentId}, Deployment Name={resourceState.DeploymentName}, Error={resourceState.Error}",
                                    ErrorReason.ArmDeploymentFailure);
                            }

                            if (resourceState.Error != null && !resourceState.Error.ToString().Contains("osprovisioningtimedout", StringComparison.OrdinalIgnoreCase))
                            {
                                throw new ProviderException(
                                    $"Failed to create the VM. (Name={resourceState.Name}, State={resourceState.State.ToString()}, Correlation ID={resourceState.CorrelationId}, " +
                                    $"Deployment ID={resourceState.DeploymentId}, Deployment Name={resourceState.DeploymentName}, Error={resourceState.Error}",
                                    ErrorReason.ArmDeploymentFailure);
                            }
                        }
                    }
                }

                return resourceGroup;
            }).ConfigureAwait(false);
        }

        private async Task<VmDefinition> DeployVirtualMachineResourceAsync(
            VmResourceGroupDefinition resourceGroup,
            VmDefinition vmDefinition,
            CancellationToken cancellationToken)
        {
            resourceGroup.ThrowIfNull(nameof(resourceGroup));
            vmDefinition.ThrowIfNull(nameof(vmDefinition));

            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(resourceGroup)
               .AddContext("experimentId", resourceGroup.ExperimentId)
               .AddContext(vmDefinition);

            var template = Encoding.Default.GetString(Resources.VirtualMachineTemplate);
            if (VmPlatform.IsLinux(resourceGroup.Platform))
            {
                template = Encoding.Default.GetString(Resources.LinuxVmTemplate);
            }

            return await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.DeployVirtualMachine", telemetryContext, async () =>
            {
                telemetryContext.AddContext(vmDefinition);

                await this.CreateSecretAsync(resourceGroup, vmDefinition, cancellationToken).ConfigureAwait(false);

                var keyVaultId = new KeyVaultIdentification(string.Format(ArmTemplateManager.KeyVaultResourceId, resourceGroup.SubscriptionId, resourceGroup.Name, resourceGroup.KeyVaultName));

                KeyVaultSecretReference adminCredentialKvReference;

                if (VmPlatform.IsLinux(resourceGroup.Platform))
                {
                    var kvReference = new KeyVaultReference(keyVaultId, vmDefinition.AdminSshPublicKeySecretName);
                    adminCredentialKvReference = new KeyVaultSecretReference(kvReference);
                }
                else
                {
                    var kvReference = new KeyVaultReference(keyVaultId, vmDefinition.AdminPasswordSecretName);
                    adminCredentialKvReference = new KeyVaultSecretReference(kvReference);
                }

                var vmTemplateParameters = new VmTemplateParameters(
                    new ParameterValue<string>(resourceGroup.Region),
                    new ParameterValue<string>(vmDefinition.OsDiskStorageAccountType),
                    new ParameterValue<string>(vmDefinition.VirtualMachineSize),
                    new ParameterValue<JObject>(vmDefinition.ImageReference),
                    new ParameterValue<string>(vmDefinition.Name),
                    new ParameterValue<string>(resourceGroup.SubnetName),
                    new ParameterValue<string>(resourceGroup.NetworkSecurityGroupName),
                    new ParameterValue<string>(resourceGroup.VirtualNetworkName),
                    new ParameterValue<string>(vmDefinition.AdminUserName),
                    adminCredentialKvReference,
                    new ParameterValue<string>(resourceGroup.TipSessionId),
                    new ParameterValue<string>(resourceGroup.ClusterId),
                    new ParameterValue<IList<VmDisk>>(vmDefinition.VirtualDisks),
                    new ParameterValue<bool>(vmDefinition.EnableAcceleratedNetworking));

                telemetryContext.AddContext(nameof(vmTemplateParameters), vmTemplateParameters);

                var response = await this.armClient.DeployAtResourceGroupScopeAsync(
                    resourceGroup.SubscriptionId,
                    resourceGroup.Name,
                    vmDefinition.DeploymentName,
                    template,
                    vmTemplateParameters,
                    cancellationToken).ConfigureAwait(false);

                var resourceState = await this.ParseArmResponseAsync(telemetryContext, response).ConfigureAwait(false);
                vmDefinition.State = resourceState.State;
                vmDefinition.DeploymentId = resourceState.DeploymentId;
                vmDefinition.DeploymentRequestStartTime = DateTime.UtcNow;
                vmDefinition.CorrelationId = resourceState.CorrelationId;
                return vmDefinition;
            }).ConfigureAwait(false);
        }

        private async Task CreateSecretAsync(VmResourceGroupDefinition resourceGroup, VmDefinition vmDefinition, CancellationToken cancellationToken)
        {
            string vaultUri = string.Format(ArmTemplateManager.KeyVaultBaseUrl, resourceGroup.KeyVaultName);
            IAzureKeyVault keyVault = this.CreateKeyVaultClient(new Uri(vaultUri));
            if (VmPlatform.IsLinux(resourceGroup.Platform))
            {
                RsaCrypto.GenerateKeyPair(ArmTemplateManager.SshKeySize, out string publicKey, out string privateKey);
                await keyVault.SetSecretAsync(vmDefinition.AdminSshPublicKeySecretName, publicKey.ToSecureString(), cancellationToken).ConfigureDefaults();
                await keyVault.SetSecretAsync(vmDefinition.AdminSshPrivateKeySecretName, privateKey.ToSecureString(), cancellationToken).ConfigureDefaults();
            }
            else
            {
                await keyVault.SetSecretAsync(vmDefinition.AdminPasswordSecretName, VmDefinition.GenerateRandomPassword().ToSecureString(), cancellationToken).ConfigureDefaults();
            }
        }

        [SuppressMessage("Readability", "AZCA1006:PrefixStaticCallsWithClassName Rule", Justification = "Code analysis is incorrect here.")]
        private async Task<VmResourceGroupDefinition> BootstrapVirtualMachineAsync(VmResourceGroupDefinition resourceGroup, VmDefinition vmDefinition, Uri installerUri, CancellationToken cancellationToken, string packageVersion = null)
        {
            EventContext telemetryContext = EventContext.Persisted()
               .AddContext(resourceGroup)
               .AddContext("experimentId", resourceGroup.ExperimentId)
               .AddContext(vmDefinition)
               .AddContext("installerUri", installerUri)
               .AddContext("packageVersion", packageVersion);

            string template;

            if (VmPlatform.IsLinux(resourceGroup.Platform))
            {
                template = Encoding.Default.GetString(Resources.LinuxVmBootstrapTemplate);
            }
            else
            {
                template = Encoding.Default.GetString(Resources.VmBootstrapTemplate);
            }
            
            return await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.BootstrapVirtualMachine", telemetryContext, async () =>
            {
                telemetryContext.AddContext(vmDefinition);
                var jwt = await this.GetBootstrapJWTTokenAsync().ConfigureAwait(false);
                /*
                 * Juno.GuestAgent.Installer.exe 
                 * --environment <environment> (e.g. juno-dev01)
                 * --experimentId <id> (e.g. Guid)
                 * --agentId <agentId> (e.g. cluster01,node01,vm01,tipSession01)
                 * --packageVersion <version> (e.g. latest or 1.0.97)
                 * --appInsightsInstrumentationKey <appInsightKey> (e.g. Guid)
                 * --keyVaultUri <keyvaultBaseUrl> (e.g. https://junodev01vault.vault.azure.net/ )
                 * --certificateName <certName>  (e.g. juno-dev01-guestagent)
                 * --nugetFeedUri <NugetFeed> (e.g. https://msazure.pkgs.visualstudio.com/_packaging/d8f12b5e-ca80-4dec-867b-7289c3fc6146/nuget/v2)
                 * --nugetPat <NuGetTokenSecretName> (e.g. NugetAccessToken )
                 * --eventHubConnectionString <connectionString>
                 * --eventHub <name> (e.g. telemetry-agents)
                 * --vmSku <sku> (e.g. Standard_D2s_v3)
                 * --region <region (e.g. East uS 2)
                 */

                NuGetFeedSettings nuGetFeed = this.executionSettings.NuGetFeeds.Get(Setting.Default);
                string version = string.IsNullOrWhiteSpace(packageVersion) ? "latest" : packageVersion;
                string bootstrapCommand;
                
                if (VmPlatform.IsLinux(resourceGroup.Platform))
                {
                    // command to run the installer on Linux Vms
                    bootstrapCommand = $"sudo ./Juno.GuestAgent.Installer";
                }                
                else
                {
                    // command to run the installer on Windows VMs
                    bootstrapCommand = $"Juno.GuestAgent.Installer.exe";
                }

                bootstrapCommand = $"{bootstrapCommand}" +
                    $" --environment \"{this.settings.Environment}\"" +
                    $" --experimentId \"{resourceGroup.ExperimentId}\"" +
                    $" --agentId \"{AgentIdentification.CreateVirtualMachineId(resourceGroup.ClusterId, resourceGroup.NodeId, vmDefinition.Name, resourceGroup.TipSessionId)}\"" +
                    $" --packageVersion \"{version}\"" +
                    $" --appInsightsInstrumentationKey \"{this.settings.AppInsightsSettings.Get(Setting.Telemetry).InstrumentationKey}\"" +
                    $" --keyVaultUri \"{this.settings.KeyVaultSettings.Get(Setting.Default).Uri.AbsoluteUri}\"" +
                    $" --certificateName \"{this.agentSettings.GuestAgentCertificateName}\"" +
                    $" --nugetFeedUri \"{nuGetFeed.Uri.AbsoluteUri}\"" +
                    $" --nugetPat \"{nuGetFeed.AccessToken}\"";

                // execution or guest agent principal?
                IAzureKeyVault keyVaultClient = HostDependencies.CreateKeyVaultClient(this.executionSvcPrincipal, this.settings.KeyVaultSettings.Get(Setting.Default));
                string eventHubConnectionString = this.settings.EventHubSettings.Get(Setting.AgentTelemetry).ConnectionString;
                string eventHub = this.settings.EventHubSettings.Get(Setting.AgentTelemetry).EventHub;
                string connectionString = keyVaultClient.ResolveSecretAsync(eventHubConnectionString, CancellationToken.None)
                    .GetAwaiter().GetResult().ToOriginalString();

                if (!string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(eventHub))
                {
                    bootstrapCommand = $"{bootstrapCommand} --eventHubConnectionString \"{connectionString}\" --eventHub \"{eventHub}\"";
                }

                if (!string.IsNullOrWhiteSpace(resourceGroup.VirtualMachines?.First()?.VirtualMachineSize))
                {
                    bootstrapCommand = $"{bootstrapCommand} --vmSku \"{resourceGroup.VirtualMachines.First().VirtualMachineSize}\"";
                }

                if (!string.IsNullOrWhiteSpace(resourceGroup.Region))
                {
                    bootstrapCommand = $"{bootstrapCommand} --region \"{resourceGroup.Region}\"";
                }

                telemetryContext.AddContext("installationCommand", Common.SensitiveData.ObscureSecrets(bootstrapCommand));

                // Add the JWT to enable the installer to access the Key Vault in order to install the
                // certificate required for the Guest Agent.
                bootstrapCommand = $"{bootstrapCommand} --accessToken {jwt}";

                var bootstrapParameters = new VmBootstrapTemplateParameters(
                     new ParameterValue<string>(resourceGroup.Region),
                     new ParameterValue<string>(installerUri.AbsoluteUri),
                     new ParameterValue<string>(bootstrapCommand),
                     new ParameterValue<string>(vmDefinition.Name));

                var response = await this.armClient.DeployAtResourceGroupScopeAsync(
                    resourceGroup.SubscriptionId,
                    resourceGroup.Name,
                    vmDefinition.BootstrapState.DeploymentName,
                    template,
                    bootstrapParameters,
                    cancellationToken).ConfigureAwait(false);

                var resourceState = await this.ParseArmResponseAsync(telemetryContext, response).ConfigureAwait(false);
                vmDefinition.BootstrapState.State = resourceState.State;
                vmDefinition.BootstrapState.DeploymentId = resourceState.DeploymentId;

                return resourceGroup;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the deployment state for shared resource group deployment
        /// </summary>
        /// <param name="deploymentId">Deployment id</param>
        /// <param name="cancellationToken">A token that can be used to request cancellation of this operations.</param>
        /// <returns>Deployment state</returns>
        private async Task<ResourceState> GetSharedResourceDeploymentStateAsync(string deploymentId, CancellationToken cancellationToken)
        {
            deploymentId.ThrowIfNull(deploymentId);
            EventContext telemetryContext = EventContext.Persisted()
              .AddContext(nameof(deploymentId), deploymentId);

            return await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.GetSharedResourceDeploymentState", telemetryContext, async () =>
            {
                var response = await this.armClient.GetSubscriptionScopeDeploymentStateAsync(deploymentId, cancellationToken).ConfigureAwait(false);
                telemetryContext.AddContext(response);
                return await this.ParseArmResponseAsync(telemetryContext, response).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the deployment state for group level scope deployments
        /// </summary>
        /// <param name="deploymentId">Deployment id</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Deployment state</returns>
        private async Task<ResourceState> GetResourceGroupScopeDeploymentStateAsync(string deploymentId, CancellationToken cancellationToken)
        {
            deploymentId.ThrowIfNull(deploymentId);
            EventContext telemetryContext = EventContext.Persisted()
           .AddContext(nameof(deploymentId), deploymentId);

            EventContext relatedContext = telemetryContext.Clone(withProperties: true);
            relatedContext.AddContext(nameof(deploymentId), deploymentId);
            return await this.logger.LogTelemetryAsync($"{nameof(ArmTemplateManager)}.GetResourceGroupScopeDeploymentState", telemetryContext, async () =>
            {
                var response = await this.armClient.GetResourceGroupScopeDeploymentStateAsync(deploymentId, cancellationToken).ConfigureAwait(false);
                return await this.ParseArmResponseAsync(telemetryContext, response).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Get jwt token
        /// </summary>
        /// <returns></returns>
        private async Task<string> GetBootstrapJWTTokenAsync()
        {
            this.authProvider = this.authProvider ?? new AadAuthenticationProvider(
                this.guestAgentPrincipal.AuthorityUri,
                this.guestAgentPrincipal.PrincipalId,
                ArmTemplateManager.KeyVaultAuthenticationResourceId,
                this.guestAgentPrincipal.PrincipalCertificateThumbprint);
            AuthenticationResult authResult = await this.authProvider.AuthenticateAsync().ConfigureAwait(false);

            return authResult.AccessToken;
        }

        private void Initialize(IConfiguration configuration, ILogger logger)
        {
            this.logger = logger ?? NullLogger.Instance;
            this.settings = EnvironmentSettings.Initialize(configuration);
            this.executionSvcPrincipal = this.settings.ExecutionSettings.AadPrincipals.Get(Setting.ExecutionSvc);
            this.guestAgentPrincipal = this.settings.AgentSettings.AadPrincipals.Get(Setting.GuestAgent);
            this.agentSettings = this.settings.AgentSettings;
            this.executionSettings = this.settings.ExecutionSettings;

            IRestClient restClient = this.CreateRestClient();
            this.armClient = new ArmClient(restClient);
            this.disposables = new List<IDisposable> { restClient };
        }
    }
}