namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json.Linq;

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

            // Schedule Metadata will be copied over and owner parameter will be added.
            Dictionary<string, IConvertible> scheduleMetadata = new Dictionary<string, IConvertible>(executionGoal.ScheduleMetadata, StringComparer.OrdinalIgnoreCase);
            if (scheduleMetadata.ContainsKey(ImportantKeys.Owner))
            {
                scheduleMetadata.Remove(ImportantKeys.Owner);
            }

            scheduleMetadata.Add(ImportantKeys.Owner, executionGoalParameters.Owner);
            // Shared parameters will be be copied over to each Target Goal Parameter
            foreach (TargetGoalParameter targetGoalParameter in executionGoalParameters.TargetGoals)
            {
                GoalBasedScheduleExtensions.MergeSharedParameters(targetGoalParameter.Parameters, executionGoalParameters.SharedParameters);
            }

            // TargetGoal Verification: Allows only targetgoal which have matching workloadType and parameters to be passed 
            List<Goal> inlinedTargetGoalList = new List<Goal>();
            StringBuilder errorList = new StringBuilder();
            foreach (Goal targetGoal in executionGoal.TargetGoals)
            {
                // All replaceable parameters inside a targetgoal
                Dictionary<string, IConvertible> targetGoalParameters = new Dictionary<string, IConvertible>();
                GoalBasedScheduleExtensions.GetParametersFromTemplate(JObject.FromObject(targetGoal), targetGoalParameters);
                IEnumerable<string> targetGoalKeys = targetGoalParameters.Keys;
                string targetGoalWorkload = targetGoal.GetWorkLoadFromTargetGoal();

                // Find the target goal parameter with matching signature.
                // targetGoalParameter => targetGoalParameter.Parameters.Keys.All(k => targetGoalKeys.Contains(k)) && // Validate all keys are present
                IEnumerable<TargetGoalParameter> matchingTargetGoalParameter = executionGoalParameters.TargetGoals.Where(targetGoalParameter =>
                    targetGoalParameter.Id.Equals(targetGoal.Id, StringComparison.Ordinal) && // Validate Id is matching
                    targetGoalParameter.Workload.Equals(targetGoalWorkload, StringComparison.Ordinal) && // Validate Workload is matching
                    targetGoalKeys.All(k => targetGoalParameter.Parameters.Keys.Contains(k))); // Validate all keys are present

                // Inline target goal with found parameter
                if (matchingTargetGoalParameter != null && matchingTargetGoalParameter.Any())
                {
                    try
                    {
                        inlinedTargetGoalList.Add(GoalBasedScheduleExtensions.Inlined(targetGoal, matchingTargetGoalParameter.FirstOrDefault()));
                    }
                    catch (SchemaException exc)
                    {
                        throw new SchemaException($"{exc.Message} for Target Goal with id: {targetGoal.Id} and workload: {targetGoalWorkload}");
                    }
                }
                else
                {
                    errorList.AppendLine($" - Target Goal does not contain combination of " +
                        $"id: {targetGoal.Id}, " +
                        $"workload: {targetGoalWorkload} and " +
                        $"parameters: [").AppendProperties(targetGoalKeys.ToArray()).Append("].");
                }
            }

            if (!inlinedTargetGoalList.Any())
            {
                throw new SchemaException($"Given Target Goals do not match the combination of workload, id and parameters in the identified Execution Goal Template: {executionGoal.ExecutionGoalId}. Errors: {errorList}");
            }

            GoalBasedSchedule inlinedExecutionGoal = new GoalBasedSchedule(
                executionGoalParameters.ExperimentName,
                executionGoalParameters.ExecutionGoalId,
                executionGoal.Name,
                executionGoal.TeamName,
                executionGoal.Description,
                scheduleMetadata,
                executionGoalParameters.Enabled,
                executionGoal.Version,
                executionGoal.Experiment,
                inlinedTargetGoalList,
                executionGoal.ControlGoals,
                null);

            return inlinedExecutionGoal;
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
                throw new SchedulerException($"The goal: '{name}' could not be found in the execution goal: '{executionGoal.Name}'", ErrorReason.GoalNotFound);
            }

            return result;
        }

        /// <summary>
        /// Determines if the precondtions are satisfied.
        /// </summary>
        /// <param name="precondtionResults">The list of preconditions.</param>
        /// <returns>True/False if the preconditions are satisfied.</returns>
        public static bool ArePreconditionsSatisfied(this IEnumerable<PreconditionResult> precondtionResults)
        {
            precondtionResults.ThrowIfNull(nameof(precondtionResults));

            return !precondtionResults.Any() || precondtionResults.All(entity => entity.Status == ExecutionStatus.Succeeded && entity.Satisfied == true);
        }

        /// <summary>
        /// Extract Parameters from an execution goal template
        /// </summary>
        /// <param name="executionGoalTemplate">The template to extract the parameters from</param>
        /// <returns>A list of parameter names</returns>
        public static ExecutionGoalParameter GetParametersFromTemplate(this GoalBasedSchedule executionGoalTemplate)
        {
            executionGoalTemplate.ThrowIfNull(nameof(executionGoalTemplate));

            IDictionary<string, IConvertible> sharedParameters = executionGoalTemplate.GetSharedParametersFromTemplate();
            IList<TargetGoalParameter> targetGoals = new List<TargetGoalParameter>();

            foreach (var targetGoal in executionGoalTemplate.TargetGoals)
            {
                SortedDictionary<string, IConvertible> targetGoalParameters = new SortedDictionary<string, IConvertible>();
                GoalBasedScheduleExtensions.GetParametersFromTemplate(JObject.FromObject(targetGoal), targetGoalParameters);

                // removing duplicates. If targetGoal parameters are present in shared parameters, remove it.
                foreach (var sharedParameter in sharedParameters)
                {
                    if (targetGoalParameters.ContainsKey(sharedParameter.Key))
                    {
                        targetGoalParameters.Remove(sharedParameter.Key);
                    }
                }

                targetGoals.Add(new TargetGoalParameter(
                    targetGoal.Id,
                    targetGoal.GetWorkLoadFromTargetGoal(),
                    targetGoalParameters));
            }

            return new ExecutionGoalParameter(
                executionGoalTemplate.ExecutionGoalId,
                executionGoalTemplate.ExperimentName,
                executionGoalTemplate.TeamName,
                executionGoalTemplate.Enabled,
                targetGoals,
                sharedParameters);
        }

        /// <summary>
        /// Extract Shared Parameters from an execution goal template
        /// Shared Parameters will be the same as GoalBasedSchedule.Parameters
        /// </summary>
        /// <param name="executionGoalTemplate">The template to extract the parameters from</param>
        /// <returns>A list of parameter names</returns>
        public static IDictionary<string, IConvertible> GetSharedParametersFromTemplate(this GoalBasedSchedule executionGoalTemplate)
        {
            executionGoalTemplate.ThrowIfNull(nameof(executionGoalTemplate));

            SortedDictionary<string, IConvertible> sharedParameters = new SortedDictionary<string, IConvertible>();
            GoalBasedScheduleExtensions.GetParametersFromTemplate(JObject.FromObject(executionGoalTemplate.Parameters), sharedParameters);

            return sharedParameters;
        }

        /// <summary>
        /// Extracts workload from input targetGoal
        /// </summary>
        /// <param name="targetGoal"> Target Goal <see cref="Goal"/> </param>
        public static string GetWorkLoadFromTargetGoal(this Goal targetGoal)
        {
            targetGoal.ThrowIfNull(nameof(targetGoal));
            ScheduleAction scheduleActionParameters = targetGoal.Actions.FirstOrDefault();

            if (!scheduleActionParameters.Parameters.ContainsKey(ImportantKeys.WorkloadKey))
            {
                throw new SchemaException($"There is no parameter named: {ImportantKeys.WorkloadKey} referred in targetGoal: {targetGoal.Name} under actiontype: {scheduleActionParameters.Type}");
            }

            return scheduleActionParameters.Parameters[ImportantKeys.WorkloadKey].ToString();
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
        /// Validates if Execution Goal Version is 2020-07-27
        /// </summary>
        /// <param name="version">Execution Goal Version</param>
        public static bool IsExecutionGoalVersion20200727(string version)
        {
            version.ThrowIfNullOrWhiteSpace(nameof(version));
            return version.Equals("2020-07-27", StringComparison.OrdinalIgnoreCase);
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

        private static Goal Inlined(Goal executionGoal, TargetGoalParameter goalParameter)
        {
            JToken goalObject = JObject.FromObject(executionGoal);
            var invalidTargetGoalParameters = goalParameter.Parameters.Where(x => x.Value == null || string.IsNullOrEmpty(x.Value.ToString()));

            if (invalidTargetGoalParameters.Any())
            {
                throw new SchemaException($"Target Goal Parameters cannot be null or empty: {invalidTargetGoalParameters.ToJson()}.");
            }

            JToken inlinedObject = GoalBasedScheduleExtensions.Inlined(goalObject, goalParameter.Parameters);

            Goal inlinedExecutionGoal = inlinedObject.ToObject<Goal>();
            return inlinedExecutionGoal;
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

        private static JToken Inlined(JToken token, IDictionary<string, IConvertible> parameters)
        {
            // Token is a string
            if (token.Type == JTokenType.String)
            {
                return GoalBasedScheduleExtensions.ReplaceParameterReference(parameters, token.ToString()).ToString();
            }

            IEnumerable<JToken> children = token.Children();

            // Token is an array
            if (token.Type == JTokenType.Array)
            {
                JArray result = new JArray();
                foreach (JToken child in children)
                {
                    result.Add(GoalBasedScheduleExtensions.Inlined(child, parameters));
                }

                return result;
            }

            // Token is a complex object
            if (token.Type == JTokenType.Object)
            {
                JObject result = (JObject)token.DeepClone();
                IList<JToken> newChildren = new List<JToken>();
                foreach (JToken child in children)
                {
                    newChildren.Add(GoalBasedScheduleExtensions.Inlined(child, parameters));
                }

                result.ReplaceAll(newChildren);
                return result;
            }

            // Token is a property (k-v pair)
            if (token.Type == JTokenType.Property)
            {
                JProperty result = (JProperty)token.DeepClone();
                result.Value = GoalBasedScheduleExtensions.Inlined(result.Value, parameters);
                return result;
            }

            return token;
        }

        private static void GetParametersFromTemplate(JToken token, IDictionary<string, IConvertible> parameters)
        {
            IEnumerable<JToken> children = token.Children();
            if (children.Any())
            {
                foreach (JToken value in children)
                {
                    GoalBasedScheduleExtensions.GetParametersFromTemplate(value, parameters);
                }
            }
            else
            {
                string parameterReference = token.ToString();
                if (parameterReference.StartsWith(GoalBasedScheduleExtensions.ParameterReference, StringComparison.OrdinalIgnoreCase))
                {
                    string parameterName = parameterReference.Substring(GoalBasedScheduleExtensions.ParameterReference.Length + 1);
                    if (!parameters.ContainsKey(parameterName))
                    {
                        parameters.Add(parameterName, string.Empty);
                    }
                }
            }
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

        private class ImportantKeys
        {
            internal const string WorkloadKey = "metadata.workload";
            internal const string GuestAgentVersionKey = "guestAgentVersion";
            internal const string PayloadPFVersionKey = "metadata.payloadPFVersion";
            internal const string GuestAgentPlatform = "guestAgentPlatform";
            internal const string Experiment = "experiment";
            internal const string Owner = "owner";
        }
    }
}
