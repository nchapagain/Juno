# Juno Experiment Providers

#### Preliminaries
It is important to understand the basic schema of an experiment before attempting to write or reference Juno experiment providers.

[Juno Experiment Schema](./ExperimentSchema.md)

In the Juno system, providers are fundamental to the execution of an experiment. In fact, each component within the **'workflow'** definition 
for an experiment will have a provider that knows how to execute the runtime expectations of that part/step of the experiment. The terms step type and
provider type mean the same thing in the Juno system. The following list of provider types are used in the Juno system.

*As noted in the experiment schema documentation, the **'type'** property of the experiment workflow step/component definition defines the exact provider
that will be created during the experiment execution runtime to handle the specifics of that particular step.*

* **Environment criteria**  
  Environment criteria providers are used to select the physical or virtual entities (e.g. clusters, nodes, VMs) that will be used in each Juno
  environment/experiment group to host experiment runtime requirements. For example, a particular environment criteria provider might query a Kusto
  data store to find a set of clusters in Azure data centers that have physical nodes/blades that have a specific Intel CPU.

  See [Authoring-ClusterSelectionProvider.md](./Authoring-ClusterSelectionProvider.md)

* **Environment setup**  
  Environment setup providers are used to describe the build-out of the actual environment (e.g. establishing TiP sessions, creating VMs) once the physical and
  virtual entities have been selected. For example, a specific environment setup step might be responsible for establishing a TiP (test-in-production)
  session for a specific node or for deploying a set of VMs to that node through ARM.

  See [Authoring-TipCreationProvider.md](./Authoring-TipCreationProvider.md)

* **Environment cleanup**  
  Environment cleanup providers define how and when to tear down/cleanup environments used in experiments. The Juno system enables authors of experiments to 
  be explicit about the cleanup of the environment to support flexibility with scenarios that require changing the environment during the course of a single
  experiment.

  See [Authoring-TipCleanupProvider.md](./Authoring-TipCleanupProvider.md)

* **Payload**  
  Payload providers handle the application of the "treatment" to physical entities (e.g. nodes) in the environment at runtime. For example, the provider might be used
  to apply an Intel microcode update (IPU) to a physical node before running workloads in VMs on that node.

  See [Authoring-ApplyPilotFishProvider.md](./Authoring-ApplyPilotFishProvider.md)

* **Workload**  
  Workload providers handle running customer-representative workloads on the physical entities (e.g. nodes, virtual machines) in the environment. Workloads 
  exercise the physical systems in order to produce data that can be used to identify differences in performance or reliability etc... between environment groups
  that have had a payload applied versus environment groups that have not.

  See [Authoring-VirtualClientWorkloadProvider](./Authoring-VirtualClientWorkloadProvider.md)

* **Diagnostics**
Diagnostics providers run operations that are focused on getting information out of the environment. This provider might be responsible for capturing
BIOS information from a physical node used as part of the experiment. The distinction between a Diagnostics provider and a Watchdog provider (below) is
that a Diagnostics provider typically executes very few times to gather data and then is finished, whereas the Watchdog is a long-running monitor.

* **Watchdog**
Similar to the Diagnostics provider type, these providers perform long-running operations focused on monitoring the state/status of resources in the
environment (e.g. physical blade state, VM state). Watchdog providers are typically responsible for installing an agent or acting as an agent on a
system running as part of a Juno experiment.

* **Dependency**
Dependency providers handle downloading, installing or configuring dependencies on a system as part of a Juno experiment. For example, the provider might
be responsible for downloading a package dependency from a NuGet feed to a VM as part of the experiment workflow.

  See [Authoring-BiosSettingsProvider](./Authoring-BiosSettingsProvider.md)

### Defining Experiment Providers
All experiment providers in the Juno system derive from the base provider ```ExperimentProvider```.

