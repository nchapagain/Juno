namespace Juno.Contracts
{
    using System;

    /// <summary>
    /// This class will be used for validation of experiment component
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ProviderInfoAttribute : Attribute
    {
        /// <summary>
        /// The name of the parameter
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The description of the parameter
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The full description of the parameter
        /// </summary>
        public string FullDescription { get; set; }
    }
}
