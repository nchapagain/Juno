namespace Juno.Contracts.Configuration
{
    using System;

    /// <summary>
    /// Represents strongly-typed configuration settings for an Azure
    /// Active Directory (AAD) app/service principal.
    /// </summary>
    public class AadPrincipalSettings : SettingsBase
    {
        /// <summary>
        /// Gets or sets the URI to the Azure Active Directory (AAD) authority
        /// from which a web token (JWT) can be granted for the principal.
        /// </summary>
        public Uri AuthorityUri { get; set; }

        /// <summary>
        /// Gets or sets the thumbprint of the certificate to use when authenticating
        /// with the Azure Active Directory (AAD) to get the web token for the app/service principal.
        /// </summary>
        public string PrincipalCertificateThumbprint { get; set; }

        /// <summary>
        /// Gets or sets the application/client ID of the Azure Active Directory (AAD) app/service
        /// principal.
        /// </summary>
        public string PrincipalId { get; set; }

        /// <summary>
        /// Gets or sets the object Id of the Azure Active Directory (AAD) enterprise app/service
        /// principal. Note that this is a different ID than the app/service principal ID itself and
        /// is the actual ID used for service-to-service/resource authentications.
        /// </summary>
        public string EnterpriseObjectId { get; set; }

        /// <summary>
        /// Gets or sets the name of the Azure Active Directory (AAD) app/service
        /// principal.
        /// </summary>
        public string PrincipalName { get; set; }

        /// <summary>
        /// Gets or sets the ID of the tenant in which the service Azure Active Directory (AAD) 
        /// the app/service principal exists.
        /// </summary>
        public string TenantId { get; set; }
    }
}
