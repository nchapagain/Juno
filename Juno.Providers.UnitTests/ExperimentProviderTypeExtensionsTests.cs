namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Juno.Contracts;
    using Juno.Providers.Environment;
    using Juno.Providers.Payloads;
    using Juno.Providers.Workloads;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentProviderTypeExtensionsTests
    {
        [Test]
        public void GetProviderTypeExtensionReturnsTheExpectedTypeForAGivenComponent()
        {
            ExperimentComponent environmentCriteria = FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), "Environment Criteria");
            ExperimentComponent environmentSetup = FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), "Environment Setup");
            ExperimentComponent environmentCleanup = FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), "Environment Cleanup");
            ExperimentComponent workload = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Workload");
            ExperimentComponent payload = FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider), "Payload");
            ExperimentComponent watchdog = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), "Watchdog");

            Assert.AreEqual(typeof(ExampleCriteriaProvider), environmentCriteria.GetProviderType());
            Assert.AreEqual(typeof(ExampleSetupProvider), environmentSetup.GetProviderType());
            Assert.AreEqual(typeof(ExampleCleanupProvider), environmentCleanup.GetProviderType());
            Assert.AreEqual(typeof(ExampleWorkloadProvider), workload.GetProviderType());
            Assert.AreEqual(typeof(ExamplePayloadProvider), payload.GetProviderType());
            Assert.AreEqual(typeof(ExampleWatchdogProvider), watchdog.GetProviderType());
        }

        [Test]
        public void GetSupportedStepTargetsExtensionReturnsTheExpectedStepTargetForAGivenComponent()
        {
            ExperimentComponent environmentCriteria = FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), "Environment Criteria");
            ExperimentComponent environmentSetup = FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), "Environment Setup");
            ExperimentComponent environmentCleanup = FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), "Environment Cleanup");
            ExperimentComponent workload = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Workload");
            ExperimentComponent payload = FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider), "Payload");
            ExperimentComponent watchdog = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), "Watchdog");

            Assert.AreEqual(SupportedStepTarget.ExecuteRemotely, environmentCriteria.GetSupportedStepTarget());
            Assert.AreEqual(SupportedStepTarget.ExecuteRemotely, environmentSetup.GetSupportedStepTarget());
            Assert.AreEqual(SupportedStepTarget.ExecuteRemotely, environmentCleanup.GetSupportedStepTarget());
            Assert.AreEqual(SupportedStepTarget.ExecuteOnVirtualMachine, workload.GetSupportedStepTarget());
            Assert.AreEqual(SupportedStepTarget.ExecuteRemotely, payload.GetSupportedStepTarget());
            Assert.AreEqual(SupportedStepTarget.ExecuteOnVirtualMachine, watchdog.GetSupportedStepTarget());
        }

        [Test]
        public void GetSupportedStepTargetsExtensionReturnsTheExpectedStepTargetForAGivenProvider()
        {
            Assert.AreEqual(SupportedStepTarget.ExecuteRemotely, typeof(ExampleCriteriaProvider).GetSupportedStepTarget());
            Assert.AreEqual(SupportedStepTarget.ExecuteRemotely, typeof(ExampleSetupProvider).GetSupportedStepTarget());
            Assert.AreEqual(SupportedStepTarget.ExecuteRemotely, typeof(ExampleCleanupProvider).GetSupportedStepTarget());
            Assert.AreEqual(SupportedStepTarget.ExecuteOnVirtualMachine, typeof(ExampleWorkloadProvider).GetSupportedStepTarget());
            Assert.AreEqual(SupportedStepTarget.ExecuteRemotely, typeof(ExamplePayloadProvider).GetSupportedStepTarget());
            Assert.AreEqual(SupportedStepTarget.ExecuteOnVirtualMachine, typeof(ExampleWatchdogProvider).GetSupportedStepTarget());
        }

        [Test]
        public void GetSupportedStepTypeExtensionReturnsTheExpectedStepTypeForAGivenComponent()
        {
            ExperimentComponent environmentCriteria = FixtureExtensions.CreateExperimentComponent(typeof(ExampleCriteriaProvider), "Environment Criteria");
            ExperimentComponent environmentSetup = FixtureExtensions.CreateExperimentComponent(typeof(ExampleSetupProvider), "Environment Setup");
            ExperimentComponent environmentCleanup = FixtureExtensions.CreateExperimentComponent(typeof(ExampleCleanupProvider), "Environment Cleanup");
            ExperimentComponent workload = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWorkloadProvider), "Workload");
            ExperimentComponent payload = FixtureExtensions.CreateExperimentComponent(typeof(ExamplePayloadProvider), "Payload");
            ExperimentComponent watchdog = FixtureExtensions.CreateExperimentComponent(typeof(ExampleWatchdogProvider), "Watchdog");

            Assert.AreEqual(SupportedStepType.EnvironmentCriteria, environmentCriteria.GetSupportedStepType());
            Assert.AreEqual(SupportedStepType.EnvironmentSetup, environmentSetup.GetSupportedStepType());
            Assert.AreEqual(SupportedStepType.EnvironmentCleanup, environmentCleanup.GetSupportedStepType());
            Assert.AreEqual(SupportedStepType.Workload, workload.GetSupportedStepType());
            Assert.AreEqual(SupportedStepType.Payload, payload.GetSupportedStepType());
            Assert.AreEqual(SupportedStepType.Watchdog, watchdog.GetSupportedStepType());
        }

        [Test]
        public void GetSupportedStepTypeExtensionReturnsTheExpectedStepTypeForAGivenProvider()
        {
            Assert.AreEqual(SupportedStepType.EnvironmentCriteria, typeof(ExampleCriteriaProvider).GetSupportedStepType());
            Assert.AreEqual(SupportedStepType.EnvironmentSetup, typeof(ExampleSetupProvider).GetSupportedStepType());
            Assert.AreEqual(SupportedStepType.EnvironmentCleanup, typeof(ExampleCleanupProvider).GetSupportedStepType());
            Assert.AreEqual(SupportedStepType.Workload, typeof(ExampleWorkloadProvider).GetSupportedStepType());
            Assert.AreEqual(SupportedStepType.Payload, typeof(ExamplePayloadProvider).GetSupportedStepType());
            Assert.AreEqual(SupportedStepType.Watchdog, typeof(ExampleWatchdogProvider).GetSupportedStepType());
        }
    }
}
