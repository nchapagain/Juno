namespace Juno.Api.Client
{
    using System.Collections.Generic;

    /// <summary>
    /// Provides a pool of Juno API clients to enable support for multiple
    /// target API endpoints.
    /// </summary>
    public class ClientPool<T> : Dictionary<ApiClientType, T>
    {
        /// <summary>
        /// Returns the client from the pool matching the client type specified
        /// (e.g. AgentApi, AgentHeartbeatApi).
        /// </summary>
        public T GetClient(ApiClientType clientType)
        {
            if (!this.ContainsKey(clientType))
            {
                throw new KeyNotFoundException($"The API client '{clientType}' does not exist in the client pool.");
            }

            return this[clientType];
        }
    }
}
