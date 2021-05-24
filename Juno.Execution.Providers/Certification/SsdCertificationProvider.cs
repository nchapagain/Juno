namespace Juno.Execution.Providers.Certification
{
    using Juno.Contracts;
    using Juno.Execution.Providers.Verification;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Certifies the SSD version for the given models are the expected firmwares.
    /// </summary>
    [ExecutionConstraints(SupportedStepType.Certification, SupportedStepTarget.ExecuteOnNode)]
    public class SsdCertificationProvider : SsdVerificationProvider
    {
        /// <summary>
        /// Initalizes a new instance of <see cref="SsdCertificationProvider"/>
        /// </summary>
        /// <param name="services">Collection of services used for dependency injection.</param>
        public SsdCertificationProvider(IServiceCollection services)
            : base(services)
        { 
        }
    }
}
