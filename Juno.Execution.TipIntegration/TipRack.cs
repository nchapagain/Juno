namespace Juno.Execution.TipIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Class to define a TIP rack with available nodes.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Json converter needs to be able to set.")]
    public class TipRack
    {
        private static char listSeparator = ';';

        /// <summary>
        /// Cluster Name (e.g. yto22prdapp02).
        /// </summary>
        /// <example>yto22prdapp02</example>
        public string ClusterName { get; set; }

        /// <summary>
        /// Machine pool name. This is needed for targeting nodes that aren't otherwise
        /// available to allocator. (ex: decomm machine pools, add-rack etc.)
        /// </summary>
        /// <example>bnz14prdapp10mp1</example>
        public string MachinePoolName { get; set; }

        /// <summary>
        /// CpuId (e.g. 50654).
        /// </summary>
        /// <example>50654</example>
        public string CpuId { get; set; }

        /// <summary>
        /// List of Node Ids (e.g. ["a8cbc655-3e9d-4634-8ee3-d63a30b36750","aaa05876-c34b-4eaf-8857-0d7e05a69506"]).
        /// </summary>
        /// <example>["a8cbc655-3e9d-4634-8ee3-d63a30b36750","aaa05876-c34b-4eaf-8857-0d7e05a69506"]</example>
        public List<string> NodeList { get; set; }

        /// <summary>
        /// RackLocation (e.g. F01C01-AX126).
        /// </summary>
        /// <example>F01C01-AX126</example>
        public string RackLocation { get; set; }

        /// <summary>
        /// Region (e.g. canadacentral).
        /// </summary>
        /// <example>canadacentral</example>
        public string Region { get; set; }

        /// <summary>
        /// Remaining Tip Sessions (e.g. 18).
        /// </summary>
        /// <example>18</example>
        public int RemainingTipSessions { get; set; }

        /// <summary>
        /// The preferred vmsku for the rack
        /// </summary>
        /// <example>Standard_D2s_v3</example>
        public string PreferredVmSku { get; set; }

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
        /// Convert list of environmentEntity to list of tip racks
        /// </summary>
        /// <param name="rackEntities">List of rack as list of <see cref="EnvironmentEntity"/></param>
        /// <returns>List of TipRack.</returns>
        public static IList<TipRack> FromEnvironmentEntities(IList<EnvironmentEntity> rackEntities)
        {
            rackEntities.ThrowIfNull(nameof(TipRack));
            List<TipRack> result = new List<TipRack>();

            foreach (EnvironmentEntity entity in rackEntities)
            {
                result.Add(TipRack.FromEnvironmentEntity(entity));
            }

            return result;
        }

        /// <summary>
        /// Convert environmentEntity to tipRack
        /// </summary>
        /// <param name="rackEntity">A rack as <see cref="EnvironmentEntity"/></param>
        /// <returns>A TipRack.</returns>
        public static TipRack FromEnvironmentEntity(EnvironmentEntity rackEntity)
        {
            rackEntity.ThrowIfNull(nameof(TipRack));
            TipRack rack = new TipRack()
            {
                RackLocation = rackEntity.Metadata.GetValue<string>(nameof(TipRack.RackLocation)),
                MachinePoolName = rackEntity.Metadata.GetValue<string>(nameof(TipRack.MachinePoolName)),
                ClusterName = rackEntity.Metadata.GetValue<string>(nameof(TipRack.ClusterName)),
                CpuId = rackEntity.Metadata.GetValue<string>(nameof(TipRack.CpuId), string.Empty),
                Region = rackEntity.Metadata.GetValue<string>(nameof(TipRack.Region)),
                RemainingTipSessions = rackEntity.Metadata.GetValue<int>(nameof(TipRack.RemainingTipSessions)),
                PreferredVmSku = rackEntity.Metadata.GetValue<string>(nameof(TipRack.PreferredVmSku)),
                SupportedVmSkus = rackEntity.Metadata.GetValue<string>(nameof(TipRack.SupportedVmSkus)).Split(TipRack.listSeparator).ToList(),
                NodeList = rackEntity.Metadata.GetValue<string>(nameof(TipRack.NodeList)).Split(TipRack.listSeparator).ToList()
            };

            return rack;
        }

        /// <summary>
        /// Convert list of TipRack to list of environmentEntity
        /// </summary>
        /// <param name="tipRacks">List of tip racks object.</param>
        /// <returns>Converted list of Environment Entities</returns>
        public static IList<EnvironmentEntity> ToEnvironmentEntities(IList<TipRack> tipRacks)
        {
            tipRacks.ThrowIfNull(nameof(TipRack));
            List<EnvironmentEntity> result = new List<EnvironmentEntity>();

            foreach (TipRack rack in tipRacks)
            {
                result.Add(TipRack.ToEnvironmentEntity(rack));
            }

            return result;
        }

        /// <summary>
        /// Convert list of TipRack to list of environmentEntity
        /// </summary>
        /// <param name="tipRack">Tip rack object.</param>
        /// <returns>Converted environment entity object.</returns>
        public static EnvironmentEntity ToEnvironmentEntity(TipRack tipRack)
        {
            tipRack.ThrowIfNull(nameof(TipRack));

            return new EnvironmentEntity(EntityType.Rack, tipRack.RackLocation, ExperimentComponent.AllGroups, tipRack.GetMetadata());
        }

        /// <summary>
        /// Get meta data for the tip rack
        /// </summary>
        /// <returns>Dictionary of MetaData</returns>
        public IDictionary<string, IConvertible> GetMetadata()
        {
            IDictionary<string, IConvertible> metadata = new Dictionary<string, IConvertible>();
            metadata.Add(nameof(TipRack.RackLocation), this.RackLocation);
            metadata.Add(nameof(TipRack.ClusterName), this.ClusterName);
            metadata.Add(nameof(TipRack.MachinePoolName), this.MachinePoolName);
            metadata.Add(nameof(TipRack.CpuId), this.CpuId);
            metadata.Add(nameof(TipRack.Region), this.Region);
            metadata.Add(nameof(TipRack.RemainingTipSessions), this.RemainingTipSessions);
            metadata.Add(nameof(TipRack.PreferredVmSku), this.PreferredVmSku);
            metadata.Add(nameof(TipRack.SupportedVmSkus), string.Join(TipRack.listSeparator.ToString(), this.SupportedVmSkus));
            metadata.Add(nameof(TipRack.NodeList), string.Join(TipRack.listSeparator.ToString(), this.NodeList));
            return metadata;
        }
    }
}