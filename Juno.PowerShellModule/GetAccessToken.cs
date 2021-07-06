namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Identity.Client;

    /// <summary>
    /// Powershell Module to get Access Token to be able to invoke Front Door APIs in the target environment 
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "AccessToken")]
    public class GetAccessToken : ExperimentCmdletBase
    {
        /// <summary>
        /// Native Application Client Id for Corp
        /// </summary>
        private const string ExperimentApiDevEndpoint = "https://junodev01experiments.azurewebsites.net";

        /// <summary>
        /// Native Application Client Id for PROD AME
        /// </summary>
        private const string ExperimentApiProdEndpoint = "https://junoprod01experiments.azurewebsites.net";

        /// <summary>
        /// Native Application Client Id for PPE AME
        /// </summary>
        private const string ExperimentApiPpeEndpoint = "https://junoppe01experiments.azurewebsites.net";

        /// <summary>
        /// Native Application Client Id for Corp
        /// </summary>
        private const string NativeAppClientIdCorp = "f442db36-48d0-46c5-a674-6b4dd3ffabcf";

        /// <summary>
        /// Native Application Client Id for AME
        /// </summary>
        private const string NativeAppClientIdAME = "66b900a9-991a-40d0-85b8-68c027e5ef2e";

        /// <summary>
        /// ExperimentApiAppId for Corp
        /// </summary>
        private const string ExperimentApiAppIdCorp = "8d43b83c-7869-425a-a18d-8cb490c9e7d2";

        /// <summary>
        /// ExperimentApiAppId for AME
        /// </summary>
        private const string ExperimentApiAppIdProdAME = "e2d9854a-429b-4bcb-ba21-415f7e978a49";

        /// <summary>
        /// ExperimentApiAppId for PPE AME
        /// </summary>
        private const string ExperimentApiAppIdPPEAME = "3b146683-117c-44cc-8919-c7b7cb637927";
        
        /// <summary>
        /// Tenant Id for Microsoft (Corp)
        /// </summary>
        private const string TenantIdCorp = "72f988bf-86f1-41af-91ab-2d7cd011db47";

        /// <summary>
        /// Tenant Id for AME
        /// </summary>
        private const string TenantIdAME = "33e01921-4d64-4f8c-a055-5bdaffd5e33d";

        /// <summary>
        /// Access Token to be used to authenticate the API calls
        /// </summary>
        public static string AccessToken { get; set; }

        /// <summary>
        /// Username of user executing the PS Module
        /// </summary>
        public static string Username { get; set; }

        /// <summary>
        /// Access Token to be used to authenticate the API calls
        /// </summary>
        public static string ServiceEndpoint { get; set; }

        /// <summary>
        /// <para type="description">
        /// Target endpoint - complete URL of endpount address
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [Alias("endpoint")]
        public Uri ServiceUri { get; set; }

        /// <summary>
        /// Flag to determine if PROD AME
        /// </summary>
        protected bool IsProd { get; set; }

        /// <summary>
        /// Flag to determine if PPE AME
        /// </summary>
        protected bool IsPpe { get; set; }

        /// <summary>
        /// Validates the parameters passed into the commandlet.
        /// </summary>
        protected override bool ValidateParameters()
        {
            return base.ValidateParameters();
        }

        /// <summary>
        /// Executes the operation to get Access Token.
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();
                if (this.ValidateParameters())
                {
                    if (this.ServiceUri == null)
                    {
                        this.ServiceUri = new Uri(GetAccessToken.ExperimentApiDevEndpoint);
                        this.IsPpe = false;
                        this.IsProd = false;
                    }
                    else if (this.ServiceUri.AbsoluteUri.Contains(GetAccessToken.ExperimentApiDevEndpoint, StringComparison.InvariantCultureIgnoreCase))
                    {
                        this.IsPpe = false;
                        this.IsProd = false;
                    }
                    else if (this.ServiceUri.AbsoluteUri.Contains(GetAccessToken.ExperimentApiProdEndpoint, StringComparison.InvariantCultureIgnoreCase))
                    {
                        this.IsPpe = false;
                        this.IsProd = true;
                    }
                    else if (this.ServiceUri.AbsoluteUri.Contains(GetAccessToken.ExperimentApiPpeEndpoint, StringComparison.InvariantCultureIgnoreCase))
                    {
                        this.IsPpe = true;
                        this.IsProd = false;
                    }
                    else
                    {
                        throw new ArgumentException("Invalid arguments passed to module. ServiceUri provided is not valid.");
                    }

                    GetAccessToken.ServiceEndpoint = this.ServiceUri.AbsoluteUri;
                    GetAccessToken.AccessToken = this.GetAccessTokenAsync().GetAwaiter().GetResult().AccessToken;
                }
                else
                {
                    throw new ArgumentException($"Invalid arguments passed to module. Required parameter is {nameof(this.ServiceUri).ToString()}.");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Creates and returns an <see cref="IPublicClientApplication"/> object.
        /// </summary>
        protected virtual IPublicClientApplication GetPublicClientApplication()
        {
            string nativeAppClientId = 
                this.IsProd || this.IsPpe ? GetAccessToken.NativeAppClientIdAME : 
                GetAccessToken.NativeAppClientIdCorp;
            string tenantId = 
                this.IsProd || this.IsPpe ? GetAccessToken.TenantIdAME : 
                GetAccessToken.TenantIdCorp;
            return PublicClientApplicationBuilder.Create(nativeAppClientId)
              .WithAuthority(new Uri(string.Format("https://login.microsoftonline.com/{0}", tenantId)))
              .WithRedirectUri("http://localhost")
              .Build();
        }

        /// <summary>
        /// Acquires a token <see cref="AuthenticationResult"/> silently.
        /// </summary>
        protected virtual Task<AuthenticationResult> AcquireTokenSilentExecutionResultAsync(IPublicClientApplication application, IEnumerable<string> scopes, IEnumerable<IAccount> accounts)
        {
            application.ThrowIfNull(nameof(application));

            return application.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
        }

        /// <summary>
        /// Acquires a token <see cref="AuthenticationResult"/> interactively.
        /// </summary>
        protected virtual Task<AuthenticationResult> AcquireTokenInteractiveExecutionResultAsync(IPublicClientApplication application, IEnumerable<string> scopes)
        {
            application.ThrowIfNull(nameof(application));

            return application.AcquireTokenInteractive(scopes).ExecuteAsync();
        }

        private async Task<AuthenticationResult> GetAccessTokenAsync()
        {
            string experimentApiAppId = 
                this.IsProd ? GetAccessToken.ExperimentApiAppIdProdAME : 
                (this.IsPpe ? GetAccessToken.ExperimentApiAppIdPPEAME : 
                GetAccessToken.ExperimentApiAppIdCorp);
            string[] scopes = new string[] 
            { 
                string.Format("api://{0}/user_impersonation", experimentApiAppId), "User.Read" 
            };
            IPublicClientApplication app = this.GetPublicClientApplication();

            var accounts = await app.GetAccountsAsync().ConfigureAwait(false);
            AuthenticationResult result = null;
            try
            {
                result = await this.AcquireTokenSilentExecutionResultAsync(app, scopes, accounts).ConfigureAwait(false);
            }
            catch (MsalUiRequiredException)
            {
                try
                {
                    result = await this.AcquireTokenInteractiveExecutionResultAsync(app, scopes).ConfigureAwait(false);
                }
                catch (MsalException)
                {
                    throw;
                }
            }
            finally
            {
                GetAccessToken.Username = result.Account.Username;
            }

            return result;
        }
    }
}