``` csharp
public abstract class ExperimentProvider : IExperimentProvider
{
    protected ExperimentProvider(IServiceProvider services)
    {
        services.ThrowIfNull(nameof(services));
        this.Services = services;
    }

    // Used to capture telemetry data
    public ILogger Logger { get; }

    // Provides a set of one or more dependencies required by the provider.
    public IServiceProvider Services { get; }

    // Enables the provider to configure additional dependencies (adding them to the Services) that it will
    // require at runtime.
    public virtual Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component);

    // The entry-point method for a provider. The Juno execution engine will call this method to execute the provider/step
    // during a typical execution flow.
    public Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)

    // Single method required to be implemented by ALL providers (i.e. that derive from the ExperimentProvider class).
    protected abstract Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken);

    // Enables the provider to perform special validation against the parameters defined in the
    // experiment component.
    protected virtual void ValidateParameters(ExperimentComponent component)
}
```

The ```ExperimentProvider``` base class requires a single method to be implemented ```ExecuteAsync``` that is responsible for handling the runtime 
specifics/requirements defined by the ```ExperimentComponent``` that is passed into the method. Additionally, context for the experiment is passed to the 
method in the ```ExperimentContext``` class.  The experiment for which the provider is related/running is provided along with the exact experiment step.
Also, the configuration settings for the environment in which the entire experiment is running is provided in the case that the provider needs environment-wide
settings as part of its dependency requirements.

``` csharp
public class ExperimentContext
{
    // Provides configuration settings for the entire Juno environment.
    public IConfiguration Configuration { get; }

    // The complete experiment instance definition.
    public ExperimentInstance Experiment { get; }

    // The specific experiment step instance definition. This corresponds to one of the steps defined in the experiment workflow.
    public ExperimentStepInstance ExperimentStep { get; }
}
```

All experiment provider implementation must abide by the following requirements:
* Providers must derive from the base ```ExperimentProvider``` class.
* Provider classes must provide an empty constructor.
* Provider classes must have a second constructor that takes in a single ```IServiceProvider``` parameter.
* Provider classes must be decorated with an ```[ExecutionConstraints]``` attribute that defines preconditions/metadata about the execution constraints required by the provider.

  ``` csharp
  [ExecutionConstraints(SupportedStepType.ExperimentCriteria, SupportedStepTarget.ExecuteRemotely)]
  public class ClusterSelection : ExperimentProvider
  {
      public ClusterSelection()
        : base()
      {
      }

      public ClusterSelection(IServiceProvider services)
        : base(services)
      {
      }
  }

  [ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteRemotely)]
  public class PFServiceApplication : ExperimentProvider
  {
      public PFServiceApplication()
        : base()
      {
      }

      public PFServiceApplication(IServiceProvider services)
        : base(services)
      {
      }
  }

  [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteOnVirtualMachine)]
  public class RunWorkload : ExperimentProvider
  {
      public RunWorkload()
        : base()
      {
      }

      public RunWorkload(IServiceProvider services)
        : base(services)
      {
      }
  }
  ```

### Types of Experiment Providers
Although the specifics of a provider implementation are up to the developer, there are essentially only 2 types of experiment provider implementations in the 
Juno system:

* **Transactional Providers**  
  Providers that are transactional execute the logic necessary to satisfy the requirements of the experiment component from which they
  were defined once and return a final result. These types of providers typically perform operations that do not require much time to complete
  and so there is no need for the runtime execution engine to monitor them in the background or reevaluate them at a later point. These providers
  execute and return a terminal state 

* **Asynchronous Monitored Providers**  
  Providers that are asynchronous monitored have execution requirements that are long-running and need to be monitored for completion by the
  runtime execution engine at various points until completion. For example, a provider that is responsible for running a workload on a given
  agent (host/node or VM/guest) in the system may not actually complete the necessary work until many hours in the future. In order to ensure
  the runtime execution engine can continue to process work for the experiment (and for other experiments), the provider must indicate to the 
  runtime execution engine that it needs to be re-evaluated in the future. It does so by providing a status of 'InProgress' or 'InProgressContinue'
  to the execution engine when it returns an execution result.

