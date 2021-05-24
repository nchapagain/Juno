namespace Juno.Contracts
{
    using System;

    /// <summary>
    /// Attribute decorates experiment provider classes defining metadata about the
    /// provider.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ExecutionConstraintsAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionConstraintsAttribute"/> class.
        /// </summary>
        /// <param name="stepType">The type of step (e.g. EnvironmentCriteria, EnvironmentSetup, Payload, Workload).</param>
        /// <param name="stepTarget">The hosting target for the step (e.g. ExecuteOnNode, ExecuteRemotely, ExecuteOnVirtualMachine).</param>
        public ExecutionConstraintsAttribute(SupportedStepType stepType, SupportedStepTarget stepTarget)
        {
            this.StepType = stepType;
            this.StepTarget = stepTarget;
        }

        /// <summary>
        /// Gets the type of provider (e.g. EnvironmentCriteria, EnvironmentSetup, Payload, Workload).
        /// </summary>
        public SupportedStepType StepType { get; }

        /// <summary>
        /// Gets the hosting target for the step (e.g. ExecuteOnNode, ExecuteRemotely, ExecuteOnVirtualMachine).
        /// </summary>
        public SupportedStepTarget StepTarget { get; }
    }
}
