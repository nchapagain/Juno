namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Extensions.Telemetry;
    using Juno.Providers.Validation;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Provides a base implementation for an <see cref="IExperimentProvider"/> 
    /// for other derived provider instances.
    /// </summary>
    [SupportedParameter(Name = StepParameters.Timeout, Type = typeof(TimeSpan))]
    [SupportedParameter(Name = StepParameters.FeatureFlag, Type = typeof(string), Required = false)]
    [SupportedParameter(Name = StepParameters.EnableDiagnostics, Type = typeof(bool), Required = false)]
    public abstract class ExperimentProvider : IExperimentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentProvider"/> class.
        /// </summary>
        /// <param name="services">The services/dependencies collection for the provider.</param>
        protected ExperimentProvider(IServiceCollection services)
        {
            services.ThrowIfNull(nameof(services));
            this.Services = services;
            this.TelemetryEventNamePrefix = this.GetType().Name;
        }

        /// <summary>
        /// Gets the collection of background/long-running tasks associated with experiment
        /// providers. This enables individual experiment providers to store long-running operations
        /// for later
        /// </summary>
        public static IDictionary<string, Task> BackgroundTasks { get; } = new Dictionary<string, Task>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the provider services/dependencies collection.
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Gets the logger to use for capturing provider telemetry.
        /// </summary>
        public ILogger Logger
        {
            get
            {
                return this.Services.HasService<ILogger>() 
                    ? this.Services.GetService<ILogger>()
                    : NullLogger.Instance;
            }
        }

        /// <summary>
        /// Gets the prefix to use for telemetry events. By default this is set
        /// to the name of the provider (e.g. TipCreationProvider).
        /// </summary>
        protected string TelemetryEventNamePrefix { get; set; }

        /// <summary>
        /// When overridden in derived classes enables the provider to configure or adds any additional
        /// required services for the provider to the services collection.
        /// </summary>
        /// <param name="context">
        /// Provides context information about the experiment within which the provider is running.
        /// </param>
        /// <param name="component">The experiment component definition for which the provider is associated.</param>
        public virtual Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executes the provider logic using details provided by the experiment component.
        /// </summary>
        /// <param name="context">
        /// Provides context information about the experiment within which the provider is running.
        /// </param>
        /// <param name="component">The experiment component definition for which the provider is associated.</param>
        /// <param name="cancellationToken">A token that can be used to request the provider cancel its operations.</param>
        /// <returns>
        /// A task that can be used to execute the provider logic asynchronously and that contains the
        /// result of the provider execution.
        /// </returns>
        public async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
        {
            context.ThrowIfNull(nameof(context));
            component.ThrowIfNull(nameof(component));

            ExecutionResult result = new ExecutionResult(ExecutionStatus.Pending);

            // Persist the activity ID so that it can be used down the callstack.
            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(context, component);

            try
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    result = await this.Logger.LogTelemetryAsync($"{this.TelemetryEventNamePrefix}.Execute", telemetryContext, async () =>
                    {
                        this.ValidateParameters(component);
                        await this.ConfigureServicesAsync(context, component).ConfigureDefaults();
                        return await this.ExecuteAsync(context, component, telemetryContext, cancellationToken)
                            .ConfigureDefaults();

                    }).ConfigureDefaults();
                }
            }
            catch (TaskCanceledException)
            {
                // In the case that a cancellation request has been issued and this exception
                // is thrown, we do not want to cause the experiment to show as 'Failed'. Cancellations
                // will be normal parts of the operation of the Juno system and we want to capture the
                // status of the experiment and steps as 'Cancelled' in those cases.
            }
            catch (Exception exc)
            {
                result = new ExecutionResult(ExecutionStatus.Failed, error: exc);
            }

            return !cancellationToken.IsCancellationRequested
                ? result
                : this.Cancelled();
        }

        /// <summary>
        /// Executes the provider logic using details provided by the experiment component.
        /// </summary>
        /// <param name="context">
        /// Provides context information about the experiment within which the provider is running.
        /// </param>
        /// <param name="component">The experiment component definition for which the provider is associated.</param>
        /// <param name="telemetryContext">Provides context properties to include in telemetry events.</param>
        /// <param name="cancellationToken">A token that can be used to request the provider cancel its operations.</param>
        /// <returns>
        /// A task that can be used to execute the provider logic asynchronously and that contains the
        /// result of the provider execution.
        /// </returns>
        protected abstract Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken);

        /// <summary>
        /// Validate required parameters of the experiment component that defines the requirements
        /// for the provider.
        /// </summary>
        /// <param name="component">The experiment component to validate.</param>
        protected virtual void ValidateParameters(ExperimentComponent component)
        {
            component.ThrowIfNull(nameof(component));

            var parameters = this.GetType().GetCustomAttributes<SupportedParameterAttribute>(true);
            if (parameters?.Any() == true)
            {
                foreach (SupportedParameterAttribute parameter in parameters)
                {
                    ProviderSchemaRules.ValidateParameter(component, parameter);
                }
            }
        }
    }
}