To define a provider as one of the following types, the developer will indicate this by defining the supported step type for the provider in the
```[ExecutionConditions]``` attribute noted in the requirements above.  The following step types describe the type of experiment providers:

*(Any step type can be either transactional or asynchronous monitored. The distinction is an implementation detail as described below.)*
``` csharp
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
    Diagnostics = 8
}
```

Additionally, the developer must indicate the execution environment(s) for which the provider can execute. This distinction tells the Juno
system what steps must run remotely (e.g. not in-process of a Juno agent) versus steps that for example must be ran on physical nodes or virtual machines
hosted by Juno agents (i.e. host or guest agents).

``` csharp
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
    ExecuteOnVirtualMachine = 4,
}
```

**Examples of Usage**
``` csharp
// Example 1:
// The step provider here declares 2 things:
// 1) It executes an environment setup operation.
// 2) It must run in the execution orchestration service for the Juno environment.
[ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
public class VirtualMachineProvider : ExperimentProvider
{
}

// Example 2:
// The step provider here declares 2 things:
// 1) It executes workloads.
// 2) It must run on a virtual machine. In the Juno system, this means that execution of the provider will be handled
//    by the Juno guest/VM agent that runs on the virtual machine.
[ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteOnVirtualMachine)]
public class WorkloadProvider : ExperimentProvider
{
}

// Example 3:
// The step provider here declares 2 things:
// 1) It executes the application of a payload.
// 2) It must run on either a physical node in the system. In the Juno system, this means that execution of the provider will be handled
//    by the Juno host agent that runs on the physical node.
[ExecutionConstraints(SupportedStepType.Payload, SupportedStepTarget.ExecuteOnNode)]
public class PayloadProvider : ExperimentProvider
{
}
```

### Provider Implementation Details
When implementing custom providers, the developer must consider the ```ExecutionResult``` of the provider execution. The Juno runtime
execution engine cannot and does not know the specifics of what the provider does internally. So, the provider communicates its needs and
intent to the runtime execution engine using information provided in the result.

``` csharp
public class ExecutionResult
{
    // Allows the provider to return any error information to the
    // execution engine.
    public Exception Error { get; }

    // e.g. Pending, InProgress, InProgressContinue, Succeeded, Failed, Cancelled
    public ExecutionStatus Status { get; }
}
```
<br/>

##### Property Descriptions

| Property         | Description     |
| ---------------- | --------------- |
| Error            | Any error/exception that occurred during the provider's execution that caused the provider to fail. Exceptions should be provided only when the provider returns a status of 'Failed'
| Status           | The status of the provider's execution. This property allows the provider to communicate it's outcome to the runtime execution engine. For transactional providers, this is typically a status of 'Succeeded' or 'Failed'. For asynchronous monitored providers, this may additionally be a status of 'InProgress' or 'InProgressContinue' which indicates to the runtime execution engine that the provider will need to be re-evaluated in the future for a final outcome (see status below).

<br/>

##### Execution Statuses

| Status             | Description     |
| ------------------ | --------------- |
| Pending            | The provider/step execution is pending (not started).
| InProgress         | The provider/step execution is in-progress and the system should wait for it to complete before continuing to the next pending step(s). This is one of the 2 statuses an asynchronous monitored provider returns when it wants to be re-evaluated at a later point in time before rendering a final outcome. The runtime execution engine will continue to re-evaluate providers in this status until a final/terminal status is provided (e.g. Succeeded, Failed, Cancelled).
| InProgressContinue | The provider/step execution is in-progress waiting for confirmation from other steps in-progress; however, the system should not wait for for completion before continuing to the next pending step(s). This is one of the 2 statuses an asynchronous monitored provider returns when it wants to be re-evaluated at a later point in time before rendering a final outcome. The runtime execution engine will continue to re-evaluate providers in this status until a final/terminal status is provided (e.g. Succeeded, Failed, Cancelled).
| Succeeded          | The provider/step execution completed successfully.
| Failed             | The provider/step execution failed.
| Cancelled          | The provider/step execution was cancelled.

