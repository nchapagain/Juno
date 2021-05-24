namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno;
    using Juno.Contracts;
    using Juno.Providers.Environment;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentProviderExtensionsTests
    {
        private ProviderFixture mockFixture;
        private IServiceCollection providerServices;
        private TestProvider mockProvider;
        private ExperimentInstance mockExperiment;
        private ExperimentComponent mockExperimentComponent;

        private Mock<IProviderDataClient> mockDataClient;
        private FixtureDependencies mockDependencies;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(TestProvider));
            this.mockDependencies = new FixtureDependencies();
            this.mockExperiment = this.mockFixture.Create<ExperimentInstance>();
            this.providerServices = new ServiceCollection();
            this.mockProvider = new TestProvider(this.providerServices);
            this.mockDataClient = new Mock<IProviderDataClient>();

            this.providerServices.AddSingleton(NullLogger.Instance);
            this.providerServices.AddSingleton(this.mockDataClient.Object);

            // A valid experiment component definition for the provider.
            this.mockExperimentComponent = new ExperimentComponent(
                typeof(TestProvider).FullName,
                "Any Name",
                "Any Description",
                "Group A",
                parameters: new Dictionary<string, IConvertible>
                {
                });
        }

        [Test]
        public void GetExecutionStatusExtensionReturnsTheExpectedResultWhenAllResultsAreSuccessful()
        {
            List<ExecutionResult> results = new List<ExecutionResult>
            {
                new ExecutionResult(ExecutionStatus.Succeeded),
                new ExecutionResult(ExecutionStatus.Succeeded)
            };

            ExecutionResult actualResult = results.GetExecutionResult();
            Assert.IsNotNull(actualResult);
            Assert.IsTrue(actualResult.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public void GetExecutionStatusExtensionReturnsTheExpectedResultWhenAnyResultsAreFailed()
        {
            List<ExecutionResult> results = new List<ExecutionResult>
            {
                new ExecutionResult(ExecutionStatus.Succeeded),
                new ExecutionResult(ExecutionStatus.Failed)
            };

            ExecutionResult actualResult = results.GetExecutionResult();
            Assert.IsNotNull(actualResult);
            Assert.IsTrue(actualResult.Status == ExecutionStatus.Failed);
        }

        [Test]
        public void GetExecutionStatusExtensionIncludesIndividualErrorsInTheResult()
        {
            List<ExecutionResult> results = new List<ExecutionResult>
            {
                new ExecutionResult(ExecutionStatus.Failed, error: new InvalidOperationException()),
                new ExecutionResult(ExecutionStatus.Failed, error: new ProviderException())
            };

            ExecutionResult actualResult = results.GetExecutionResult();
            Assert.IsNotNull(actualResult);
            Assert.IsNotNull(actualResult.Error);
            Assert.IsTrue(actualResult.Status == ExecutionStatus.Failed);
            Assert.IsInstanceOf<AggregateException>(actualResult.Error);
            Assert.IsTrue(object.ReferenceEquals(results[0].Error, (actualResult.Error as AggregateException).InnerExceptions[0]));
            Assert.IsTrue(object.ReferenceEquals(results[1].Error, (actualResult.Error as AggregateException).InnerExceptions[1]));
        }

        [Test]
        public void GetExecutionStatusExtensionReturnsTheExpectedResultWhenAnyResultsAreInProgress()
        {
            List<ExecutionResult> results = new List<ExecutionResult>
            {
                new ExecutionResult(ExecutionStatus.Succeeded),
                new ExecutionResult(ExecutionStatus.InProgress),
                new ExecutionResult(ExecutionStatus.InProgressContinue)
            };

            ExecutionResult actualResult = results.GetExecutionResult();
            Assert.IsNotNull(actualResult);
            Assert.IsTrue(actualResult.Status == ExecutionStatus.InProgress);
        }

        [Test]
        public void GetExecutionStatusExtensionReturnsTheExpectedResultWhenAnyResultsAreInProgressContinue()
        {
            List<ExecutionResult> results = new List<ExecutionResult>
            {
                new ExecutionResult(ExecutionStatus.Succeeded),
                new ExecutionResult(ExecutionStatus.Succeeded),
                new ExecutionResult(ExecutionStatus.InProgressContinue)
            };

            ExecutionResult actualResult = results.GetExecutionResult();
            Assert.IsNotNull(actualResult);
            Assert.IsTrue(actualResult.Status == ExecutionStatus.InProgressContinue);
        }

        [Test]
        public void HasFeatureFlagsExtensionIdentifiesAnExpectedFlagCorrectly()
        {
            string expectedFlag = "AnyFeatureFlag";
            ExperimentComponent anyComponent = this.mockFixture.Create<ExperimentComponent>();
            anyComponent.Parameters.Add(StepParameters.FeatureFlag, expectedFlag);

            Assert.IsTrue(this.mockProvider.HasFeatureFlag(anyComponent, expectedFlag));
        }

        [Test]
        [TestCase("ExpectedFlag;AnyOtherFeatureFlag1;AnyOtherFeatureFlag2")]
        [TestCase("ExpectedFlag,AnyOtherFeatureFlag1,AnyOtherFeatureFlag2")]
        [TestCase("ExpectedFlag;AnyOtherFeatureFlag1,AnyOtherFeatureFlag2")]
        public void HasFeatureFlagsExtensionHandlesScenariosWhereMultipleFeatureFlagsAreDefined(string flags)
        {
            string expectedFlag = "ExpectedFlag";
            ExperimentComponent anyComponent = this.mockFixture.Create<ExperimentComponent>();

            anyComponent.Parameters.Add(StepParameters.FeatureFlag, flags);
            Assert.IsTrue(this.mockProvider.HasFeatureFlag(anyComponent, expectedFlag));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(";")]
        [TestCase(",")]
        public void HasFeatureFlagsExtensionHandlesInvalidAndIncompleteFeatureFlagDefinitions(string incompleteValue)
        {
            string expectedFlag = "AnyFeatureFlag";
            ExperimentComponent anyComponent = this.mockFixture.Create<ExperimentComponent>();

            anyComponent.Parameters.Add(StepParameters.FeatureFlag, incompleteValue);
            Assert.IsFalse(this.mockProvider.HasFeatureFlag(anyComponent, expectedFlag));
        }

        [Test]
        public async Task InstallDependenciesExtensionExecutesTheExpectedDependencyProviders()
        {
            ExperimentComponent template = this.mockFixture.Create<ExperimentComponent>();
            ExperimentComponent componentWithDependencies = new ExperimentComponent(
                template.ComponentType,
                template.Name,
                template.Group,
                dependencies: new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(TestDependencyProvider)),
                    FixtureExtensions.CreateExperimentComponent(typeof(TestDependencyProvider))
                });

            await this.mockProvider.InstallDependenciesAsync(this.mockFixture.Context, componentWithDependencies, CancellationToken.None);

            Assert.IsTrue(TestDependencyProvider.DependenciesInstalled.Count == 2);
            CollectionAssert.AreEquivalent(componentWithDependencies.Dependencies, TestDependencyProvider.DependenciesInstalled);
        }

        [Test]
        public async Task InstallDependenciesExtensionReturnsTheExpectedResultWhenAllDependenciesInstallSuccessfully()
        {
            ExperimentComponent template = this.mockFixture.Create<ExperimentComponent>();
            ExperimentComponent componentWithDependencies = new ExperimentComponent(
                template.ComponentType,
                template.Name,
                template.Group,
                dependencies: new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(TestDependencyProvider)),
                    FixtureExtensions.CreateExperimentComponent(typeof(TestDependencyProvider))
                });

            // Behavior Setup:
            // The test dependencies succeed at installation.
            TestDependencyProvider.ExecutionResult = new ExecutionResult(ExecutionStatus.Succeeded);

            ExecutionResult result = await this.mockProvider.InstallDependenciesAsync(
                this.mockFixture.Context,
                componentWithDependencies,
                CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Succeeded);
        }

        [Test]
        public async Task InstallDependenciesExtensionReturnsTheExpectedResultWhenAnyDependenciesFailInstallation()
        {
            ExperimentComponent template = this.mockFixture.Create<ExperimentComponent>();
            ExperimentComponent componentWithDependencies = new ExperimentComponent(
                template.ComponentType,
                template.Name,
                template.Group,
                dependencies: new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(TestDependencyProvider)),
                    FixtureExtensions.CreateExperimentComponent(typeof(TestDependencyProvider))
                });

            // Behavior Setup:
            // The test dependencies succeed at installation.
            TestDependencyProvider.ExecutionResult = new ExecutionResult(ExecutionStatus.Failed);

            ExecutionResult result = await this.mockProvider.InstallDependenciesAsync(
                this.mockFixture.Context,
                componentWithDependencies,
                CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
        }

        [Test]
        public void GetEntityPoolExtensionsSearchesForStateObjectsInTheExpectedLocation()
        {
            this.mockProvider.GetEntityPoolAsync(this.mockFixture.Context, CancellationToken.None);

            this.mockDataClient.Verify(client => client.GetOrCreateStateAsync<object>(
                It.IsAny<string>(),
                ContractExtension.EntityPool,
                It.IsAny<CancellationToken>(),
                null));
        }

        [Test]
        public void GetEntitiesProvisionedExtensionsSearchesForStateObjectsInTheExpectedLocation()
        {
            this.mockProvider.GetEntitiesProvisionedAsync(this.mockFixture.Context, CancellationToken.None);

            this.mockDataClient.Verify(client => client.GetOrCreateStateAsync<object>(
                It.IsAny<string>(),
                ContractExtension.EntitiesProvisioned,
                It.IsAny<CancellationToken>(),
                null));
        }

        [Test]
        public void GetStateAsyncExtensionSearchesForProviderStateObjectsInTheExpectedLocationByDefault()
        {
            // Until all of the changes for provider-specific state have been propagated, the default location
            // for state objects is in the shared/global context.
            this.mockProvider.GetStateAsync<object>(this.mockFixture.Context, CancellationToken.None);

            this.mockDataClient.Verify(client => client.GetOrCreateStateAsync<object>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                null));
        }

        [Test]
        public void GetStateAsyncExtensionUsesTheExpectedStateIdForSharedGlobalStateObjects()
        {
            this.mockProvider.GetStateAsync<object>(this.mockFixture.Context, CancellationToken.None, sharedState: true);

            this.mockDataClient.Verify(client => client.GetOrCreateStateAsync<object>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                null));
        }

        [Test]
        public void GetStateAsyncExtensionUsesTheExpectedStateIdForProviderSpecificStateObjects()
        {
            this.mockProvider.GetStateAsync<object>(this.mockFixture.Context, CancellationToken.None, sharedState: false);

            this.mockDataClient.Verify(client => client.GetOrCreateStateAsync<object>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                this.mockFixture.Context.ExperimentStep.Id));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void IsDiagnosticsEnabledExtensionReturnsTheExpectedResultWhenTheEnableDiagnosticsParameterIsDefined(bool enableDiagnostics)
        {
            this.mockExperimentComponent.Parameters[StepParameters.EnableDiagnostics] = enableDiagnostics;

            bool actual = this.mockProvider.IsDiagnosticsEnabled(this.mockExperimentComponent);
            Assert.That(actual, Is.EqualTo(enableDiagnostics));
        }

        [Test]
        public void IsDiagnosticsEnabledExtensionReturnsTheExpectedResultWhenTheEnableDiagnosticsParameterIsNotDefined()
        {
            bool actual = this.mockProvider.IsDiagnosticsEnabled(this.mockExperimentComponent);
            Assert.That(actual, Is.EqualTo(false));
        }

        private IList<ExperimentStepInstance> CreateSteps()
        {
            int sequence = 0;
            var stepsInfo = new List<KeyValuePair<ExecutionStatus, Type>>()
            {
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCriteriaProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleSetupProvider)),
              new KeyValuePair<ExecutionStatus, Type>(ExecutionStatus.Pending, typeof(ExampleCleanupProvider)),
            };

            List<ExperimentStepInstance> steps = new List<ExperimentStepInstance>();
            foreach (var item in stepsInfo)
            {
                var component = new ExperimentComponent(item.Value.FullName, "Any Name", "Any Description", "Any Group");

                ExperimentStepInstance step = this.mockFixture.CreateExperimentStep(component);
                step.Status = item.Key;
                step.Sequence = sequence += 100;
                steps.Add(step);
            }

            return steps;
        }

        [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteOnVirtualMachine)]
        private class TestProvider : ExperimentProvider
        {
            public TestProvider(IServiceCollection services)
                : base(services)
            {
            }

            public Func<ExecutionResult> OnExecute { get; set; }

            protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                ExecutionResult result = this.OnExecute != null
                    ? this.OnExecute.Invoke()
                    : new ExecutionResult(ExecutionStatus.Succeeded);

                return Task.FromResult(result);
            }
        }

        [ExecutionConstraints(SupportedStepType.Dependency, SupportedStepTarget.ExecuteOnVirtualMachine)]
        private class TestDependencyProvider : ExperimentProvider
        {
            public TestDependencyProvider(IServiceCollection services)
                : base(services)
            {
            }

            public static ExecutionResult ExecutionResult { get; set; } = new ExecutionResult(ExecutionStatus.Succeeded);

            public static List<ExperimentComponent> DependenciesInstalled { get; } = new List<ExperimentComponent>();

            protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                TestDependencyProvider.DependenciesInstalled.Add(component);
                return Task.FromResult(TestDependencyProvider.ExecutionResult);
            }
        }
    }
}
