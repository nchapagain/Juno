namespace Juno.Execution.ArmIntegration
{
    using System.Collections.Generic;
    using System.Linq;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.TipIntegration;
    using Juno.Extensions.Telemetry;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;

    /// <summary>
    /// Extension methods for <see cref="EventContext"/> instances
    /// and related components.
    /// </summary>
    public static class ArmContractTelemetryExtensions
    {
        /// <summary>
        /// Extension adds context information defined in the ResourceGroupDefinition to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="resourceGroup">The resource group definition.</param>
        public static EventContext AddContext(this EventContext telemetryContext, VmResourceGroupDefinition resourceGroup)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (resourceGroup == null)
            {
                telemetryContext.AddContext(nameof(resourceGroup), resourceGroup);
            }
            else
            {
                // Adding the whole definition can exceed the maximum telemetry property size
                // limitations.
                telemetryContext.AddContext(nameof(resourceGroup), new
                {
                    experimentId = resourceGroup.ExperimentId,
                    stepId = resourceGroup.StepId,
                    name = resourceGroup.Name,
                    subscriptionId = resourceGroup.SubscriptionId,
                    region = resourceGroup.Region,
                    cluster = resourceGroup.ClusterId,
                    tipSessionId = resourceGroup.TipSessionId,
                    deploymentName = resourceGroup.DeploymentName,
                    subnetName = resourceGroup.SubnetName,
                    vmNetworkName = resourceGroup.VirtualNetworkName,
                    networkSecurityGroupName = resourceGroup.NetworkSecurityGroupName,
                    state = resourceGroup.State,
                    deletionState = resourceGroup.DeletionState
                });

                telemetryContext.AddContext(resourceGroup.VirtualMachines);
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the VirtualMachineDefinition to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="virtualMachine">The virtual machine definition.<see cref="VmDefinition"/></param>
        public static EventContext AddContext(this EventContext telemetryContext, VmDefinition virtualMachine)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            if (virtualMachine == null)
            {
                telemetryContext.AddContext(nameof(virtualMachine), virtualMachine);
            }
            else
            {
                telemetryContext.AddContext(nameof(virtualMachine), new
                {
                    vmName = virtualMachine.Name,
                    vmSize = virtualMachine.VirtualMachineSize,
                    vmOsDiskStorageAccountType = virtualMachine.OsDiskStorageAccountType,
                    vmOsSku = virtualMachine.ImageReference,
                    vmDisks = virtualMachine.VirtualDisks,
                    enableAcceleratedNetworking = virtualMachine.EnableAcceleratedNetworking,
                    deploymentCorrelationId = virtualMachine.CorrelationId,
                    deploymentId = virtualMachine.DeploymentId,
                    deploymentName = virtualMachine.DeploymentName,
                    deploymentState = virtualMachine.State
                });
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the VirtualMachineDefinition to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="virtualMachines">The virtual machine definitions.<see cref="VmDefinition"/></param>
        public static EventContext AddContext(this EventContext telemetryContext, IEnumerable<VmDefinition> virtualMachines)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            if (virtualMachines == null)
            {
                telemetryContext.AddContext(nameof(virtualMachines), virtualMachines);
            }
            else
            {
                telemetryContext.AddContext(nameof(virtualMachines), virtualMachines.Select(vm => new
                {
                    vmName = vm.Name,
                    vmSize = vm.VirtualMachineSize,
                    vmOsDiskStorageAccountType = vm.OsDiskStorageAccountType,
                    vmOsSku = vm.ImageReference,
                    vmDisks = vm.VirtualDisks,
                    enableAcceleratedNetworking = vm.EnableAcceleratedNetworking,
                    deploymentCorrelationId = vm.CorrelationId,
                    deploymentId = vm.DeploymentId,
                    deploymentName = vm.DeploymentName,
                    deploymentState = vm.State
                }));
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the ArmDeploymentResponse to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="armDeploymentResponse">The arm deployment response.</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, ArmDeploymentResponse armDeploymentResponse, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            // Adding the whole ArmDeploymentResponse can easily exceeds maximum limit (8192) because it contains parameter,templates, providers, error message etc.
            if (armDeploymentResponse == null)
            {
                telemetryContext.AddContext(name ?? nameof(armDeploymentResponse), armDeploymentResponse);
            }
            else
            {
                DeploymentProperties deploymentProperties = null;
                if (armDeploymentResponse?.Properties != null)
                {
                    deploymentProperties = new DeploymentProperties()
                    {
                        CorrelationId = armDeploymentResponse.Properties.CorrelationId,
                        ProvisioningState = armDeploymentResponse.Properties.ProvisioningState,
                        Duration = armDeploymentResponse.Properties.Duration,
                        Mode = armDeploymentResponse.Properties.Mode,
                        Timestamp = armDeploymentResponse.Properties.Timestamp
                    };

                    if (armDeploymentResponse?.Properties.Error != null)
                    {
                        deploymentProperties.Error = new ErrorResponse
                        {
                            Code = armDeploymentResponse?.Properties?.Error.Code,
                            Message = armDeploymentResponse?.Properties?.Error.Message,
                            Target = armDeploymentResponse?.Properties?.Error.Target
                        };
                    }
                }

                telemetryContext.AddContext(name ?? nameof(armDeploymentResponse), new
                {
                    deploymentId = armDeploymentResponse.Id,
                    deploymentName = armDeploymentResponse.Name,
                    deploymentLocation = armDeploymentResponse.Location,
                    deploymentProperties = deploymentProperties
                });
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the CloudError to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="response">The arm deployment error response.<see cref="CloudError"/></param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, CloudError response, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            // I didn't see this exceeding the limit but keeping it just for proctection, since ErrorResponse is nested.
            if (response == null)
            {
                telemetryContext.AddContext(name ?? nameof(response), response);
            }
            else
            {
                ErrorResponse errorResponse = null;
                if (response?.Error != null)
                {
                    string errorMessage = response.Error.Message;
                    if (errorMessage != null && errorMessage.Length > 2000)
                    {
                        // Ensure that we don't exceed telemetry event size constraints
                        errorMessage = $"{errorMessage.Substring(0, 1997)}...";
                    }

                    errorResponse = new ErrorResponse()
                    {
                        Code = response.Error.Code,
                        Message = errorMessage, 
                        Target = response.Error.Target,
                    };
                }

                telemetryContext.AddContext(name ?? nameof(response), errorResponse);
            }

            return telemetryContext;
        }
    }
}
