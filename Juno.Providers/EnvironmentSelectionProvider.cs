namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Juno.Extensions.Telemetry;
    using Juno.Providers.Validation;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Abstract implementation of the EnvironmentFilter provider
    /// </summary>
    public abstract class EnvironmentSelectionProvider : IEnvironmentSelectionProvider
    {
        /// <summary>
        /// Instantiates an instance of <see cref="EnvironmentSelectionProvider"/>
        /// </summary>
        /// <param name="services">A collection of services used for dependency injection</param>
        /// <param name="ttl">The time to live for this providers results in the cache.</param>
        /// <param name="configuration">Current configuration of execution environment</param>
        /// <param name="logger"><see cref="ILogger"/></param>
        protected EnvironmentSelectionProvider(IServiceCollection services, TimeSpan ttl, IConfiguration configuration, ILogger logger)
        {
            services.ThrowIfNull(nameof(services));
            configuration.ThrowIfNull(nameof(configuration));
            ttl.ThrowIfNull(nameof(ttl));

            this.Services = services;
            this.TTL = ttl;
            this.Configuration = configuration;
            this.Logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Collection of services used for dependency injection
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Time to live for the results returned from this provider.
        /// </summary>
        public TimeSpan TTL { get; }

        /// <summary>
        /// Logger used for capturing telemetry
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Current configuration of execution environment
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Configure Services required for successful completion of the filter
        /// </summary>
        public virtual Task ConfigureServicesAsync()
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, CancellationToken token)
        {
            filter.ThrowIfNull(nameof(filter));

            EventContext telemetryContext = EventContext.Persisted()
                .AddContext(nameof(filter), filter);

            IDictionary<string, EnvironmentCandidate> result = new Dictionary<string, EnvironmentCandidate>();
            if (!token.IsCancellationRequested)
            {
                result = await this.Logger.LogTelemetryAsync($"{this.GetType().FullName}.Execute", telemetryContext, async () =>
                {
                    this.ValidateParameters(filter);
                    await this.ConfigureServicesAsync().ConfigureDefaults();
                    return await this.ExecuteAsync(filter, telemetryContext, token).ConfigureDefaults();
                }).ConfigureDefaults();
            }

            return result;
        }

        /// <summary>
        /// Abstract method that allows derived providers to apply filters in
        /// their specific manner
        /// </summary>
        /// <param name="filter">Filter used to apply to a set of resources</param>
        /// <param name="telemetryContext">Event context used for capturing telemetry</param>
        /// <param name="token"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        protected abstract Task<IDictionary<string, EnvironmentCandidate>> ExecuteAsync(EnvironmentFilter filter, EventContext telemetryContext, CancellationToken token);

        /// <summary>
        /// Validates parameters that are given via the EnvironmentFilter
        /// </summary>
        /// <param name="filter">Environment Filter to validate.</param>
        protected virtual void ValidateParameters(EnvironmentFilter filter)
        {
            filter.ThrowIfNull(nameof(filter));

            EnvironmentFilterValidation instance = EnvironmentFilterValidation.Instance;
            ValidationResult result = instance.Validate(filter);

            if (!result.IsValid)
            {
                throw new SchemaException($"Validation errors occured in provider: {this.GetType().Name} " +
                    $"{string.Join(", ", result.ValidationErrors.ToArray())}");
            }

        }
    }
}
