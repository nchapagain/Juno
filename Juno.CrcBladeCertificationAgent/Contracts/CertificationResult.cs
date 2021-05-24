namespace Juno.CRCTipBladeCertification.Contracts
{
    /// <summary>
    /// Describes the result of a blade certification
    /// </summary>
    public class CertificationResult
    {
        /// <summary>
        /// Indicates whether the certification was successful
        /// </summary>
        public bool CertificationPassed { get; set; }

        /// <summary>
        /// Name of the provider who certified the result
        /// </summary>
        public string ProviderName { get; set; }

        /// <summary>
        /// Any errors found during certification
        /// </summary>
        public string Error { get; set; }
    }
}
