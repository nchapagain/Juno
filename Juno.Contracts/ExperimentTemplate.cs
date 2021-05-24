using System;
using System.Collections.Generic;
using System.Text;

namespace Juno.Contracts
{
    /// <summary>
    /// Represents an Experiment Template definition
    /// </summary>
    public class ExperimentTemplate
    {
        /// <summary>
        /// Default constructor of Experiment Template class
        /// </summary>
        public ExperimentTemplate()
        {
        }

        /// <summary>
        /// Instance of an Experiment class
        /// </summary>
        public Experiment Experiment { get; set; }

        /// <summary>
        /// String to overwrite the Experiment class 
        /// </summary>
        public string Override { get; set; }
    }
}
