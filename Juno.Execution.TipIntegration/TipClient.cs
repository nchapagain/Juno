namespace Juno.Execution.TipIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts.Configuration;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json.Linq;
    using Polly;
    using TipGateway;
    using TipGateway.Entities;
    using TipGateway.FabricApi;
    using TipGateway.FabricApi.Requests;
    using TipGateway.Interfaces;
    using TipGateway.TokenProvider;

    /// <summary>
    /// Client for communications with Tip service via tip gateway.
    /// </summary>
    public class TipClient : ITipClient
    {
        /// <summary>
        /// Defines the default retry policy for Api operations on timeout only
        /// </summary>
        private static IAsyncPolicy defaultRetryPolicy = Policy.Handle<Exception>()
        .WaitAndRetryAsync(5, (retries) => TimeSpan.FromMilliseconds(retries * 1000));

        private IConfiguration configuration;
        private ITipGateway gateway;
        private IAsyncPolicy retryPolicy;

        /// <summary>
        /// Initializes a new instance of the <see cref="TipClient"/> class
        /// </summary>
        public TipClient(IConfiguration configuration, ITipGateway tipGateway = null, IAsyncPolicy retryPolicy = null)
        {
            this.configuration = configuration;
            this.gateway = tipGateway;
            this.retryPolicy = retryPolicy ?? TipClient.defaultRetryPolicy;
        }

        /// <inheritdoc/>
        public Task<TipNodeSessionChange> ApplyPilotFishServicesAsync(string tipSessionId, List<KeyValuePair<string, string>> pilotfishServices, CancellationToken cancellationToken)
        {
            UpdateTipNodeSession sessionRequest = new UpdateTipNodeSession
            {
                TipNodeSessionId = tipSessionId,
                AutopilotServices = pilotfishServices
            };

            return this.UpdateSessionAsync(sessionRequest);
        }

        /// <inheritdoc/>
        public Task<TipNodeSessionChange> ApplyPilotFishServicesOnSocAsync(string tipSessionId, List<KeyValuePair<string, string>> pilotfishServices, CancellationToken cancellationToken)
        {
            UpdateTipNodeSession sessionRequest = new UpdateTipNodeSession
            {
                TipNodeSessionId = tipSessionId,
                OverlakeAutopilotServices = pilotfishServices
            };

            return this.UpdateSessionAsync(sessionRequest);
        }

        /// <inheritdoc/>
        public async Task<TipNodeSessionChange> CreateTipSessionAsync(TipParameters tipParameters, CancellationToken cancellationToken)
        {
            tipParameters.ThrowIfNull(nameof(tipParameters));
            NewTipNodeSession sessionRequest = new NewTipNodeSession
            {
                ClusterName = tipParameters.ClusterName,
                NodeCount = tipParameters.NodeCount,
                CandidateNodes = tipParameters.CandidateNodesId,
                ClientAppId = this.CreateTipClient().AppId,
                Region = tipParameters.Region,
                DurationInMinutes = tipParameters.DurationInMinutes,
                Details = tipParameters.Details,
                AutopilotServices = tipParameters.AutopilotServices,
                CreatedBy = "junosvc@microsoft.com",
                RequireManualRemoval = TipParameters.RequireManualRemoval,
                IsAmberNodeRequest = tipParameters.IsAmberNodeRequest,
                TargetMachinePoolNames = tipParameters.MachinePoolNames
            };

            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.CreateAsync(sessionRequest).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task<TipNodeSessionChange> CreateTipSessionAsync(
            string clusterName,
            string region,
            int durationInMinutes,
            CancellationToken cancellationToken,
            List<string> nodeIdList = null,
            string details = null,
            List<KeyValuePair<string, string>> autopilotServices = null,
            bool allowAmberNodes = false)
        {
            TipParameters tipParameters = new TipParameters()
            {
                ClusterName = clusterName,
                NodeCount = 1,
                CandidateNodesId = nodeIdList,
                Region = region,
                DurationInMinutes = durationInMinutes,
                Details = details,
                AutopilotServices = autopilotServices,
                IsAmberNodeRequest = allowAmberNodes
            };

            return this.CreateTipSessionAsync(tipParameters, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<TipNodeSessionChange> DeleteTipSessionAsync(string tipSessionId, CancellationToken cancellationToken)
        {
            DeleteTipNodeSession deleteRequest = new DeleteTipNodeSession
            {
                TipNodeSessionId = tipSessionId
            };

            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                string sessionChangeId = await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.DeleteAsync(deleteRequest).ConfigureAwait(false);
                }).ConfigureAwait(false);

                return new TipNodeSessionChange
                {
                    Status = TipNodeSessionChangeStatus.Unknown,
                    TipNodeSessionChangeId = sessionChangeId,
                    TipNodeSessionId = tipSessionId
                };
            }
        }

        /// <inheritdoc/>
        public async Task<TipNodeSessionChange> DeployHostingEnvironmentAsync(string tipSessionId, List<HostingEnvironmentLineItem> hostingEnvironments, CancellationToken cancellationToken)
        {
            UpdateTipNodeSession sessionRequest = new UpdateTipNodeSession
            {
                TipNodeSessionId = tipSessionId,
                HostingEnvironmentCollection = hostingEnvironments
            };

            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.UpdateAsync(sessionRequest).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<TipNodeSessionChange> ExecuteNodeCommandAsync(string tipSessionId, string nodeId, string command, TimeSpan timeout, CancellationToken cancellationToken)
        {
            TipNodeSessionDetails sessionDetails = await this.GetTipSessionDetailsAsync(tipSessionId).ConfigureAwait(false);

            NodeExecuteCommandRequest request = new NodeExecuteCommandRequest()
            {
                Command = command,
                TimeOutInSeconds = timeout.TotalSeconds < int.MaxValue ? (int)timeout.TotalSeconds : int.MaxValue,
                ClusterName = sessionDetails.Cluster,
                HostNodeId = nodeId,
                CreatedBy = sessionDetails.CreatedBy,
                TipNodeSessionId = tipSessionId
            };

            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.InvokeFabricApiAsync(FCApi.NodeExecuteCommand, tipSessionId, JObject.FromObject(request)).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<TipNodeSessionChange> ExtendTipSessionAsync(string tipSessionId, int extendExpirationTimeInMinutes, CancellationToken cancellationToken)
        {
            UpdateTipNodeSession sessionRequest = new UpdateTipNodeSession
            {
                TipNodeSessionId = tipSessionId,
                ExtendExpirationTimeInMinutes = extendExpirationTimeInMinutes
            };

            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.UpdateAsync(sessionRequest).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<TipNodeSessionChange> GetNodeStatusAsync(string tipSessionId, string nodeId, CancellationToken cancellationToken)
        {
            tipSessionId.ThrowIfNullOrWhiteSpace(nameof(tipSessionId));
            nodeId.ThrowIfNullOrWhiteSpace(nameof(nodeId));

            TipNodeSessionDetails sessionDetails = await this.GetTipSessionDetailsAsync(tipSessionId).ConfigureAwait(false);

            GetNodeStatusRequest request = new GetNodeStatusRequest()
            {
                ClusterName = sessionDetails.Cluster,
                HostNodeId = nodeId,
                CreatedBy = sessionDetails.CreatedBy,
                TipNodeSessionId = tipSessionId
            };

            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.InvokeFabricApiAsync(FCApi.GetNodeStatus, tipSessionId, JObject.FromObject(request)).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<TipNodeSession> GetTipSessionAsync(string tipSessionId, CancellationToken cancellationToken)
        {
            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.GetAsync(tipSessionId).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<TipNodeSessionChangeDetails> GetTipSessionChangeAsync(string tipSessionId, string tipSessionChangeId, CancellationToken cancellationToken)
        {
            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.GetChangeDetailsAsync(tipSessionId, tipSessionChangeId).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<TipNodeSessionDetails> GetTipSessionDetailsAsync(string tipSessionId)
        {
            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.GetDetailsAsync(tipSessionId).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<TipNodeSession>> GetTipSessionsByAppIdAsync(string appPrincipleId, CancellationToken cancellationToken)
        {
            appPrincipleId.ThrowIfNullOrWhiteSpace(nameof(appPrincipleId));

            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.GetSessionsByApplicationAsync(appPrincipleId).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsTipSessionChangeFailedAsync(string tipSessionId, string tipSessionChangeId, CancellationToken cancellationToken)
        {
            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.IsFailedAsync(tipSessionId, tipSessionChangeId).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsTipSessionCreatedAsync(string tipSessionId, CancellationToken cancellationToken)
        {
            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.IsCreatedAsync(tipSessionId).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<IDictionary<string, bool>> IsTipSessionCreatedAsync(IEnumerable<string> tipSessionIds, CancellationToken cancellationToken)
        {
            tipSessionIds.ThrowIfNullOrEmpty(nameof(tipSessionIds));
            IDictionary<string, Task<bool>> createdTaskDictionary = new Dictionary<string, Task<bool>>();
            ITipGateway gateway = this.GetTipGateway(this.CreateTipClient());

            foreach (string tipSessionId in tipSessionIds.Distinct())
            {
                Task<bool> isCreatedTask = this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.IsCreatedAsync(tipSessionId).ConfigureAwait(false);
                });

                createdTaskDictionary.Add(tipSessionId, isCreatedTask);
            }

            // All tasks will complete here.
            await Task.WhenAll(createdTaskDictionary.Values).ConfigureAwait(false);

            // Map each Task<bool> to bool
            IDictionary<string, bool> result = new Dictionary<string, bool>();
            foreach (KeyValuePair<string, Task<bool>> pair in createdTaskDictionary)
            {
                // there is no delay here.
                bool value = await pair.Value.ConfigureAwait(false);
                result.Add(pair.Key, value);
            }

            gateway.Dispose();
            return result;
        }

        /// <inheritdoc/>
        public async Task<TipNodeSessionChange> ResetNodeHealthAsync(string tipSessionId, string nodeId, CancellationToken cancellationToken)
        {
            tipSessionId.ThrowIfNullOrWhiteSpace(nameof(tipSessionId));
            nodeId.ThrowIfNullOrWhiteSpace(nameof(nodeId));

            TipNodeSessionDetails sessionDetails = await this.GetTipSessionDetailsAsync(tipSessionId).ConfigureAwait(false);

            ResetNodeHealthRequest request = new ResetNodeHealthRequest()
            {
                ClusterName = sessionDetails.Cluster,
                HostNodeId = nodeId,
                CreatedBy = sessionDetails.CreatedBy,
                TipNodeSessionId = tipSessionId
            };

            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.InvokeFabricApiAsync(FCApi.ResetNodeHealth, tipSessionId, JObject.FromObject(request)).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<TipNodeSessionChange> SetNodeStateAsync(string tipSessionId, string tipNodeId, NodeState nodeState, CancellationToken cancellationToken)
        {
            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                TipNodeSessionDetails sessionDetails = await this.GetTipSessionDetailsAsync(tipSessionId).ConfigureAwait(false);

                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    ForceNodeStateRequest request = new ForceNodeStateRequest
                    {
                        CreatedBy = sessionDetails.CreatedBy,
                        TipNodeSessionId = tipSessionId,
                        ClusterName = sessionDetails.Cluster,
                        HostNodeId = tipNodeId,
                        NodeState = nodeState
                    };

                    return await gateway.InvokeFabricApiAsync(FCApi.ForceNodeState, tipSessionId, JObject.FromObject(request))
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<TipNodeSessionChange> SetNodePowerStateAsync(string tipSessionId, string nodeId, PowerAction powerAction, CancellationToken cancellationToken)
        {
            tipSessionId.ThrowIfNullOrWhiteSpace(nameof(tipSessionId));
            nodeId.ThrowIfNullOrWhiteSpace(nameof(nodeId));

            TipNodeSessionDetails sessionDetails = await this.GetTipSessionDetailsAsync(tipSessionId).ConfigureAwait(false);

            SetNodePowerStateRequest request = new SetNodePowerStateRequest()
            {
                ClusterName = sessionDetails.Cluster,
                HostNodeId = nodeId,
                CreatedBy = sessionDetails.CreatedBy,
                TipNodeSessionId = tipSessionId,
                PowerAction = powerAction
            };

            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.InvokeFabricApiAsync(FCApi.SetNodePowerState, tipSessionId, JObject.FromObject(request)).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        private async Task<TipNodeSessionChange> UpdateSessionAsync(UpdateTipNodeSession tipNodeSessionUpdate)
        {
            using (ITipGateway gateway = this.GetTipGateway(this.CreateTipClient()))
            {
                return await this.retryPolicy.ExecuteAsync(async () =>
                {
                    return await gateway.UpdateAsync(tipNodeSessionUpdate).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        private Client CreateTipClient()
        {
            EnvironmentSettings settings = EnvironmentSettings.Initialize(this.configuration);
            TipSettings tipSettings = settings.TipSettings;
            AadPrincipalSettings principalSettings = tipSettings.AadPrincipals.Get(Setting.Default);
            return new Client(
                appId: principalSettings.PrincipalId,
                secret: null,
                certThumbPrint: principalSettings.PrincipalCertificateThumbprint);
        }

        private ITipGateway GetTipGateway(Client tipClient)
        {
            return this.gateway ?? new TipGateway(TipEndPoint.Prod, tipClient);
        }
    }
}