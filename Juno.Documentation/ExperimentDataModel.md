# Juno Experiment Data Model
The following documentation describes the details for how the Juno system represents experiment data. The Juno
system uses various Azure resources to maintain the data structures/objects and those will be covered in this
document.

## Juno Experiment Instances
Juno experiment instances are stored in an Azure Cosmos DB. Each experiment, when requested, will be given a unique ID and the JSON-formatted representation of
the experiment instance will be stored in Cosmos DB.

#### Schema Definitions
Reference [Experiment Schema](./ExperimentSchema.md) for a detailed description of the experiment schema itself.

#### Example Structure

``` json
{
    "id": "082bdb10-15e5-45ac-8523-c13e1262b5ae",
    "created": "2019-11-20T01:29:43.0025002Z",
    "lastModified": "2019-11-20T01:29:43.0025002Z",
    "definition": {
        "$schema": "http://juno-prod01.westus2/workflows/v1.0/schema",
        "contentVersion": "1.0.0",
        "name": "AB Experiment",
        "description": "A standard AB experiment",
        "metadata": {
            ...
        },
        "parameters": {
            ...
        },
        "workflow": [
            ...
             ...
        ]
    },
    "_partition": "082b",
    "_rid": "gVoyAKs6+HwBAAAAAAAAAA==",
    "_self": "dbs/gVoyAA==/colls/gVoyAKs6+Hw=/docs/gVoyAKs6+HwBAAAAAAAAAA==/",
    "_etag": "\"4f02b344-0000-0800-0000-5dd88b8e0000\"",
    "_attachments": "attachments/",
    "_ts": 1574472590
}
```

## Juno Experiment Orchestration Steps
Juno experiment environment and workflow steps are stored in an Azure Cosmos DB Table. Environment steps describe individual components of an end-to-end experiment
flow that are responsible for selecting, provisioning and cleaning up environments in which experiments run. Workflow steps describe the individual components of
an end-to-end experiment flow that are responsible for running workloads and applying payloads on/into environments provisioned for the experiment.

<div style="color:#1569C7">
<div style="font-weight:600">Note on the Sequence of Execution:</div>
Steps may be processed one at a time or bulk. Certain steps may be processed together as is necessary by the orchestration services.
</div>
<br/>

The following tables define the properties that are stored in this table as well as examples of the data structure.

#### Schema Definitions

| Property                       | Description                           |
| ------------------------------ | ------------------------------------- |
| PartitionKey                   | The partition key for the experiment step data which groups steps associated with a specific experiment together. The experiment ID itself is used as the partition key for all experiment steps.
| RowKey                         | The row key/unique identity for each individual experiment step. The unique ID of the individual step is used as the row key.
| Timestamp(Created)             | The date/time (UTC) at which the experiment step was created.
| Attempts                       | The number of attempts the Juno system has made to successfully complete the experiment step.
| Definition                     | A JSON-formatted instance of the experiment component which defines the details and execution requirements of the step.
| EndTime                        | The date/time (UTC) at which the experiment step execution ended. This is initially set to '1/1/1900 00:00:00 AM' meaning the step has not completed.
| ExperimentId                   | The unique ID of the experiment with which the step is associated.
| Group                          | The experiment group for which the experiment step is associated.
| LastModified                   | The date/time (UTC) at which the experiment step data was last modified/updated.
| Name                           | The name of the experiment step (as defined in the component definition itself).
| ParentStepId                   | The unique ID of the parent step. The parent step is typically an orchestration step. A step having a parent step is a "monitored" step which means that it is typically a long-running step that is being watched by the parent step.
| Sequence                       | The ordinal sequence at which the experiment step should be executed.
| StartTime                      | The date/time (UTC) at which the experiment step execution started. This is initially set to '1/1/1900 00:00:00 AM' meaning the step has not started.
| Status                         | The current/latest status of the step. Valid values include: <ul><li>Pending</li><li>InProcess</li><li>InProcessWaiting</li><li>Succeeded</li><li>Failed</li></ul>
| StepType                       | The type of experiment step (or the type of provider that handles the step).  Valid values include: <ul><li>EnvironmentCleanup</li><li>EnvironmentCriteria</li><li>EnvironmentSetup</li><li>Payload</li><li>Workload</li>

