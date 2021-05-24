namespace Juno
{
    using System.Collections.Generic;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution;

    /// <summary>
    /// Common dependencies for tests that involve experiments and 
    /// experiment steps.
    /// </summary>
    public class ExperimentFixture : FixtureDependencies
    {
        private ExperimentStepFactory stepFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentFixture"/> class.
        /// </summary>
        public ExperimentFixture()
            : base()
        {
            this.stepFactory = new ExperimentStepFactory();
        }

        /// <summary>
        /// A mock experiment definition.
        /// </summary>
        public Experiment Experiment { get; set; }

        /// <summary>
        /// A mock experiment instance.
        /// </summary>
        public ExperimentInstance ExperimentInstance { get; set; }

        /// <summary>
        /// A mock experiment notice-of-work.
        /// </summary>
        public ExperimentMetadataInstance ExperimentNotice { get; set; }

        /// <summary>
        /// A set of mock experiment steps.
        /// </summary>
        public List<ExperimentStepInstance> ExperimentSteps { get; set; }

        /// <summary>
        /// Sets up the fixture using the values defined for the properties.
        /// </summary>
        /// <returns></returns>
        public ExperimentFixture Setup()
        {
            this.SetupExperimentMocks();
            this.SetupAgentMocks();

            if (this.Experiment == null)
            {
                this.Experiment = this.Create<Experiment>();
            }

            if (this.ExperimentInstance == null)
            {
                this.ExperimentInstance = this.Create<ExperimentInstance>();
            }

            if (this.ExperimentSteps == null)
            {
                this.ExperimentSteps = new List<ExperimentStepInstance>(
                    this.stepFactory.CreateOrchestrationSteps(this.Experiment.Workflow, this.ExperimentInstance.Id, 100));
            }

            return this;
        }
    }
}
