namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Attribute that directs the validation of kusto results 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class KustoColumnAttribute : Attribute
    {
        /// <summary>
        /// Name of the Kusto Column
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Defines if this is additional Info or high priority information.
        /// </summary>
        public bool AdditionalInfo { get; set; }

        /// <summary>
        /// Determines if this value is used as part of
        /// the cache key
        /// </summary>
        public bool ComposesCacheKey { get; set; }
    }
}
