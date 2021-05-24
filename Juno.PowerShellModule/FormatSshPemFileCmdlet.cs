namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Management.Automation;
    using System.Security.AccessControl;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Azure.CRC.Rest;

    /// <summary>
    /// Powershell Module to format the ssh key file.
    /// </summary>
    [Cmdlet(VerbsCommon.Format, "SshPemFile")]
    public class FormatSshPemFileCmdlet : ExperimentCmdletBase
    {
        private IFileSystem fileSystem;

        /// <summary>
        /// Constructor for FormatSshPemFile class
        /// </summary>
        public FormatSshPemFileCmdlet(IFileSystem fileSystem = null)
            : base()
        {
            this.fileSystem = fileSystem ?? new FileSystem();
        }

        /// <summary>
        /// <para type="description">
        /// SSH private key.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [Alias("PrivateKey")]
        public string PrivateKey { get; set; }

        /// <summary>
        /// <para type="description">
        /// FilePath for the pem file.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [Alias("FilePath")]
        public string FilePath { get; set; }

        /// <summary>
        /// Executes the operation to get Experiment resources.
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();
                if (this.ValidateParameters())
                {
                    this.WritePemFileAsync(this.PrivateKey, this.FilePath).GetAwaiter().GetResult();
                    this.ProvisionPermission(this.FilePath);
                }
                else
                {
                    throw new ArgumentException($"Invalid arguments passed to module. Required arguments are {nameof(this.FilePath).ToString()} and {nameof(this.PrivateKey).ToString()}.");
                }
            }
            catch (Exception ex)
            {
                this.WriteObject(ex.Message);
            }
        }

        /// <summary>
        /// Validates the parameters passed into the commandlet.
        /// </summary>
        protected override bool ValidateParameters()
        {
            base.ValidateParameters();
            bool isValid = true;
            if (string.IsNullOrEmpty(this.PrivateKey) || string.IsNullOrEmpty(this.FilePath))
            {
                isValid = false;
            }

            return isValid;
        }

        private Task WritePemFileAsync(string privatekey, string filePath)
        {
            string pemString = RsaCrypto.ToPemFormat("PRIVATE KEY", Convert.FromBase64String(privatekey));
            return this.fileSystem.File.WriteAllTextAsync(filePath, pemString);
        }

        private void ProvisionPermission(string filePath)
        {
            IFileInfo pemInfo = this.fileSystem.FileInfo.FromFileName(filePath);
            FileSecurity fileSecurity = pemInfo.GetAccessControl(AccessControlSections.All);
            // The first true protects from future inheritance and the false disable inheritance on this.
            fileSecurity.SetAccessRuleProtection(true, false);
            var exisitingRules = fileSecurity.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(System.Security.Principal.NTAccount));
            var owner = fileSecurity.GetOwner(typeof(System.Security.Principal.NTAccount));
            foreach (AuthorizationRule rule in exisitingRules)
            {
                FileSystemAccessRule fileRule = rule as FileSystemAccessRule;
                if (fileRule != null)
                {
                    fileSecurity.RemoveAccessRule(fileRule);
                }
            }

            fileSecurity.ModifyAccessRule(
                AccessControlModification.Add,
                new FileSystemAccessRule(owner, FileSystemRights.Modify, AccessControlType.Allow),
                out bool modified);
            pemInfo.SetAccessControl(fileSecurity);
        }
    }
}
