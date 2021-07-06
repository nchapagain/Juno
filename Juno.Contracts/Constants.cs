namespace Juno.Contracts
{
    using System.Collections.Generic;

    /// <summary>
    /// Constants that define well-known extensions to Juno data contract
    /// objects.
    /// </summary>
    public static class ContractExtension
    {
        /// <summary>
        /// The name of the key for the error extension 
        /// property (name = 'error').
        /// </summary>
        public const string Error = "error";

        /// <summary>
        /// Extension contains the global entity pool for the experiment (name = 'entityPool').
        /// </summary>
        public const string EntityPool = "entityPool";

        /// <summary>
        /// Extension contains the global pool of entities successfully provisioned for
        /// and used in the experiment (name = 'entitiesProvisioned').
        /// </summary>
        public const string EntitiesProvisioned = "entitiesProvisioned";

        /// <summary>
        /// The name of the key which contains resource group information.
        /// Mainly used by ARM provider and Install guest agent provider to communicate.
        /// The placeholder will be replaced by group name.
        /// </summary>
        public const string ResourceGroup = "resourceGroup_{0}";

        /// <summary>
        /// The name of the key which contains the list of nodes for a group.
        /// The group is defined by which group the executing experiment step belongs to
        /// Used for storing nodes to state, and then retrieving them to 
        /// create tip session out of them.
        /// </summary>
        public const string NodeGroup = "nodeGroup_{0}";

        /// <summary>
        /// The name of the key for the child steps/components extension 
        /// property (name = 'steps').
        /// </summary>
        public const string Steps = "steps";

        /// <summary>
        /// The name of the key for the component target group extension property
        /// (name = 'targetGroup').
        /// </summary>
        public const string TargetGroup = "targetGroup";

        /// <summary>
        /// The string literal of the Type of a TimerTriggerProvider. Included here as to 
        /// not have to include an uneccessary dependency on Juno.Scheduler.Preconditions
        /// </summary>
        public const string TimerTriggerType = "Juno.Scheduler.Preconditions.TimerTriggerProvider";

        /// <summary>
        /// Name of a required parameter in the TimerTriggerProvider, used for validation.
        /// </summary>
        public const string CronExpression = "cronExpression";

        /// <summary>
        /// The string literal of the Type of a SuccessfulExperimentsProvider. Included here as to 
        /// not have to include an uneccessary dependency elsewhere
        /// </summary>
        public const string SuccessfulExperimentsProvider = "Juno.Scheduler.Preconditions.SuccessfulExperimentsProvider";

        /// <summary>
        /// The string literal of the Type of a InProgressExperimentsProvider. Included here as to 
        /// not have to include an uneccessary dependency elsewhere
        /// </summary>
        public const string InProgressExperimentsProvider = "Juno.Scheduler.Preconditions.InProgressExperimentsProvider";

        /// <summary>
        /// The string literal of the Type of a SelectEnvironmentAndCreateExperimentProvider.
        /// </summary>
        public const string SelectEnvironmentAndCreateExperimentProvider = "Juno.Scheduler.Actions.SelectEnvironmentAndCreateExperimentProvider";

        /// <summary>
        /// Name of a required parameter in the SuccessfulExperimentsProvider, used for validation.
        /// </summary>
        public const string TargetExperimentInstances = "targetExperimentInstances";

        /// <summary>
        /// The name of the key for the experiment flow override definiton.
        /// </summary>
        public const string Flow = "flow";

        /// <summary>
        /// The current envrionment query version supported by Goal Based Scheduler
        /// </summary>
        public const string CurrentEnvironmentQueryVersion = "2020-10-14";

        /// <summary>
        /// Name of the global list of diagnostics request items.
        /// </summary>
        public const string DiagnosticsRequests = "diagnosticsRequests";

        /// <summary>
        /// The current execution Goal Version supported by Goal Based Scheduler
        /// </summary>
        public static readonly List<string> SupportedExecutionGoalVersions = new List<string>() { "2020-07-27", "2021-01-01" };
    }

    /// <summary>
    /// Constants that define experiment metadata property names.
    /// </summary>
    public static class MetadataProperty
    {
        /// <summary>
        /// The ID of an agent in the system.
        /// </summary>
        public const string AgentId = "agentId";

        /// <summary>
        /// The type of an agent (e.g. HostAgent, GuestAgent).
        /// </summary>
        public const string AgentType = "agentType";

        /// <summary>
        /// The name of the cluster in which the Juno experiment is running.
        /// </summary>
        public const string ClusterName = "clusterName";

        /// <summary>
        /// The VM container ID.
        /// </summary>
        public const string ContainerId = "containerId";

        /// <summary>
        /// Context information associated with the experiment.
        /// </summary>
        public const string Context = "context";

        /// <summary>
        /// Metadata property defines whether auto-triage diagnostics should be enabled on the
        /// experiment.
        /// </summary>
        public const string EnableDiagnostics = "enableDiagnostics";

        /// <summary>
        /// The experiment group (e.g. Group A, Group B).
        /// </summary>
        public const string ExperimentGroup = "experimentGroup";

        /// <summary>
        /// The unique ID of the experiment instance.
        /// </summary>
        public const string ExperimentId = "experimentId";

        /// <summary>
        /// The unique ID of the experiment step instance.
        /// </summary>
        public const string ExperimentStepId = "experimentStepId";

        /// <summary>
        /// Metadata property defines the type of experiment (e.g. QoS, Production).
        /// </summary>
        public const string ExperimentType = "experimentType";

        /// <summary>
        /// Metadata property defines the generation of hardware for which the experiment
        /// is associated (e.g. Gen4, Gen5, Gen6, Gen7).
        /// </summary>
        public const string Generation = "generation";

        /// <summary>
        /// The experiment group (e.g. Group A, Group B).
        /// </summary>
        public const string GroupId = "groupId";

        /// <summary>
        /// Metadata property that defines the type of impact (or potential impact)
        /// associated with the experiment (e.g. None, Impactful).
        /// </summary>
        public const string ImpactType = "impactType";

        /// <summary>
        /// Metadata property defines the ID of the node/blade on which the experiment
        /// will run (e.g. 50654 for Gen6 Intel, 50657 for Gen7 Intel).
        /// </summary>
        public const string NodeCpuId = "nodeCpuId";

        /// <summary>
        /// The unique ID of the physical blade/node on which a Juno agent is running.
        /// </summary>
        public const string NodeId = "nodeId";

        /// <summary>
        /// The unique ID/name of the physical blade/node on which a Juno agent is running.
        /// </summary>
        public const string NodeName = "nodeName";

        /// <summary>
        /// Metadata property defines the payload that is being applied as part of the
        /// experiment (e.g. MCU2021.1).
        /// </summary>
        public const string Payload = "payload";

        /// <summary>
        /// Metadata property defines the type of payload that is being applied as part of the
        /// experiment (e.g. MicrocodeUpdate).
        /// </summary>
        public const string PayloadType = "payloadType";

        /// <summary>
        /// Metadata property defines the version of payload Pilotfish package that contains the payload
        /// being applied as part of the experiment.
        /// </summary>
        public const string PayloadPFVersion = "payloadPFVersion";

        /// <summary>
        /// Metadata property defines the version of payload that is being applied as part of the
        /// experiment.
        /// </summary>
        public const string PayloadVersion = "payloadVersion";

        /// <summary>
        /// Metadata property that defines the recommendation ID of the experiment.
        /// </summary>
        public const string RecommendationId = "recommendationId";

        /// <summary>
        /// Metadata property that defines the revision of the experiment.
        /// </summary>
        public const string Revision = "revision";

        /// <summary>
        /// Metadata property that defines the tenant ID of the experiment.
        /// </summary>
        public const string TenantId = "tenantId";

        /// <summary>
        /// The ID of the TiP node session (typically a Guid).
        /// </summary>
        public const string TipSessionId = "tipSessionId";

        /// <summary>
        /// The virtual machine name on which a Juno agent is running.
        /// </summary>
        public const string VirtualMachineName = "virtualMachineName";

        /// <summary>
        /// Metadata property that defines the workload that will run on the guest
        /// or host as part of the experiment (e.g. PERF-IO-FIO-V1).
        /// </summary>
        public const string Workload = "workload";

        /// <summary>
        /// Metadata property that defines the type of workload that will run on the guest
        /// or host as part of the experiment (e.g. VirtualClient).
        /// </summary>
        public const string WorkloadType = "workloadType";

        /// <summary>
        /// Metadata property that defines the version of the workload that will run on the guest
        /// or host as part of the experiment.
        /// </summary>
        public const string WorkloadVersion = "workloadVersion";
    }

    /// <summary>
    /// Constants associated with experiment work notice metadata.
    /// </summary>
    public static class NoticeMetadataKey
    {
        /// <summary>
        /// 'audit'
        /// </summary>
        public const string Audit = "audit";

        /// <summary>
        /// 'previousMessageId'. The ID of a previous/related message on a work queue.
        /// </summary>
        public const string PreviousMessageId = "previousMessageId";

        /// <summary>
        /// 'previousPopReceipt'. The pop receipt of a previous/related message on a work queue.
        /// </summary>
        public const string PreviousPopReceipt = "previousPopReceipt";
    }

    /// <summary>
    /// Constants that represent the names of admin accounts used for 
    /// Virtual Machines in the environment.
    /// </summary>
    public static class VmAdminAccounts
    {
        /// <summary>
        /// The default VM admin account.
        /// </summary>
        public const string Default = "junovmadmin";
    }

    /// <summary>
    /// Constants that represent the required executionGoalMetadata 
    /// attributes shared between execution goals and templates
    /// </summary>
    public static class ExecutionGoalMetadata
    {
        /// <summary>
        /// Name of the attribute containing the email monitoring attribute
        /// </summary>
        public const string MonitoringEnabled = "monitoringEnabled";

        /// <summary>
        /// Name of the attribute containing the email address of the experiment owner
        /// </summary>
        public const string Owner = "owner";

        /// <summary>
        /// Name of the metadata that refernces the team name of the execution goal.
        /// </summary>
        public const string TeamName = "teamName";

        /// <summary>
        /// Name of the metadata that references the version of the execution goal.
        /// </summary>
        public const string Version = "version";

        /// <summary>
        /// the id of the tenant that the execution goal belongs to.
        /// </summary>
        public const string TenantId = "tenantId";

        /// <summary>
        /// The parameter key for the experiment name
        /// </summary>
        public const string ExperimentName = "experiment.name";

        /// <summary>
        /// The tempalte in which the execution goal was created from.
        /// </summary>
        public const string ExecutionGoalTemplateId = "executionGoalTemplateId";

        /// <summary>
        /// Metadata that is required by the Execution Goal.
        /// </summary>
        public static readonly string[] RequiredMetadata = { ExecutionGoalMetadata.Owner, ExecutionGoalMetadata.Version, ExecutionGoalMetadata.TeamName, ExecutionGoalMetadata.TenantId };

        /// <summary>
        /// Metadata that is required by the Execution Goal Parameter.
        /// </summary>
        public static readonly string[] RequiredParameterMetadata = { ExecutionGoalMetadata.Owner, ExecutionGoalMetadata.Version, ExecutionGoalMetadata.TeamName, ExecutionGoalMetadata.TenantId, ExecutionGoalMetadata.ExperimentName };
    }

    /// <summary>
    /// Parameters that are found in target goals.
    /// </summary>
    public static class TargetGoalParameters
    {
        /// <summary>
        /// The parameter key for the workload
        /// </summary>
        public const string Workload = "metadata.workload";
    }
}
