namespace Juno.Contracts
{
    using System;
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
    }
}
