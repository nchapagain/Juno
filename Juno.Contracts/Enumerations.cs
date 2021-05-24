namespace Juno.Contracts
{
    /// <summary>
    /// Constants that represent different entity types (e.g. Cluster).
    /// </summary>
    public enum EntityType
    {
        /// <summary>
        /// Entity Type = Cluster
        /// </summary>
        Cluster,

        /// <summary>
        /// Entity Type = Rack
        /// </summary>
        Rack,

        /// <summary>
        /// Entity Type = Node
        /// </summary>
        Node,

        /// <summary>
        /// Entity Type = TiP Session
        /// </summary>
        TipSession,

        /// <summary>
        /// Entity Type = VirtualMachine
        /// </summary>
        VirtualMachine
    }

    /// <summary>
    /// Enumeration represents possible types of notices that
    /// may be used during an end-to-end experiment.
    /// </summary>
    public enum ExperimentNoticeType
    {
        /// <summary>
        /// Notice represents a request for work.
        /// </summary>
        WorkRequest
    }

    /// <summary>
    /// Enumeration represents the possible statuses for a an experiment.
    /// </summary>
    public enum ExperimentStatus
    {
        /// <summary>
        /// Status = Pending
        /// </summary>
        Pending,

        /// <summary>
        /// Status = InProgress
        /// </summary>
        InProgress,

        /// <summary>
        /// Status = PendingEnvironmentSetup
        /// </summary>
        PendingEnvironmentSetup,

        /// <summary>
        /// Status = PendingEnvironmentCleanup
        /// </summary>
        PendingEnvironmentCleanup,

        /// <summary>
        /// Status = PendingWorkflow
        /// </summary>
        PendingWorkflow,

        /// <summary>
        /// Status = EnvironmentSetupInProgress
        /// </summary>
        EnvironmentSetupInProgress,

        /// <summary>
        /// Status = EnvironmentCleanupInProgress
        /// </summary>
        EnvironmentCleanupInProgress,

        /// <summary>
        /// Status = WorkflowInProgress
        /// </summary>
        WorkflowInProgress,

        /// <summary>
        /// Status = Succeeded
        /// </summary>
        Succeeded,

        /// <summary>
        /// Status = Failed
        /// </summary>
        Failed,

        /// <summary>
        /// Status = Cancelled
        /// </summary>
        Cancelled,

        /// <summary>
        /// Status = Paused
        /// </summary>
        Paused
    }

    /// <summary>
    /// Enumeration represents the possible statuses for an experiment
    /// environment or workflow step.
    /// </summary>
    public enum ExecutionStatus
    {
        /// <summary>
        /// The step is pending (not started).
        /// </summary>
        Pending,

        /// <summary>
        /// The step is in-progress and the system should wait for it to complete
        /// before continuing to the next pending step(s).
        /// </summary>
        InProgress,

        /// <summary>
        /// The step is in-progress waiting for confirmation from other steps in-progress
        /// however, the system should not wait for for completion before continuing to the
        /// next pending step(s).
        /// </summary>
        InProgressContinue,

        /// <summary>
        /// The step completed successfully.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The step failed.
        /// </summary>
        Failed,

        /// <summary>
        /// The step was cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The step was cancelled by the system.
        /// </summary>
        SystemCancelled
    }

    /// <summary>
    /// Enumeration provides error reasons associated with 
    /// data store operation failures.
    /// </summary>
    public enum ErrorReason
    {
        /// <summary>
        /// The reason is undefined.
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// The Azure RM service deployment failed.
        /// </summary>
        ArmDeploymentFailure,

        /// <summary>
        /// A data/state object associated with a provider operation
        /// already exists in the backing data store.
        /// </summary>
        DataAlreadyExists,

        /// <summary>
        /// A data/state object expected by the provider does not exist.
        /// </summary>
        DataNotFound,

        /// <summary>
        /// The eTag for a data/state object for which the provider is attempting
        /// to update does not match expected.
        /// </summary>
        DataETagMismatch,

        /// <summary>
        /// The partition/partition key for a data/state object for which the provider is attempting
        /// to update does not match expected.
        /// </summary>
        DataPartitionKeyMismatch,

        /// <summary>
        /// There are no entities matching the critieria of the experiment.
        /// </summary>
        EntitiesMatchingCriteriaNotFound,

        /// <summary>
        /// Expected environment entities (e.g. Cluster, Node, TipSession) were not found
        /// in the system.
        /// </summary>
        ExpectedEnvironmentEntitiesNotFound,

        /// <summary>
        /// Expected environment entities (e.g. Cluster, Node, TipSession) were invalid
        /// in the system.
        /// </summary>
        ExpectedEnvironmentEntitiesInvalid,

        /// <summary>
        /// The experiment component definition usage invalid.
        /// </summary>
        InvalidUsage,

        /// <summary>
        /// The provider definition is invalid.
        /// </summary>
        ProviderDefinitionInvalid,

        /// <summary>
        /// A matching provider is not found in any of the assemblies loaded
        /// into the app domain.
        /// </summary>
        ProviderNotFound,

        /// <summary>
        /// The provider is not supported for the supported step type or targets
        /// defined in the execution constraints.
        /// </summary>
        ProviderNotSupported,

        /// <summary>
        /// Expected provider state/object data is invalid.
        /// </summary>
        ProviderStateInvalid,

        /// <summary>
        /// Expected state data/objects are missing for the provider.
        /// </summary>
        ProviderStateMissing,

        /// <summary>
        /// Expected steps (agent steps) are missing for the provider.
        /// </summary>
        ProviderStepsMissing,

        /// <summary>
        /// The schema of a particular experiment component definition is invalid.
        /// </summary>
        SchemaInvalid,

        /// <summary>
        /// Expected target agents do not exist in the experiment entity set definitions.
        /// </summary>
        TargetAgentsNotFound,

        /// <summary>
        /// Provider could not finish operation with in timeout.
        /// </summary>
        Timeout,

        /// <summary>
        /// A call to the TiP API service failed.
        /// </summary>
        TipRequestFailure,

        /// <summary>
        /// Expected virtual machines do not exist.
        /// </summary>
        VirtualMachinesNotFound,

        /// <summary>
        /// Provider dependency is failed.
        /// </summary>
        DependencyFailure,

        /// <summary>
        /// Unauthorized access
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Applying firewall rule failed.
        /// </summary>
        FirewallRuleApplicationFailure,

        /// <summary>
        /// Removing firewall rule failed.
        /// </summary>
        FirewallRuleRemovalFailure,

        /// <summary>
        /// Reaching the maximum failure threshold or retry policy.
        /// </summary>
        MaximumFailureReached,

        /// <summary>
        /// When a goal in a schedule is requested but can not be found.
        /// </summary>
        GoalNotFound
    }

    /// <summary>
    /// Enumeration defines the type of affinity (selection strategy) should be applied
    /// to nodes selected as part of an experiment.
    /// </summary>
    public enum NodeAffinity
    {
        /// <summary>
        /// Nodes selected as part of the experiment must be on the same rack in a given cluster.
        /// </summary>
        SameRack,

        /// <summary>
        /// Nodes selected as part of the experiment must exist in the same cluster but do not necessarily
        /// need to be on the same rack within that cluster.
        /// </summary>
        SameCluster,

        /// <summary>
        /// Nodes selected as part of the experiment must exist in different clusters entirely.
        /// </summary>
        DifferentCluster,

        /// <summary>
        /// Nodes selected as part of the experiment do not need to have any specific affinity
        /// to each other.
        /// </summary>
        Any
    }

    /// <summary>
    /// Enumeration represents the target runtime host on which the step (and corresponding
    /// provider) will be executed.
    /// </summary>
    public enum SupportedStepTarget
    {
        /// <summary>
        /// Step runs on the Juno host/node agent.
        /// </summary>
        ExecuteOnNode = 1,

        /// <summary>
        /// Step runs in the Juno execution orchestration service.
        /// </summary>
        ExecuteRemotely = 2,

        /// <summary>
        /// Step runs on the Juno guest/VM agent.
        /// </summary>
        ExecuteOnVirtualMachine = 3,
    }

    /// <summary>
    /// Enumeration represents the types of steps and step providers available
    /// for an experiment.
    /// </summary>
    public enum SupportedStepType
    {
        /// <summary>
        /// Step type is not defined.
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// Step is used in the cleanup/teardown of the environment.
        /// </summary>
        EnvironmentCleanup = 1,

        /// <summary>
        /// Step is used in the selection of an environment.
        /// </summary>
        EnvironmentCriteria = 2,

        /// <summary>
        /// Step is used in the setup/deployment of the environment.
        /// </summary>
        EnvironmentSetup = 3,

        /// <summary>
        /// Step is used to execute/apply a payload step.
        /// </summary>
        Payload = 4,

        /// <summary>
        /// Step is used to execute/apply a workload step.
        /// </summary>
        Workload = 5,

        /// <summary>
        /// Step is used to install a provider dependency.
        /// </summary>
        Dependency = 6,

        /// <summary>
        /// Step is used to collect information
        /// </summary>
        Watchdog = 7,

        /// <summary>
        /// Step is used for diagnostics
        /// </summary>
        Diagnostics = 8,

        /// <summary>
        /// Step is used for certification.
        /// </summary>
        Certification = 9,

        /// <summary>
        /// Step is used to cancel an experiment.
        /// </summary>
        Cancellation = 10
    }

    /// <summary>
    /// Enumeration represents agent heartbeat status
    /// </summary>
    public enum AgentHeartbeatStatus
    {
        /// <summary>
        /// Agent heartbeat is not defined.
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// Agent is starting.
        /// </summary>
        Starting = 1,

        /// <summary>
        /// Agent is running.
        /// </summary>
        Running = 2,

        /// <summary>
        /// Agent failed to start.
        /// </summary>
        Failed = 3
    }

    /// <summary>
    /// Type of the agent.
    /// </summary>
    public enum AgentType
    {
        /// <summary>
        /// Represents guest agent.
        /// </summary>
        GuestAgent,

        /// <summary>
        /// Represents Host agent
        /// </summary>
        HostAgent
    }

    /// <summary>
    /// Determine the extent of response being returned by the API
    /// </summary>
    public enum View
    {
        /// <summary>
        /// Summary view of the response
        /// </summary>
        Summary = 1,

        /// <summary>
        /// Full view of the response
        /// </summary>
        Full
    }

    /// <summary>
    /// Determine the extent of response for Execution Goal being returned by the API
    /// </summary>
    public enum ExecutionGoalView
    {
        /// <summary>
        /// Summary view of the response
        /// </summary>
        Summary = 1,

        /// <summary>
        /// Full view of the response
        /// </summary>
        Full = 2,

        /// <summary>
        /// Execution Goal Status
        /// </summary>
        Status = 3,

        /// <summary>
        /// Execution Goal Status
        /// </summary>
        Timeline = 4
    }

    /// <summary>
    /// An enumeration of all months in the
    /// Gregorian Calendar
    /// </summary>
    public enum Month
    {
        /// <summary>
        /// January
        /// </summary>
        JAN,

        /// <summary>
        /// February
        /// </summary>
        FEB,

        /// <summary>
        /// March
        /// </summary>
        MAR,

        /// <summary>
        /// April
        /// </summary>
        APR,

        /// <summary>
        /// May
        /// </summary>
        MAY,

        /// <summary>
        /// June
        /// </summary>
        JUN,

        /// <summary>
        /// July
        /// </summary>
        JUL,

        /// <summary>
        /// August
        /// </summary>
        AUG,

        /// <summary>
        /// September
        /// </summary>
        SEP,

        /// <summary>
        /// October
        /// </summary>
        OCT,

        /// <summary>
        /// November
        /// </summary>
        NOV,

        /// <summary>
        /// December
        /// </summary>
        DEC
    }

    /// <summary>
    /// An enumeration of all week days
    /// </summary>
    public enum WeekDay
    {
        /// <summary>
        /// Sunday
        /// </summary>
        SUN,

        /// <summary>
        /// Monday
        /// </summary>
        MON,

        /// <summary>
        /// Tuesday
        /// </summary>
        TUE,

        /// <summary>
        /// Wednesday
        /// </summary>
        WED,

        /// <summary>
        /// Thursday
        /// </summary>
        THU,

        /// <summary>
        /// Friday
        /// </summary>
        FRI,

        /// <summary>
        /// Saturday
        /// </summary>
        SAT
    }

    /// <summary>
    /// Constants that represent different experiment Impact types (e.g. None).
    /// </summary>
    public enum ImpactType
    {
        /// <summary>
        /// Impact Type = Impactful
        /// </summary>
        Impactful,

        /// <summary>
        /// Impact Type = None
        /// </summary>
        None
    }

    /// <summary>
    /// Categorize the type of environment filter.
    /// </summary>
    public enum FilterCategory
    {
        /// <summary>
        /// The filter is meant to search the Node space
        /// </summary>
        Node,

        /// <summary>
        /// The filter is meant to search the subscription space
        /// </summary>
        Subscription,

        /// <summary>
        /// The filter is meant to search the cluster space
        /// </summary>
        Cluster,

        /// <summary>
        /// The filter category is undefined
        /// </summary>
        Undefined
    }

    /// <summary>
    /// Defines the source of the parameters 
    /// for environment filters
    /// </summary>
    public enum FilterParameterSource
    {
        /// <summary>
        /// Source comes from the subscription filters
        /// </summary>
        Subscription,

        /// <summary>
        /// Source comes from an external parameter
        /// </summary>
        External,

        /// <summary>
        /// Source comes from the cluster filters
        /// </summary>
        Cluster
    }

    /// <summary>
    /// Defines various type of diagnostic issues offered by diagnostics framework.
    /// </summary>
    public enum DiagnosticsIssueType
    {
        /// <summary>
        /// Default issue type.
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// Microcode update failure issue.
        /// </summary>
        MicrocodeUpdateFailure,

        /// <summary>
        /// TIP pilotfish package deployment failure issue.
        /// </summary>
        TipPilotfishPackageDeploymentFailure,

        /// <summary>
        /// ARM VM creation failure issue.
        /// </summary>
        ArmVmCreationFailure
    }

    /// <summary>
    /// Defines the types of SSD types in the azure cloud.
    /// </summary>
    public enum SsdDriveType
    { 
        /// <summary>
        /// System Drive
        /// </summary>
        System = 0,

        /// <summary>
        /// Data Drive
        /// </summary>
        Data = 1
    }

    /// <summary>
    /// Constants that represent different sources for Leaked Resources
    /// </summary>
    public enum LeakedResourceSource
    {
        /// <summary>
        /// Azure CM
        /// </summary>
        AzureCM,

        /// <summary>
        /// TipClient
        /// </summary>
        TipClient,

        /// <summary>
        /// AzureCM And TipClient
        /// </summary>
        TipClientAndAzureCM,

        /// <summary>
        /// AzureResourceGroupManagement
        /// </summary>
        AzureResourceGroupManagement
    }
}
