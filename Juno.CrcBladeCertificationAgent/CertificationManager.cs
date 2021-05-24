namespace Juno.CRCTipBladeCertification.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Juno.CRCTipBladeCertification.Contracts;

    /// <summary>
    /// Certification manager manages all the certification
    /// providers and their results
    /// </summary>
    public class CertificationManager : ICertificationManager
    {
        private const string CanDeleteFile = "canDelete.csv";
        private const string CannotDeleteFile = "cannotDelete.csv";

        private List<IBladeCertificationProvider> certificationProviders;

        /// <inheritdoc/>
        public CertificationManager(List<IBladeCertificationProvider> certificationProviders = null)
        {
            if (certificationProviders != null)
            {
                this.certificationProviders = certificationProviders;
            }
            else
            {
                this.certificationProviders = new List<IBladeCertificationProvider>
                {
                    new BscBladeCertificationProvider(),
                    new SPSBladeCertificationProvider()
                };
            }
        }

        /// <inheritdoc/>
        public bool CompareWithBaseline(out string errors)
        {
            StringBuilder certificationMessage = new StringBuilder();
            var results = new Dictionary<string, CertificationResult>();
            foreach (var provider in this.certificationProviders)
            {
                var result = provider.Certify();
                results.Add(result.ProviderName, result);
            }

            foreach (var result in results)
            {
                certificationMessage.AppendLine($"{result.Key}, {result.Value.CertificationPassed}, {result.Value.Error}");
            }

            errors = certificationMessage.ToString();
            if (results.Where(s => s.Value.CertificationPassed != true).Any())
            {
                // we have some failures
                File.WriteAllText(CertificationManager.CannotDeleteFile, certificationMessage.ToString());
                return false;
            }
            else
            {
                // no failures
                File.WriteAllText(CertificationManager.CanDeleteFile, certificationMessage.ToString());
                return true;
            }
        }

        /// <inheritdoc/>
        public void CollectBaseline()
        {
            foreach (var provider in this.certificationProviders)
            {
                provider.CollectBaseline();
            }
        }
    }
}
