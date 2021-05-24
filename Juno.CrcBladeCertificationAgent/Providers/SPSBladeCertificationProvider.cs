namespace Juno.CRCTipBladeCertification.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Configuration.Provider;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using Juno.Contracts;
    using Juno.CRCTipBladeCertification.Contracts;
    using Juno.Execution.AgentRuntime;
    using Juno.Execution.AgentRuntime.Contract;
    using Juno.Execution.AgentRuntime.Windows;
    using Kusto.Cloud.Platform.Utils;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    /// <summary>
    /// Certifies the blade using the Bios setting output
    /// for the intel sps version 
    /// </summary>
    public class SPSBladeCertificationProvider : IBladeCertificationProvider
    {
        private readonly string providerPrefix = $"{nameof(SPSBladeCertificationProvider)}_";
        private IFirmwareReader<BiosInfo> spsReader;

        /// <summary>
        /// Creates an instance of the SPSBladeCertification provider
        /// </summary>
        /// <param name="spsReader">SPSReader to call actual GetIntelSPS code</param>
        public SPSBladeCertificationProvider(IFirmwareReader<BiosInfo> spsReader = null)
        {
            this.spsReader = spsReader ?? new BiosReader();
        }

        /// <inheritdoc/>
        public CertificationResult Certify()
        {
            try
            {
                bool spsCompare = this.RunSPSCompare(out var errors);
                return new CertificationResult
                {
                    CertificationPassed = spsCompare,
                    Error = errors,
                    ProviderName = nameof(SPSBladeCertificationProvider)
                };
            }
            catch (Exception ex)
            {
                throw new CertificationException($"The SPS Certification failed {ex}");
            }
        }

        /// <inheritdoc/>
        public void CollectBaseline()
        {
            string filePaths = Directory.GetCurrentDirectory();
            string filePath = Path.Combine(filePaths, this.providerPrefix);
            string beforeFile = $"{filePath}before.log";
            try
            {
                string spsResult = this.GetSPS();
                File.WriteAllText($"{beforeFile}", spsResult);
            }
            catch (Exception ex)
            {
                throw new CertificationException($"Collect Baseline failed {ex}");
            }
        }

        private static bool CompareLog(string beforeLog, string afterLog, out string errors)
        {
            bool result;
            string logFileBefore = File.ReadAllText(beforeLog);
            string logFileAfter = File.ReadAllText(afterLog);

            if (logFileBefore.Equals(logFileAfter))
            {
                errors = string.Empty;
                result = true;
            }
            else
            {
                errors = logFileAfter;
                result = false;
            }

            return result;
        }

        private string GetSPS()
        {
            return this.spsReader.Read().SpsVersion;
        }

        private bool RunSPSCompare(out string errors)
        {
            string filePaths = Directory.GetCurrentDirectory();
            string filePath = Path.Combine(filePaths, this.providerPrefix);
            string afterFile = $"{filePath}after.log";
            string beforeFile = $"{filePath}before.log";
            bool result;

            bool baselineExists = File.Exists(beforeFile);

            if (!baselineExists)
            {
                errors = "No before file found";
                result = false;
            }
            else
            {
                File.Delete(afterFile);
                string spsResult = this.GetSPS();
                File.WriteAllText(afterFile, spsResult);
                result = SPSBladeCertificationProvider.CompareLog(beforeFile, afterFile, out errors);
            }

            return result;
        }
    }
}
