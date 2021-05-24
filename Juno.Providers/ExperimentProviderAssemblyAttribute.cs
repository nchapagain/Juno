namespace Juno.Providers
{
    using System;

    /// <summary>
    /// Attribute is used to mark an assembly as one that contains <see cref="ExperimentProvider"/>
    /// implementations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class ExperimentProviderAssemblyAttribute : Attribute
    {
    }
}
