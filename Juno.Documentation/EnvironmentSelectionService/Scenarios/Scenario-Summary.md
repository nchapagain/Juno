<div style="font-size:24pt;font-weight:600;color:#1569C7">ESS Scenarios</div>
<br/>

# Scenario Summary
This page outlines a brief summary for each scenario in the *Scenarios* folder. Treat this summaries page as a quick reference guide to
know which scenario is the correct scenario for a use case. 

## Basic Scenarios
It is suggested that if an author has no prior experience authoring environment queries to read each scenario 
in progression. If questions about any provider arises it is suggested to refer to the documentation for each 
individual provider. Basic scenarios follow a progressive composition, such that any given scenario contains the
same composition of all previous scenarios plus some. Basic scenarios are defined as:
* Scenarios an author might refer to further their understanding of authoring environment
queries
* Scenarios an author may use to create an environment query with very basic requirements.
### Scenario 1: Healthy Nodes In a Cluster
This scenario shows how to write an environment query that returns a set of healthy nodes in a cluster. 
Use this scenario when there are no attribute requirements for a node.  

#### Prerequisites
* Cluster Id must be known.

[See Scenario 1](./Scenario-HealthyNodes.md)

### Scenario 2: Healthy TiP-able Nodes In a Region
This scenario shows how to write an environment query that returns a set of healthy nodes whom belong to a cluster
that can support more tip sessions in a region. Use this scenario when there are no attribute requirements for a node to have, 
but the node should belong to a cluster that can support another tip session.

#### Prerequisites
* External Region Name must be known.

[See Scenario 2](./Scenario-TipableNodes.md)

### Scenario 3: Experiment Ready Nodes
This scenario shows how to write an environment query that returns a set of nodes that can be used in an experiment.
Nodes that are ready to be used in an experiment have the following requirements:  
* The node is healthy
* The node belongs to a cluster that supports a deployment of another tip session
* The node belongs to a cluster that supports a deployment of another VM.

#### Prerequisites
* External Region Name must be known
* Desired VM Sku must be known

[See Scenario 3](./Scenario-ExperimentNodes.md)

### Scenario 4: Experiment Ready Environments
This scenario shows how to write an environment query that returns a set of nodes and a subscription that can be used
in an experiment.  
Nodes that are ready to be used in an experiment have the same requirements outlined in Scenario 3.  
A subscription is experiment ready if it can support the deployment of the desired count of VMs.  

#### Prerequisites
* Desired VM Sku must be known.

[See Scenario 4](./Scenario-ExperimentEnvironments.md)

### Scenario 5: Experiment Ready Environments With Attributes
This scenario shows how to write an environment query that returns a set of nodes and a subscription that can be used
in an experiment, and the nodes must have a certain attribute(BIOS, OS, CPUID, etc...).   
Nodes that are ready to be used in an experiment have the same requirements outlined in Scenario 3.  
A subscription is experiment ready if it can support the deployment of the desired count of VMs. 

#### Prerequisites
* Desired VM Sku must be known.
* Desired attribute must be known. (CPUID is used explicitly in scenario.) 

[See Scenario 5](./Scenario-SpecificExperimentEnvironment.md)

## Advanced Scenarios
Advanced scenarios should only be referred to if the author has prior experience with authoring environment queries. Advanced Scenarios do not 
follow a progressive composition like the Basic scenarios. Advanced scenarios are defined as:
* Scenarios that fit a use case that breaks conventional use case patterns. 
* Scenarios that may offer insight into designing more complex environment queries.

### Scenario 6: Targeted Cluster Environment 
This scenario should be used when the nodes that should be returned have to belong to a known set of clusters. (The definition of known
in this context is specific Cluster Names are known).  
This scenario returns a subscription, a node and and eligible vm sku.

#### Prerequisites
* A Desired Vm Sku must be known.
* A set of clusters must be known.

[See Scenario 6](./Scenario-KnownClusterEnvironments.md)

### Scenario 7: Limited Scale Experiments
This scenario should be used when the number of nodes expected to be retrieved from ESS does not come close to the amount of quota
available for the desired vm sku to be launched on the environments. For example if there is to be 10 nodes utilized with 10 vms deployed 
on each node, such that each vm is comprised of 10 cores, the total quota that would be cosumed in this case is: 1000 cores. If the total
number of cores allocated for the quota (aggregate across all regions) is: 10,000 cores, then this use case fits the definiton of a low 
scale experiment. 

### Prerequisites
* Use case fits definition of "Low Scale"
* A desired vm sku must be known.
* Regions to target must be known.

[See Scenario 7](./Scenario-LowScaleEnvironments.md)
