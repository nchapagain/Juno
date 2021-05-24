namespace Juno.Execution.ArmIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Juno.Execution.ArmIntegration;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Azure.CRC.Identity;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Data contract for a virtual machine
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Need to modify tags and disks.")]
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class VmDefinition : ResourceState
    {
        /// <summary>
        /// Get or set OS disk type
        /// </summary>
        public string OsDiskStorageAccountType { get; set; }

        /// <summary>
        /// Get or set virtual machine size
        /// Sizes for Windows virtual machines in Azure
        /// https://docs.microsoft.com/en-us/azure/virtual-machines/windows/sizes?toc=%2Fazure%2Fvirtual-machines%2Fwindows%2Ftoc.json
        /// </summary>
        public string VirtualMachineSize { get; set; }

        /// <summary>
        /// Get or set the image reference.
        /// https://docs.microsoft.com/en-us/azure/templates/microsoft.compute/2019-07-01/virtualmachines#ImageReference
        /// </summary>
        public JObject ImageReference { get; set; }

        /// <summary>
        /// Get or set virtual machine name
        /// </summary>
        public string VirtualMachineName { get; set; }

        /// <summary>
        /// Get or set admin user name
        /// </summary>
        public string AdminUserName { get; set; }

        /// <summary>
        /// Get or set admin public key secret name
        /// </summary>
        public string AdminSshPublicKeySecretName { get; set; }

        /// <summary>
        /// Get or set admin private key secret name
        /// </summary>
        public string AdminSshPrivateKeySecretName { get; set; }

        /// <summary>
        /// Get or set admin password secret name
        /// </summary>
        public string AdminPasswordSecretName { get; set; }

        /// <summary>
        /// Get or set the VM data disks
        /// </summary>
        public IList<VmDisk> VirtualDisks { get; set; }

        /// <summary>
        ///  Whether accelerated networking should be used on the VM
        /// </summary>
        public bool EnableAcceleratedNetworking { get; set; }

        /// <summary>
        /// Get or set bootstrap state
        /// </summary>
        public ResourceState BootstrapState { get; set; }

        /// <summary>
        /// The time at which the VM deployment request was started.
        /// </summary>
        public DateTime? DeploymentRequestStartTime { get; set; }

        /// <summary>
        /// The range of time for which the deployment of the VM will be
        /// allowed to run before timeout.
        /// </summary>
        public TimeSpan DeploymentRequestTimeout { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// True/false whether the deployment request is timed out.
        /// </summary>
        public bool IsDeploymentRequestTimedOut
        {
            get
            {
                return this.DeploymentRequestStartTime != null
                    && DateTime.UtcNow > this.DeploymentRequestStartTime.Value.Add(this.DeploymentRequestTimeout);
            }
        }

        /// <summary>
        /// Generates a Random Password
        /// respecting the given strength requirements.
        /// </summary>
        /// <param name="passwordOptions">A valid PasswordOptions object
        /// containing the password strength requirements.</param>
        /// <returns>A random password</returns>
        public static string GenerateRandomPassword(PasswordOptions passwordOptions = null)
        {
            passwordOptions = passwordOptions ?? new PasswordOptions()
            {
                RequiredLength = 8,
                RequiredUniqueChars = 4,
                RequireDigit = true,
                RequireLowercase = true,
                RequireNonAlphanumeric = true,
                RequireUppercase = true
            };

            List<string> randomChars = new List<string>()
            {
                "ABCDEFGHJKLMNOPQRSTUVWXYZ",    // uppercase 
                "abcdefghijkmnopqrstuvwxyz",    // lowercase
                "0123456789",                   // digits
                "!@#$$?%^&*()-+=_" // non-alphanumeric
            };

            Random rand = new Random(Environment.TickCount);
            List<char> chars = new List<char>();

            if (passwordOptions.RequireUppercase)
            {
                chars.Insert(rand.Next(0, chars.Count), randomChars[0][rand.Next(0, randomChars[0].Length)]);
            }

            if (passwordOptions.RequireLowercase)
            {
                chars.Insert(rand.Next(0, chars.Count), randomChars[1][rand.Next(0, randomChars[1].Length)]);
            }

            if (passwordOptions.RequireDigit)
            {
                chars.Insert(rand.Next(0, chars.Count), randomChars[2][rand.Next(0, randomChars[2].Length)]);
            }

            if (passwordOptions.RequireNonAlphanumeric)
            {
                chars.Insert(rand.Next(0, chars.Count), randomChars[3][rand.Next(0, randomChars[3].Length)]);
            }

            for (int i = chars.Count; i < passwordOptions.RequiredLength
                || chars.Distinct().Count() < passwordOptions.RequiredUniqueChars; i++)
            {
                string rcs = randomChars[rand.Next(0, randomChars.Count)];
                chars.Insert(rand.Next(0, chars.Count), rcs[rand.Next(0, rcs.Length)]);
            }

            return new string(chars.ToArray());
        }
    }
}