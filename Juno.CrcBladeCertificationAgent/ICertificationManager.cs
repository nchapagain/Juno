namespace Juno.CRCTipBladeCertification
{
    internal interface ICertificationManager
    {
        /// <summary>
        /// Certifies the blade according to the provider rules
        /// </summary>
        /// <returns>True if blade is certified for cleanup. Otherwise, false</returns>
        bool CompareWithBaseline(out string errors);

        /// <summary>
        /// Collects the baseline for all the certification providers
        /// </summary>
        void CollectBaseline();
    }
}
