<div style="font-size:24pt;font-weight:600;color:#1569C7">Authoring Juno Experiment Steps</div>
<br/>

## DebugAgentProvider
The following documentation illustrates how to define a Juno workflow step that allows a user to define an agent ID explicitly and is used to enable debugging agent experiment steps
on the local machine (inner development loop). Once agent steps are created using an explicit agent ID defined by the user, the Juno Host Agent can be supplied that agent ID on the
command line. This will enable the Juno Host Agent to pickup experiment workflow steps from the system for that agent ID.

### Preliminaries
It is important to understand the basic schema of an experiment beforehand.

[Authoring Experiments](./Authoring-Experiments.md)  
[Juno Experiment Schema](./Authoring-ExperimentSchema.md)

### Dependencies

* Agent IDs must follow a prescriptive format
  * To debug the Juno Host Agent, the agent ID must follow a "&lt;clusterName&gt;,&lt;nodeName&gt;" format (e.g. cluster01,mycomputername)
  * To debug the Juno Guest Agent, the agent ID must follow a "&lt;clusterName&gt;,&lt;nodeName&gt;,&lt;vmName&gt;" format (e.g. cluster01,mycomputername,anyVM01).

### Step Authoring
This section describes how to author an experiment workflow step to instruct Juno to create agent steps using an explicit agent ID provided by the user.

##### Type
The 'type' must be ```Juno.Execution.Providers.Demo.DebugAgentProvider```

##### Name and description
The 'name' and 'description' properties are used to describe steps in the experiment runtime workflow.  The values can be set to anything the experiment
author wishes but should generally be accurate descriptions of the purpose of the step.

##### Group
The 'group' defines the experiment group for which the runtime workflow step is associated (e.g. Group A, Group B).

##### Parameters
The following parameters will be used creating experiment step.

| Name                 | Required   | Data Type         | Description                |
| -------------------- | ---------- | ----------------- | -------------------------- |
| useAgentId           | Yes        | string/path       | Defines the explicit agent ID to use for any agent experiment steps that will be created for the experiment group. The agent ID can be anything for the most part so long as it follows the format as noted in the dependencies above.

<div style="color:green">
<div style="font-weight:600">IMPORTANT</div>
When debugging Juno agents locally, there are a few things that must be ensured. The environment must be provided (e.g. juno-dev01) and background monitoring tasks that
are typically running in live experiment scenarios must be disabled. Additionally, it is common that the user use a different work/notice queue than the main queue so that
it experiments used for debugging do not interfere with the normal operations of the Dev or Prod environments. Each of these can be defined on the command-line to the agents.
<br/><br/>
In Visual Studio, you can provide these command-line parameters in the project 'Properties -> Debug -> Application Arguments' section.
</div>
<br/>

##### Example Definitions
``` json
{
    "type": "Juno.Execution.Providers.Demo.DebugAgentProvider",
    "name": "Debu Juno Host Agent",
    "description": "Enables the user to debug the Juno Host Agent on their local system for steps associated with Group A.",
    "group": "Group A",
    "parameters": {
        "useAgentId": "anyCluster,computer01"
    }
}

// Then to setup the Juno Host Agent on the local machine, pass in the following command-line parameters:
// (note: the environment must be defined, the monitors must be disabled and the work queue must be defined)
// --environment juno-dev01 --agentId anyCluster,computer01 --workQueue experimentnotices-brdeyo --disableMonitors

{
    "type": "Juno.Execution.Providers.Demo.DebugAgentProvider",
    "name": "Debu Juno Guest Agent",
    "description": "Enables the user to debug the Juno Guest Agent on their local system for steps associated with Group A.",
    "group": "Group A",
    "parameters": {
        "useAgentId": "anyCluster,computer01,anyVM"
    }
}

// Then to setup the Juno Guest Agent on the local machine, pass in the following command-line parameters:
// (note: the environment must be defined, the monitors must be disabled and the work queue must be defined)
// --environment juno-dev01 --agentId anyCluster,computer01,anyVM --workQueue experimentnotices-brdeyo --disableMonitors
```