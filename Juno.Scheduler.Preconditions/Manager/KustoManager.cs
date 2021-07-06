namespace Juno.Scheduler.Preconditions.Manager
{
    using System;
    using System.Data;
    using System.Linq;
    using System.Threading.Tasks;
    using Juno.Contracts.Configuration;
    using Juno.Extensions.AspNetCore;
    using Kusto.Cloud.Platform.Utils;
    using Kusto.Data.Exceptions;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Kusto;
    using Microsoft.Extensions.Configuration;
    using Polly;

    /// <summary>
    /// Kusto Manager helper class to assist in connection with Kusto.
    /// </summary>
    public class KustoManager : IKustoManager
    {
        private static IKustoManager instance = KustoManager.instance ?? new KustoManager();

        private readonly int retryCounter = 3;

        /// <summary>
        /// Adding retry mechanism for specfic failure codes
        /// 408: Request Timeout
        /// 500: Internal Server Error
        /// 504: Gateway Timeout
        /// </summary>
        private readonly int[] kustoFailureCodes = { 408, 500, 504 };

        /// <summary>
        /// Default absolute eviction policy for items retrieved from Kusto
        /// </summary>
        private readonly TimeSpan defaultTTL = TimeSpan.FromHours(1);

        /// <summary>
        /// The retry policy to use with the Kusto client call to the Kusto cluster.
        /// </summary>
        private IAsyncPolicy retryPolicy;

        private IKustoQueryIssuer issuer;

        private IMemoryCache<DataTable> cache;

        private KustoManager()
        {
            this.retryPolicy = Policy.Handle<KustoException>(e => this.kustoFailureCodes.Contains(e.FailureCode))
                .WaitAndRetryAsync(retryCount: this.retryCounter, (retries) => TimeSpan.FromSeconds(Math.Pow(2, this.retryCounter)));
        }

        /// <summary>
        /// Singleton instance of Kusto Manager
        /// </summary>
        public static IKustoManager Instance => KustoManager.instance;

        /// <inheritdoc/>
        public void SetUp(IConfiguration configuration)
        {
            configuration.ThrowIfNull(nameof(configuration));

            EnvironmentSettings settings = EnvironmentSettings.Initialize(configuration);
            settings.ThrowIfNull(nameof(settings));

            KustoSettings kustoSettings = settings.KustoSettings.Get(Setting.AzureCM);
            kustoSettings.ThrowIfNull(nameof(kustoSettings));
         
            if (this.issuer == null)
            {
                AadPrincipalSettings principalSettings = kustoSettings.AadPrincipals.Get(Setting.Default);
                this.issuer = new KustoQueryIssuer(
                        principalSettings.PrincipalId,
                        principalSettings.PrincipalCertificateThumbprint,
                        principalSettings.TenantId);
            }

            this.cache = this.cache ?? new MemoryCache<DataTable>();            
        }

        /// <inheritdoc/>
        public void SetUp(IKustoQueryIssuer issuer)
        {
            issuer.ThrowIfNull(nameof(issuer));

            this.issuer = this.issuer ?? issuer;
            this.cache = this.cache ?? new MemoryCache<DataTable>();
        }

        /// <inheritdoc/>
        public Task<DataTable> GetKustoResponseAsync(string cacheKey, KustoSettings kustoSettings, string query, double? minutesToLive = null)
        {
            cacheKey.ThrowIfNullOrWhiteSpace(nameof(cacheKey));
            kustoSettings.ThrowIfNull(nameof(kustoSettings));
            query.ThrowIfNullOrWhiteSpace(nameof(query));
            kustoSettings.ThrowIfInvalid(nameof(kustoSettings), ks =>
            {
                if (ks.ClusterUri == null)
                {
                    return false;
                }

                return !string.IsNullOrEmpty(ks.ClusterUri.AbsoluteUri) && !string.IsNullOrEmpty(ks.ClusterDatabase);
            });
            return this.GetKustoResponseAsync(cacheKey, kustoSettings.ClusterUri.AbsoluteUri, kustoSettings.ClusterDatabase, query, minutesToLive);
        }

        private async Task<DataTable> GetKustoResponseAsync(string cacheKey, string absoluteUri, string databaseCluster, string query, double? minutesToLive = null)
        {
            cacheKey.ThrowIfNullOrWhiteSpace(nameof(cacheKey));
            absoluteUri.ThrowIfNullOrWhiteSpace(nameof(absoluteUri));
            databaseCluster.ThrowIfNullOrWhiteSpace(nameof(databaseCluster));
            query.ThrowIfNullOrWhiteSpace(nameof(query));

            TimeSpan ttl = minutesToLive.HasValue ? TimeSpan.FromMinutes(minutesToLive.Value) : this.defaultTTL;

            Func<Task<DataTable>> retrievalFunction = async () =>
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await this.issuer.IssueAsync(absoluteUri, databaseCluster, query).ConfigureDefaults();
                }).ConfigureDefaults();
            };

            return await this.cache.GetOrAddAsync(cacheKey, ttl, retrievalFunction).ConfigureDefaults();
        }
    }
}
