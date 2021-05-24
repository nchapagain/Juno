namespace Juno.Execution.Providers.Environment
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.ArmIntegration;
    using Juno.Extensions.Telemetry;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A provider for deleting resource group and with all resources including virtual machines
    /// </summary>
    /// 
    [ExecutionConstraints(SupportedStepType.EnvironmentCleanup, SupportedStepTarget.ExecuteRemotely)]
    [SupportedParameter(Name = Constants.ResourceGroupName, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = Constants.SubscriptionId, Type = typeof(string), Required = false)]
    [ProviderInfo(Name = "Cleanup Virtual Machine Resources", Description = "Removes/deletes VMs and related Azure subscription resources that were created for the experiment group")]
    public partial class ArmVmCleanupProvider : ExperimentProvider
    {
        /// <summary>
        /// Default timeout for the resource group deletion to be completed.
        /// </summary>
        private TimeSpan defaultTimeout = TimeSpan.FromMinutes(120);

        /// <summary>
        /// Default timeout used for retrying the delete
        /// </summary>
        private TimeSpan defaultCleanupAttemptTimeout = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Key used to find the last delete attempt start time
        /// </summary>
        private string deleteAttemptStartKey = "deleteAttemptStart_";

        /// <summary>
        /// Initializes a new instance of the <see cref="ArmVmCleanupProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        public ArmVmCleanupProvider(IServiceCollection services)
            : base(services)
        {
        }

        /// <inheritdoc/>
        protected async override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            var result = new ExecutionResult(ExecutionStatus.Pending);

            if (!cancellationToken.IsCancellationRequested)
            {
                result = new ExecutionResult(ExecutionStatus.InProgress);

                VmResourceGroupDefinition rgDefinition;
                string stateKey = string.Format(ContractExtension.ResourceGroup, context.ExperimentStep.ExperimentGroup);

                if (!cancellationToken.IsCancellationRequested)
                {
                    // check if the step itself has timed out
                    TimeSpan timeoutValue = this.defaultTimeout;
                    if (component.Parameters?.ContainsKey(StepParameters.Timeout) == true)
                    {
                        timeoutValue = TimeSpan.Parse(component.Parameters.GetValue<string>(StepParameters.Timeout));
                    }

                    bool hasStepTimedOut = await this.CheckStepTimeoutAsync(context, telemetryContext, timeoutValue, cancellationToken)
                        .ConfigureDefaults();

                    if (hasStepTimedOut)
                    {
                        throw new ProviderException(
                            $"Virtual machine cleanup failed. The virtual machines in the environment could not be confirmed deleted within the time range allowed " +
                            $"(timeout = '{timeoutValue}').",
                            ErrorReason.Timeout);
                    }

                    var timeoutKey = $"{this.deleteAttemptStartKey}{context.ExperimentStep.Id}";
                    var resourceGroupFromParameter = await this.GetResourceGroupFromParameterToDeleteAsync(context, component, timeoutKey, cancellationToken)
                        .ConfigureDefaults();

                    if (resourceGroupFromParameter != null)
                    {
                        telemetryContext.AddContext("resourceGroupProvidedByUser", resourceGroupFromParameter);
                        telemetryContext.AddContext("explicitresourceGroup", true);
                        rgDefinition = resourceGroupFromParameter;
                    }
                    else
                    {
                        rgDefinition = await this.GetStateAsync<VmResourceGroupDefinition>(context, stateKey, cancellationToken)
                            .ConfigureDefaults();
                    }

                    telemetryContext.AddContext(rgDefinition);

                    if (rgDefinition == null)
                    {
                        // Nothing to cleanup
                        result = new ExecutionResult(ExecutionStatus.Succeeded);
                    }
                    else
                    {
                        using (var resourceManager = this.CreateArmResourceManager(context))
                        {
                            // check if the last cleanup attempt has timed out
                            TimeSpan deleteAttemptTimeOutValue = this.defaultCleanupAttemptTimeout;
                            if (component.Parameters?.ContainsKey(Constants.CleanupAttemptTimeout) == true)
                            {
                                deleteAttemptTimeOutValue = TimeSpan.Parse(component.Parameters.GetValue<string>(Constants.CleanupAttemptTimeout));
                            }

                            bool hasDeleteAttemptTimedOut = await this.CheckDeleteAttemptTimeoutAsync(context, telemetryContext, deleteAttemptTimeOutValue, cancellationToken)
                                .ConfigureDefaults();

                            if (hasDeleteAttemptTimedOut)
                            {
                                // if last step timed out, reset the start time of the last attempt (since we're starting a new attempt)
                                await this.SaveStateAsync(context, timeoutKey, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

                                // force the RG cleanup even though the last request is pending
                                await resourceManager.DeleteResourceGroupAsync(rgDefinition, cancellationToken, true).ConfigureDefaults();
                                await this.SaveStateAsync<VmResourceGroupDefinition>(context, stateKey, rgDefinition, cancellationToken).ConfigureDefaults();
                            }
                            else
                            {
                                await resourceManager.DeleteResourceGroupAsync(rgDefinition, cancellationToken).ConfigureDefaults();
                                await this.SaveStateAsync<VmResourceGroupDefinition>(context, stateKey, rgDefinition, cancellationToken).ConfigureDefaults();
                            }

                            if (rgDefinition.DeletionState == CleanupState.Succeeded)
                            {
                                result = new ExecutionResult(ExecutionStatus.Succeeded);
                            }
                            else if (rgDefinition.DeletionState == CleanupState.Failed)
                            {
                                result = new ExecutionResult(ExecutionStatus.Failed);
                            }
                        }
                    }
                }

            }

            return result;
        }

        /// <summary>
        /// Creates an ARM resource manager to handle interactions with the Azure Resource Manager (ARM) endpoint to delete 
        /// virtual machines and the resource group that contains them.
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

        private async Task<bool> CheckStepTimeoutAsync(
            ExperimentContext context,
            EventContext telemetryContext,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);
            return await this.Logger.LogTelemetryAsync($"{nameof(ArmVmCleanupProvider)}.CheckStepTimeout", relatedContext, async () =>
            {
                bool result = false;
                try
                {
                    var timeoutKey = $"heartbeatTimeout_{context.ExperimentStep.Id}";
                    var startTime = await this.GetStateAsync<DateTime>(context, timeoutKey, cancellationToken).ConfigureDefaults();
                    // This means it is first time, save current time utc as start time
                    if (startTime == default)
                    {
                        await this.SaveStateAsync(context, timeoutKey, DateTime.UtcNow, cancellationToken).ConfigureDefaults();
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
            }).ConfigureDefaults();
        }

        private async Task<bool> CheckDeleteAttemptTimeoutAsync(
            ExperimentContext context,
            EventContext telemetryContext,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            EventContext relatedContext = telemetryContext.Clone(withProperties: true);
            return await this.Logger.LogTelemetryAsync($"{nameof(ArmVmCleanupProvider)}.CheckDeleteAttemptTimeout", relatedContext, async () =>
            {
                bool result = false;
                try
                {
                    var timeoutKey = $"{this.deleteAttemptStartKey}{context.ExperimentStep.Id}";
                    var lastCleanupAttemptStart = await this.GetStateAsync<DateTime>(context, timeoutKey, cancellationToken).ConfigureDefaults();
                    // This means it is first time, save current time utc as start time
                    if (lastCleanupAttemptStart == default)
                    {
                        await this.SaveStateAsync(context, timeoutKey, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
                    }
                    else if (DateTime.UtcNow > lastCleanupAttemptStart.Add(timeout))
                    {
                        result = true;
                    }
                }
                catch (Exception exception)
                {
                    relatedContext.AddError(exception);
                }

                return result;
            }).ConfigureDefaults();
        }

        private async Task<VmResourceGroupDefinition> GetResourceGroupFromParameterToDeleteAsync(ExperimentContext context, ExperimentComponent component, string timeoutKey, CancellationToken cancellationToken)
        {
            VmResourceGroupDefinition vmResourceGroup = null;

            string resourceGroupName = component.Parameters.GetValue<string>(Constants.ResourceGroupName, string.Empty);
            string subscriptionId = component.Parameters.GetValue<string>(Constants.SubscriptionId, string.Empty);

            if ((!string.IsNullOrEmpty(resourceGroupName)) && (!string.IsNullOrEmpty(subscriptionId)))
            {
                vmResourceGroup = new VmResourceGroupDefinition();
                vmResourceGroup.DeletionState = CleanupState.NotStarted;
                vmResourceGroup.SubscriptionId = subscriptionId;
                vmResourceGroup.Name = resourceGroupName;

                // This means it is first time, save current time utc as start time
                var startTime = await this.GetStateAsync<DateTime>(context, timeoutKey, cancellationToken).ConfigureAwait(false);
                if (startTime == default)
                {
                    vmResourceGroup.DeletionState = CleanupState.NotStarted;
                }
                else
                {
                    vmResourceGroup.DeletionState = CleanupState.Accepted;
                }
            }

            return vmResourceGroup;
        }

        private class Constants
        {
            internal const string CleanupAttemptTimeout = "CleanupAttemptTimeout";
            internal const string ResourceGroupName = "resourceGroupName";
            internal const string SubscriptionId = "subscriptionId";
        }
    }
}