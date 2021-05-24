namespace Juno.Execution.Management
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Juno.Execution.Management.Strategy;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Provides base functionality for processing experiment steps.
    /// </summary>
    public abstract class StepExecution
    {
        /// <summary>
        /// Create new instance of <see cref="StepExecution"/>
        /// </summary>
        /// <param name="services">The trace/telemetry logger for the controller.</param>
        /// <param name="configuration">The configuration for execution managements and providers.</param>
        protected StepExecution(IServiceCollection services, IConfiguration configuration)
        {
            services.ThrowIfNull(nameof(services));
            configuration.ThrowIfNull(nameof(configuration));

            this.Configuration = configuration;
            this.Services = services;

            ILogger logger;
            if (!services.TryGetService<ILogger>(out logger))
            {
                logger = NullLogger.Instance;
            }

            this.Logger = logger;
        }

        /// <summary>
        /// Gets the configuration settings for the execution manager.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// The execution host cancellation token used to receive signals from the host that
        /// cancellation is requested and it is shutting down.
        /// </summary>
        public CancellationToken HostCancellationToken { get; protected set; }

        /// <summary>
        /// Gets the telemetry logger.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Gets the services/dependencies for the execution manager.
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Executes the experiment step processing logic.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns></returns>
        public abstract Task ExecuteAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Get the next steps to execute from given list of steps.
        ///  1. Filter candidate steps: If any step is failed or cancelled, add environment cleanup steps as candidate steps, 
        ///  otherwise add all given steps to candidate steps
        ///  2. From the candidate steps
        ///     2.1 Get all steps which is inprogress or inprogresscontinue, and add them to selected steps collection
        ///     2.2 Check if selected steps contains any step with status=inprogress, if no add the next pending step to collection based on sequence ranking.
        /// </summary>
        /// <param name="steps">List of steps</param>
        /// <returns>Select steps</returns>
        protected static IEnumerable<ExperimentStepInstance> GetNextExperimentSteps(IEnumerable<ExperimentStepInstance> steps)
        {
            steps.ThrowIfNull(nameof(steps));
            List<ExperimentStepInstance> nextSteps = new List<ExperimentStepInstance>();

            if (steps.Any(step => !ExecutionResult.CompletedStatuses.Contains(step.Status)))
            {
                nextSteps = StepSelectionStrategy.Instance.GetSteps(steps).StepsSelected.ToList();
            }

            return nextSteps;
        }

        /// <summary>
        /// Check if experiment is completed based on experiment steps status
        /// </summary>
        /// <param name="steps">Experiment steps</param>
        /// <param name="experimentStatus">Experiment status</param>
        /// <returns>True if experiment is completed/in terminal state</returns>
        protected static bool IsExperimentCompleted(IEnumerable<ExperimentStepInstance> steps, out ExperimentStatus experimentStatus)
        {
            steps.ThrowIfNull(nameof(steps));
            var status = false;

            experimentStatus = ExperimentStatus.InProgress;

            if (steps.All(step => StepExecution.IsTerminalState(step)))
            {
                experimentStatus = StepExecution.GetExperimentStatus(steps);
                status = true;
            }

            return status;
        }

        /// <summary>
        /// Get experiment status from experiment steps status
        /// </summary>
        /// <param name="steps">Experiment steps</param>
        /// <returns>Experiment status</returns>
        protected static ExperimentStatus GetExperimentStatus(IEnumerable<ExperimentStepInstance> steps)
        {
            steps.ThrowIfNull(nameof(steps));

            ExperimentStatus executionStatus = ExperimentStatus.InProgress;
            if (steps.Any(step => step.Status == ExecutionStatus.Failed))
            {
                executionStatus = ExperimentStatus.Failed;
            }
            else if (steps.Any(step => step.Status == ExecutionStatus.Cancelled))
            {
                executionStatus = ExperimentStatus.Cancelled;
            }
            else if (steps.All(step => step.Status == ExecutionStatus.Succeeded))
            {
                executionStatus = ExperimentStatus.Succeeded;
            }

            return executionStatus;
        }

        /// <summary>
        /// Check if step in terminal status
        /// </summary>
        /// <param name="step">Experiment step instance</param>
        /// <returns></returns>
        protected static bool IsTerminalState(ExperimentStepInstance step)
        {
            step.ThrowIfNull(nameof(step));
            return step.Status == ExecutionStatus.Succeeded
                   || step.Status == ExecutionStatus.Failed
                   || step.Status == ExecutionStatus.Cancelled
                   || step.Status == ExecutionStatus.SystemCancelled;
        }
    }
}
