namespace Juno.Contracts
{
    using System;

    /// <summary>
    /// This class will be used for validation of experiment component
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class SupportedParameterAttribute : Attribute
    {
        /// <summary>
        /// The name of the parameter
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of parameter
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// Whether this is a required parameter.
        /// </summary>
        public bool Required { get; set; }
    }
}