#### Examples
The following example shows the structure of the orchestration and agent tables for an experiment. This experiment uses shared criteria for selecting the environment.
The partition key for the table is the ID of the experiment.

##### Example 1: Orchestration Steps Table
High-level steps for the experiment that determine the order of things to do when executing the experiment.

| PartitionKey  | RowKey        | Timestamp              | Attempts | Definition                      | EndTime                | ExperimentId | Group     | LastModified           | Name                        | Sequence | StartTime              | Status     | StepType            |
| ------------- | ------------- | ---------------------- | -------  | ------------------------------- | ---------------------- | ------------ | --------- | ---------------------- | --------------------------- | -------- |----------------------- | ---------- | ------------------- |
| 082bdb10...   | 0743623d...   | 11/20/2019 02:25:00 PM | 1        | { "type": "Juno.Providers..." } | 11/20/2019 02:26:00 PM | 082bdb10...  | *         | 11/20/2019 02:26:00 PM | Select Cluster by TiP       | 100      | 11/20/2019 02:25:00 PM | Succeeded  | EnvironmentCriteria |
| 082bdb10...   | f08740c3...   | 11/20/2019 02:25:00 PM | 1        | { "type": "Juno.Providers..." } | 11/20/2019 02:27:00 PM | 082bdb10...  | *         | 11/20/2019 02:27:00 PM | Select Cluster by VM SKU    | 200      | 11/20/2019 02:26:00 PM | Succeeded  | EnvironmentCriteria |
| 082bdb10...   | 6b882c6d...   | 11/20/2019 02:25:00 PM | 1        | { "type": "Juno.Providers..." } | 11/20/2019 02:30:00 PM | 082bdb10...  | Group A   | 11/20/2019 02:30:00 PM | Acquire TiP Session/Node    | 300      | 11/20/2019 02:27:00 PM | Succeeded  | EnvironmentSetup    |
| 082bdb10...   | c39c422a...   | 11/20/2019 02:25:00 PM | 1        | { "type": "Juno.Providers..." } | 11/20/2019 02:32:00 PM | 082bdb10...  | Group B   | 11/20/2019 02:32:00 PM | Acquire TiP Session/Node    | 400      | 11/20/2019 02:30:00 PM | Succeeded  | EnvironmentSetup    |
| 082bdb10...   | 6a92afc8...   | 11/20/2019 02:25:00 PM | 1        | { "type": "Juno.Providers..." } | 11/20/2019 02:35:00 PM | 082bdb10...  | Group A   | 11/20/2019 02:35:00 PM | Create VMs                  | 500      | 11/20/2019 02:32:00 PM | Succeeded  | EnvironmentSetup    |
| 082bdb10...   | 1adcec6b...   | 11/20/2019 02:25:00 PM | 1        | { "type": "Juno.Providers..." } | 11/20/2019 02:36:00 PM | 082bdb10...  | Group B   | 11/20/2019 02:36:00 PM | Create VMs                  | 600      | 11/20/2019 02:35:00 PM | Succeeded  | EnvironmentSetup    |
| 082bdb10...   | f92332b7...   | 11/20/2019 02:25:00 PM | 1        | { "type": "Juno.Providers..." } | 11/20/2019 02:38:00 PM | 082bdb10...  | Group A   | 11/20/2019 02:38:00 PM | Install Agents              | 700      | 11/20/2019 02:36:00 PM | Succeeded  | EnvironmentSetup    |
| 082bdb10...   | 9981f78a...   | 11/20/2019 02:25:00 PM | 1        | { "type": "Juno.Providers..." } | 11/20/2019 02:39:00 PM | 082bdb10...  | Group B   | 11/20/2019 02:39:00 PM | Install Agents              | 800      | 11/20/2019 02:38:00 PM | Succeeded  | EnvironmentSetup    |
| 082bdb10...   | 886bc83c...   | 11/20/2019 02:25:00 PM | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM   | 082bdb10...  | Group A   | 11/20/2019 02:39:00 PM | Run Workload (steady state) | 900      | 11/20/2019 02:39:00 PM | InProgress | Workload            |
| 082bdb10...   | 39f3040d...   | 11/20/2019 02:25:00 PM | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM   | 082bdb10...  | Group B   | 11/20/2019 02:39:00 PM | Run Workload (steady state) | 1000     | 11/20/2019 02:39:00 PM | InProgress | Workload            |
| 082bdb10...   | ecdaf0c9...   | 11/20/2019 02:25:00 PM | 0        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM   | 082bdb10...  | *         | 11/20/2019 02:25:00 PM | Wait for 2 Hours            | 1100     | 1/1/1900 00:00:00 AM   | InProgress | Workload            |
| 082bdb10...   | c8327b86...   | 11/20/2019 02:25:00 PM | 0        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM   | 082bdb10...  | Group B   | 11/20/2019 02:25:52 PM | Apply Payload               | 1200     | 1/1/1900 00:00:00 AM   | Pending    | Payload             |
| 082bdb10...   | 1fc2eff6...   | 11/20/2019 02:25:00 PM | 0        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM   | 082bdb10...  | Group A   | 11/20/2019 02:25:52 PM | Run Workload                | 1300     | 1/1/1900 00:00:00 AM   | Pending    | Workload            |
| 082bdb10...   | 6d8c0e55...   | 11/20/2019 02:25:00 PM | 0        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM   | 082bdb10...  | Group B   | 11/20/2019 02:25:52 PM | Run Workload                | 1400     | 1/1/1900 00:00:00 AM   | Pending    | Workload            |
| 082bdb10...   | 24fd6402...   | 11/20/2019 02:25:00 PM | 0        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM   | 082bdb10...  | Group A   | 11/20/2019 02:25:52 PM | Cleanup TiP Session/Node    | 1600     | 1/1/1900 00:00:00 AM   | Pending    | EnvironmentCleanup  |
| 082bdb10...   | 0dbb9841...   | 11/20/2019 02:25:00 PM | 0        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM   | 082bdb10...  | Group B   | 11/20/2019 02:25:52 PM | Cleanup TiP Session/Node    | 1700     | 1/1/1900 00:00:00 AM   | Pending    | EnvironmentCleanup  |

