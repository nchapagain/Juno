namespace Juno.CRCTipBladeCertification
{
    using Juno.CRCTipBladeCertification.Contracts;

    /// <summary>
    /// Provides the ability to certify the blade
    /// </summary>
    public interface IBladeCertificationProvider
    {
        /// <summary>
        /// Certifies the blade according to the provider rules
        /// </summary>
        /// <returns>CertificationResult which has the result and any errors</returns>
        CertificationResult Certify();

        /// <summary>
        /// Collects the baseline for the current blade
        /// </summary>
        void CollectBaseline();
    }
}
