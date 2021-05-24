namespace Juno.CRCTipBladeCertification.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using Juno.CRCTipBladeCertification.Contracts;
    using Juno.Execution.AgentRuntime;
    using Juno.Execution.AgentRuntime.Windows;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Org.XmlUnit.Builder;
    using Org.XmlUnit.Diff;

    /// <summary>
    /// Certifies the blade using the blade sku check output for hardware
    /// and firmware
    /// </summary>
    public class BscBladeCertificationProvider : IBladeCertificationProvider
    {        
        private readonly string providerPrefix = $"{nameof(BscBladeCertificationProvider)}_";
        private IBscReader bscReader;

        /// <summary>
        /// Creates an instance of the BSCBladeCertification provider
        /// </summary>
        /// <param name="bscReader">BscReader to call actual BSC code</param>
        public BscBladeCertificationProvider(IBscReader bscReader = null)
        {
            if (bscReader != null)
            {
                this.bscReader = bscReader;
            }
            else
            {
                this.bscReader = new BscReader();
            }
        }

        /// <inheritdoc/>
        public CertificationResult Certify()
        {
            var result = this.RunBSCCompare(out var errors);
            return new CertificationResult
            {
                CertificationPassed = result,
                Error = errors,
                ProviderName = nameof(BscBladeCertificationProvider)
            };
        }

        /// <inheritdoc/>
        public void CollectBaseline()
        {
            var filePaths = Directory.GetCurrentDirectory();
            var filePath = Path.Combine(filePaths, this.providerPrefix);
            var beforeFile = $"{filePath}before.xml";
            var bscResult = this.RunBsc();
            File.WriteAllText($"{beforeFile}", bscResult);
        }

        private static bool CompareXml(string beforeXml, string afterXml, out string errors)
        {
            var xmlFileBefore = File.ReadAllText(beforeXml);
            var xmlFileAfter = File.ReadAllText(afterXml);
            Diff myDiff = DiffBuilder.Compare(xmlFileBefore)
            .WithTest(xmlFileAfter)
            .CheckForSimilar().CheckForIdentical()
            .IgnoreComments()
            .IgnoreWhitespace()
            .NormalizeWhitespace().Build();

            if (!myDiff.HasDifferences())
            {
                errors = string.Empty;
                return true;
            }

            StringBuilder differences = new StringBuilder();
            foreach (var diff in myDiff.Differences)
            {
                differences.Append(diff.Comparison);
                differences.Append(";");
            }

            errors = differences.ToString();
            return false;
        }

        private string RunBsc()
        {
            return this.bscReader.Read();
        }

        private bool RunBSCCompare(out string errors)
        {
            var filePaths = Directory.GetCurrentDirectory();
            var filePath = Path.Combine(filePaths, this.providerPrefix);
            var afterFile = $"{filePath}after.xml";
            var beforeFile = $"{filePath}before.xml";

            var baselineExists = File.Exists(beforeFile);

            if (!baselineExists)
            {
                errors = "No before file found";
                return false;
            }
            else
            {
                File.Delete(afterFile);
                var bscResult = this.RunBsc();
                File.WriteAllText(afterFile, bscResult);
                return BscBladeCertificationProvider.CompareXml(beforeFile, afterFile, out errors);
            }
        }
    }
}