##### Example 1: Agent Steps Table
Execution steps for individual agents within the environment. The partition key is the ID of the node on which the agent runs.

| PartitionKey | RowKey       | Timestamp              | AgentId               | Attempts | Definition                      | EndTime              | ExperimentId | Group     | LastModified           | Name                                 | Sequence | StartTime            | Status     | StepType            |
| ------------ | ------------ | ---------------------- | --------------------- | -------  | ------------------------------- | -------------------- | -----------  | --------- | ---------------------- | ------------------------------------ | -------- | -------------------- | ---------- | ------------------- |
| Node01       | 0743623d...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM01 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:32:55 PM | Run Workload on Agent (steady state) | 100      | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node01       | 6a92afc8...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM02 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:33:33 PM | Run Workload on Agent (steady state) | 200      | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node01       | 3b1330ad...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM03 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:34:22 PM | Run Workload on Agent (steady state) | 300      | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node01       | 7f6de995...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM04 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:35:16 PM | Run Workload on Agent (steady state) | 400      | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node01       | 0dbb9841...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM05 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:36:58 PM | Run Workload on Agent (steady state) | 500      | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node02       | c3e4c4bb...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM01 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:32:55 PM | Run Workload on Agent (steady state) | 600      | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node02       | c5ca7bca...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM02 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:33:33 PM | Run Workload on Agent (steady state) | 700      | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node02       | 01403c6f...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM03 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:34:22 PM | Run Workload on Agent (steady state) | 800      | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node02       | dd6c883c...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM04 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:35:16 PM | Run Workload on Agent (steady state) | 900      | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node02       | 39cb8458...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM05 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:36:58 PM | Run Workload on Agent (steady state) | 1000     | 1/1/1900 00:00:00 AM | InProgress | Workload            |

