namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Providers.Environment;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NuGet.Protocol;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentAgentMonitoringProviderExtensionsTests
    {
        private ProviderFixture mockFixture;
        private IEnumerable<ExperimentComponent> mockAgentComponents;
        private IEnumerable<ExperimentStepInstance> mockAgentSteps;
        private IEnumerable<EnvironmentEntity> mockAgentEntities;
        private TestVmStepProvider provider;
        private ExperimentAgentMonitoringProviderExtensions.AgentStepMonitorState state;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(TestVmStepProvider));

            // Valid child/agent steps (e.g. they target agents).
            this.mockAgentComponents = new List<ExperimentComponent>
            {
                FixtureExtensions.CreateExperimentComponent(typeof(TestVmStepProvider)),
                FixtureExtensions.CreateExperimentComponent(typeof(TestVmStepProvider)),
                FixtureExtensions.CreateExperimentComponent(typeof(TestNodeStepProvider))
            };

            this.mockAgentSteps = new List<ExperimentStepInstance>
            {
                // In typical experiment scenarios, there will often be more than 1 VM/agent on a node for a
                // given TiP session.
                this.mockFixture.CreateExperimentStep(
                    this.mockAgentComponents.ElementAt(0),
                    agentId: "Cluster01,Node01,VM01,Tip01",
                    parentStepId: this.mockFixture.Context.ExperimentStep.Id),

                this.mockFixture.CreateExperimentStep(
                    this.mockAgentComponents.ElementAt(1),
                    agentId: "Cluster01,Node01,VM02,Tip01",
                    parentStepId: this.mockFixture.Context.ExperimentStep.Id),

                // In typical experiment scenarios, there will be a single node/agent associated with a given
                // TiP session.
                this.mockFixture.CreateExperimentStep(
                    this.mockAgentComponents.ElementAt(2),
                    agentId: "Cluster01,Node01,Tip01",
                    parentStepId: this.mockFixture.Context.ExperimentStep.Id)
            };

            this.mockAgentEntities = new List<EnvironmentEntity>
            {
                 EnvironmentEntity.VirtualMachine("VM01", "Node01", this.mockAgentSteps.ElementAt(0).ExperimentGroup, new Dictionary<string, IConvertible>
                 {
                     // Cluster01,Node01,VM01,Tip01
                     ["agentId"] = this.mockAgentSteps.ElementAt(0).AgentId
                 }),
                 EnvironmentEntity.VirtualMachine("VM02", "Node01", this.mockAgentSteps.ElementAt(1).ExperimentGroup, new Dictionary<string, IConvertible>
                 {
                     // Cluster01,Node01,VM02,Tip01
                     ["agentId"] = this.mockAgentSteps.ElementAt(1).AgentId
                 }),
                 EnvironmentEntity.Node("Node01", "Tip01", this.mockAgentSteps.ElementAt(2).ExperimentGroup, new Dictionary<string, IConvertible>
                 {
                     // Cluster01,Node01,Tip01
                     ["agentId"] = this.mockAgentSteps.ElementAt(2).AgentId
                 })
            };

            IServiceCollection providerServices = new ServiceCollection()
                .AddSingleton<IProviderDataClient>(this.mockFixture.DataClient.Object);

            this.provider = new TestVmStepProvider(providerServices);
            this.state = new ExperimentAgentMonitoringProviderExtensions.AgentStepMonitorState();
        }

        [Test]
        public void CreateAgentStepsAsyncExtensionValidatesThatTheParentStepHasChildStepsDefined()
        {
            ExperimentComponent invalidParentComponent = this.mockFixture.Create<ExperimentComponent>();
            invalidParentComponent.Extensions.Clear();

            ProviderException error = Assert.Throws<ProviderException>(
                () => this.provider.CreateAgentStepsAsync(this.mockFixture.Context, invalidParentComponent, CancellationToken.None)
                .GetAwaiter().GetResult());

            Assert.IsNotNull(error);
            Assert.AreEqual(ErrorReason.SchemaInvalid, error.Reason);
        }

        [Test]
        public void CreateAgentStepsAsyncExtensionDoesNotAllowStepsNotTargetedForAgentsToBeCreated()
        {
            // The agent component targets a provider that is NOT targeted to run on an
            // agent
            ExperimentComponent invalidAgentComponent = FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider));
            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(invalidAgentComponent);

            ProviderException error = Assert.Throws<ProviderException>(
                () => this.provider.CreateAgentStepsAsync(this.mockFixture.Context, parentComponent, CancellationToken.None)
                .GetAwaiter().GetResult());

            Assert.IsNotNull(error);
            Assert.AreEqual(ErrorReason.ProviderDefinitionInvalid, error.Reason);
        }

        [Test]
        public void CreateAgentStepsAsyncExtensionThrowsIfMatchingTargetAgentsAreNotDefined()
        {
            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            this.mockFixture.DataClient
              .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                  It.IsAny<string>(),
                  ContractExtension.EntitiesProvisioned,
                  It.IsAny<CancellationToken>(),
                  It.IsAny<string>()))
              .Returns(Task.FromResult(null as IEnumerable<EnvironmentEntity>));

            ProviderException error = Assert.Throws<ProviderException>(
                () => this.provider.CreateAgentStepsAsync(this.mockFixture.Context, parentComponent, CancellationToken.None)
                .GetAwaiter().GetResult());

            Assert.IsNotNull(error);
            Assert.AreEqual(ErrorReason.TargetAgentsNotFound, error.Reason);
        }

        [Test]
        public void CreateAgentStepsAsyncExtensionCreatesTheExpectedAgentStepsForTheParentStepForStepsTargetedAtVirtualMachineAgents()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition); // A VM-targeted step

            int stepsCreated = 0;
            this.mockFixture.DataClient
                .Setup(client => client.CreateAgentStepsAsync(
                    this.mockFixture.Context.ExperimentStep,
                    It.IsAny<ExperimentComponent>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ExperimentStepInstance, ExperimentComponent, string, CancellationToken>((parentStep, agentStep, agentId, token) =>
                {
                    Assert.IsTrue(this.mockAgentSteps.First().Definition.Equals(agentStep));
                    Assert.IsTrue(this.mockAgentSteps.ElementAt(stepsCreated).AgentId == agentId);
                    Assert.IsTrue(this.mockAgentSteps.ElementAt(stepsCreated).ParentStepId == parentStep.Id);
                    stepsCreated++;
                })
                .Returns(Task.FromResult(this.mockAgentSteps.Take(2)));

            this.provider.CreateAgentStepsAsync(this.mockFixture.Context, parentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(stepsCreated == 2);
        }

        [Test]
        public void CreateAgentStepsAsyncExtensionCreatesTheExpectedAgentStepsForTheParentStepForStepsTargetedAtPhysicalNodeAgents()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.Last().Definition); // A Node-targeted step

            int stepsCreated = 0;
            this.mockFixture.DataClient
                .Setup(client => client.CreateAgentStepsAsync(
                    this.mockFixture.Context.ExperimentStep,
                    It.IsAny<ExperimentComponent>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ExperimentStepInstance, ExperimentComponent, string, CancellationToken>((parentStep, agentStep, agentId, token) =>
                {
                    Assert.IsTrue(this.mockAgentSteps.Last().Definition.Equals(agentStep));
                    Assert.IsTrue(this.mockAgentSteps.Last().AgentId == agentId);
                    Assert.IsTrue(this.mockAgentSteps.Last().ParentStepId == parentStep.Id);
                    stepsCreated++;
                })
                .Returns(Task.FromResult(this.mockAgentSteps.TakeLast(1)));

            this.provider.CreateAgentStepsAsync(this.mockFixture.Context, parentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(stepsCreated == 1);
        }

        [Test]
        public void CreateAgentStepsAsyncExtensionUsesTheAgentIdOfTheVmEntitiesForStepsTargetedAtVirtualMachineAgents()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition); // A VM-targeted step

            int stepsCreated = 0;
            this.mockFixture.DataClient
                .Setup(client => client.CreateAgentStepsAsync(
                    this.mockFixture.Context.ExperimentStep,
                    It.IsAny<ExperimentComponent>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ExperimentStepInstance, ExperimentComponent, string, CancellationToken>((parentStep, agentStep, agentId, token) =>
                {
                    Assert.IsTrue(this.mockAgentSteps.ElementAt(stepsCreated).AgentId == agentId);
                    stepsCreated++;
                })
                .Returns(Task.FromResult(this.mockAgentSteps.Take(2)));

            this.provider.CreateAgentStepsAsync(this.mockFixture.Context, parentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(stepsCreated > 0);
        }

        [Test]
        public void CreateAgentStepsAsyncExtensionUsesTheAgentIdOfTheNodeEntitiesForStepsTargetedAtPhysicalNodeAgents()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.Last().Definition); // A Node-targeted step

            int stepsCreated = 0;
            this.mockFixture.DataClient
                .Setup(client => client.CreateAgentStepsAsync(
                    this.mockFixture.Context.ExperimentStep,
                    It.IsAny<ExperimentComponent>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ExperimentStepInstance, ExperimentComponent, string, CancellationToken>((parentStep, agentStep, agentId, token) =>
                {
                    Assert.IsTrue(this.mockAgentSteps.Last().AgentId == agentId);
                    stepsCreated++;
                })
                .Returns(Task.FromResult(this.mockAgentSteps.TakeLast(1)));

            this.provider.CreateAgentStepsAsync(this.mockFixture.Context, parentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(stepsCreated == 1);
        }

        [Test]
        public async Task AgentMonitoringProvidersCreateAgentStepsFirst()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            this.state.AgentStepsCreated = false;
            await this.provider.ExecuteMonitoringStepsAsync(this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None);

            this.mockFixture.DataClient.Verify(client => client.CreateAgentStepsAsync(
                It.IsAny<ExperimentStepInstance>(),
                It.IsAny<ExperimentComponent>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Test]
        public async Task AgentMonitoringProvidersEvaluateTheExecutionStatusOfAgentStepsAsExpected_Scenario1()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            // Scenario:
            // All agent steps are in a Pending status.
            this.mockAgentSteps.ToList().ForEach(step => step.Status = ExecutionStatus.Pending);

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            this.state.AgentStepsCreated = true;
            ExecutionResult result = await this.provider.ExecuteMonitoringStepsAsync(this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
        }

        [Test]
        public async Task AgentMonitoringProvidersEvaluateTheExecutionStatusOfAgentStepsAsExpected_Scenario2()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            // Scenario:
            // All agent steps are in a InProgress status.
            this.mockAgentSteps.ToList().ForEach(step => step.Status = ExecutionStatus.InProgress);

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            this.state.AgentStepsCreated = true;
            ExecutionResult result = await this.provider.ExecuteMonitoringStepsAsync(this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
        }

        [Test]
        public async Task AgentMonitoringProvidersEvaluateTheExecutionStatusOfAgentStepsAsExpected_Scenario3()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            // Scenario:
            // All agent steps are in an InProgressContinue status.
            this.mockAgentSteps.ToList().ForEach(step => step.Status = ExecutionStatus.InProgress);

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            this.state.AgentStepsCreated = true;
            ExecutionResult result = await this.provider.ExecuteMonitoringStepsAsync(this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
        }

        [Test]
        public async Task AgentMonitoringProvidersEvaluateTheExecutionStatusOfAgentStepsAsExpected_Scenario4()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            // Scenario:
            // All agent steps are in an Succeeded status.
            this.mockAgentSteps.ToList().ForEach(step => step.Status = ExecutionStatus.Succeeded);

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            this.state.AgentStepsCreated = true;
            ExecutionResult result = await this.provider.ExecuteMonitoringStepsAsync(this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public async Task AgentMonitoringProvidersEvaluateTheExecutionStatusOfAgentStepsAsExpected_Scenario5()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            // Scenario:
            // All agent steps are in an Failed status.
            this.mockAgentSteps.ToList().ForEach(step => step.Status = ExecutionStatus.Failed);

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            this.state.AgentStepsCreated = true;
            ExecutionResult result = await this.provider.ExecuteMonitoringStepsAsync(this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
        }

        [Test]
        public async Task AgentMonitoringProvidersEvaluateTheExecutionStatusOfAgentStepsAsExpected_Scenario6()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            // Scenario:
            // All agent steps are in an Cancelled status.
            this.mockAgentSteps.ToList().ForEach(step => step.Status = ExecutionStatus.Cancelled);

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            this.state.AgentStepsCreated = true;
            ExecutionResult result = await this.provider.ExecuteMonitoringStepsAsync(this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Cancelled);
        }

        [Test]
        public async Task AgentMonitoringProvidersEvaluateTheExecutionStatusOfAgentStepsAsExpected_Scenario7()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            // Scenario:
            // Some agent steps are Pending, some InProgress
            this.mockAgentSteps.ElementAt(0).Status = ExecutionStatus.InProgress;
            this.mockAgentSteps.ElementAt(0).Status = ExecutionStatus.Pending;

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            this.state.AgentStepsCreated = true;
            ExecutionResult result = await this.provider.ExecuteMonitoringStepsAsync(this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
        }

        [Test]
        public async Task AgentMonitoringProvidersEvaluateTheExecutionStatusOfAgentStepsAsExpected_Scenario8()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            // Scenario:
            // Some agent steps are Succeeded, some InProgress
            this.mockAgentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockAgentSteps.ElementAt(0).Status = ExecutionStatus.InProgress;

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            this.state.AgentStepsCreated = true;
            ExecutionResult result = await this.provider.ExecuteMonitoringStepsAsync(this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
        }

        [Test]
        public async Task AgentMonitoringProvidersEvaluateTheExecutionStatusOfAgentStepsAsExpected_Scenario9()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            // Scenario:
            // Some agent steps are Succeeded, some InProgressContinue
            this.mockAgentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockAgentSteps.ElementAt(0).Status = ExecutionStatus.InProgressContinue;

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            this.state.AgentStepsCreated = true;
            ExecutionResult result = await this.provider.ExecuteMonitoringStepsAsync(this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgressContinue);
        }

        [Test]
        public async Task AgentMonitoringProvidersEvaluateTheExecutionStatusOfAgentStepsAsExpected_Scenario10()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            // Scenario:
            // Some agent steps are Succeeded, some InProgressContinue
            this.mockAgentSteps.ElementAt(0).Status = ExecutionStatus.Succeeded;
            this.mockAgentSteps.ElementAt(0).Status = ExecutionStatus.Failed;

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            this.state.AgentStepsCreated = true;
            ExecutionResult result = await this.provider.ExecuteMonitoringStepsAsync(this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
        }

        [Test]
        public void AgentMonitoringProvidersThrowWhenTheAgentStepsHaveNotCompletedWithinASpecifiedTimeout()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            // Force a step timeout.
            this.state.AgentStepsTimeout = DateTime.UtcNow;

            this.state.AgentStepsCreated = true;
            Assert.Throws<TimeoutException>(() => this.provider.ExecuteMonitoringStepsAsync(
                this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None)
                .GetAwaiter().GetResult());
        }

        [Test]
        public void AgentMonitoringProvidersHandlesStepTimeoutsAsExpectedWhenMinStepsCompletedInstructionsAreApplied()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            // Apply a min steps completed timeout restriction
            parentComponent.Parameters[StepParameters.TimeoutMinStepsSucceeded] = this.mockAgentSteps.Count() - 1;

            // Force a step timeout.
            this.state.AgentStepsTimeout = DateTime.UtcNow;
            this.state.AgentStepsCreated = true;

            // Ensure all but one step is succeeded.
            this.mockAgentSteps.ForEach(step => step.Status = ExecutionStatus.Succeeded);
            this.mockAgentSteps.First().Status = ExecutionStatus.InProgressContinue;

            ExecutionResult result = null;
            Assert.DoesNotThrow(() => result = this.provider.ExecuteMonitoringStepsAsync(
                this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None)
                .GetAwaiter().GetResult());

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public void AgentMonitoringProvidersHandlesStepTimeoutsHandleSerializationDeserializationTypeConversionsAssociatedWithMinStepsCompletedInstructions()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            // Apply a min steps completed timeout restriction
            parentComponent.Parameters[StepParameters.TimeoutMinStepsSucceeded] = this.mockAgentSteps.Count() - 1;

            // Serialize/deserialize to force data type conversions (e.g. Int32 -> Int64) that occur in
            // Newtonsoft translations.
            parentComponent = parentComponent.ToJson().FromJson<ExperimentComponent>();

            // Force a step timeout.
            this.state.AgentStepsTimeout = DateTime.UtcNow;
            this.state.AgentStepsCreated = true;

            // Ensure at least one step is not succeeded
            this.mockAgentSteps.ForEach(step => step.Status = ExecutionStatus.Succeeded);
            this.mockAgentSteps.First().Status = ExecutionStatus.InProgressContinue;

            Assert.DoesNotThrow(() => this.provider.ExecuteMonitoringStepsAsync(
                this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None)
                .GetAwaiter().GetResult());
        }

        [Test]
        public void AgentMonitoringProvidersHandlesStepTimeoutsAsExpectedWhenMinStepsCompletedInstructionsAreApplied_AllStepsMustHaveStarted()
        {
            this.SetupMonitoringProviderDefaultBehaviors();

            ExperimentComponent parentComponent = this.mockFixture.Create<ExperimentComponent>()
                .AddOrReplaceChildSteps(this.mockAgentSteps.First().Definition);

            // Apply a min steps completed timeout restriction
            parentComponent.Parameters[StepParameters.TimeoutMinStepsSucceeded] = this.mockAgentSteps.Count() - 1;

            // Force a step timeout.
            this.state.AgentStepsTimeout = DateTime.UtcNow;
            this.state.AgentStepsCreated = true;

            // Ensure all but one step is succeeded.
            this.mockAgentSteps.ForEach(step => step.Status = ExecutionStatus.Succeeded);
            this.mockAgentSteps.Last().Status = ExecutionStatus.Pending;

            Assert.Throws<TimeoutException>(() => this.provider.ExecuteMonitoringStepsAsync(
                this.mockFixture.Context, parentComponent, new EventContext(Guid.NewGuid()), CancellationToken.None)
                .GetAwaiter().GetResult());
        }

        private void SetupMonitoringProviderDefaultBehaviors()
        {
            // Agent monitoring provider will create agent steps targeted to run on individual
            // agents that are part of the experiment.
            this.mockFixture.DataClient
                .Setup(client => client.CreateAgentStepsAsync(
                    It.IsAny<ExperimentStepInstance>(),
                    It.IsAny<ExperimentComponent>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockAgentSteps));

            // Agent monitoring provider will get agent steps targeted to run on individual
            // agents that are part of the experiment.
            this.mockFixture.DataClient
                .Setup(client => client.GetAgentStepsAsync(
                    It.IsAny<ExperimentStepInstance>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockAgentSteps));

            // Agent monitoring providers will query for agent heartbeats to confirm the agents
            // are up and running before verifying the execution status of agent steps.
            this.mockFixture.DataClient
                .Setup(client => client.GetAgentHeartbeatAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new AgentHeartbeatInstance(
                    Guid.NewGuid().ToString(), "Cluster01,Node01,VM01,Tip01", AgentHeartbeatStatus.Running, AgentType.GuestAgent)));

            // Agent monitoring providers need the environment entities (e.g. Virtual Machines) to
            // determine the agent ID when creating agent steps.
            this.mockFixture.DataClient
                .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    It.IsAny<string>(),
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Returns(Task.FromResult(this.mockAgentEntities));

            // Agent monitoring providers need to access their own state objects.
            this.mockFixture.DataClient
                .Setup(client => client.GetOrCreateStateAsync<ExperimentAgentMonitoringProviderExtensions.AgentStepMonitorState>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Returns(Task.FromResult(this.state));
        }

        [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteOnVirtualMachine)]
        private class TestVmStepProvider : ExperimentProvider, IExperimentStepMonitoringProvider
        {
            public TestVmStepProvider(IServiceCollection services)
                : base(services)
            {
            }

            protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
            }
        }

        [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteOnNode)]
        private class TestNodeStepProvider : ExperimentProvider, IExperimentStepMonitoringProvider
        {
            public TestNodeStepProvider(IServiceCollection services)
                : base(services)
            {
            }

            protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                return Task.FromResult(new ExecutionResult(ExecutionStatus.Succeeded));
            }
        }
    }
}
