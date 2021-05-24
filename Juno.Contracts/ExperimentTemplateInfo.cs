using System;
using System.Collections.Generic;
using System.Text;

namespace Juno.Contracts
{
    /// <summary>
    /// Represents an Experiment Template definition
    /// </summary>
    public class ExperimentTemplateInfo
    {
        /// <summary>
        /// Default constructor of Experiment Template class
        /// </summary>
        public ExperimentTemplateInfo()
        {
        }

        /// <summary>
        /// Id of an Experiment Template
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Description of the Experiment Template 
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Team Name of the Experiment Template 
        /// </summary>
        public string TeamName { get; set; }
    }
}