*(...then after the 2 hour wait...)*

| PartitionKey | RowKey       | Timestamp              | AgentId               | Attempts | Definition                      | EndTime              | ExperimentId | Group     | LastModified           | Name                                 | Sequence | StartTime            | Status     | StepType            |
| ------------ | ------------ | ---------------------- | --------------------- | -------  | ------------------------------- | -------------------- | -----------  | --------- | ---------------------- | ------------------------------------ | -------- | -------------------- | ---------- | ------------------- |
| Node01       | 0743623d...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM01 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:32:55 PM | Run Workload on Agent (steady state) | 100      | 1/1/1900 00:00:00 AM | Succeeded  | Workload            |
| Node01       | 6a92afc8...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM02 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:33:33 PM | Run Workload on Agent (steady state) | 200      | 1/1/1900 00:00:00 AM | Succeeded  | Workload            |
| Node01       | 3b1330ad...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM03 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:34:22 PM | Run Workload on Agent (steady state) | 300      | 1/1/1900 00:00:00 AM | Succeeded  | Workload            |
| Node01       | 7f6de995...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM04 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:35:16 PM | Run Workload on Agent (steady state) | 400      | 1/1/1900 00:00:00 AM | Succeeded  | Workload            |
| Node01       | 0dbb9841...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM05 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:36:58 PM | Run Workload on Agent (steady state) | 500      | 1/1/1900 00:00:00 AM | Succeeded  | Workload            |
| Node02       | c3e4c4bb...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM01 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:32:55 PM | Run Workload on Agent (steady state) | 600      | 1/1/1900 00:00:00 AM | Succeeded  | Workload            |
| Node02       | c5ca7bca...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM02 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:33:33 PM | Run Workload on Agent (steady state) | 700      | 1/1/1900 00:00:00 AM | Succeeded  | Workload            |
| Node02       | 01403c6f...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM03 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:34:22 PM | Run Workload on Agent (steady state) | 800      | 1/1/1900 00:00:00 AM | Succeeded  | Workload            |
| Node02       | dd6c883c...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM04 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:35:16 PM | Run Workload on Agent (steady state) | 900      | 1/1/1900 00:00:00 AM | Succeeded  | Workload            |
| Node02       | 39cb8458...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM05 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:36:58 PM | Run Workload on Agent (steady state) | 1000     | 1/1/1900 00:00:00 AM | Succeeded  | Workload            |
| Node01       | ef455c40...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM01 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:36:58 PM | Run Workload on Agent                | 1200     | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node01       | 703ed3ed...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM02 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:36:58 PM | Run Workload on Agent                | 1300     | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node01       | 12168f65...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM03 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:36:58 PM | Run Workload on Agent                | 1400     | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node01       | ca575f80...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM04 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:36:58 PM | Run Workload on Agent                | 1500     | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node01       | fd736b99...  | 11/20/2019 02:25:00 PM | cluster01_node01_VM05 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group A   | 11/20/2019 03:36:58 PM | Run Workload on Agent                | 1600     | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node02       | 1a66c8cf...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM01 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:36:58 PM | Run Workload on Agent                | 1700     | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node02       | f92332b7...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM02 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:36:58 PM | Run Workload on Agent                | 1800     | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node02       | c39c422a...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM03 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:36:58 PM | Run Workload on Agent                | 1900     | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node02       | 1adcec6b...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM04 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:36:58 PM | Run Workload on Agent                | 2000     | 1/1/1900 00:00:00 AM | InProgress | Workload            |
| Node02       | 9981f78a...  | 11/20/2019 02:25:00 PM | cluster01_node02_VM05 | 1        | { "type": "Juno.Providers..." } | 1/1/1900 00:00:00 AM | 082bdb10...  | Group B   | 11/20/2019 03:36:58 PM | Run Workload on Agent                | 2100     | 1/1/1900 00:00:00 AM | InProgress | Workload            |