##### Provider Execution Reentrancy
Asynchronous monitored providers must be implemented to be reentrant. When a provider returns a status of **InProgress** or **InProgressContinue**, the runtime execution
engine cannot determine the final outcome of the provider/step execution at that time. In fact, the provider is indicating to the runtime execution engine that it does not
yet have a final outcome/status. Thus, it is important that any asynchronous monitored provider (i.e. one that might return a status of InProgress or InProgressContinue) be 
implemented such that it can be executed any number of times until a final/terminal status is provided (e.g. Succeeded, Failed, Cancelled). The runtime execution engine will
continue to execute providers that are in **InProgress** or **InProgressContinue** any number of times until that final/terminal status is provided.

##### Storing Provider State
To make a provider reentrant, it is often important for a provider to know what it did on the last execution. Asynchronous monitored providers will often have the
need to store state as such. The Juno system and provider framework provides a facility for any provider to store state for itself.  Every provider
has access to a method ```SaveState``` that can be called at any time to preserve state information. The object used to represent the state must be JSON-serializable but
can otherwise be any structure desired for a particular scenario. The ```GetState``` method can be called when a provider executes to retrieve any previously stored state.
Note that Juno can save state information in a shared/global state object (by default), but also allows providers to indicate their state should be saved
in individual objects instead.

``` csharp
Task<TState> GetStateAsync<TState>(ExperimentContext context, CancellationToken cancellationToken, bool sharedState = true)
{
}

Task SaveStateAsync<TState>(ExperimentContext context, TState state, CancellationToken cancellationToken, bool sharedState = true)
{
}
```

### Requirements for Environment Criteria Providers (MVP behavior)
Environment criteria/selection providers can be a bit of a special case in the Juno system. Whereas, it is not generally a requirement that
providers work in a specific way, Juno environment criteria providers must follow a certain set of rules for the MVP scenario:

* The providers must store information for any environment entities selected to meet the criteria of an experiment in the global
  context object for the experiment under the name/key 'entityPool'.

* The data structure for the **'entityPool'** context/state object must be a collection of EnvironmentEntity objects (e.g. IEnumerable&lt;EnvironmentEntity&gt;).  See below.

* Environment criteria providers must match individual results (for environment entities) against any existing objects in the **'entityPool'**. The match algorithm
  must be subtractive meaning that any entities of a given type (e.g. ClusterEntity) that do not match with previously selected entities must be removed
  from the entity pool (e.g. filtered out using a logical 'and' algorithm).

  ``` json
  (Example Structure)
  [
    {
        "id": "Cluster01",
        "parentId": null,
        "entityType": "ClusterEntity",
        "environmentGroup": "Group A",
        "metadata": {
            "region": "WestUS2"
        }
    },
    {
        "id": "Cluster02",
        "parentId": null,
        "entityType": "ClusterEntity",
        "environmentGroup": "Group B",
        "metadata": {
            "region": "WestUS2"
        }
    },
    {
        "id": "Node01",
        "parentId": "Cluster01",
        "entityType": "NodeEntity",
        "environmentGroup": "Group A",
        "metadata": {
            "cpuId": "50654",
            "cpuName": "Skylake"
        }
    },
    {
        "id": "Node02",
        "parentId": "Cluster01",
        "entityType": "NodeEntity",
        "environmentGroup": "Group A",
        "metadata": {
            "cpuId": "50654",
            "cpuName": "Skylake"
        }
    },
    {
        "id": "Node03",
        "parentId": "Cluster02",
        "entityType": "NodeEntity",
        "environmentGroup": "Group B",
        "metadata": {
            "cpuId": "50654",
            "cpuName": "Skylake"
        }
    },
    {
        "id": "Node04",
        "parentId": "Cluster02",
        "entityType": "NodeEntity",
        "environmentGroup": "Group B",
        "metadata": {
            "cpuId": "50654",
            "cpuName": "Skylake"
        }
    }
  ]
  ```

