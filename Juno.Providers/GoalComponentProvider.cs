namespace Juno.Providers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Providers.Validation;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Provides an intermediary implementation for an <see cref="IGoalComponentProvider"/>
    /// for other derived provider instances.
    /// </summary>
    public abstract class GoalComponentProvider : IGoalComponentProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GoalComponentProvider"/>
        /// </summary>
        /// <param name="services"></param>
        protected GoalComponentProvider(IServiceCollection services)
        {
            services.ThrowIfNull(nameof(services));
            this.Services = services;
        }

        /// <summary>
        /// Gets the logger to use for capturing telemetry.
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
        /// Gets the service provider/locator for the Precondition Provider
        /// </summary>
        protected IServiceCollection Services { get; }

        /// <inheritdoc/>
        public virtual Task ConfigureServicesAsync(GoalComponent component, ScheduleContext scheduleContext)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Validate required parameters of the Precondition component that defines
        /// the requirements of the provider.
        /// </summary>
        /// <param name="component">The precondition component to validate.</param>
        protected virtual void ValidateParameters(GoalComponent component)
        {
            component.ThrowIfNull(nameof(component));
            var parameters = this.GetType().GetCustomAttributes<SupportedParameterAttribute>(true);
            if (parameters?.Any() == true)
            {
                foreach (SupportedParameterAttribute parameter in parameters)
                {
                    GoalComponentProviderSchemaRules.ValidateParameter(component, parameter);
                }
            }
        }
    }
}
