namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Provides context information and dependencies required to execute
    /// experiment provider workflows.
    /// </summary>
    public class ExperimentContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentContext"/> object.
        /// </summary>
        /// <param name="experiment">The experiment for which the context is associated.</param>
        /// <param name="experimentStep">The experiment step for which the context is associated.</param>
        /// <param name="configuration">Configuration/settings associated with the environment.</param>
        public ExperimentContext(ExperimentInstance experiment, ExperimentStepInstance experimentStep, IConfiguration configuration)
        {
            experimentStep.ThrowIfNull(nameof(experimentStep));
            configuration.ThrowIfNull(nameof(configuration));

            this.Configuration = configuration;
            this.Experiment = experiment;
            this.ExperimentStep = experimentStep;
        }

        /// <summary>
        /// Gets the experiment configuration/settings.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Gets the experiment for which the context is associated.
        /// </summary>
        public ExperimentInstance Experiment { get; }

        /// <summary>
        /// Gets the experiment ID for which the context is associated.
        /// </summary>
        public string ExperimentId
        {
            get
            {
                return this.Experiment.Id;
            }
        }

        /// <summary>
        /// Gets the the experiment step for which the context is associated.
        /// </summary>
        public ExperimentStepInstance ExperimentStep { get; }

        /// <summary>
        /// Gets the set of groups/group names associated with the experiment.
        /// </summary>
        public IEnumerable<string> GetExperimentGroups()
        {
            return ExperimentContext.GetExperimentGroups(this.Experiment.Definition.Workflow);
        }

        private static IEnumerable<string> GetExperimentGroups(IEnumerable<ExperimentComponent> components)
        {
            HashSet<string> uniqueGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ExperimentComponent component in components)
            {
                if (component.IsParallelExecution())
                {
                    ExperimentContext.GetExperimentGroups(component.GetChildSteps())?.ToList().ForEach(group => uniqueGroups.Add(group));
                }
                else if (!string.IsNullOrWhiteSpace(component.Group) && component.Group != ExperimentComponent.AllGroups)
                {
                    uniqueGroups.Add(component.Group);
                }
            }

            return uniqueGroups.Distinct().OrderBy(name => name);
        }
    }
}
