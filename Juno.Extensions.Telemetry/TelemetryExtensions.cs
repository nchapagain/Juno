namespace Juno.Extensions.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.CRC.Telemetry.Logging;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Extension methods for <see cref="EventContext"/> instances
    /// and related components.
    /// </summary>
    public static class TelemetryExtensions
    {
        /// <summary>
        /// Extension adds context information defined in the experiment component to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="component">The experiment component.</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, ExperimentComponent component, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (component != null)
            {
                telemetryContext.AddContext(name ?? nameof(component), component);
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the experiment to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="experiment">The experiment.</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, Experiment experiment, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (experiment != null)
            {
                // Clarification:
                // Whereas we could just add the entire experiment to the context, it is important
                // to bear in mind that most telemetry systems have a max size restriction on an individual
                // telemetry event. For example, both ETW as well as Application Insights have a max message
                // size of 65,535 KB. The bigger issue here is that a single context property has a maximum size
                // 8192 chars in Application Insights. To work around this constraint, we have to divide the experiment
                // into different context properties. We are thus distributing the size of the entire experiment object
                // over more than one context property. We definitely want to capture the fundamental identifiers for the
                // experiment object even if other context properties exceed the maximum and are trimmed/corrupted.
                string prefix = name ?? nameof(experiment);
                telemetryContext.AddContext(prefix, new
                {
                    name = experiment.Name,
                    description = experiment.Description,
                    contentVersion = experiment.ContentVersion,
                    schema = experiment.Schema,
                    metadata = experiment.Metadata
                });

                telemetryContext.AddContext($"{prefix}Parameters", experiment.Parameters);
                telemetryContext.AddContext($"{prefix}Workflow", experiment.Workflow.Select(component => new
                {
                    type = component.ComponentType,
                    name = component.Name,
                    group = component.Group
                }));
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the environment entity to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="environmentEntity">The environment entity.</param>
        public static EventContext AddContext(this EventContext telemetryContext, EnvironmentEntity environmentEntity)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (environmentEntity != null)
            {
                telemetryContext.AddContext("experimentGroup", environmentEntity?.EnvironmentGroup);
                environmentEntity.Metadata?.ToList().ForEach(entry => telemetryContext.AddContext(entry.Key, entry.Value));
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the experiment instance to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="experimentInstance">The experiment.</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, Item<Experiment> experimentInstance, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (experimentInstance != null)
            {
                // Clarification:
                // Whereas we could just add the entire experiment to the context, it is important
                // to bear in mind that most telemetry systems have a max size restriction on an individual
                // telemetry event. For example, both ETW as well as Application Insights have a max message
                // size of 65,535 KB. The bigger issue here is that a single context property has a maximum size
                // 8192 chars in Application Insights. To work around this constraint, we have to divide the experiment
                // into different context properties. We are thus distributing the size of the entire experiment object
                // over more than one context property. We definitely want to capture the fundamental identifiers for the
                // experiment object even if other context properties exceed the maximum and are trimmed/corrupted.
                string prefix = name ?? "experimentn";
                Experiment experiment = experimentInstance.Definition;
                telemetryContext.AddContext(prefix, new
                {
                    id = experimentInstance.Id,
                    name = experiment.Name,
                    description = experiment.Description,
                    contentVersion = experiment.ContentVersion,
                    schema = experiment.Schema,
                    metadata = experiment.Metadata,
                    created = experimentInstance.Created,
                    lastModified = experimentInstance.LastModified,
                    _eTag = experimentInstance.GetETag()
                });

                telemetryContext.AddContext($"{prefix}Parameters", experiment.Parameters);
                telemetryContext.AddContext($"{prefix}Workflow", experiment.Workflow.Select(component => new
                {
                    type = component.ComponentType,
                    name = component.Name,
                    group = component.Group
                }));
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the experiment to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="context">The experiment context/metadata.</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, ExperimentMetadata context, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            if (context != null)
            {
                telemetryContext.AddContext(name ?? nameof(context), context);
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the experiment context/metadata instance to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="contextInstance">The experiment context/metadata instance.</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, ExperimentMetadataInstance contextInstance, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            if (contextInstance != null)
            {
                telemetryContext.AddContext(name ?? "contextn", new
                {
                    id = contextInstance.Id,
                    experimentId = contextInstance.Definition.ExperimentId,
                    metadata = contextInstance.Definition.Metadata?.Select(item => new
                    {
                        item.Key
                    }),
                    created = contextInstance.Created,
                    lastModified = contextInstance.LastModified,
                    _eTag = contextInstance.GetETag()
                });
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the experiment step to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="stepInstance">The experiment step.</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, ExperimentStepInstance stepInstance, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (stepInstance != null)
            {
                string prefix = name ?? "stepn";
                if (stepInstance.AgentId != null)
                {
                    telemetryContext.AddContext(prefix, new
                    {
                        id = stepInstance.Id,
                        name = stepInstance.Definition.Name,
                        agentId = stepInstance.AgentId,
                        parentStepId = stepInstance.ParentStepId,
                        experimentId = stepInstance.ExperimentId,
                        experimentGroup = stepInstance.ExperimentGroup,
                        provider = stepInstance.Definition.ComponentType,
                        stepType = stepInstance.StepType.ToString(),
                        status = stepInstance.Status.ToString(),
                        sequence = stepInstance.Sequence,
                        attempts = stepInstance.Attempts,
                        _eTag = stepInstance.GetETag()
                    });
                }
                else
                {
                    telemetryContext.AddContext(prefix, new
                    {
                        id = stepInstance.Id,
                        name = stepInstance.Definition.Name,
                        experimentId = stepInstance.ExperimentId,
                        experimentGroup = stepInstance.ExperimentGroup,
                        provider = stepInstance.Definition.ComponentType,
                        stepType = stepInstance.StepType.ToString(),
                        status = stepInstance.Status.ToString(),
                        sequence = stepInstance.Sequence,
                        attempts = stepInstance.Attempts,
                        _eTag = stepInstance.GetETag()
                    });
                }

                telemetryContext.AddContext($"{prefix}Parameters", stepInstance.Definition.Parameters);
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the experiment to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="stepInstances">The experiment context/metadata.</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, IEnumerable<ExperimentStepInstance> stepInstances, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (stepInstances?.Any() == true)
            {
                string prefix = name ?? "stepsn";
                if (stepInstances.Any(step => step?.AgentId != null))
                {
                    telemetryContext.AddContext(new Dictionary<string, object>
                    {
                        [prefix] = stepInstances.Select(item => new
                        {
                            id = item.Id,
                            name = item.Definition.Name,
                            agentId = item.AgentId,
                            parentStepId = item.ParentStepId,
                            experimentId = item.ExperimentId,
                            experimentGroup = item.ExperimentGroup,
                            provider = item.Definition.ComponentType,
                            stepType = item.StepType.ToString(),
                            status = item.Status.ToString(),
                            sequence = item.Sequence,
                            attempts = item.Attempts,
                            _eTag = item.GetETag()
                        })
                    });
                }
                else
                {
                    telemetryContext.AddContext(new Dictionary<string, object>
                    {
                        [prefix] = stepInstances.Select(item => new
                        {
                            id = item.Id,
                            name = item.Definition.Name,
                            experimentId = item.ExperimentId,
                            experimentGroup = item.ExperimentGroup,
                            provider = item.Definition.ComponentType,
                            stepType = item.StepType.ToString(),
                            status = item.Status.ToString(),
                            sequence = item.Sequence,
                            attempts = item.Attempts,
                            _eTag = item.GetETag()
                        })
                    });
                }
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the diagnostics to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="diagnosticRequest"> The diagnostic request being processed.</param>
        public static EventContext AddContext(this EventContext telemetryContext, DiagnosticsRequest diagnosticRequest)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            diagnosticRequest.ThrowIfNull(nameof(diagnosticRequest));

            if (diagnosticRequest != null)
            {
                telemetryContext.AddContext(nameof(diagnosticRequest), diagnosticRequest.ToString());
                telemetryContext.AddContext("experimentId", diagnosticRequest.ExperimentId);
                telemetryContext.AddContext("diagnosticId", diagnosticRequest.Id);
                telemetryContext.AddContext("diagnosticIssueType", diagnosticRequest.IssueType.ToString());
                telemetryContext.AddContext("queryStart", diagnosticRequest.TimeRangeBegin);
                telemetryContext.AddContext("queryEnd", diagnosticRequest.TimeRangeEnd);
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the agent heartbeat to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="agentId">
        /// Defines the ID of a Juno agent. Note that in the Juno system, the ID of an agent follows a prescriptive format.
        /// For agents that run on physical blades/nodes the format is as follows: "{clusterName},{nodeId},{tipSessionId}".
        /// For agents that run on virtual machines the format is as follows: "{clusterName},{nodeId},{vmName},{tipSessionId}".
        /// </param>
        public static EventContext AddContext(this EventContext telemetryContext, AgentIdentification agentId)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            if (agentId != null)
            {
                telemetryContext.AddContext(nameof(agentId), agentId.ToString());
                telemetryContext.AddContext("agentCluster", agentId.ClusterName);
                telemetryContext.AddContext("agentNodeId", agentId.NodeName);
                telemetryContext.AddContext("agentContextId", agentId.Context);

                if (!string.IsNullOrWhiteSpace(agentId.VirtualMachineName))
                {
                    telemetryContext.AddContext("agentVmName", agentId.VirtualMachineName);
                }
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the agent heartbeat to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="heartbeat">The agent hearbeat.</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, AgentHeartbeat heartbeat, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            if (heartbeat != null)
            {
                telemetryContext.AddContext(name ?? nameof(heartbeat), heartbeat);
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the agent heartbeat instance to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="heartbeatInstance">agent heartbeat instance</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, AgentHeartbeatInstance heartbeatInstance, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            if (heartbeatInstance != null)
            {
                telemetryContext.AddContext(name ?? "heartbeatn", heartbeatInstance);
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the execution result to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="executionResult">The execution result.</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, ExecutionResult executionResult, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            if (executionResult != null)
            {
                telemetryContext.AddContext(name ?? nameof(executionResult), executionResult.Status.ToString());
                if (executionResult.Error != null)
                {
                    if (executionResult.Error != null)
                    {
                        telemetryContext.AddError(executionResult.Error, withCallStack: true);
                    }
                }
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the HTTP response message to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="response">An HTTP response message (e.g. from an API call).</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, HttpResponseMessage response, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            if (response != null)
            {
                telemetryContext.AddContext(name ?? nameof(response), new
                {
                    statusCode = response.StatusCode,
                    reason = response.ReasonPhrase,
                    requestMethod = response.RequestMessage?.Method,
                    requestUri = response.RequestMessage?.RequestUri?.PathAndQuery,
                    timestamp = response.Headers.Date?.DateTime.ToUniversalTime(),
                    content = response.Content?.ReadAsStringAsync().GetAwaiter().GetResult()
                });
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the HTTP response messages to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="responses">A set of HTTP response messages (e.g. from an API call).</param>
        /// <param name="name">Optional parameter allows for an override to the default context property name.</param>
        public static EventContext AddContext(this EventContext telemetryContext, IEnumerable<HttpResponseMessage> responses, string name = null)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            if (responses?.Any() == true)
            {
                telemetryContext.AddContext(name ?? nameof(responses), responses.Where(response => response != null).Select(response => new
                {
                    statusCode = response.StatusCode,
                    reason = response.ReasonPhrase,
                    requestMethod = response.RequestMessage?.Method,
                    requestUri = response.RequestMessage?.RequestUri?.PathAndQuery,
                    timestamp = response.Headers.Date?.DateTime.ToUniversalTime()
                }));
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension adds context information defined in the execution result to the telemetry
        /// <see cref="EventContext"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="context">Provides context information and properties to an experiment provider.</param>
        /// <param name="component">The component that provides the definition of the provider work.</param>
        public static EventContext AddContext(this EventContext telemetryContext, ExperimentContext context, ExperimentComponent component)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            if (context != null)
            {
                telemetryContext
                    .AddContext("experimentId", context?.Experiment?.Id)
                    .AddContext(context?.ExperimentStep)
                    .AddContext(component);
            }

            return telemetryContext;
        }

        /// <summary>
        /// Logs the ScheduleContext and GoalComponent
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="context">Provides context to the scheduler provider instance.</param>
        /// <param name="component">A GoalComponent instance.</param>
        public static EventContext AddContext(this EventContext telemetryContext, ScheduleContext context, GoalComponent component)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            if (context != null)
            {
                telemetryContext
                    .AddContext("id", context.ExecutionGoal.Id)
                    .AddContext("targetGoal", context.TargetGoalTrigger.Name)
                    .AddContext("experimentName", context.ExecutionGoal.Definition.ExperimentName)
                    .AddContext("componentType", component.Type);
            }

            return telemetryContext;
        }

        /// <summary>
        /// Captures the <see cref="TargetGoalTrigger"/> instance in telemetry.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="targetGoals">The target goal instances to capture.</param>
        public static EventContext AddContext(this EventContext telemetryContext, IEnumerable<TargetGoalTrigger> targetGoals)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            targetGoals.ThrowIfNull(nameof(targetGoals));

            telemetryContext.AddContext(nameof(targetGoals), targetGoals.Select(tg => tg.Name));

            return telemetryContext;
        }

        /// <summary>
        /// Logs the ScheduleContext and Goal
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="context">Provides context to the scheduler provider instance.</param>
        /// <param name="goal">A goal instance.</param>
        public static EventContext AddContext(this EventContext telemetryContext, ScheduleContext context, Goal goal)
        {
            {
                telemetryContext.ThrowIfNull(nameof(telemetryContext));
                if (context != null)
                {
                    telemetryContext
                        .AddContext("id", context.ExecutionGoal.Id)
                        .AddContext("targetGoal", context.TargetGoalTrigger.Name)
                        .AddContext("name", context.ExecutionGoal.Definition.ExperimentName)
                        .AddContext("goal", goal.Name);
                }

                return telemetryContext;
            }
        }

        /// <summary>
        /// Extension adds context information defined in the experiment component to the telemetry
        /// <see cref="ExperimentSummary"/> instance.
        /// </summary>
        /// <param name="telemetryContext">The telemetry event context instance.</param>
        /// <param name="summaries">Experiment Summaries.</param>
        public static EventContext AddContext(this EventContext telemetryContext, IEnumerable<ExperimentSummary> summaries)
        {
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            if (summaries != null)
            {
                telemetryContext
                    .AddContext("summariesCount", summaries.Count())
                    .AddContext("experimentName", summaries.FirstOrDefault().ExperimentName)
                    .AddContext("revision", summaries.FirstOrDefault().Revision)
                    .AddContext("progress", summaries.FirstOrDefault().Progress.ToString());
            }

            return telemetryContext;
        }

        /// <summary>
        /// Extension logs the set of experiment steps (grouped by step type) to telemetry
        /// providers defined for the logger.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="eventName">The event name base/prefix.</param>
        /// <param name="telemetryContext">The telemetry event context.</param>
        /// <param name="steps">The steps capture in telemetry.</param>
        public static Task LogTelemetryAsync(this ILogger logger, string eventName, EventContext telemetryContext, IEnumerable<ExperimentStepInstance> steps)
        {
            logger.ThrowIfNull(nameof(logger));
            eventName.ThrowIfNullOrWhiteSpace(nameof(eventName));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));

            Task logTask = Task.CompletedTask;
            if (steps?.Any() == true)
            {
                logTask = logger.LogTelemetryAsync(eventName, LogLevel.Information, telemetryContext.Clone()
                    .AddContext("stepCount", steps?.Count() ?? 0)
                    .AddContext("stepSummary", steps?.Select(step => $"id={step.Id}, name={step.Definition.Name}, status={step.Status.ToString()}"))
                    .AddContext(steps, nameof(steps)));
            }

            return logTask;
        }

        /// <summary>
        /// Extension logs the entries in telemetry events in batches.
        /// </summary>
        /// <param name="logger">Specifies a logger.</param>
        /// <param name="eventName">Specifies the name of the telemetry event.</param>
        /// <param name="telemetryContext">Specifies the context associated with the telemetry event.</param>
        /// <param name="entries">Specifies the entry items.</param>
        /// <param name="batchSize">Specifies the size of batching, depends on the item size. Default is 5</param>
        /// <param name="logLevel">Specifies the log level. Default is Trace.</param>
        /// <returns></returns>
        public static async Task<EventContext> LogTelemetryAsync<T>(this ILogger logger, string eventName, EventContext telemetryContext, IEnumerable<T> entries, int batchSize = 5, LogLevel logLevel = LogLevel.Trace)
        {
            logger.ThrowIfNull(nameof(logger));
            eventName.ThrowIfNullOrWhiteSpace(nameof(eventName));
            telemetryContext.ThrowIfNull(nameof(telemetryContext));
            entries.ThrowIfNull(nameof(entries));
            if (batchSize < 1)
            {
                throw new ArgumentException("The batch size must be greater than 0.", nameof(batchSize));
            }

            var batches = entries
                .Select((entry, index) => new { Index = index, Entry = entry })
                .GroupBy(item => item.Index % ((entries.Count() / batchSize) + 1))
                .Select(groupItem => groupItem.Select(item => item.Entry));

            foreach (IEnumerable<T> batch in batches)
            {
                EventContext relatedContext = telemetryContext.Clone().AddContext("items", batch);
                await logger.LogTelemetryAsync(eventName, logLevel, relatedContext).ConfigureAwait(false);
            }

            return telemetryContext;
        }
    }
}