### Example Implementations
The following examples illustrate common scenarios and requirements for implmenting Juno experiment providers. The examples will be abridged in order to provide
focus on the most important parts of the experiment provider implementation.

##### Example 1: A Basic Transactional Provider
In this scenario, the provider will be responsible for selecting entities to add to the 'entity pool'. The entity pool is a set of physical entities within the
environment that can be used to setup the various environments for the experiment groups on which payloads and workloads will be applied.  This
provider is a 'Transactional Provider'. This is because the provider executes and completes the requirement of the step in a single
execution. A terminal result (Succeeded, Failed or Cancelled) will be returned on that first execution.

[Source Code](https://msazure.visualstudio.com/One/_git/CSI-CRC-AIR?path=%2Fsrc%2FJuno%2FJuno.Providers%2FDemo%2FClusterSelectionProvider.cs) 

<div style="color:#87AFC7;font-weight:500">Example Workflow Step</div>

``` json
{
    "type": "Juno.Providers.Environment.Demo.ClusterSelectionProvider",
    "name": "Experiment Cluster Selection",
    "description": "Example of cluster selection provider.",
    "group": "*",
    "parameters": {
        "nodeCpuId": "50654",
        "minTipSessionsAvailable": 5,
        "vmSku": "Standard_DS2_V2"
    }
}
```

<div style="color:#87AFC7;font-weight:500">Example Implementation</div>

``` csharp
// The experiment provider is attributed to support environment criteria/selection steps and to execute in the Juno
// execution orchestration service.
[ExecutionConstraints(SupportedStepType.EnvironmentCriteria, SupportedStepTarget.ExecuteRemotely)]
public class ClusterSelectionProvider : ExperimentProvider
{
    public ClusterSelectionProvider(IServiceProvider services)
        : base(services)
    {
    }

    protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
    {
        context.ThrowIfNull(nameof(context));
        component.ThrowIfNull(nameof(component));

        // 1) Query Kusto cluster to get clusters having nodes that match the criteria of
        //    the experiment. The criteria are defined in the component parameters as shown in the
        //    example step above.
        IEnumerable<EnvironmentEntity> entityPool = await this.ExecuteClusterSearchAsync(context, component);
        telemetryContext.AddContext(nameof(entityPool), entityPool);

        // 2) Get the existing entity pool (e.g. populated by any previous environment criteria/selection providers
        IEnumerable<EnvironmentEntity> existingEntityPool = await this.GetEntityPoolAsync(context, cancellationToken);
                
        if (existingEntityPool?.Any() == true)
        {
            // 3) Resolve the entities matched by this provider with entities matched by previous environment
            //    criteria providers that executed. This essential works like a filter. Any entities that will
            //    will remain in the entity pool, must have been matched by ALL environment criteria providers.
            entityPool = existingEntityPool.MatchOrRemove(EntityType.Cluster, entityPool);
        }

        // 3) Save the matching pool of entities/clusters from that matching set. If the results of #3 above
        //    resulted in no remaining entities that match the criteria defined, then the experiment will likely
        //    be failed.
        await this.SaveEntityPoolAsync(context, entityPool, cancellationToken);

        return new ExecutionResult(ExecutionStatus.Succeeded);
    }
}
```

##### Example 2: A Basic Asynchronous Provider
In this scenario, the provider will be responsible for creating VMs on a physical node in an Azure data center cluster that
has been isolated from customer workloads (e.g. a TiPNode). This physical node will later have an Intel IPU microcode update applied. 
The provider is an 'Asynchronous Provider' because the process of creating VMs is a long-running process that involves creating an initial 
ARM request and then polling for the status of that request over time. The provider cannot and should not block the execution service for that
long-running period of time. Thus the provider is implemented to be reentrant and can be executed any number of times until a final result can
be determined.

This example illustrates the use of 'state' preservation in the Juno system to enable the implementation of a stateful/reentrant provider.
Essentially, the provider needs to be idempotent and must remember what it did previously each subsequent time it is executed until it
finally completes its requirements. It uses a state object preserved on each execution of the provider to accomplish this.

[Source Code](https://msazure.visualstudio.com/One/_git/CSI-CRC-AIR?path=%2Fsrc%2FJuno%2FJuno.Providers%2FDemo%2FVirtualMachineProvider.cs) 

<div style="color:#87AFC7;font-weight:500">Example Workflow Step</div>

``` json
{
    "type": "Juno.Providers.Environment.Demo.VirtualMachineProvider",
    "name": "Create Virtual Machines",
    "description": "Create virtual machines on target nodes in the Group A environment.",
    "group": "Group A",
    "parameters": {
        "subscription": "1ae2ae24-8e85-4fa5-9a87-a9c2b54be2d0",
        "vmCount": "5",
        "vmSku": "Standard_DS2_V2"
    }
}
```

<div style="color:#87AFC7;font-weight:500">Example Implementation</div>

``` csharp
// The experiment provider is attributed to support environment setup steps and will execute in the Juno
// execution orchestration service.
[ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
public class VirtualMachineProvider : ExperimentProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualMachineProvider"/> class.
    /// </summary>
    /// <param name="services">The services/dependencies collection for the provider.</param>
    public VirtualMachineProvider(IServiceProvider services)
        : base(services)
    {
    }

    /// <summary>
    /// Mock executes the logic to request the deployment of the PF service application.
    /// </summary>
    protected override async Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
    {
        context.ThrowIfNull(nameof(context));
        component.ThrowIfNull(nameof(component));

        ExecutionResult result = null;

        VirtualMachineProviderState state = await this.GetStateAsync<VirtualMachineProviderState>(context, cancellationToken)
            ?? new VirtualMachineProviderState();

        if (state.VirtualMachinesRequested == null)
        {
            IEnumerable<EnvironmentEntity> entitiesProvisioned = await this.GetEntitiesProvisionedAsync(context, cancellationToken);

            if (entitiesProvisioned?.Any() != true)
            {
                throw new ProviderException(
                    $"The experiment does does not have matching physical nodes/blades in the target group '{context.ExperimentStep.ExperimentGroup}' " +
                    $"to which VMs can be deployed.",
                    ProviderErrorReason.DataNotFound);
            }

            string environmentGroup = context.ExperimentStep.ExperimentGroup;
            IEnumerable<EnvironmentEntity> nodesInGroup = entitiesProvisioned.GetEntities(EntityType.Node, environmentGroup);

            state.VirtualMachinesRequested = await this.CreateVirtualMachineRequestAsync(context, component, nodesInGroup);
            await this.SaveStateAsync(context, state, cancellationToken);

            result = new ExecutionResult(ExecutionStatus.InProgress);
        }
        else
        {
            // The result will be Succeeded when all VMs have been confirmed created via the ARM service. If
            // any VM fails to be created, the experiment will be failed.
            if(await this.VerifyVirtualMachinesCreated(context, state.VirtualMachinesRequested))
            {
                result = new ExecutionResult(ExecutionStatus.Succeeded);
            }
        }

        return result;
    }

    // Custom 'State' object used by the provider to enable reentrant/idempotent 
    // capabilities.
    private class VirtualMachineProviderState
    {
        public IEnumerable<EnvironmentEntity> VirtualMachinesRequested { get; set; }
    }
}
```

### Additional Documentation
The following documentation provides further details for scenarios, patterns and technologies used by Juno experiment providers.

* [Azure ARM Quickstart Templates](https://github.com/Azure/azure-quickstart-templates)
