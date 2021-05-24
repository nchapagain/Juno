namespace Juno
{
    using System;
    using System.IO;
    using System.Reflection;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;

    /// <summary>
    /// Common dependencies for provider tests.
    /// </summary>
    public class ProviderFixture : FixtureDependencies
    {
        private static Assembly testAssembly = Assembly.GetAssembly(typeof(ProviderFixture));

        private Type fixtureProviderType;
        private string fixtureAgentId;
        private string fixtureParentStepId;
        private ExperimentType fixtureExperimentType;

        /// <summary>
        /// Initializes a <see cref="ProviderFixture"/> for a specific provider
        /// type.
        /// </summary>
        public ProviderFixture(Type providerType, string agentId = null, string parentStepId = null, ExperimentType experimentType = ExperimentType.AB)
        {
            this.Initialize(providerType, agentId, parentStepId, null, experimentType);
        }

        /// <summary>
        /// Mock experiment context to supply to providers in-test.
        /// </summary>
        public ExperimentContext Context { get; set; }

        /// <summary>
        /// Mock experiment component to supply to providers in-test.
        /// </summary>
        public ExperimentComponent Component { get; set; }

        /// <summary>
        /// Mock provider data client.
        /// </summary>
        public Mock<IProviderDataClient> DataClient { get; set; }

        /// <summary>
        /// Mock entity manager.
        /// </summary>
        public EntityManager EntityManager { get; set; }

        /// <summary>
        /// The mock <see cref="ExperimentInstance"/> object.
        /// </summary>
        public ExperimentInstance Experiment
        {
            get
            {
                return this.Context.Experiment;
            }
        }

        /// <summary>
        /// The ID of the mock experiment from the <see cref="ExperimentContext"/> instance
        /// defined for the provider fixture.
        /// </summary>
        public string ExperimentId
        {
            get
            {
                return this.Context.Experiment.Id;
            }
        }

        /// <summary>
        /// Changes the setup of the provider fixture.
        /// </summary>
        /// <param name="experimentType">The experiment type to setup for the fixture (e.g. A/B, A/B/C).</param>
        /// <param name="environmentGroup">The experiment group for the workflow component and step (e.g. Group A, Group B).</param>
        public void Setup(ExperimentType experimentType, string environmentGroup = null)
        {
            this.Initialize(this.fixtureProviderType, this.fixtureAgentId, this.fixtureParentStepId, environmentGroup, experimentType);
        }

        private void Initialize(Type providerType, string agentId = null, string parentStepId = null, string experimentGroup = null, ExperimentType experimentType = ExperimentType.AB)
        {
            this.fixtureProviderType = providerType;
            this.fixtureAgentId = agentId;
            this.fixtureParentStepId = parentStepId;
            this.fixtureExperimentType = experimentType;

            this.SetupExperimentMocks(experimentType);
            this.SetupAgentMocks();

            string configurationFilePath = Path.Combine(
                Path.GetDirectoryName(ProviderFixture.testAssembly.Location),
                @"Configuration\juno-dev01.environmentsettings.json");

            if (!File.Exists(configurationFilePath))
            {
                throw new FileNotFoundException(
                    $"Expected configuration file not found. The {nameof(ProviderFixture)} class depends upon this file to setup testing/mock dependencies.");
            }

            this.Component = FixtureExtensions.CreateExperimentComponent(providerType, group: experimentGroup);
            this.Context = new ExperimentContext(
                this.Create<ExperimentInstance>(),
                this.CreateExperimentStep(this.Component, agentId, parentStepId),
                new ConfigurationBuilder().AddJsonFile(configurationFilePath).Build());

            this.DataClient = new Mock<IProviderDataClient>();
            this.EntityManager = new EntityManager(this.DataClient.Object);

            this.Services = new ServiceCollection();
            this.Services.AddSingleton(this.DataClient.Object);
            this.Services.AddSingleton(this.Create<AgentIdentification>());
        }
    }
}
