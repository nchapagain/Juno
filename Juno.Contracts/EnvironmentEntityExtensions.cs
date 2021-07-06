namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension methods for <see cref="EnvironmentEntity"/> instances.
    /// </summary>
    public static class EnvironmentEntityExtensions
    {
        /// <summary>
        /// Metadata 'AgentId'
        /// </summary>
        public static string AgentId(this EnvironmentEntity entity, string agentId = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(agentId))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.AgentId)] = agentId;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.AgentId), string.Empty);
        }

        /// <summary>
        /// Metadata 'ClusterName'
        /// </summary>
        public static string ClusterName(this EnvironmentEntity entity, string clusterName = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(clusterName))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.ClusterName)] = clusterName;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.ClusterName), string.Empty);
        }

        /// <summary>
        /// Metadata 'DataDisks'
        /// </summary>
        public static string DataDisks(this EnvironmentEntity entity, string dataDisks = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(dataDisks))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.DataDisks)] = dataDisks;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.DataDisks), string.Empty);
        }

        /// <summary>
        /// Metadata 'Discarded'
        /// </summary>
        /// <param name="entities">The entity to mark as "discarded" in the entity pool.</param>
        /// <param name="id">True/false value to set on the entity metadata to indicate if it is a discarded entity.</param>
        public static IEnumerable<EnvironmentEntity> Discard(this IEnumerable<EnvironmentEntity> entities, string id = null)
        {
            entities.ThrowIfNull(nameof(entities));

            if (!string.IsNullOrWhiteSpace(id))
            {
                entities.Where(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.ToList().ForEach(e => e.Discarded(true));
            }
            else
            {
                entities.ToList().ForEach(e => e.Discarded(true));
            }

            return entities;
        }

        /// <summary>
        /// Metadata 'Discarded'
        /// </summary>
        /// <param name="entity">The entity to mark as "discarded" in the entity pool.</param>
        /// <param name="discarded">True/false value to set on the entity metadata to indicate if it is a discarded entity.</param>
        public static bool Discarded(this EnvironmentEntity entity, bool? discarded = null)
        {
            entity.ThrowIfNull(nameof(entity));

            if (discarded != null)
            {
                if (discarded == false)
                {
                    entity.Metadata.Remove(nameof(EnvironmentEntityExtensions.Discarded));
                }
                else
                {
                    entity.Metadata[nameof(EnvironmentEntityExtensions.Discarded)] = discarded;
                }
            }

            return entity.Metadata.GetValue<bool>(nameof(EnvironmentEntityExtensions.Discarded), false);
        }

        /// <summary>
        /// Metadata 'GroupName'
        /// </summary>
        public static string GroupName(this EnvironmentEntity entity, string groupName = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(groupName))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.GroupName)] = groupName;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.GroupName), string.Empty);
        }

        /// <summary>
        /// Metadata 'MachinePoolName'
        /// </summary>
        public static string MachinePoolName(this EnvironmentEntity entity, string machinePoolName = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(machinePoolName))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.MachinePoolName)] = machinePoolName;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.MachinePoolName), string.Empty);
        }

        /// <summary>
        /// Metadata 'NodeId'
        /// </summary>
        public static string NodeId(this EnvironmentEntity entity, string nodeId = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.NodeId)] = nodeId;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.NodeId), string.Empty);
        }

        /// <summary>
        /// Metadata 'NodeList'
        /// </summary>
        public static IEnumerable<string> NodeList(this EnvironmentEntity entity, params string[] nodeIds)
        {
            entity.ThrowIfNull(nameof(entity));
            if (nodeIds?.Any() == true)
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.NodeList)] = string.Join(";", nodeIds);
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.NodeList), string.Empty)
                .Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Metadata 'NodeState'
        /// </summary>
        public static string NodeState(this EnvironmentEntity entity, string nodeState = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(nodeState))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.NodeState)] = nodeState;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.NodeState), string.Empty);
        }

        /// <summary>
        /// Metadata 'OsDiskSku'
        /// </summary>
        public static string OsDiskSku(this EnvironmentEntity entity, string diskSku = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(diskSku))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.OsDiskSku)] = diskSku;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.OsDiskSku), string.Empty);
        }

        /// <summary>
        /// Metadata 'PreferredVmSku'
        /// </summary>
        public static string PreferredVmSku(this EnvironmentEntity entity, string preferredVmSku = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(preferredVmSku))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.PreferredVmSku)] = preferredVmSku;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.PreferredVmSku), string.Empty);
        }

        /// <summary>
        /// Metadata 'RackLocation'
        /// </summary>
        public static string RackLocation(this EnvironmentEntity entity, string rackLocation = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(rackLocation))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.RackLocation)] = rackLocation;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.RackLocation), string.Empty);
        }

        /// <summary>
        /// Metadata 'Region'
        /// </summary>
        public static string Region(this EnvironmentEntity entity, string region = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(region))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.Region)] = region;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.Region), string.Empty);
        }

        /// <summary>
        /// Metadata 'SupportedVmSkus'
        /// </summary>
        public static IEnumerable<string> SupportedVmSkus(this EnvironmentEntity entity, params string[] vmSkus)
        {
            entity.ThrowIfNull(nameof(entity));
            if (vmSkus?.Any() == true)
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.SupportedVmSkus)] = string.Join(";", vmSkus);
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.SupportedVmSkus), string.Empty)
                .Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Metadata 'TipSessionId'
        /// </summary>
        public static string TipSessionId(this EnvironmentEntity entity, string tipSessionId = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(tipSessionId))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.TipSessionId)] = tipSessionId;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.TipSessionId), string.Empty);
        }

        /// <summary>
        /// Metadata 'TipSessionDeleteRequestChangeId'
        /// </summary>
        public static string TipSessionDeleteRequestChangeId(this EnvironmentEntity entity, string tipSessionChangeId = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(tipSessionChangeId))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.TipSessionDeleteRequestChangeId)] = tipSessionChangeId;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.TipSessionDeleteRequestChangeId), string.Empty);
        }

        /// <summary>
        /// Metadata 'TipSessionRequestChangeId'
        /// </summary>
        public static string TipSessionRequestChangeId(this EnvironmentEntity entity, string tipSessionChangeId = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(tipSessionChangeId))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.TipSessionRequestChangeId)] = tipSessionChangeId;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.TipSessionRequestChangeId), string.Empty);
        }

        /// <summary>
        /// Metadata 'TipSessionCreatedTime'
        /// </summary>
        public static DateTime TipSessionCreatedTime(this EnvironmentEntity entity, DateTime? tipSessionCreatedTime = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (tipSessionCreatedTime != null)
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.TipSessionCreatedTime)] = tipSessionCreatedTime;
            }

            return entity.GetDateMetadata(nameof(EnvironmentEntityExtensions.TipSessionCreatedTime));
        }

        /// <summary>
        /// Metadata 'TipSessionDeletedTime'
        /// </summary>
        public static DateTime TipSessionDeletedTime(this EnvironmentEntity entity, DateTime? tipSessionDeletedTime = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (tipSessionDeletedTime != null)
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.TipSessionDeletedTime)] = tipSessionDeletedTime;
            }

            return entity.GetDateMetadata(nameof(EnvironmentEntityExtensions.TipSessionDeletedTime));
        }

        /// <summary>
        /// Metadata 'TipSessionExpirationTime'
        /// </summary>
        public static DateTime TipSessionExpirationTime(this EnvironmentEntity entity, DateTime? tipSessionExpirationTime = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (tipSessionExpirationTime != null)
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.TipSessionExpirationTime)] = tipSessionExpirationTime;
            }

            return entity.GetDateMetadata(nameof(EnvironmentEntityExtensions.TipSessionExpirationTime));
        }

        /// <summary>
        /// Metadata 'TipSessionRequestedTime'
        /// </summary>
        public static DateTime TipSessionRequestedTime(this EnvironmentEntity entity, DateTime? tipSessionRequestedTime = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (tipSessionRequestedTime != null)
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.TipSessionRequestedTime)] = tipSessionRequestedTime;
            }

            return entity.GetDateMetadata(nameof(EnvironmentEntityExtensions.TipSessionRequestedTime));
        }

        /// <summary>
        /// Metadata 'TipSessionStatus'
        /// </summary>
        public static string TipSessionStatus(this EnvironmentEntity entity, string tipSessionStatus = null)
        {
            entity.ThrowIfNull(nameof(entity));
            if (!string.IsNullOrWhiteSpace(tipSessionStatus))
            {
                entity.Metadata[nameof(EnvironmentEntityExtensions.TipSessionStatus)] = tipSessionStatus;
            }

            return entity.Metadata.GetValue<string>(nameof(EnvironmentEntityExtensions.TipSessionStatus), string.Empty);
        }

        /// <summary>
        /// Extension returns all nodes of a specific type from the source set.
        /// </summary>
        /// <param name="source">The source set of environment entities.</param>
        /// <param name="entityType">Defines the type of entity (e.g. Cluster, Node).</param>
        /// <param name="environmentGroup">Optional parameter defines the target environment group on which to filter.</param>
        /// <returns>
        ///  The set of <see cref="EnvironmentEntity"/> objects matching the specific type.
        /// </returns>
        public static IEnumerable<EnvironmentEntity> GetEntities(this IEnumerable<EnvironmentEntity> source, EntityType entityType, string environmentGroup = null)
        {
            source.ThrowIfNull(nameof(source));

            List<EnvironmentEntity> entities = new List<EnvironmentEntity>();
            StringComparison ignoreCase = StringComparison.OrdinalIgnoreCase;

            IEnumerable<EnvironmentEntity> matchingSet = !string.IsNullOrWhiteSpace(environmentGroup)
                ? source.Where(entity => entity.EntityType == entityType && string.Equals(entity.EnvironmentGroup, environmentGroup, ignoreCase))
                : source.Where(entity => entity.EntityType == entityType);

            if (matchingSet?.Any() == true)
            {
                entities.AddRange(matchingSet);
            }

            return entities;
        }

        /// <summary>
        /// Returns node entities from the entity pool matching the node affinity designation (i.e. selection strategy)
        /// and that have not been marked as discarded (in the entity metadata).
        /// </summary>
        /// <param name="entities">The pool of entities to search within.</param>
        /// <param name="nodeAffinity">The node affinity to apply to the selection criteria.</param>
        /// <param name="environmentGroup">Defines the target environment group for which the nodes are related.</param>
        /// <param name="withAffinityToNodes">
        /// Optional parameter defines a reference node to use as a starting place for the selection of other nodes that must have
        /// the defined affinity to it.
        /// </param>
        /// <param name="countPerGroup">Count of nodes per group. Only used in validation so that request will be rejected if withAffinityToNodes already have counts larger than required amount.</param>
        public static EnvironmentEntity GetNode(
            this IEnumerable<EnvironmentEntity> entities, NodeAffinity nodeAffinity, string environmentGroup, int countPerGroup = 1, params EnvironmentEntity[] withAffinityToNodes)
        {
            entities.ThrowIfNull(nameof(entities));
            withAffinityToNodes?.ThrowIfInvalid(
                nameof(withAffinityToNodes),
                (affinityNodes) => !(affinityNodes.Where(e => e.EnvironmentGroup == environmentGroup).Distinct().Count() >= countPerGroup),
                $"Invalid environment group specified. " +
                $"The affinity nodes provided already have {withAffinityToNodes.Where(e => e.EnvironmentGroup == environmentGroup).Distinct().Count()} node in the environment group '{environmentGroup}'. " +
                $"which is larger or eqaul to the TipSessionCountPerGroup defined '{countPerGroup}'.");

            EnvironmentEntity matchingNode = null;

            // Get all node entities that have not been marked as "discarded"
            IEnumerable<EnvironmentEntity> nodeEntities = entities.GetEntities(EntityType.Node, environmentGroup)
                ?.Where(node => !node.Discarded());

            if (nodeEntities?.Any() == true)
            {
                switch (nodeAffinity)
                {
                    case NodeAffinity.Any:
                        // Selection Strategy
                        // - Nodes cannot match the affinity reference nodes (by ID)
                        // - Nodes cannot be in the same group as the affinity reference nodes.
                        matchingNode = nodeEntities
                           ?.NotMatching(withAffinityToNodes)
                           ?.Shuffle()
                           ?.FirstOrDefault(node => node.EnvironmentGroup.Equals(environmentGroup, StringComparison.OrdinalIgnoreCase));

                        break;

                    case NodeAffinity.SameRack:
                        // Selection Strategy
                        // - Nodes cannot match the affinity reference nodes (by ID)
                        // - Nodes cannot be in the same group as the affinity reference nodes.
                        // - Nodes must be in the same cluster
                        // - Nodes must be in the same rack
                        // - Best Match = Nodes associated with the rack having the most nodes
                        matchingNode = nodeEntities
                            ?.NotMatching(withAffinityToNodes)
                            ?.InSameClusterAs(withAffinityToNodes)
                            ?.InSameRackAs(withAffinityToNodes)
                            ?.GroupBy(node => node.RackLocation())
                            ?.BestMatch(environmentGroup);

                        break;

                    case NodeAffinity.SameCluster:
                        // Selection Strategy
                        // - Nodes cannot match the affinity reference nodes (by ID)
                        // - Nodes cannot be in the same group as the affinity reference nodes.
                        // - Nodes must be in the same cluster
                        // - It is preferable that the nodes not be on the same rack but ok if they are.
                        // - Best Match = Nodes associated with the rack having the most nodes
                        matchingNode = nodeEntities
                            ?.NotMatching(withAffinityToNodes)
                            ?.InSameClusterAs(withAffinityToNodes)
                            ?.Shuffle()
                            ?.FirstOrDefault(node => node.EnvironmentGroup.Equals(environmentGroup, StringComparison.OrdinalIgnoreCase));
                        break;

                    case NodeAffinity.DifferentCluster:
                        // Selection Strategy
                        // - Nodes cannot match the affinity reference nodes (by ID)
                        // - Nodes cannot be in the same group as the affinity reference nodes.
                        // - Nodes must NOT be in the same cluster
                        // - Best Match = Nodes associated with the cluster having the most nodes
                        matchingNode = nodeEntities
                            ?.NotMatching(withAffinityToNodes)
                            ?.NotInSameClusterAs(withAffinityToNodes)
                            ?.GroupBy(node => node.ClusterName())
                            ?.BestMatch(environmentGroup);
                        break;
                }
            }

            return matchingNode;
        }

        /// <summary>
        /// Returns node entities from the entity pool.
        /// </summary>
        /// <param name="entities">The pool of entities to search within.</param>
        /// <param name="environmentGroup">Defines the target environment group for which the nodes are related.</param>
        public static IEnumerable<EnvironmentEntity> GetNodes(this IEnumerable<EnvironmentEntity> entities, string environmentGroup = null)
        {
            return entities.GetEntities(EntityType.Node, environmentGroup);
        }

        /// <summary>
        /// Returns TiP session entities from the entity pool.
        /// </summary>
        /// <param name="entities">The pool of entities to search within.</param>
        /// <param name="environmentGroup">Defines the target environment group for which the TiP sessions are related.</param>
        public static IEnumerable<EnvironmentEntity> GetTipSessions(this IEnumerable<EnvironmentEntity> entities, string environmentGroup = null)
        {
            return entities.GetEntities(EntityType.TipSession, environmentGroup);
        }

        private static DateTime GetDateMetadata(this EnvironmentEntity entity, string metadataKey)
        {
            DateTime dateValue = DateTime.MinValue;
            if (entity.Metadata.TryGetValue(metadataKey, out IConvertible metadataValue))
            {
                dateValue = metadataValue.ToDateTime(CultureInfo.InvariantCulture);
            }

            return dateValue;
        }

        private static EnvironmentEntity BestMatch(this IEnumerable<IGrouping<string, EnvironmentEntity>> nodeGroups, string environmentGroup)
        {
            EnvironmentEntity matchingNode = null;

            if (nodeGroups?.Any() == true)
            {
                // Example:
                // Rack01 -> Node01,Group A
                //        -> Node02,Group B
                //        -> Node03,Group A
                //        -> Node04,Group B
                // Rack02 -> Node03,Group A
                // Rack02 -> Node04,Group B
                foreach (IGrouping<string, EnvironmentEntity> nodeGroup in nodeGroups.OrderByDescending(nodes => nodes.Count()))
                {
                    // Find the group with the most options that can satisfy the same-rack requirement.
                    matchingNode = nodeGroup.GetEntities(EntityType.Node, environmentGroup)
                        ?.FirstOrDefault();

                    if (matchingNode != null)
                    {
                        break;
                    }
                }
            }

            return matchingNode;
        }

        private static IEnumerable<EnvironmentEntity> InSameClusterAs(this IEnumerable<EnvironmentEntity> entities, params EnvironmentEntity[] referenceEntities)
        {
            return referenceEntities?.Any() == true
                ? entities.Where(entity => entity.ClusterName().Equals(referenceEntities.First().ClusterName(), StringComparison.OrdinalIgnoreCase))
                : entities;
        }

        private static IEnumerable<EnvironmentEntity> InSameRackAs(this IEnumerable<EnvironmentEntity> entities, params EnvironmentEntity[] referenceEntities)
        {
            return referenceEntities?.Any() == true
                ? entities.Where(entity => entity.RackLocation().Equals(referenceEntities.First().RackLocation(), StringComparison.OrdinalIgnoreCase))
                : entities;
        }

        private static IEnumerable<EnvironmentEntity> NotInSameClusterAs(this IEnumerable<EnvironmentEntity> entities, params EnvironmentEntity[] referenceEntities)
        {
            return referenceEntities?.Any() == true
                ? entities.Where(entity => !referenceEntities.Select(e => e.ClusterName()).Contains(entity.ClusterName(), StringComparer.OrdinalIgnoreCase))
                : entities;
        }

        private static IEnumerable<EnvironmentEntity> NotInSameGroupAs(this IEnumerable<EnvironmentEntity> entities, params EnvironmentEntity[] referenceEntities)
        {
            return referenceEntities?.Any() == true
                ? entities.Where(entity => !referenceEntities.Select(e => e.EnvironmentGroup).Contains(entity.EnvironmentGroup, StringComparer.OrdinalIgnoreCase))
                : entities;
        }

        private static IEnumerable<EnvironmentEntity> NotMatching(this IEnumerable<EnvironmentEntity> entities, params EnvironmentEntity[] referenceEntities)
        {
            return referenceEntities?.Any() == true
                ? entities.Where(entity => !referenceEntities.Select(e => e.Id).Contains(entity.Id, StringComparer.OrdinalIgnoreCase))
                : entities;
        }
    }
}
