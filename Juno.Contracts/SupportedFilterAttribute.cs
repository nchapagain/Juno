namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Attribute contract that allows the definitions of  parameters for EnvironmentFilters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class SupportedFilterAttribute : Attribute
    {
        /// <summary>
        /// The name of the parameter
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// The type of the parameter
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// Whether this is a required parameter
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// The name of the set where a singleton must exist
        /// </summary>
        public string SetName { get; set; }

        /// <summary>
        /// The label associated with values from kusto and the cache
        /// </summary>
        public string CacheLabel { get; set; }

        /// <summary>
        /// Allow for a default parameter value for not required parameters that 
        /// are needed to query the cache.
        /// </summary>
        public string Default { get; set; }
    }
}
