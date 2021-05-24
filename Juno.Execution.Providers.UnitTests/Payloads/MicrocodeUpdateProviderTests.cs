namespace Juno.Execution.Providers.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution.Providers.AutoTriage;
    using Juno.Execution.TipIntegration;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;
    using TipGateway.Entities;

    [TestFixture]
    [Category("Unit")]
    public class MicrocodeUpdateProviderTests
    {
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private ServiceCollection mockProviderServices;
        private Mock<ITipClient> mockTipClient;
        private Mock<IProviderDataClient> mockProviderDataClient;

        private TestMicrocodeUpdateProvider provider;
        private ExperimentContext testExperimentContext;
        private ExperimentComponent testExperimentComponent;
        private List<EnvironmentEntity> testEntitiesProvisioned;
        private MicrocodeUpdateProvider.MicrocodeUpdateProviderState testProviderState;
        private List<DiagnosticsRequest> mockDiagnosticRequest;

        [SetUp]
        public void SetupTest()
        {
            // This is a fairly complex unit testing scenario. The provider under test has a number of requirements that it must
            // meet as part of the execution workflow. So that the developer is clear on the steps in the workflow, the following
            // defines the expected flow of the provider as it attempts to deploy a microcode update to the physical node:
            // 
            // 1) Send request to TiP Gateway service to request the deployment of the microcode update to the physical node(s)
            //    in the experiment group.
            // 2) Confirm that the TiP Gateway service successfully hands-off the request to the PilotFish agent on the physical
            //    node(s).
            // 3) Create agent steps that will run in the Host Agent process on the physical nodes to explicitly verify the
            //    microcode update was successfully applied.
            // 4) Check the execution status of the agent steps to confirm whether they complete successfully indicating the
            //    microcode update was verified to be applied on the physical node(s).

            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();

            this.mockDependencies = new FixtureDependencies();
            this.mockProviderServices = new ServiceCollection();

            // A valid experiment component definition for the provider.
            this.testExperimentComponent = new ExperimentComponent(
                typeof(MicrocodeUpdateProvider).FullName,
                "Apply Microcode",
                "Any Description",
                "Group B",
                parameters: new Dictionary<string, IConvertible>
                {
                    // The following parameters are required by the provider.
                    ["microcodeProvider"] = "Intel",
                    ["microcodeVersion"] = "123456",
                    ["pfServiceName"] = "Any microcode update",
                    ["pfServicePath"] = @"\\any\path\to\the\PFService\app"
                });

            // The test objects have all of the dependencies required to moderate the provider
            // execution workflow (e.g. state, entities, TiP request responses). This give us
            // dials that we can change to affect the flow of the code while keeping the code in 
            // the individual unit tests to a minimum
            this.testExperimentContext = new ExperimentContext(
                this.mockFixture.Create<ExperimentInstance>(),
                this.mockFixture.CreateExperimentStep(this.testExperimentComponent),
                this.mockDependencies.Configuration);

            this.testEntitiesProvisioned = new List<EnvironmentEntity>
            {
                // Set the 'entities provisioned' to contain TiP sessions. The provider expects TiP sessions to have
                // been established in order to know which physical nodes on which the microcode should be deployed.
                EnvironmentEntity.TipSession("AnySessionId1", "Group B", new Dictionary<string, IConvertible>
                {
                    [nameof(TipSession.NodeId)] = "Node01",
                    [nameof(TipSession.ClusterName)] = "Cluster01",
                    [nameof(TipSession.TipSessionId)] = "AnySessionId1",
                    [nameof(TipSession.ChangeIdList)] = string.Join(",", new List<string> { "AnyOtherChangeId" })
                })
            };

            this.testProviderState = new MicrocodeUpdateProvider.MicrocodeUpdateProviderState
            {
                TipRequests = new List<MicrocodeUpdateProvider.TipRequestDescription>
                {
                    new MicrocodeUpdateProvider.TipRequestDescription
                    {
                        DeploymentVerified = false,
                        RequestTime = DateTime.UtcNow,
                        RequestTimeout = TimeSpan.FromMinutes(10),
                        TipNodeId = "Node01",
                        TipNodeSessionChangeId = "AnyChangeId1",
                        TipNodeSessionId = "AnySessionId1"
                    }
                }
            };

            // API services involved in the operation of the provider:
            // 1) TiP Gateway service for requesting the deployment of the microcode update (via PilotFish).
            // 2) Juno Execution API service for accessing Juno system environment entities and for creating agent steps.
            //    The provider accesses the API via the provider data client.
            this.mockTipClient = new Mock<ITipClient>();
            this.mockProviderDataClient = new Mock<IProviderDataClient>();
            this.mockProviderServices.AddSingleton<IProviderDataClient>(this.mockProviderDataClient.Object);
            this.mockProviderServices.AddSingleton<ITipClient>(this.mockTipClient.Object);

            this.OnGetEntitiesProvisioned(() => this.testEntitiesProvisioned);

            this.mockDiagnosticRequest = new List<DiagnosticsRequest>()
                {
                    new DiagnosticsRequest(
                        this.testExperimentContext.ExperimentId,
                        Guid.NewGuid().ToString(),
                        DiagnosticsIssueType.MicrocodeUpdateFailure,
                        DateTime.UtcNow.AddHours(-2),
                        DateTime.UtcNow,
                        new Dictionary<string, IConvertible>()
                        {
                            { DiagnosticsParameter.TipNodeId,  "Node01" },
                            { DiagnosticsParameter.TipSessionId, "AnySessionId1" },
                            { DiagnosticsParameter.TipSessionChangeId, "AnyChangeId1" },
                            { DiagnosticsParameter.ExperimentId, this.testExperimentContext.ExperimentId },
                            { DiagnosticsParameter.ProviderName, nameof(MicrocodeUpdateProvider) }
                        })
                };

            this.provider = new TestMicrocodeUpdateProvider(this.mockProviderServices);
        }

        [Test]
        public void MicrocodeUpdateProviderValidatesRequiredParametersAreProvidedForTheExperiment()
        {
            // Invalidate the definition by removing required parameters
            this.testExperimentComponent.Parameters.Clear();

            MicrocodeUpdateProvider provider = new MicrocodeUpdateProvider(this.mockProviderServices);
            ExecutionResult result = provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsInstanceOf<SchemaException>(result.Error);
        }

        [Test]
        public void MicrocodeUpdateProviderMaintainsStateInItsOwnIndividualStateObject()
        {
            // When provider gets entities provisioned...
            this.OnGetEntitiesProvisioned(() => this.testEntitiesProvisioned);
            this.OnRequestMicrocodeUpdate((tipSessionId, pfServices, token) => { });

            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            this.mockProviderDataClient.Verify(client => client.GetOrCreateStateAsync<MicrocodeUpdateProvider.MicrocodeUpdateProviderState>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.Is<string>(stateId => !string.IsNullOrEmpty(stateId))));

            this.mockProviderDataClient.Verify(client => client.SaveStateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<MicrocodeUpdateProvider.MicrocodeUpdateProviderState>(),
                It.IsAny<CancellationToken>(),
                It.Is<string>(stateId => !string.IsNullOrEmpty(stateId))));
        }

        [Test]
        public void MicrocodeUpdateProviderDefaultTimeoutsMatchExpected()
        {
            Assert.AreEqual(TimeSpan.FromMinutes(20), MicrocodeUpdateProvider.DefaultRequestTimeout);
            Assert.AreEqual(TimeSpan.FromMinutes(20), MicrocodeUpdateProvider.DefaultVerificationTimeout);
        }

        [Test]
        public void MicrocodeUpdateProviderMakesTheExpectedCallToTheTipServiceToRequestMicrocodeUpdate()
        {
            // When provider gets entities provisioned...
            this.OnGetEntitiesProvisioned(() => this.testEntitiesProvisioned);
            // When the provider calls the TiP Gateway to request the deployment of the microcode update...
            this.OnRequestMicrocodeUpdate((tipSessionId, pfServices, token) =>
            {
                Assert.IsTrue(this.testEntitiesProvisioned.Any(entity => entity.Id == tipSessionId));
                Assert.IsTrue(pfServices.Count == 1);
                Assert.AreEqual(this.testExperimentComponent.Parameters.GetValue<string>("pfServiceName"), pfServices.First().Key);
                Assert.AreEqual(this.testExperimentComponent.Parameters.GetValue<string>("pfServicePath"), pfServices.First().Value);
            });

            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void MicrocodeUpdateProviderThrowsIfExpectedEntitiesProvisionedRequiredToIdentifyTheTargetTiPNodesDoNotExist()
        {
            // There will be no matching TiP Sessions/Nodes
            this.OnGetEntitiesProvisioned(() => new List<EnvironmentEntity>());

            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsInstanceOf<ProviderException>(result.Error);
            Assert.AreEqual(ErrorReason.ExpectedEnvironmentEntitiesNotFound, (result.Error as ProviderException).Reason);
        }

        [Test]
        public void MicrocodeUpdateProviderSavesTheTipNodeSessionChangeIdsForTheTipSessionEnvironmentEntities()
        {
            // When provider gets entities provisioned...
            this.OnGetEntitiesProvisioned(() => this.testEntitiesProvisioned);

            // When the provider calls the TiP Gateway to request the deployment of the microcode update...
            this.OnRequestMicrocodeUpdate((tipSessionId, pfServices, token) => { });

            // When provider gets entities provisioned...
            this.OnSaveEntitiesProvisioned((entities) =>
            {
                IEnumerable<EnvironmentEntity> tipSessions = entities.GetEntities(EntityType.TipSession);
                tipSessions.ToList().ForEach(entity =>
                {
                    Assert.IsTrue(entity.Metadata.ContainsKey(nameof(TipSession.ChangeIdList)));
                    Assert.IsTrue(entity.Metadata[nameof(TipSession.ChangeIdList)].ToString().Split(",").ToList().Contains("AnyChangeId"));
                });
            });

            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void MicrocodeUpdateProviderHandlesErrorsThatOccurInTheCallToTheTipServiceToRequestMicrocodeUpdate()
        {
            // When provider gets entities provisioned...
            this.OnGetEntitiesProvisioned(() => this.testEntitiesProvisioned);

            // When the provider calls the TiP Gateway to request the deployment of the microcode update...
            this.OnRequestMicrocodeUpdate((tipSessionId, pfServices, token) =>
            {
                throw new InvalidOperationException("Any TiP Gateway request error.");
            });

            ExecutionResult result = null;
            Assert.DoesNotThrow(() => result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult());

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsInstanceOf<InvalidOperationException>(result.Error);
        }

        [Test]
        public void MicrocodeUpdateProviderHandlesCancellationBeforeAttemptToRequestMicrocodeUpdate()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                MicrocodeUpdateProviderTests.CancelExection(tokenSource);

                this.OnGetState(() => this.testProviderState);

                ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, tokenSource.Token)
                    .GetAwaiter().GetResult();

                Assert.IsNotNull(result);
                Assert.IsTrue(result.Status == ExecutionStatus.Cancelled);
            }
        }

        [Test]
        public void MicrocodeUpdateProviderMakesTheExpectedCallToTheTipServiceToVerifyTheMicrocodeDeploymentRequestCompleted()
        {
            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.TipRequests.ForEach(request => request.DeploymentVerified = false);

            this.OnGetState(() => this.testProviderState);

            // When the provider calls the TiP Gateway to request the status of the deployment of 
            // the microcode update...
            this.OnRequestMicrocodeUpdateStatus((tipSessionId, tipSessionChangeId, token) =>
            {
                Assert.AreEqual(tipSessionId, this.testProviderState.TipRequests.First().TipNodeSessionId);
                Assert.AreEqual(tipSessionChangeId, this.testProviderState.TipRequests.First().TipNodeSessionChangeId);
            });

            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public void MicrocodeUpdateProviderResetsRequestTimeAndRetryWhenTiPFailedPilotfishUpdate()
        {
            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.TipRequests.ForEach(request => request.DeploymentVerified = false);

            MicrocodeUpdateProvider.MicrocodeUpdateProviderState returnedState = new MicrocodeUpdateProvider.MicrocodeUpdateProviderState();
            
            this.OnGetState(() => this.testProviderState);
            this.OnSaveState((state) =>
            {
                returnedState = state;
            });

            TipNodeSessionChangeDetails failedChange = new TipNodeSessionChangeDetails()
            {
                Status = TipNodeSessionChangeStatus.Finished,
                Result = TipNodeSessionChangeResult.Failed,
                ErrorMessage = "Failed to send DeliveryPath to dynamic storage"
            };

            // When the provider calls the TiP Gateway to request the status of the deployment of 
            // the microcode update...
            this.OnRequestMicrocodeUpdateStatus((tipSessionId, tipSessionChangeId, token) =>
            {
                Assert.AreEqual(tipSessionId, this.testProviderState.TipRequests.First().TipNodeSessionId);
                Assert.AreEqual(tipSessionChangeId, this.testProviderState.TipRequests.First().TipNodeSessionChangeId);
            }, failedChange);

            this.OnRequestMicrocodeUpdate((tipSessionId, pfServices, token) => { });

            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsTrue(returnedState != null);
            Assert.IsNotNull(returnedState.TipRequests);
            Assert.IsTrue(returnedState.InstallationRetries == 1);
        }

        [Test]
        public void MicrocodeUpdateProviderStepToValidateTheMicrocodeUpdateFailsIfNotCompletedWithinTheSpecifiedTimeout()
        {
            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            // 2) The time allowed to confirm the request completed (hand-off to PilotFish) expired.
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.TipRequests.ForEach(request => request.DeploymentVerified = false);
            this.testProviderState.TipRequests.ForEach(request => request.RequestTimeout = TimeSpan.Zero);

            this.OnGetState(() => this.testProviderState);

            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsInstanceOf<ProviderException>(result.Error);
            Assert.IsTrue((result.Error as ProviderException).Reason == ErrorReason.Timeout);
        }

        [Test]
        public void MicrocodeUpdateProviderHandlesCancellationBeforeAttemptsToVerifyTheMicrocodeUpdateRequestCompletion()
        {
            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            this.testProviderState.DeploymentRequested = true;

            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                MicrocodeUpdateProviderTests.CancelExection(tokenSource);

                this.OnGetState(() => this.testProviderState);

                ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, tokenSource.Token)
                    .GetAwaiter().GetResult();

                Assert.IsNotNull(result);
                Assert.IsTrue(result.Status == ExecutionStatus.Cancelled);
            }
        }

        [Test]
        public void MicrocodeUpdateProviderHandlesErrorsThatOccurInTheCallToTheTipServiceToRequestMicrocodeDeploymentStatus()
        {
            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            this.testProviderState.DeploymentRequested = true;
            this.OnGetState(() => this.testProviderState);

            // When the provider calls the TiP Gateway to request the status of the deployment of 
            // the microcode update...
            this.OnRequestMicrocodeUpdateStatus((tipSessionId, tipSessionChangeId, token) =>
            {
                throw new InvalidOperationException("Call to TiP Gateway failed");
            });

            ExecutionResult result = null;
            Assert.DoesNotThrow(() => result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult());

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsInstanceOf<InvalidOperationException>(result.Error);
        }

        [Test]
        public void MicrocodeUpdateProviderCreatesTheExpectedAgentStepsResponsibleForVerifyingTheMicrocodeApplication()
        {
            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            // 2) The deployment of the microcode update to the node was confirmed.
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.DeploymentRequestsCompleted = true;

            this.OnGetState(() => this.testProviderState);

            // Now the provider needs to create agent steps that will verify the microcode update
            // version expected was actually applied by on the node (i.e. by PilotFish).
            this.OnCreateAgentSteps((parentStep, agentStep, agentId, token) =>
            {
                Assert.IsTrue(object.ReferenceEquals(this.testExperimentComponent, parentStep.Definition));
                Assert.IsTrue(agentStep.ComponentType == typeof(MicrocodeActivationProvider).FullName);
                Assert.IsTrue(agentStep.Parameters.ContainsKey("microcodeVersion"));
                Assert.IsTrue(agentStep.Parameters["microcodeVersion"] == parentStep.Definition.Parameters["microcodeVersion"]);
            });

            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
        }

        [Test]
        public void MicrocodeUpdateProviderHandlesCancellationBeforeAttemptsToCreateAgentSteps()
        {
            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            // 2) The deployment of the microcode update to the node was confirmed.
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.DeploymentRequestsCompleted = true;

            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                MicrocodeUpdateProviderTests.CancelExection(tokenSource);

                this.OnGetState(() => this.testProviderState);

                ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, tokenSource.Token)
                    .GetAwaiter().GetResult();

                Assert.IsNotNull(result);
                Assert.IsTrue(result.Status == ExecutionStatus.Cancelled);
            }
        }

        [Test]
        public void MicrocodeUpdateProviderHandlesErrorsThatOccurInTheAttemptToCreateAgentSteps()
        {
            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            // 2) The deployment of the microcode update to the node was confirmed.
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.DeploymentRequestsCompleted = true;

            this.OnGetState(() => this.testProviderState);

            // Now the provider needs to create agent steps that will verify the microcode update
            // version expected was actually applied by on the node (i.e. by PilotFish).
            this.OnCreateAgentSteps((parentStep, agentStep, agentId, token) =>
            {
                throw new InvalidOperationException("Request to create agent step(s) failed.");
            });

            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsInstanceOf<InvalidOperationException>(result.Error);
        }

        [Test]
        public void MicrocodeUpdateProviderValidatesTheStatusOfTheAgentStepsToDetermineIfTheMicrocodeUpdateWasSuccessfullyApplied()
        {
            ExperimentStepInstance agentStep = this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(MicrocodeActivationProvider)));
            agentStep.Status = ExecutionStatus.InProgress;

            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            // 2) The deployment of the microcode update to the node was confirmed.
            // 3) The agent step(s) responsible for validating the microcode update was successfully applied are created.
            this.testProviderState.AgentStepsCreated = true;
            this.testProviderState.AgentStepCreationTime = DateTime.UtcNow;
            this.testProviderState.UpdateVerificationTimeout = TimeSpan.FromDays(1);
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.DeploymentRequestsCompleted = true;

            this.OnGetState(() => this.testProviderState);

            // Now the provider needs to create agent steps that will verify the microcode update
            // version expected was actually applied by on the node (i.e. by PilotFish).
            this.OnGetAgentStepsStatus((parentStep, token) =>
            {
                Assert.IsTrue(object.ReferenceEquals(this.testExperimentComponent, parentStep.Definition));
            },
            returns: new List<ExperimentStepInstance> { agentStep });

            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);
        }

        [Test]
        public void MicrocodeUpdateProviderVerifiesTheMicrocodeUpdateAppliedSuccessfullyWhenAllAgentStepsCompleteSuccessfully()
        {
            ExperimentStepInstance agentStep1 = this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(MicrocodeActivationProvider)));
            ExperimentStepInstance agentStep2 = this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(MicrocodeActivationProvider)));

            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            // 2) The deployment of the microcode update to the node was confirmed.
            // 3) The agent step(s) responsible for validating the microcode update was successfully applied are created.
            this.testProviderState.AgentStepsCreated = true;
            this.testProviderState.AgentStepCreationTime = DateTime.UtcNow;
            this.testProviderState.UpdateVerificationTimeout = TimeSpan.FromDays(1);
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.DeploymentRequestsCompleted = true;

            this.OnGetState(() => this.testProviderState);

            // Now the provider needs to create agent steps that will verify the microcode update
            // version expected was actually applied by on the node (i.e. by PilotFish).
            this.OnGetAgentStepsStatus((parentStep, token) =>
            {
                Assert.IsTrue(object.ReferenceEquals(this.testExperimentComponent, parentStep.Definition));
            },
            returns: new List<ExperimentStepInstance> { agentStep1, agentStep2 });

            // Not all of the steps are completed.
            agentStep1.Status = ExecutionStatus.Succeeded;
            agentStep2.Status = ExecutionStatus.InProgress;
            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgress);

            // All of the steps are completed successfully.  Microcode update applied to all nodes.
            agentStep1.Status = ExecutionStatus.Succeeded;
            agentStep2.Status = ExecutionStatus.Succeeded;
            result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public void MicrocodeUpdateProviderFailsTheStepIfAnyOneOfTheAgentStepsFailToSuccessfullyApplyTheMicrocodeUpdate()
        {
            ExperimentStepInstance agentStep1 = this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(MicrocodeActivationProvider)));
            ExperimentStepInstance agentStep2 = this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(MicrocodeActivationProvider)));

            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            // 2) The deployment of the microcode update to the node was confirmed.
            // 3) The agent step(s) responsible for validating the microcode update was successfully applied are created.
            this.testProviderState.AgentStepsCreated = true;
            this.testProviderState.AgentStepCreationTime = DateTime.UtcNow;
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.DeploymentRequestsCompleted = true;

            this.OnGetState(() => this.testProviderState);

            // Now the provider needs to create agent steps that will verify the microcode update
            // version expected was actually applied by on the node (i.e. by PilotFish).
            this.OnGetAgentStepsStatus((parentStep, token) => { },
            returns: new List<ExperimentStepInstance> { agentStep1, agentStep2 });

            // Not all of the steps are completed.
            agentStep1.Status = ExecutionStatus.Succeeded;
            agentStep2.Status = ExecutionStatus.Failed;
            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
        }

        [Test]
        public void MicrocodeUpdateProviderHandlesCancellationBeforeAttemptsToGetTheStatusOfTheAgentSteps()
        {
            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            // 2) The deployment of the microcode update to the node was confirmed.
            // 3) The agent step(s) responsible for validating the microcode update was successfully applied are created.
            this.testProviderState.AgentStepsCreated = true;
            this.testProviderState.AgentStepCreationTime = DateTime.UtcNow;
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.DeploymentRequestsCompleted = true;

            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                MicrocodeUpdateProviderTests.CancelExection(tokenSource);

                this.OnGetState(() => this.testProviderState);

                ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, tokenSource.Token)
                    .GetAwaiter().GetResult();

                Assert.IsNotNull(result);
                Assert.IsTrue(result.Status == ExecutionStatus.Cancelled);
            }
        }

        [Test]
        public void MicrocodeUpdateProviderHandlesErrorsThatOccurInTheAttemptToGetTheStatusOfAgentSteps()
        {
            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            // 2) The deployment of the microcode update to the node was confirmed.
            // 3) The agent step(s) responsible for validating the microcode update was successfully applied are created.
            this.testProviderState.AgentStepsCreated = true;
            this.testProviderState.AgentStepCreationTime = DateTime.UtcNow;
            this.testProviderState.UpdateVerificationTimeout = TimeSpan.FromDays(1);
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.DeploymentRequestsCompleted = true;

            this.OnGetState(() => this.testProviderState);

            // Now the provider needs to create agent steps that will verify the microcode update
            // version expected was actually applied by on the node (i.e. by PilotFish).
            this.OnGetAgentStepsStatus((parentStep, token) =>
            {
                throw new InvalidOperationException("Request to get agent steps status failed.");
            },
            null);

            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsInstanceOf<InvalidOperationException>(result.Error);
        }

        [Test]
        public void MicrocodeUpdateProviderStepToVerifyTheMicrocodeUpdateWasActuallyAppliedFailsIfNotCompletedWithinTheSpecifiedTimeout()
        {
            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            // 2) The deployment of the microcode update to the node was confirmed.
            // 3) The agent step(s) responsible for validating the microcode update was successfully applied are created.
            // 4) The time allowed for verifying the application of the microcode update expired.
            this.testProviderState.AgentStepsCreated = true;
            this.testProviderState.AgentStepCreationTime = DateTime.UtcNow;
            this.testProviderState.UpdateVerificationTimeout = TimeSpan.Zero;
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.DeploymentRequestsCompleted = true;

            this.OnGetState(() => this.testProviderState);

            ExecutionResult result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsInstanceOf<ProviderException>(result.Error);
            Assert.IsTrue((result.Error as ProviderException).Reason == ErrorReason.Timeout);
        }

        [Test]
        public void MicrocodeUpdateProviderSupportsFeatureFlags()
        {
            string expectedFlag = "AnyFeatureFlag";
            this.testExperimentComponent.Parameters[StepParameters.FeatureFlag] = expectedFlag;

            Assert.IsTrue(this.provider.HasFeatureFlag(this.testExperimentComponent, expectedFlag));
            Assert.DoesNotThrow(() => this.provider.ValidateParameters(this.testExperimentComponent));
        }

        [Test]
        public void MicrocodeUpdateProviderRequestsAutoTriageDiagnosticsOnFailedExperimentsWithDiagnosticsEnabled()
        {
            // Enable Diagnostics flag is on
            this.testExperimentComponent.Parameters[StepParameters.EnableDiagnostics] = true;
            // Execution will fail with invalid resource group deployment
            this.ValidateMicrocodeUpdate(ExecutionStatus.Failed);

            // call to request autotriage diagnostics are made
            this.mockProviderDataClient.Verify(method =>
                method.UpdateStateItemsAsync(
                    this.testExperimentContext.ExperimentId,
                    ContractExtension.DiagnosticsRequests,
                    this.mockDiagnosticRequest,
                    CancellationToken.None,
                    null),
                Times.AtMostOnce);
        }

        [Test]
        public void MicrocodeUpdateProviderDoesNotRequestAutoTriageDiagnosticsOnFailedExperimentsWithoutDiagnosticsEnabled()
        {
            // Enable Diagnostics flag is not enabled
            this.testExperimentComponent.Parameters[StepParameters.EnableDiagnostics] = false;
            // Execution will fail with invalid resource group deployment
            this.ValidateMicrocodeUpdate(ExecutionStatus.Failed);

            // call to request autotriage diagnostics are not made
            this.mockProviderDataClient.Verify(method =>
                method.UpdateStateItemsAsync(
                    this.testExperimentContext.ExperimentId,
                    ContractExtension.DiagnosticsRequests,
                    this.mockDiagnosticRequest,
                    CancellationToken.None,
                    null),
                Times.Never);
        }

        [Test]
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.InProgress)]
        [TestCase(ExecutionStatus.Succeeded)]
        public void MicrocodeUpdateProviderWithDiagnosticsEnabledDoesNotRequestDiagnosticsFromUnfailedExperiments(ExecutionStatus status)
        {
            // Enable Diagnostics flag is enabled
            this.testExperimentComponent.Parameters[StepParameters.EnableDiagnostics] = true;
            // Testing other resource group deployment execution statuses
            this.ValidateMicrocodeUpdate(status);

            // call to request autotriage diagnostics are not made
            this.mockProviderDataClient.Verify(method =>
                method.UpdateStateItemsAsync(
                    this.testExperimentContext.ExperimentId,
                    ContractExtension.DiagnosticsRequests,
                    this.mockDiagnosticRequest,
                    CancellationToken.None,
                    null),
                Times.Never);
        }

        [Test]
        public void MicrocodeUpdateProviderReturnCorrectStatusOnMicrocodeUpdate()
        {
            this.ValidateMicrocodeUpdate(ExecutionStatus.Cancelled);
            this.ValidateMicrocodeUpdate(ExecutionStatus.InProgress);
            this.ValidateMicrocodeUpdate(ExecutionStatus.Failed);
            this.ValidateMicrocodeUpdate(ExecutionStatus.Succeeded);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MicrocodeUpdateProviderSupportsEnableDiagnosticsFlag(bool enableDiagnostics)
        {
            this.testExperimentComponent.Parameters[StepParameters.EnableDiagnostics] = enableDiagnostics;

            bool actual = this.testExperimentComponent.Parameters.GetValue<bool>(StepParameters.EnableDiagnostics);
            Assert.That(actual, Is.EqualTo(enableDiagnostics));
            Assert.DoesNotThrow(() => this.provider.ValidateParameters(this.testExperimentComponent));
        }

        private static void CancelExection(CancellationTokenSource tokenSource)
        {
            try
            {
                tokenSource.Cancel();
            }
            catch
            {
            }
        }

        private void ValidateMicrocodeUpdate(ExecutionStatus expectedStatus)
        {
            ExperimentStepInstance agentStep1 = this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(MicrocodeActivationProvider)));
            ExperimentStepInstance agentStep2 = this.mockFixture.CreateExperimentStep(FixtureExtensions.CreateExperimentComponent(typeof(MicrocodeActivationProvider)));

            // Setup state where:
            // 1) The deployment of the microcode update was requested.
            // 2) The deployment of the microcode update to the node was confirmed.
            // 3) The agent step(s) responsible for validating the microcode update was successfully applied are created.
            this.testProviderState.AgentStepsCreated = true;
            this.testProviderState.AgentStepCreationTime = DateTime.UtcNow;
            this.testProviderState.UpdateVerificationTimeout = TimeSpan.FromDays(1);
            this.testProviderState.DeploymentRequested = true;
            this.testProviderState.DeploymentRequestsCompleted = true;

            this.OnGetState(() => this.testProviderState);

            // Now the provider needs to create agent steps that will verify the microcode update
            // version expected was actually applied by on the node (i.e. by PilotFish).
            this.OnGetAgentStepsStatus((parentStep, token) =>
            {
                Assert.IsTrue(object.ReferenceEquals(this.testExperimentComponent, parentStep.Definition));
            },
            returns: new List<ExperimentStepInstance> { agentStep1, agentStep2 });
            // All of the steps are completed successfully.  Microcode update applied to all nodes.
            agentStep1.Status = ExecutionStatus.Succeeded;
            agentStep2.Status = expectedStatus;

            var result = this.provider.ExecuteAsync(this.testExperimentContext, this.testExperimentComponent, CancellationToken.None).Result;

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == expectedStatus);
        }

        private void OnCreateAgentSteps(Action<ExperimentStepInstance, ExperimentComponent, string, CancellationToken> executeThisAction, IEnumerable<ExperimentStepInstance> returns = null)
        {
            // Mock Setup:
            // Provider needs to get state on each individual execution so that it can determine what it
            // has already done and at what state of the workflow it is.
            this.mockProviderDataClient
                .Setup(client => client.CreateAgentStepsAsync(
                    It.IsAny<ExperimentStepInstance>(),
                    It.IsAny<ExperimentComponent>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ExperimentStepInstance, ExperimentComponent, string, CancellationToken>((parentStep, agentStep, agentId, token) =>
                {
                    executeThisAction.Invoke(parentStep, agentStep, agentId, token);
                })
                .Returns(Task.FromResult(returns ?? new List<ExperimentStepInstance> { this.mockFixture.CreateExperimentStep() }));
        }

        private void OnGetAgentStepsStatus(Action<ExperimentStepInstance, CancellationToken> executeThisAction, IEnumerable<ExperimentStepInstance> returns)
        {
            // Mock Setup:
            // Provider needs to get set of agent/child steps for itself to see if they have completed
            // successfully (and thus have validated the microcode update was applied).
            this.mockProviderDataClient
                .Setup(client => client.GetAgentStepsAsync(
                    It.IsAny<ExperimentStepInstance>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ExperimentStepInstance, CancellationToken>((parentStep, token) =>
                {
                    executeThisAction.Invoke(parentStep, token);
                })
                .Returns(Task.FromResult(returns));
        }

        private void OnGetEntitiesProvisioned(Func<IEnumerable<EnvironmentEntity>> executeThisAction)
        {
            // Mock Setup:
            // Provider needs to get entities provisioned so that it can find TiP sessions and nodes on
            // which it needs to request microcode updates.
            this.mockProviderDataClient
                .Setup(client => client.GetOrCreateStateAsync<IEnumerable<EnvironmentEntity>>(
                    It.IsAny<string>(),
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Returns(Task.FromResult(executeThisAction.Invoke() as IEnumerable<EnvironmentEntity>));
        }

        private void OnSaveEntitiesProvisioned(Action<IEnumerable<EnvironmentEntity>> executeThisAction)
        {
            // Mock Setup:
            // Provider needs to save entities provisioned so that it can preserve any information added
            // to them during the process.
            this.mockProviderDataClient
                .Setup(client => client.SaveStateAsync<IEnumerable<EnvironmentEntity>>(
                    It.IsAny<string>(),
                    ContractExtension.EntitiesProvisioned,
                    It.IsAny<IEnumerable<EnvironmentEntity>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Callback<string, string, IEnumerable<EnvironmentEntity>, CancellationToken, string>((experimentId, key, state, token, stateId) =>
                {
                    executeThisAction.Invoke(state);
                })
                .Returns(Task.CompletedTask);
        }

        private void OnGetState(Func<MicrocodeUpdateProvider.MicrocodeUpdateProviderState> executeThisAction)
        {
            // Mock Setup:
            // Provider needs to get state on each individual execution so that it can determine what it
            // has already done and at what state of the workflow it is.
            this.mockProviderDataClient
                .Setup(client => client.GetOrCreateStateAsync<MicrocodeUpdateProvider.MicrocodeUpdateProviderState>(
                    It.IsAny<string>(),
                    It.Is<string>(key => key != ContractExtension.EntitiesProvisioned),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Returns(Task.FromResult(executeThisAction.Invoke()));
        }

        private void OnSaveState(Action<MicrocodeUpdateProvider.MicrocodeUpdateProviderState> executeThisAction)
        {
            // Mock Setup:
            // Provider needs to get state on each individual execution so that it can determine what it
            // has already done and at what state of the workflow it is.
            this.mockProviderDataClient
                .Setup(client => client.SaveStateAsync<MicrocodeUpdateProvider.MicrocodeUpdateProviderState>(
                    It.IsAny<string>(),
                    It.Is<string>(key => key != ContractExtension.EntitiesProvisioned),
                    It.IsAny<MicrocodeUpdateProvider.MicrocodeUpdateProviderState>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string>()))
                .Callback<string, string, MicrocodeUpdateProvider.MicrocodeUpdateProviderState, CancellationToken, string>((expId, key, state, token, stateId) =>
                {
                    executeThisAction.Invoke(state);
                })
                .Returns(Task.CompletedTask);
        }

        private void OnRequestMicrocodeUpdate(Action<string, List<KeyValuePair<string, string>>, CancellationToken> executeThisAction, TipNodeSessionChange returns = null)
        {
            // Mock Setup:
            // Provider calls the TiP Gateway to request the deployment of the microcode update on the physical
            // node (via the PilotFish agent).
            this.mockTipClient
                .Setup(client => client.ApplyPilotFishServicesAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<KeyValuePair<string, string>>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, List<KeyValuePair<string, string>>, CancellationToken>((tipSessionId, pfServices, token) =>
                {
                    executeThisAction.Invoke(tipSessionId, pfServices, token);
                })
                .Returns(Task.FromResult(returns ?? new TipNodeSessionChange
                {
                    Status = TipNodeSessionChangeStatus.Executing,
                    TipNodeSessionChangeId = "AnyChangeId",
                    TipNodeSessionId = "AnySessionId"
                }));
        }

        private void OnRequestMicrocodeUpdateStatus(Action<string, string, CancellationToken> executeThisAction, TipNodeSessionChangeDetails returns = null)
        {
            // Mock Setup:
            // Provider calls the TiP Gateway to request the deployment of the microcode update on the physical
            // node (via the PilotFish agent).
            this.mockTipClient
                .Setup(client => client.GetTipSessionChangeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((tipSessionId, tipSessionChangeId, token) =>
                {
                    executeThisAction.Invoke(tipSessionId, tipSessionChangeId, token);
                })
                .Returns(Task.FromResult(returns ?? new TipNodeSessionChangeDetails
                {
                    Status = TipNodeSessionChangeStatus.Executing
                }));
        }

        private class TestMicrocodeUpdateProvider : MicrocodeUpdateProvider
        {
            public TestMicrocodeUpdateProvider(IServiceCollection services)
                : base(services)
            {
            }

            public new void ValidateParameters(ExperimentComponent component)
            {
                base.ValidateParameters(component);
            }
        }
    }
}