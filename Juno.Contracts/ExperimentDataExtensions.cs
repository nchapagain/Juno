namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Extension methods for experiment data contract classes.
    /// </summary>
    public static class ExperimentDataExtensions
    {
        /// <summary>
        /// Returns true if all previous steps have succeeded.
        /// </summary>
        /// <param name="steps">The steps to use for the evaluation.</param>
        /// <param name="currentStep">The current step </param>
        public static bool AllPreviousStepsSucceeded(this IEnumerable<ExperimentStepInstance> steps, ExperimentStepInstance currentStep)
        {
            steps.ThrowIfNull(nameof(steps));
            currentStep.ThrowIfNull(nameof(currentStep));

            return steps.Where(step => step.Sequence < currentStep.Sequence)?
                .All(step => step.Status == ExecutionStatus.Succeeded) == true;
        }

        /// <summary>
        /// Returns true if any previous step failed.
        /// </summary>
        /// <param name="steps">The steps to use for the evaluation.</param>
        /// <param name="currentStep">The current step </param>
        /// <param name="status">The execution status of interest (e.g. InProgress, Failed, Cancelled).</param>
        public static bool AnyPreviousStepsInStatus(this IEnumerable<ExperimentStepInstance> steps, ExperimentStepInstance currentStep, ExecutionStatus status)
        {
            steps.ThrowIfNull(nameof(steps));
            currentStep.ThrowIfNull(nameof(currentStep));

            return steps.Where(step => step.Sequence < currentStep.Sequence)?
                .Any(step => step.Status == status) == true;
        }

        /// <summary>
        /// Returns true/false whether the set of steps contains any that are in a terminal state (e.g. Failed, Cancelled) 
        /// or non-terminal state (e.g. Pending, InProgress) depdending upon the flag used.
        /// .
        /// </summary>
        /// <param name="steps">The steps for which to check status.</param>
        /// <returns>
        /// True if all steps are in a terminal status (e.g. Succeeded, Failed, Cancelled), false if not.
        /// </returns>
        public static bool ContainsTerminalSteps(this IEnumerable<ExperimentStepInstance> steps)
        {
            steps.ThrowIfNullOrEmpty(nameof(steps));
            return steps.Any(step => ExecutionResult.TerminalStatuses.Contains(step.Status));
        }

        /// <summary>
        /// Extension returns error details saved in the step.
        /// </summary>
        /// <param name="step">The step containing the error information.</param>
        /// <returns>
        /// The error information defined in the error extension for the step.
        /// </returns>
        public static Exception GetError(this ExperimentStepInstance step)
        {
            step.ThrowIfNull(nameof(step));

            JToken error;
            step.Extensions.TryGetValue(ContractExtension.Error, out error);
            ExperimentException exception = null;

            if (error != null)
            {
                try
                {
                    exception = error.ToObject<ExperimentException>();
                }
                catch
                {
                    exception = new ExperimentException(error.ToString());
                }
            }

            return exception;
        }

        /// <summary>
        /// Extension returns error details saved in the steps.
        /// </summary>
        /// <param name="steps">The steps containing the error information.</param>
        /// <returns>
        /// The error information defined in the error extension for the steps.
        /// </returns>
        public static IEnumerable<Exception> GetErrors(this IEnumerable<ExperimentStepInstance> steps)
        {
            steps.ThrowIfNullOrEmpty(nameof(steps));

            List<Exception> errors = new List<Exception>();
            steps.ToList().ForEach(step =>
            {
                Exception error = step.GetError();
                if (error != null)
                {
                    errors.Add(error);
                }
            });

            return errors;
        }

        /// <summary>
        /// Returns true/false whether the status of the result represents a final/completed
        /// status. Final statuses include: Succeeded, Failed and Cancelled.
        /// </summary>
        /// <returns>
        /// True if the result status is final/completed (e.g. Succeeded, Failed, Cancelled), false if not.
        /// </returns>
        public static bool IsCompleted(this ExecutionResult result)
        {
            result.ThrowIfNull(nameof(result));
            return ExecutionResult.CompletedStatuses.Contains(result.Status);
        }

        /// <summary>
        /// Returns true/false whether the status of the result represents a terminal/final
        /// status. Terminal statuses include: Failed and Cancelled.
        /// </summary>
        /// <returns>
        /// True if the result status is terminal/final (e.g. Failed, Cancelled), false if not.
        /// </returns>
        public static bool IsTerminal(this ExecutionResult result)
        {
            result.ThrowIfNull(nameof(result));
            return ExecutionResult.CompletedStatuses.Contains(result.Status);
        }

        /// <summary>
        /// Extension filters out entities in the source set that do match entities's id in the filter set.
        /// </summary>
        /// <param name="source">The source environment entities set that will be filtered.</param>
        /// <param name="filterSet">The set of other environment entities that represent the filter set or the set of entities that define the filter(s).</param>
        /// <returns>
        /// The set of <see cref="EnvironmentEntity"/> objects that exist in the source but not the filter set.
        /// </returns>
        public static IEnumerable<T> Filter<T>(this IEnumerable<T> source, IEnumerable<T> filterSet)
            where T : IIdentifiable
        {
            source.ThrowIfNull(nameof(source));
            filterSet.ThrowIfNull(nameof(filterSet));

            return source.Where(e => !filterSet.Any(filter => filter.Id == e.Id));
        }

        /// <summary>
        /// Extension that update the source if id exists in both source and filter, if it doesn't exist, it will add to the source.
        /// </summary>
        /// <param name="source">The source environment entities set that will be filtered.</param>
        /// <param name="filterSet">The set of other environment entities that represent the filter set or the set of entities that define the filter(s).</param>
        /// <returns>
        /// The set of <see cref="EnvironmentEntity"/> Updated objects in source and new objects in filter..
        /// </returns>
        public static IEnumerable<T> UpdateOrAdd<T>(this IEnumerable<T> source, IEnumerable<T> filterSet)
            where T : IIdentifiable
        {
            source.ThrowIfNull(nameof(source));
            filterSet.ThrowIfNull(nameof(filterSet));

            return source.Filter(filterSet).Union(filterSet);
        }

        /// <summary>
        /// Extension adds/sets the error for the step definition.
        /// </summary>
        /// <param name="step">The step to which the error should be added/set on the step.</param>
        /// <param name="error">The error/exception information.</param>
        public static void SetError(this ExperimentStepInstance step, Exception error)
        {
            step.ThrowIfNull(nameof(step));
            error.ThrowIfNull(nameof(error));

            ExperimentException exception = error as ExperimentException;
            if (exception == null)
            {
                exception = new ExperimentException(error.Message, error);
            }

            step.Extensions[ContractExtension.Error] = JToken.FromObject(exception);
        }

        /// <summary>
        /// Extension sets the sequence for each of the steps.
        /// </summary>
        /// <param name="steps">The experiment steps.</param>
        /// <param name="increment">The increment to use for each of the step sequence values (e.g. 100 -> 100, 200, 300...)</param>
        public static void SetSequences(this IEnumerable<ExperimentStepInstance> steps, int increment = 100)
        {
            steps.ThrowIfNull(nameof(steps));
            increment.ThrowIfInvalid(nameof(increment), (inc) => inc > 0, "The increment must be greater than 0.");

            int currentSequence = 0;
            foreach (ExperimentStepInstance step in steps)
            {
                step.Sequence = (currentSequence += increment);
            }
        }
    }
}
