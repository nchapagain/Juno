namespace Juno
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.Providers;
    using Juno.Providers.Environment;
    using Juno.Providers.Payloads;
    using Juno.Providers.Workloads;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension methods for <see cref="Fixture"/> instances and for general
    /// testing classes.
    /// </summary>
    /// <remarks>
    /// The <see cref="Fixture"/> class is part of a library called Autofixture
    /// which is used to help ease the creation of mock objects that are commonly
    /// used in Juno project tests (e.g. unit, functional).
    ///
    /// Source Code:
    /// https://github.com/AutoFixture/AutoFixture"
    ///
    /// Cheat Sheet:
    /// https://github.com/AutoFixture/AutoFixture/wiki/Cheat-Sheet
    ///
    /// </remarks>
    public static class FixtureExtensions
    {
        private static Random randomGen = new Random();
        private static Guid currentExperimentId = Guid.NewGuid();

        /// <summary>
        /// Creates a mock object using the experiment setup/instructions.
        /// </summary>
        /// <typeparam name="T">The data type of the mock object to create.</typeparam>
        /// <param name="fixture">The fixture.</param>
        /// <param name="setup">Provides setup instructions to use when creating mock <see cref="Experiment"/> instances.</param>
        public static T Create<T>(this Fixture fixture, ExperimentSetup setup)
        {
            fixture.Register(() => setup);
            return fixture.Create<T>();
        }

        /// <summary>
        /// Creates a mock <see cref="HttpResponseMessage"/> object.
        /// </summary>
        /// <param name="fixture">The fixture.</param>
        /// <param name="statusCode">The HTTP status code of the response (e.g. 200/OK).</param>
        /// <param name="content">The content/body of the HTTP response.</param>
        public static HttpResponseMessage CreateHttpResponse(this Fixture fixture, HttpStatusCode statusCode, object content = null)
        {
            HttpResponseMessage mockResponse = new HttpResponseMessage(statusCode);

            if (content != null)
            {
                mockResponse.Content = new StringContent(content.ToJson());
            }

            return mockResponse;
        }

        /// <summary>
        /// Creates a mock <see cref="ExperimentComponent"/> for the type and name provided
        /// (e.g. typeof(MockCriteria).FullName ).
        /// </summary>
        /// <param name="type">The component/provider type.</param>
        /// <param name="name">The name of the experiment.</param>
        /// <param name="group">The experiment environment group for which the component/step is targeted.</param>
        /// <param name="dependencies">One or more dependencies of the component.</param>
        public static ExperimentComponent CreateExperimentComponent(Type type, string name = null, string group = null, IEnumerable<ExperimentComponent> dependencies = null)
        {
            return FixtureExtensions.CreateExperimentComponent(type?.FullName, name, group, dependencies);
        }

        /// <summary>
        /// Creates a mock <see cref="ExperimentComponent"/> for the type and name provided
        /// (e.g. typeof(MockCriteria).FullName ).
        /// </summary>
        /// <param name="type">The component/provider type.</param>
        /// <param name="name">The name of the experiment.</param>
        /// <param name="group">The experiment environment group for which the component/step is targeted.</param>
        /// <param name="dependencies">One or more dependencies of the component.</param>
        public static ExperimentComponent CreateExperimentComponent(string type, string name = null, string group = null, IEnumerable<ExperimentComponent> dependencies = null)
        {
            Guid guid = Guid.NewGuid();

            return new ExperimentComponent(
                type,
                name ?? $"{type} experiment component/step",
                $"A description of {name} {guid.ToString()}",
                group ?? "Group A",
                dependencies: dependencies,
                tags: new Dictionary<string, IConvertible>
                {
                    ["tag1"] = "AnyTag",
                    ["tag2"] = DateTime.UtcNow
                });
        }

        /// <summary>
        /// Creates a mock parallel execution <see cref="ExperimentComponent"/> with the child steps provided.
        /// </summary>
        /// <param name="childComponents">The component/provider type.</param>
        public static ExperimentComponent CreateParallelExecutionExperimentComponent(params ExperimentComponent[] childComponents)
        {
            ExperimentComponent parallelExecution = new ExperimentComponent(
                ExperimentComponent.ParallelExecutionType,
                "Parallel Step Execution",
                "Execute steps in parallel");

            if (childComponents?.Any() == true)
            {
                parallelExecution.AddOrReplaceChildSteps(childComponents);
            }
            else
            {
                parallelExecution.AddOrReplaceChildSteps(
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider)),
                    FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider)));
            }

            return parallelExecution;
        }

        /// <summary>
        /// Creates a mock <see cref="ExperimentStepInstance"/> from registered experiment instance and
        /// the properties provided.
        /// </summary>
        /// <param name="fixture">The mock fixture.</param>
        /// <param name="definition">Optional step definition/component.</param>
        /// <param name="agentId">Optional agent ID.</param>
        /// <param name="parentStepId">Optional parent step ID.</param>
        /// <param name="experimentGroup">Optional environment/experiment group (e.g. Group A, Group B, *).</param>
        /// <param name="sequence">The step sequence.</param>
        /// <param name="status">Defines the status of the experiment step.</param>
        public static ExperimentStepInstance CreateExperimentStep(
            this Fixture fixture, ExperimentComponent definition = null, string agentId = null, string parentStepId = null, string experimentGroup = null, int? sequence = null, ExecutionStatus status = ExecutionStatus.Pending)
        {
            ExperimentInstance experiment = fixture.Create<ExperimentInstance>();
            ExperimentComponent stepDefinition = definition ?? FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), "Environment Setup Step", experimentGroup);

            return new ExperimentStepInstance(
                Guid.NewGuid().ToString(),
                experiment.Id,
                stepDefinition.Group,
                stepDefinition.GetSupportedStepType(),
                status,
                sequence ?? 100,
                0,
                stepDefinition,
                agentId: agentId,
                parentStepId: parentStepId);
        }

        /// <summary>
        /// Registers a full set of mock objects associated with a Juno agents/agent workflows.
        /// </summary>
        /// <param name="fixture">The test/auto fixture.</param>
        /// <returns></returns>
        public static Fixture SetupAgentMocks(this Fixture fixture)
        {
            fixture.Register(() => FixtureExtensions.CreateAgentIdentification());
            fixture.Register(() => FixtureExtensions.CreateAgentHeartbeat());
            fixture.Register(() => FixtureExtensions.CreateAgentHeartbeatInstance());

            return fixture;
        }

        /// <summary>
        /// Registers a full set of mock objects associated with a Juno experiment.
        /// </summary>
        /// <param name="fixture">The test/auto fixture.</param>
        /// <param name="experimentType">The type of experiment (e.g. A/B).</param>
        public static Fixture SetupExperimentMocks(this Fixture fixture, ExperimentType experimentType = ExperimentType.AB)
        {
            // The fixture setup allows the developer to supply instructions to the
            // auto-fixture to affect the way it constructs mock objects. To override the
            // defaults defined here, the developer would simply call the 'Register' function
            // in the individual unit test supplying the fixture setup desired.
            fixture.Register(() => new ExperimentSetup
            {
                ExperimentId = FixtureExtensions.currentExperimentId.ToString(),
                IncludeSharedEnvironmentCriteria = true
            });

            fixture.Register(() => FixtureExtensions.CreateEnvironmentEntity());
            fixture.Register(() => FixtureExtensions.CreateExperiment(experimentType, fixture.Create<ExperimentSetup>()));
            fixture.Register(() => FixtureExtensions.CreateExperimentItem(fixture.Create<Experiment>()));

            // Build the remainder of the mock objects within an experiment from the experiment
            // registered above for consistency.
            fixture.Register(() => fixture.Create<Experiment>().Workflow.First());

            fixture.Register(() => FixtureExtensions.CreateExperimentInstance(fixture.Create<Experiment>(), fixture.Create<ExperimentSetup>()));
            fixture.Register(() => FixtureExtensions.CreateExperimentStep(fixture.Create<ExperimentInstance>()));
            fixture.Register(() => FixtureExtensions.CreateExperimentMetadata(fixture.Create<ExperimentInstance>()));
            fixture.Register(() => FixtureExtensions.CreateExperimentMetadataInstance(fixture.Create<ExperimentMetadata>()));

            fixture.Register(() => FixtureExtensions.CreateAgentIdentification());
            fixture.Register(() => FixtureExtensions.CreateAgentHeartbeat());
            fixture.Register(() => FixtureExtensions.CreateAgentHeartbeatInstance());
            fixture.Register(() => FixtureExtensions.CreateExperimentInstanceStatus());

            return fixture;
        }

        /// <summary>
        /// Registers a set of Mocks associated with the Environment Selection Service
        /// </summary>
        /// <param name="fixture">The test/auto fixture</param>
        /// <returns></returns>
        public static Fixture SetupEnvironmentSelectionMocks(this Fixture fixture)
        {
            fixture.Register(() => FixtureExtensions.CreateEnvironmentFilterInstance());
            fixture.Register(() => FixtureExtensions.CreateEnvironmentCandidateInstance());
            fixture.Register(() => FixtureExtensions.CreateEnvironmentQueryInstance(new List<EnvironmentFilter> { fixture.Create<EnvironmentFilter>() }));
            return fixture;
        }

        /// <summary>
        /// Create a new environment filter object mock
        /// </summary>
        /// <returns><see cref="EnvironmentFilter"/></returns>
        public static EnvironmentFilter CreateEnvironmentFilterInstance()
        {
            return new EnvironmentFilter(
                type: "Juno.EnvironmentSelection.Filters.TestFilter",
                parameters: new Dictionary<string, IConvertible>()
                {
                    ["Parameter1"] = "string",
                    ["Parameter2"] = 12344,
                    ["Parameter3"] = true
                });
        }

        /// <summary>
        /// Create a new environment query with given environment filters
        /// </summary>
        /// <param name="filters">filters to compose the query</param>
        /// <returns></returns>
        public static EnvironmentQuery CreateEnvironmentQueryInstance(IEnumerable<EnvironmentFilter> filters)
        {
            return new EnvironmentQuery(
                "anunsuspectingname",
                8,
                filters,
                NodeAffinity.SameRack,
                new Dictionary<string, IConvertible>()
                {
                    ["Parameter1"] = "string",
                    ["Parameter2"] = 12344,
                    ["Parameter3"] = true
                });
        }

        /// <summary>
        /// Creates a new environment filter with given type but with no parameters
        /// </summary>
        /// <param name="type">The type to assign to the environment filter</param>
        /// <returns><see cref="EnvironmentFilter"/></returns>
        public static EnvironmentFilter CreateEnvironmentFilterFromType(Type type)
        {
            type.ThrowIfNull(nameof(type));

            return new EnvironmentFilter(type: type.FullName);
        }

        /// <summary>
        /// Creates a new environment candidate object mock
        /// </summary>
        /// <returns><see cref="EnvironmentCandidate"/></returns>
        public static EnvironmentCandidate CreateEnvironmentCandidateInstance()
        {
            return new EnvironmentCandidate(
                "subscription",
                "Cluster",
                "Region",
                "machines dont swim",
                "rack",
                "node",
                new List<string> { "vmsku", "vmsku2" },
                "cpuId",
                new Dictionary<string, string>()
                {
                    ["additionalInfo"] = "additionalInfo",
                    ["moreAdditionalInfo"] = "moreAdditionalInfo"
                });
        }

        /// <summary>
        /// Registers a set of Mocks associated with a Juno Goal Based schedule
        /// </summary>
        /// <param name="fixture">The test/auto fixture</param>
        /// <returns></returns>
        public static Fixture SetUpGoalBasedScheduleMocks(this Fixture fixture)
        {
            fixture.SetupExperimentMocks();

            fixture.Register(() => FixtureExtensions.CreatePreConditionInstance());
            fixture.Register(() => FixtureExtensions.CreateScheduleActionInstance());
            fixture.Register(() => FixtureExtensions.CreateGoalInstance(fixture.Create<Precondition>(), fixture.Create<ScheduleAction>()));            
            fixture.Register(() => FixtureExtensions.CreateGoalBasedScheduleInstance(fixture.Create<Goal>(), fixture.Create<Goal>(), fixture.Create<Experiment>()));
            fixture.Register(() => FixtureExtensions.CreateTargetGoalTableEntity());
            fixture.Register(() => FixtureExtensions.CreateTargetGoalTrigger());

            fixture.Register(() => FixtureExtensions.CreateTemplateOverrideInstance());
            fixture.Register(() => FixtureExtensions.CreateExecutionGoalParameter());
            fixture.Register(() => FixtureExtensions.CreateExecutionGoalMetadata());
            return fixture;
        }

        /// <summary>
        /// Creates a mock Template Override for testing purposes
        /// </summary>
        public static TemplateOverride CreateTemplateOverrideInstance()
        {
            return new TemplateOverride(new Dictionary<string, IConvertible>()
            {
                ["Parameter1"] = "AnyValue",
                ["Parameter2"] = 1234,
                ["Parameter3"] = true
            });
        }

        /// <summary>
        /// Creates an EnvironmentEntity object
        /// </summary>
        /// <returns>An Environment Entity object.</returns>
        public static EnvironmentEntity CreateEnvironmentEntityInstance()
        {
            return FixtureExtensions.CreateEnvironmentEntity();
        }

        /// <summary>
        /// Creates a mock ExecutionGoal Metadata template for testing purposes
        /// </summary>
        /// <returns><see cref="ExecutionGoalSummary"/></returns>
        public static ExecutionGoalSummary CreateExecutionGoalMetadata()
        {
            string executionGoal = "$.parameters.executionGoalId";

            ExecutionGoalParameter executionGoalPrams = FixtureExtensions.CreateExecutionGoalParameter();

            return new ExecutionGoalSummary(executionGoal, "description", "teamName", executionGoalPrams);
        }

        /// <summary>
        /// Creates a mock Execution Goal Metadata Parameters for testing purposes
        /// </summary>
        /// <returns><see cref="ExecutionGoalParameter"/></returns>
        public static ExecutionGoalParameter CreateExecutionGoalParameter()
        {
            string executionGoal = Guid.NewGuid().ToString();

            var targetGoalPram = new List<TargetGoalParameter>()
            {
                new TargetGoalParameter("1", "WorkloadA", new Dictionary<string, IConvertible>() { ["targetGoal1"] = Guid.NewGuid().ToString() }),
                new TargetGoalParameter("2", "WorkloadA", new Dictionary<string, IConvertible>() { ["targetGoal2"] = Guid.NewGuid().ToString() })
            };

            return new ExecutionGoalParameter(executionGoal, "ExperimentName", "joe@microsoft.com", true, targetGoalPram);
        }

        /// <summary>
        /// Creates a new experiment Instance Status object mock
        /// </summary>
        /// <returns><see cref="ExperimentInstanceStatus"/></returns>
        public static ExperimentInstanceStatus CreateExperimentInstanceStatus()
        {
            return new ExperimentInstanceStatus(
                "experimentId",
                "experimentName",
                ExperimentStatus.Succeeded,
                "juno-dev01",
                "executionGoal",
                "targetGoal",
                ImpactType.Impactful,
                DateTime.UtcNow.AddDays(-2),
                DateTime.UtcNow);
        }

        /// <summary>
        /// Creates a Precondition Component with given type
        /// </summary>
        /// <param name="preconditionType">The type that should be assigned to the Precondition</param>
        /// <returns>A precondition with given type</returns>
        public static Precondition CreatePreconditionComponent(Type preconditionType)
        {
            return FixtureExtensions.CreatePreconditionComponent(preconditionType?.FullName);
        }

        /// <summary>
        /// Creates a Precondition Component with given type
        /// </summary>
        /// <param name="preconditionType">The type that should be assigned to the Precondition</param>
        /// <returns>A precondition with given type</returns>
        public static Precondition CreatePreconditionComponent(string preconditionType)
        {
            return new Precondition(
               type: preconditionType ?? "PreconditionTest",
               parameters: new Dictionary<string, IConvertible>
               {
                   ["cronExpression"] = "*/20 * * * *",
                   ["Parameter1"] = "AnyValue",
                   ["Parameter2"] = 1234,
                   ["Parameter3"] = true
               });
        }

        /// <summary>
        /// Creates a ScheduleAction component with given type
        /// </summary>
        /// <param name="scheduleActionType">The type that should be assigned to the Schedule Action</param>
        /// <returns>A Schedule Action with given type</returns>
        public static ScheduleAction CreateScheduleActionComponent(Type scheduleActionType)
        {
            return FixtureExtensions.CreateScheduleActionComponent(scheduleActionType?.FullName);
        }

        /// <summary>
        /// Creates a ScheduleAction component with given type
        /// </summary>
        /// <param name="scheduleActionType">The type that should be assigned to the Schedule Action</param>
        /// <returns>A Schedule Action with given type</returns>
        public static ScheduleAction CreateScheduleActionComponent(string scheduleActionType)
        {
            return new ScheduleAction(
                type: scheduleActionType ?? "ScheduleActionTest",
                parameters: new Dictionary<string, IConvertible>
                {
                    ["Parameter1"] = "AnyValue",
                    ["Parameter2"] = 1234,
                    ["Parameter3"] = true,
                    ["metadata.workload"] = "WorkloadA",
                    ["guestAgentPlatform"] = "platformB",
                    ["guestAgentVersion"] = "GA1.1",
                    ["metadata.payloadPFVersion"] = "1.0.1"
                });
        }

        /// <summary>
        /// Create a mock TargetGoalTableEntity
        /// </summary>
        /// <returns><see cref="TargetGoalTableEntity"/></returns>
        public static TargetGoalTableEntity CreateTargetGoalTableEntity()
        {
            return new TargetGoalTableEntity()
            {
                PartitionKey = "Version",
                RowKey = "TargetGoal",
                Id = "TargetGoal",
                ExperimentName = "ExperimentName",
                TeamName = "TeamName",
                ExecutionGoal = "ExecutionGoal",
                CronExpression = "*/1 * * * * *",
                Enabled = true
            };
        }

        /// <summary>
        /// Create a mock TargetGoalTrigger
        /// </summary>
        /// <returns><see cref="TargetGoalTrigger"/></returns>
        public static TargetGoalTrigger CreateTargetGoalTrigger()
        {
            return new TargetGoalTrigger(
                executionGoal: "ExecutionGoal",
                id: "TargetGoal",
                experimentName: "ExperimentName",
                teamName: "TeamName",
                version: "Version",
                cronExpression: "*/1 * * * * *",
                targetGoal: "TargetGoal",
                enabled: true,
                created: DateTime.UtcNow,
                lastModified: DateTime.UtcNow);
        }

        /// <summary>
        /// Give the ability to construct a GoalBasedSchedule with 
        /// custom parameters while leaving the rest default
        /// </summary>
        /// <param name="experimentName">name of experiment</param>
        /// <param name="executionGoalId">Id of experiment goal</param>
        /// <param name="name">Execution Goal Name</param>
        /// <param name="teamName">Name of team whom owns the execution goal</param>
        /// <param name="description">description of the execution goal</param>
        /// <param name="metaData">meta data of the execution goal</param>
        /// <param name="enabled">wheter or not the execution goal is enabled</param>
        /// <param name="version">version of execution goal</param>
        /// <param name="experiment">experiment item</param>
        /// <param name="targetGoals">list of target goals</param>
        /// <param name="controlGoals">list of control goals</param>
        /// <returns></returns>
        public static GoalBasedSchedule CreateExecutionGoalFromTemplate(
            string experimentName = null,
            string executionGoalId = null,
            string name = null,
            string teamName = null,
            string description = null,
            Dictionary<string, IConvertible> metaData = null,
            bool? enabled = null,
            string version = null,
            Experiment experiment = null,
            List<Goal> targetGoals = null,
            List<Goal> controlGoals = null)
        {
            ExperimentSetup experimentSetup = new ExperimentSetup()
            {
                ExperimentId = Guid.NewGuid().ToString(),
                IncludeSharedEnvironmentCriteria = true
            };

            GoalBasedSchedule template = FixtureExtensions.CreateGoalBasedScheduleInstance(
                FixtureExtensions.CreateTargetGoal(),
                FixtureExtensions.CreateGoalInstance(
                    FixtureExtensions.CreatePreconditionComponent("ControlPrecondition"),
                    FixtureExtensions.CreateScheduleActionComponent("ControlAction")),
                FixtureExtensions.CreateExperiment(ExperimentType.AB, experimentSetup));

            return new GoalBasedSchedule(
                experimentName: experimentName ?? template.ExperimentName,
                executionGoalId: executionGoalId ?? template.ExecutionGoalId,
                name: name ?? template.Name,
                teamName: teamName ?? template.TeamName,
                description: description ?? template.Description,
                metaData: metaData ?? template.ScheduleMetadata,
                enabled: enabled ?? template.Enabled,
                version: version ?? template.Version,
                experiment: experiment ?? template.Experiment,
                targetGoals: targetGoals ?? template.TargetGoals,
                controlGoals: controlGoals ?? template.ControlGoals);
        }

        /// <summary>
        /// 
        /// </summary>
        public static GoalBasedSchedule CreateExecutionGoalTemplate()
        {
            ExperimentSetup experimentSetup = new ExperimentSetup()
            {
                ExperimentId = Guid.NewGuid().ToString(),
                IncludeSharedEnvironmentCriteria = true
            };

            return new GoalBasedSchedule(
                experimentName: "ExperimentName",
                executionGoalId: "$.parameters.executionGoalId",
                name: "ExecutionGoalTemplate",
                teamName: "teamName",
                description: "description",
                metaData: new Dictionary<string, IConvertible>() { ["owner"] = "$.parameter.owner" },
                enabled: true,
                version: "2021-01-01",
                experiment: FixtureExtensions.CreateExperiment(ExperimentType.AB, experimentSetup),
                targetGoals: new List<Goal>()
                {
                    FixtureExtensions.CreateTargetGoal("$.parameters.targetGoal1"),
                    FixtureExtensions.CreateTargetGoal("$.parameters.targetGoal2")
                },
                controlGoals: new List<Goal>()
                {
                    new Goal(
                        name: "ControlGoal",
                        new List<Precondition>()
                        {
                            new Precondition(type: "preconditionOne", new Dictionary<string, IConvertible>())
                        },
                        new List<ScheduleAction>() { FixtureExtensions.CreateScheduleActionComponent("type") })
                });
        }

        /// <summary>
        /// Create a table entity from execution goal and target goal
        /// </summary>
        /// <param name="executionGoal"><see cref="GoalBasedSchedule"/></param>
        /// <param name="targetGoal"><see cref="Goal"/></param>
        /// <param name="cronExpression">Cron Expression to express frequency</param>
        /// <returns></returns>
        public static TargetGoalTableEntity CreateTargetTableEntityFromTemplates(GoalBasedSchedule executionGoal, Goal targetGoal, string cronExpression = null)
        {
            executionGoal.ThrowIfNull(nameof(executionGoal));
            targetGoal.ThrowIfNull(nameof(targetGoal));

            return new TargetGoalTableEntity
            {
                Id = targetGoal.Name,
                PartitionKey = executionGoal.Version,
                RowKey = targetGoal.Name,
                CronExpression = cronExpression ?? "* * * * *",
                Enabled = executionGoal.Enabled,
                ExperimentName = executionGoal.ExperimentName,
                TeamName = executionGoal.TeamName,
                ExecutionGoal = executionGoal.ExecutionGoalId,
                Created = DateTime.UtcNow.AddSeconds(-10),
                Timestamp = DateTime.UtcNow.AddSeconds(-10),
            };
        }

        /// <summary>
        /// Creates a valid Target goal
        /// </summary>
        /// <returns></returns>
        public static Goal CreateTargetGoal(string name = null, string id = null)
        {
            return new Goal(
                name: name ?? "TargetGoal1",
                preconditions: new List<Precondition>()
                    {
                        new Precondition(
                            type: ContractExtension.TimerTriggerType,
                            parameters: new Dictionary<string, IConvertible>()
                            {
                                [ContractExtension.CronExpression] = "* * * * *"
                            })
                    },
                actions: new List<ScheduleAction>() { FixtureExtensions.CreateScheduleActionInstance() },
                id: id ?? Guid.NewGuid().ToString());
        }

        private static Precondition CreatePreConditionInstance()
        {
            return FixtureExtensions.CreatePreconditionComponent(typeof(PreconditionProvider));
        }

        private static ScheduleAction CreateScheduleActionInstance()
        {
            return FixtureExtensions.CreateScheduleActionComponent(typeof(ScheduleActionProvider));
        }

        private static Goal CreateGoalInstance(Precondition precondition, ScheduleAction scheduleAction)
        {
            List<Precondition> preconditions = new List<Precondition> { precondition };
            List<ScheduleAction> actions = new List<ScheduleAction> { scheduleAction };
            return new Goal(
                name: "Goal Name",
                preconditions: preconditions,
                actions: actions);
        }

        private static GoalBasedSchedule CreateGoalBasedScheduleInstance(Goal targetGoal, Goal controlGoal, Experiment experiment)
        {
            List<Goal> targetGoals = new List<Goal> { targetGoal };
            List<Goal> controlGoals = new List<Goal> { controlGoal };
            return new GoalBasedSchedule(
                experimentName: "MockExperiment",
                executionGoalId: "MockExecutionGoal.json",
                name: "Mock Schedule",
                teamName: "TeamName",
                description: "A Schedule, but for testing",
                metaData: new Dictionary<string, IConvertible>
                {
                    ["Parmaeter1"] = "value1",
                    ["Parameter2"] = "value2",
                    ["Parameter3"] = "value3",
                    ["Parameter4"] = false
                },
                enabled: true,
                version: "2021-01-01",
                experiment: experiment,
                targetGoals: targetGoals,
                controlGoals: controlGoals);
        }

        private static EnvironmentEntity CreateEnvironmentEntity()
        {
            return EnvironmentEntity.Cluster($"Cluster{Guid.NewGuid()}", "Group A", new Dictionary<string, IConvertible>
            {
                ["Region"] = "WestUS2",
                ["DataCenter"] = "DataCenter1234"
            });
        }

        private static AgentIdentification CreateAgentIdentification()
        {
            return new AgentIdentification("Cluster_01", "Node_01", "VM-01", Guid.NewGuid().ToString());
        }

        private static AgentHeartbeat CreateAgentHeartbeat()
        {
            return new AgentHeartbeat(FixtureExtensions.CreateAgentIdentification(), AgentHeartbeatStatus.Running, AgentType.GuestAgent);
        }

        private static AgentHeartbeatInstance CreateAgentHeartbeatInstance()
        {
            return new AgentHeartbeatInstance(
                Guid.NewGuid().ToString(),
                FixtureExtensions.CreateAgentIdentification().ToString(),
                AgentHeartbeatStatus.Running,
                AgentType.GuestAgent,
                DateTime.UtcNow,
                DateTime.UtcNow);
        }

        private static Experiment CreateExperiment(ExperimentType experimentType, ExperimentSetup setup)
        {
            List<ExperimentComponent> workflowSteps = new List<ExperimentComponent>();
            char[] groups = experimentType.ToString().ToCharArray();

            // EnvironmentSetup/Criteria steps
            foreach (char group in groups)
            {
                workflowSteps.Add(FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), $"Group {group} Environment Criteria", $"Group {group}"));
            }

            // Additional EnvironmentSetup steps
            foreach (char group in groups)
            {
                workflowSteps.Add(FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), $"Group {group} Environment Setup 1", $"Group {group}"));
            }

            // Additional EnvironmentSetup steps
            foreach (char group in groups)
            {
                workflowSteps.Add(FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), $"Group {group} Environment Setup 2", $"Group {group}"));
            }

            // Payload steps
            foreach (char group in groups.Skip(1))
            {
                workflowSteps.Add(FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider), $"Group {group} Payload", $"Group {group}"));
            }

            // Workload steps
            foreach (char group in groups)
            {
                workflowSteps.Add(FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), $"Group {group} Workload", $"Group {group}"));
            }

            // EnvironmentCleanup steps
            foreach (char group in groups)
            {
                workflowSteps.Add(FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), $"Group {group} Environment Cleanup", $"Group {group}"));
            }

            return new Experiment(
                $"{experimentType.ToString()} Experiment",
                $"A standard {experimentType.ToString()} experiment {setup.ExperimentId ?? Guid.NewGuid().ToString()}",
                "1.0.0",
                new Dictionary<string, IConvertible>
                {
                    ["teamName"] = "Any Team",
                    ["email"] = "anyTeam@somewhere.anywhere.com",
                    ["owners"] = "larry;curly;moe;jennifer",

                    // ALL experiments require the following metadata properties
                    ["experimentType"] = "Test",
                    ["generation"] = "Any",
                    ["nodeCpuId"] = "12345",
                    ["payload"] = "MCU3030.1",
                    ["payloadType"] = "Microcode Update",
                    ["payloadVersion"] = "30b123",
                    ["payloadPFVersion"] = "10.20.1596.22",
                    ["workload"] = "PERF-CPU-V1",
                    ["workloadType"] = "VirtualClient",
                    ["workloadVersion"] = "1.0.9876.11",
                    ["impactType"] = "None"
                },
                new Dictionary<string, IConvertible>
                {
                    ["Parameter1"] = "AnyValue",
                    ["Parameter2"] = 1234,
                    ["Parameter3"] = true
                },
                workflowSteps);
        }

        private static ExperimentItem CreateExperimentItem(Experiment experiment)
        {
            return new ExperimentItem(Guid.NewGuid().ToString(), experiment);
        }

        private static ExperimentInstance CreateExperimentInstance(Experiment experiment, ExperimentSetup setup)
        {
            return new ExperimentInstance(setup.ExperimentId, experiment);
        }

        private static ExperimentMetadata CreateExperimentMetadata(ExperimentInstance experiment)
        {
            // Note:
            // The experiment ID of the instance is defined by the ExperimentSetup registered for the
            // Fixture/extensions. This ensures that all objects that reference an experiment use the same
            // ID for the experiment.
            return new ExperimentMetadata(
                experiment.Id.ToString(),
                new Dictionary<string, IConvertible>
                {
                    ["QueueName"] = $"Queue-{experiment.Id}",
                    ["Property2"] = Guid.NewGuid().ToString()
                });
        }

        private static ExperimentMetadataInstance CreateExperimentMetadataInstance(ExperimentMetadata metadata)
        {
            ExperimentMetadataInstance instance = new ExperimentMetadataInstance(Guid.NewGuid().ToString(), metadata);
            instance.Extensions.Add("messageId", "someId");
            instance.Extensions.Add("popReceipt", "someReceipt");
            return instance;
        }

        private static ExperimentStepInstance CreateExperimentStep(ExperimentInstance experiment)
        {
            // Note:
            // The experiment ID of the instance is defined by the ExperimentSetup registered for the
            // Fixture/extensions. This ensures that all objects that reference an experiment use the same
            // ID for the experiment.
            ExperimentComponent component = experiment.Definition.Workflow.First();

            ExperimentStepInstance step = new ExperimentStepInstance(
                Guid.NewGuid().ToString(),
                experiment.Id,
                "Group A",
                component.GetSupportedStepType(),
                ExecutionStatus.Succeeded,
                FixtureExtensions.randomGen.Next(100, 1000),
                attempts: 1,
                definition: component,
                startTime: DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)),
                endTime: DateTime.UtcNow);

            return step;
        }
    }
}