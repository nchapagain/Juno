namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension methods for goal based schedule
    /// </summary>
    public static class GoalBasedScheduleExtensions
    {
        internal const string ParameterReference = "$.parameters";

        /// <summary>
        /// Extension will create a new <see cref="GoalBasedSchedule"/> with all components
        /// and parameters inlined.
        /// </summary>
        /// <param name="executionGoal">The executionGoal to inline.</param>
        /// <param name="executionGoalParameters">Execution Goal parameters necessary to inline Execution Goal Template <see cref="ExecutionGoalParameter"/></param>
        /// <returns>
        /// An <see cref="GoalBasedSchedule"/> having all components and parameters inlined.
        /// </returns>
        public static GoalBasedSchedule Inlined(this GoalBasedSchedule executionGoal, ExecutionGoalParameter executionGoalParameters)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));
            executionGoalParameters.ThrowIfNull(nameof(executionGoalParameters));

            List<TargetGoal> inlinedGoals = new List<TargetGoal>();
            foreach (TargetGoalParameter goalParameters in executionGoalParameters.TargetGoals)
            {
                TargetGoal goal = executionGoal.GetGoal(goalParameters.Name) as TargetGoal;
                GoalBasedScheduleExtensions.MergeSharedParameters(goalParameters.Parameters, executionGoalParameters.SharedParameters);
                TargetGoal inlinedGoal = GoalBasedScheduleExtensions.Inlined(goal, goalParameters);

                inlinedGoals.Add(inlinedGoal);
            }

            foreach (string metadata in executionGoal.Metadata.Keys)
            {
                if (!executionGoalParameters.Metadata.ContainsKey(metadata))
                {
                    executionGoalParameters.Metadata.Add(metadata, executionGoal.Metadata[metadata]);
                }
            }

            GoalBasedSchedule inlinedExecutionGoal = new GoalBasedSchedule(
                executionGoalParameters.ExperimentName,
                executionGoal.Description,
                executionGoal.Experiment,
                inlinedGoals,
                executionGoal.ControlGoals,
                executionGoalParameters.Metadata);

            return inlinedExecutionGoal;
        }

        /// <summary>
        /// Determines if the execution goal given is inlined
        /// </summary>
        /// <param name="executionGoal">The execution goal to evaluate</param>
        /// <returns>If the execution goal is inlined</returns>
        public static bool IsInlined(this GoalBasedSchedule executionGoal)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));
            ExecutionGoalParameter executionGoalMetadata = executionGoal.GetParametersFromTemplate();
            var targetGoalParameters = executionGoalMetadata.TargetGoals.Select(x => x.Parameters.Keys).SelectMany(y => y);

            return targetGoalParameters.Any();
        }

        /// <summary>
        /// Extract Parameters from an execution goal template
        /// </summary>
        /// <param name="executionGoalTemplate">The template to extract the parameters from</param>
        /// <returns>A list of parameter names</returns>
        public static ExecutionGoalParameter GetParametersFromTemplate(this GoalBasedSchedule executionGoalTemplate)
        {
            executionGoalTemplate.ThrowIfNull(nameof(executionGoalTemplate));

            IDictionary<string, IConvertible> sharedParameters = GoalBasedScheduleExtensions.GetParametersFromTemplate(executionGoalTemplate.Parameters)
                .OrderBy(p => p).ToDictionary(p => p, p => string.Empty as IConvertible);

            IList<TargetGoalParameter> targetGoals = new List<TargetGoalParameter>();
            foreach (var targetGoal in executionGoalTemplate.TargetGoals)
            {
                IDictionary<string, IConvertible> targetGoalParameters = GoalBasedScheduleExtensions.GetTargetGoalParametersFromTemplate(targetGoal)
                                    .OrderBy(p => p).ToDictionary(p => p, p => string.Empty as IConvertible);

                // removing duplicates. If targetGoal parameters are present in shared parameters, remove it.
                foreach (var sharedParameter in sharedParameters)
                {
                    if (targetGoalParameters.ContainsKey(sharedParameter.Key))
                    {
                        targetGoalParameters.Remove(sharedParameter.Key);
                    }
                }

                targetGoals.Add(new TargetGoalParameter(
                    targetGoal.Name,
                    targetGoal.Enabled,
                    targetGoalParameters));
            }

            IDictionary<string, IConvertible> requiredMetadata = ExecutionGoalMetadata.RequiredParameterMetadata
                .ToDictionary(data => data, data => (IConvertible)string.Empty);

            return new ExecutionGoalParameter(
                targetGoals,
                requiredMetadata,
                sharedParameters: sharedParameters);
        }

        /// <summary>
        /// Retrieves the desired goal from the Goal Based Schedule.
        /// </summary>
        /// <param name="executionGoal">The execution goal to search in.</param>
        /// <param name="name">The name of the goal to retrieve.</param>
        /// <returns>The designated goal.</returns>
        /// <exception cref="ArgumentException">Throws exception if name is null or empty.</exception>
        /// <exception cref="SchedulerException">Throws excpetion if name is not present in schedule.</exception>
        public static Goal GetGoal(this GoalBasedSchedule executionGoal, string name)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));
            name.ThrowIfNullOrWhiteSpace(nameof(name));

            Goal result = executionGoal.TargetGoals.FirstOrDefault(goal => goal.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (result != null)
            {
                return result;
            }

            // Attempt to find target goal suffixed with team name for backwards compatability.
            result = executionGoal.TargetGoals.FirstOrDefault(goal => name.Equals($"{goal.Name}-{executionGoal.TeamName}", StringComparison.OrdinalIgnoreCase));
            if (result != null)
            {
                return result;
            }

            result = executionGoal.ControlGoals.FirstOrDefault(goal => goal.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (result == null)
            {
                throw new SchedulerException($"The goal: '{name}' could not be found in the execution goal: '{executionGoal.ExperimentName}'", ErrorReason.GoalNotFound);
            }

            return result;
        }

        /// <summary>
        /// Determines if the goal is a target goal in the given execution goal.
        /// </summary>
        /// <param name="goal">The goal to determine if is (not) a target goal.</param>
        /// <param name="executionGoal">The execution goal in which to search for the target goal.</param>
        /// <returns>True/False if the goal is a target goal in the given execution goal.</returns>
        public static bool IsTargetGoal(this Goal goal, GoalBasedSchedule executionGoal)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));
            goal.ThrowIfNull(nameof(goal));

            return executionGoal.TargetGoals.Select(tg => tg.Name).Any(name => name.Equals(goal.Name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Retrieves the workload from the target goal.
        /// </summary>
        /// <param name="goal">The goal to derive the workload from.</param>
        /// <returns>The name of the workload found in the target goal.</returns>
        public static string GetWorkload(this TargetGoal goal)
        {
            goal.ThrowIfNull(nameof(goal));

            try
            {
                string workload = goal.Actions.SelectMany(a => a.Parameters)
                    .FirstOrDefault(k => k.Key.Equals(TargetGoalParameters.Workload, StringComparison.OrdinalIgnoreCase)).Value.ToString();
                return workload;
            }
            catch (Exception)
            {
                throw new SchemaException($"The workload was not found in any of the parameters " +
                    $"of the given actions in {nameof(TargetGoal)}: {goal.Name}");
            }
        }

        /// <summary>
        /// Generates an id for the execution goal given
        /// </summary>
        /// <param name="executionGoal">The execution goal to derive the execution goal from.</param>
        /// <returns>A unique Id</returns>
        public static string GenerateExecutionGoalId(this GoalBasedSchedule executionGoal)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));

            return $"{executionGoal.ExperimentName.Substring(0, 8)}{Guid.NewGuid()}".Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Appends a string representation of each component to the builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="components">The set of components to append in string form to the builder.</param>
        internal static StringBuilder AppendComponents(this StringBuilder builder, IEnumerable<TargetGoalParameter> components)
        {
            builder.ThrowIfNull(nameof(builder));

            if (components?.Any() == true)
            {
                builder.Append($"{string.Join(",", components.Select(entry => $"{entry?.GetHashCode()}"))}");
            }

            return builder;
        }

        private static IEnumerable<string> GetTargetGoalParametersFromTemplate(Goal goalTemplate)
        {
            List<GoalComponent> components = new List<GoalComponent>();
            components.AddRange(goalTemplate.Preconditions);
            components.AddRange(goalTemplate.Actions);

            HashSet<string> parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GoalComponent component in components)
            {
                parameterNames.AddRange(GoalBasedScheduleExtensions.GetParametersFromTemplate(component.Parameters));
            }

            return parameterNames;
        }

        private static void MergeSharedParameters(IDictionary<string, IConvertible> targetGoalParameters, IEnumerable<KeyValuePair<string, IConvertible>> sharedParameters)
        {
            foreach (KeyValuePair<string, IConvertible> entry in sharedParameters)
            {
                if (entry.Key.ContainsNullEmptyOrWhiteSpace() || entry.Value.ContainsNullEmptyOrWhiteSpace())
                {
                    throw new SchemaException($"Shared Parameters cannot contain invalid key-value pair. Key: {entry.Key} Value: {entry.Value}");
                }

                // if target goal parameter doesn't contain valid key, remove it. We will then fill it with shared parameters.
                if (targetGoalParameters.ContainsKey(entry.Key) && (targetGoalParameters[entry.Key] == null || string.IsNullOrWhiteSpace(targetGoalParameters[entry.Key].ToString())))
                {
                    targetGoalParameters.Remove(entry.Key);
                }

                if (!targetGoalParameters.ContainsKey(entry.Key))
                {
                    targetGoalParameters.Add(entry);
                }
            }
        }

        private static TargetGoal Inlined(TargetGoal goal, TargetGoalParameter goalParameters)
        {
            List<GoalComponent> components = new List<GoalComponent>();

            components.AddRange(goal.Actions);
            components.AddRange(goal.Preconditions);
            foreach (var dictionary in components.Select(c => c.Parameters))
            {
                foreach (var pair in dictionary)
                {
                    dictionary[pair.Key] = GoalBasedScheduleExtensions.ReplaceParameterReference(goalParameters.Parameters, pair.Value);
                }
            }

            return new TargetGoal(goal.Name, goalParameters.Enabled, goal.Preconditions, goal.Actions, Guid.NewGuid().ToString());
        }

        private static IEnumerable<string> GetParametersFromTemplate(IDictionary<string, IConvertible> parameters)
        {
            HashSet<string> parameterNames = new HashSet<string>();
            foreach (IConvertible parameterReference in parameters.Values)
            { 
                if (parameterReference.ToString().StartsWith(GoalBasedScheduleExtensions.ParameterReference, StringComparison.OrdinalIgnoreCase))
                {
                    string parameterName = parameterReference.ToString().Substring(GoalBasedScheduleExtensions.ParameterReference.Length + 1);
                    parameterNames.Add(parameterName);
                }
            }

            return parameterNames;
        }

        private static IConvertible ReplaceParameterReference(IDictionary<string, IConvertible> parameters, IConvertible field)
        {
            if (GoalBasedScheduleExtensions.TryGetParameterReference(field, out string parameterName))
            {
                if (!parameters.ContainsKey(parameterName))
                {
                    throw new SchemaException($"There is no parameter named: {parameterName} referred from field: {field}");
                }

                field = parameters[parameterName];
            }

            return field;
        }

        private static bool TryGetParameterReference(IConvertible paramaterReference, out string parameterName)
        {
            paramaterReference.ThrowIfNull(nameof(paramaterReference));

            parameterName = null;
            bool isParameter = paramaterReference.ToString().StartsWith(GoalBasedScheduleExtensions.ParameterReference, StringComparison.OrdinalIgnoreCase);
            if (isParameter)
            {
                parameterName = paramaterReference.ToString().Substring(GoalBasedScheduleExtensions.ParameterReference.Length + 1);
            }

            return isParameter;
        }
    }
}
