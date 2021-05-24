namespace Juno.Execution.TipIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Class to represent a tip session
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Json converter needs to be able to set.")]
    public class TipSession
    {
        private static char listSeparator = ';';

        /// <summary>
        /// Get or set Tip session id, in GUID format.
        /// </summary>
        public string TipSessionId { get; set; }

        /// <summary>
        /// Get or set ClusterName (e.g. yto22prdapp02)
        /// </summary>
        /// <example>yto22prdapp02</example>
        public string ClusterName { get; set; }

        /// <summary>
        /// Get or set Azure Region (e.g. canadacentral)
        /// </summary>
        /// <example>canadacentral</example>
        public string Region { get; set; }

        /// <summary>
        /// Get or set Node id, in Guid format.
        /// </summary>
        public string NodeId { get; set; }

        /// <summary>
        /// Get or set Group Name in the experiment (e.g. Group A)
        /// </summary>
        /// <example>Group A.</example>
        public string GroupName { get; set; }

        /// <summary>
        /// Get or set List of change Ids. Format of list of GUIDs.
        /// </summary>
        public List<string> ChangeIdList { get; set; }

        /// <summary>
        /// Get or set Tip session status
        /// </summary>
        public TipSessionStatus Status { get; set; }

        /// <summary>
        /// Get or set created Time of the Tip session.
        /// </summary>
        public DateTime CreatedTimeUtc { get; set; }

        /// <summary>
        /// Get or set expiration Time of the Tip session.
        /// </summary>
        public DateTime ExpirationTimeUtc { get; set; }

        /// <summary>
        /// Get or set deleted Time of the Tip session.
        /// </summary>
        public DateTime DeletedTimeUtc { get; set; }

        /// <summary>
        /// List of supported vm skus (e.g. [  "Standard_D2_v3",  "Standard_D16_v3",  "Standard_D64_v3"]).
        /// </summary>
        /// <example>[  "Standard_D2_v3",  "Standard_D16_v3",  "Standard_D64_v3"]</example>
        /// <remarks>
        /// This is a temporary solution to allow us to integrate the 'SupportedVmSkus' list
        /// below. This is preferable terminology given that it is a list of SKUs and not a singular SKU.
        /// </remarks>
        public List<string> SupportedVmSku
        {
            get
            {
                return this.SupportedVmSkus;
            }

            set
            {
                this.SupportedVmSkus = value;
            }
        }

        /// <summary>
        /// List of supported vm skus (e.g. [  "Standard_D2_v3",  "Standard_D16_v3",  "Standard_D64_v3"]).
        /// </summary>
        /// <example>[  "Standard_D2_v3",  "Standard_D16_v3",  "Standard_D64_v3"]</example>
        public List<string> SupportedVmSkus { get; set; } = new List<string>();

        /// <summary>
        /// The preferred VM SKU from the list of supported VM SKUs. This allows providers
        /// to apply different requirements (e.g. same VM SKU across the board) to the creation
        /// of VMs.
        /// </summary>
        public string PreferredVmSku { get; set; }

        /// <summary>
        /// Convert list of TipRack to list of environmentEntity
        /// </summary>
        /// <param name="tipSessions">List of tip sessions.</param>
        /// <param name="environmentGroup">The environment group name.</param>
        /// <returns>List of converted environment entity.</returns>
        public static IList<EnvironmentEntity> ToEnvironmentEntities(IEnumerable<TipSession> tipSessions, string environmentGroup = null)
        {
            tipSessions.ThrowIfNull(nameof(TipRack));
            List<EnvironmentEntity> result = new List<EnvironmentEntity>();

            foreach (TipSession session in tipSessions)
            {
                result.Add(TipSession.ToEnvironmentEntity(session, environmentGroup ?? session.GroupName));
            }

            return result;
        }

        /// <summary>
        /// Convert list of TipRack to list of environmentEntity
        /// </summary>
        /// <param name="tipSession">Tip session</param>
        /// <param name="environmentGroup">Environment group name.</param>
        /// <returns>Converted enviroment entity.</returns>
        public static EnvironmentEntity ToEnvironmentEntity(TipSession tipSession, string environmentGroup)
        {
            tipSession.ThrowIfNull(nameof(tipSession));

            return new EnvironmentEntity(EntityType.TipSession, tipSession.TipSessionId, environmentGroup, tipSession.GetMetaData());
        }

        /// <summary>
        /// Convert list of environmentEntity to list of tip sessions
        /// </summary>
        /// <param name="sessionEntities">List of Tip session as environment entities.</param>
        /// <returns>List of tip sessions object.</returns>
        public static IList<TipSession> FromEnvironmentEntities(IList<EnvironmentEntity> sessionEntities)
        {
            sessionEntities.ThrowIfNull(nameof(sessionEntities));
            List<TipSession> result = new List<TipSession>();

            foreach (EnvironmentEntity entity in sessionEntities)
            {
                result.Add(TipSession.FromEnvironmentEntity(entity));
            }

            return result;
        }

        /// <summary>
        /// Convert environmentEntity to Tip session
        /// </summary>
        /// <param name="sessionEntity">The Tip session entity as environment entity.</param>
        /// <returns>The tip session object.</returns>
        public static TipSession FromEnvironmentEntity(EnvironmentEntity sessionEntity)
        {
            sessionEntity.ThrowIfNull(nameof(sessionEntity));
            TipSession session = new TipSession()
            {
                TipSessionId = sessionEntity.Metadata.GetValue<string>(nameof(TipSession.TipSessionId)),
                ClusterName = sessionEntity.Metadata.GetValue<string>(nameof(TipSession.ClusterName)),
                Region = sessionEntity.Metadata.GetValue<string>(nameof(TipSession.Region)),
                GroupName = sessionEntity.Metadata.GetValue<string>(nameof(TipSession.GroupName)),
                NodeId = sessionEntity.Metadata.GetValue<string>(nameof(TipSession.NodeId)),
                ChangeIdList = sessionEntity.Metadata.GetValue<string>(nameof(TipSession.ChangeIdList)).Split(TipSession.listSeparator).ToList(),
                Status = sessionEntity.Metadata.GetEnumValue<TipSessionStatus>(nameof(TipSession.Status)),
                CreatedTimeUtc = DateTime.Parse(sessionEntity.Metadata.GetValue<string>(nameof(TipSession.CreatedTimeUtc))),
                ExpirationTimeUtc = DateTime.Parse(sessionEntity.Metadata.GetValue<string>(nameof(TipSession.ExpirationTimeUtc))),
                DeletedTimeUtc = DateTime.Parse(sessionEntity.Metadata.GetValue<string>(nameof(TipSession.DeletedTimeUtc))),
                PreferredVmSku = sessionEntity.Metadata.GetValue<string>(nameof(TipSession.PreferredVmSku), string.Empty),
                SupportedVmSkus = sessionEntity.Metadata.GetValue<string>(nameof(TipSession.SupportedVmSkus), string.Empty)
                    .Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList()
            };

            return session;
        }

        /// <summary>
        /// Get meta data for the tip session
        /// </summary>
        /// <returns>Metadata of the tip session.</returns>
        public IDictionary<string, IConvertible> GetMetaData()
        {
            IDictionary<string, IConvertible> metadata = new Dictionary<string, IConvertible>();
            metadata.Add(nameof(TipSession.TipSessionId), this.TipSessionId);
            metadata.Add(nameof(TipSession.ClusterName), this.ClusterName);
            metadata.Add(nameof(TipSession.Region), this.Region);
            metadata.Add(nameof(TipSession.NodeId), this.NodeId);
            metadata.Add(nameof(TipSession.ChangeIdList), string.Join(TipSession.listSeparator.ToString(), this.ChangeIdList));
            metadata.Add(nameof(TipSession.Status), this.Status.ToString());
            metadata.Add(nameof(TipSession.CreatedTimeUtc), this.CreatedTimeUtc.ToString());
            metadata.Add(nameof(TipSession.ExpirationTimeUtc), this.ExpirationTimeUtc.ToString());
            metadata.Add(nameof(TipSession.DeletedTimeUtc), this.DeletedTimeUtc.ToString());
            metadata.Add(nameof(TipSession.GroupName), this.GroupName);
            metadata.Add(nameof(TipSession.PreferredVmSku), this.PreferredVmSku);
            metadata.Add(nameof(TipSession.SupportedVmSkus), this.SupportedVmSkus != null ? string.Join(";", this.SupportedVmSkus) : string.Empty);

            return metadata;
        }
    }
}