namespace Juno.Execution.Providers.Diagnostics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Hosting.Common;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NUnit.Framework;
    using static Juno.Execution.Providers.Diagnostics.SleepProvider;

    [TestFixture]
    [Category("Unit")]
    public class SleepProviderTests
    {
        private string testDuration = "0:01:00:00";
        private ProviderFixture mockFixture;
        private SleepProvider provider;
        private SleepProvider.State providerState;
        private IEnumerable<ExperimentStepInstance> mockExperimentSteps;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(SleepProvider));
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockFixture.Component = this.mockFixture.Create<ExperimentComponent>();
            this.mockFixture.Component.Parameters.Add("duration", this.testDuration);
            this.mockFixture.Services.AddSingleton(NullLogger.Instance);
            this.mockFixture.Services.AddSingleton(this.mockFixture.DataClient);

            this.provider = new SleepProvider(this.mockFixture.Services);

            // Setup the default/happy path for the provider tests
            this.providerState = new SleepProvider.State
            {
                SleepStartTime = DateTime.UtcNow,
                SleepEndTime = DateTime.UtcNow.AddHours(1)
            };

            this.mockExperimentSteps = new List<ExperimentStepInstance>
            {
                this.mockFixture.CreateExperimentStep(),
                this.mockFixture.CreateExperimentStep(),
                this.mockFixture.Context.ExperimentStep
            };

            this.mockExperimentSteps.SetSequences();

            this.mockFixture.DataClient.SetReturnsDefault(new HttpResponseMessage(HttpStatusCode.OK));
            this.mockFixture.DataClient.OnGetState<SleepProvider.State>().Returns(Task.FromResult(this.providerState));
            this.mockFixture.DataClient.OnSaveState<SleepProvider.State>().Returns(Task.CompletedTask);
            this.mockFixture.DataClient.OnGetExperimentSteps().Returns(Task.FromResult(this.mockExperimentSteps));
        }

        [Test]
        public void SleepProviderValidatesRequiredParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteAsync(null, this.mockFixture.Component, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteAsync(this.mockFixture.Context, null, CancellationToken.None));
        }

        [Test]
        public async Task SleepProviderReturnsTheExpectedResultWhenTheSleepDurationIsNotExceeded()
        {
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Provider should return InProgress when time not reached
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);
        }

        [Test]
        public async Task SleepProviderReturnsTheExpectedResultWhenTheSleepDurationIsExceeded()
        {
            this.providerState.SleepStartTime = DateTime.UtcNow - TimeSpan.FromHours(1);
            this.providerState.SleepEndTime = DateTime.UtcNow - TimeSpan.FromSeconds(1);

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Provider should return succeed when time reached.
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }

        [Test]
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.InProgress)]
        [TestCase(ExecutionStatus.InProgressContinue)]
        [TestCase(ExecutionStatus.Pending)]
        [TestCase(ExecutionStatus.Succeeded)]
        public async Task SleepProvider_WithOption_WhenAnyPreviousStepsFailed_ReturnsTheExpectedResultWhenThereAreNoPreviousStepsFailedDuringTheSleepDuration(ExecutionStatus status)
        {
            this.mockFixture.Component.Parameters.Add("option", SleepProviderOption.WhenAnyPreviousStepsFailed);
            this.mockExperimentSteps.ForEach(step => step.Status = status);

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Provider should return Succeeded if there aren't any previous steps failed.
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }

        [Test]
        public async Task SleepProvider_WithOption_WhenAnyPreviousStepsFailed_ReturnsTheExpectedResultWhenAnyPreviousStepsHaveFailedDuringTheSleepDuration()
        {
            this.mockFixture.Component.Parameters.Add("option", SleepProviderOption.WhenAnyPreviousStepsFailed);

            // Ensure at least 1 previous step is failed
            this.mockExperimentSteps.First().Status = ExecutionStatus.Failed;

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Provider should return InProgress if there is at least 1 previous step failed.
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);
        }

        [Test]
        [TestCase(ExecutionStatus.Cancelled)]
        [TestCase(ExecutionStatus.InProgress)]
        [TestCase(ExecutionStatus.InProgressContinue)]
        [TestCase(ExecutionStatus.Pending)]
        [TestCase(ExecutionStatus.Succeeded)]
        public async Task SleepProvider_WithOption_WhenNoPreviousStepsFailed_ReturnsTheExpectedResultWhenThereAreNoPreviousStepsFailedDuringTheSleepDuration(ExecutionStatus status)
        {
            this.mockFixture.Component.Parameters.Add("option", SleepProviderOption.WhenNoPreviousStepsFailed);
            this.mockExperimentSteps.ForEach(step => step.Status = status);

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Provider should return InProgress if there aren't any previous steps that are failed.
            Assert.AreEqual(ExecutionStatus.InProgress, result.Status);
        }

        [Test]
        public async Task SleepProvider_WithOption_WhenNoPreviousStepsFailed_ReturnsTheExpectedResultWhenThereArePreviousStepsFailedDuringTheSleepDuration()
        {
            this.mockFixture.Component.Parameters.Add("option", SleepProviderOption.WhenNoPreviousStepsFailed);

            // Ensure at least 1 previous step is failed
            this.mockExperimentSteps.First().Status = ExecutionStatus.Failed;

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Provider should return Succeeded if there is at least 1 previous step that is failed.
            Assert.AreEqual(ExecutionStatus.Succeeded, result.Status);
        }

        [Test]
        public async Task SleepProviderSavesStateAsExpected()
        {
            this.providerState.SleepStartTime = DateTime.UtcNow;
            this.providerState.SleepEndTime = DateTime.UtcNow.AddHours(1);

            // Save state
            this.mockFixture.DataClient.Setup(c => c.SaveStateAsync<State>(
                this.mockFixture.Context.Experiment.Id, $"state-{this.mockFixture.Context.ExperimentStep.Id}", this.providerState, It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .Verifiable();

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            this.mockFixture.DataClient.Verify();
        }

        /// <summary>
        /// Check if expected time equals actual with a small buffer just for running the test: 1second.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="actual"></param>
        /// <returns></returns>
        private void CheckTime(DateTime expected, DateTime actual)
        {
            Assert.IsTrue(Math.Abs(expected.Ticks - actual.Ticks) < TimeSpan.FromSeconds(1).Ticks, 
                $"Expected: {expected}. Actual: {actual}. Expected: {expected.Ticks}. Actual: {actual.Ticks}.");
        }
    }
}
