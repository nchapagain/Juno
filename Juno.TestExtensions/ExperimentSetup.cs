namespace Juno
{
    using Juno.Contracts;

    /// <summary>
    /// Provides setup/configuration instructions to Juno auto-fixture
    /// extensions. To supply an override to the auto-fixture defaults, the developer
    /// would simply call the fixture's 'Register' method in an individual unit test
    /// and supply the desired setup.
    /// </summary>
    public class ExperimentSetup
    {
        /// <summary>
        /// Gets or sets the ID that should be used for experiment instances
        /// (and related object instances that reference an experiment ID, e.g. experiment context).
        /// </summary>
        public string ExperimentId { get; set; }

        /// <summary>
        /// Gets or sets true false whether the fixture extensions 
        /// should create shared environment criteria (vs. group-specific)
        /// when creating mock <see cref="Experiment"/> instances.
        /// </summary>
        public bool IncludeSharedEnvironmentCriteria { get; set; }
    }
}
