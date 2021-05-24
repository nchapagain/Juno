# In Progress Precondition Provider

#### In Progress Precondition Provider
The In Progress Precondition provider controls the number of experiments that can be In-Progress State. 

This allows to run not too many experiments if the target experiment threshold is not met.

#### Scope
- Data Availability Delay: N/A
- Dependencies: targetInstances
- Target Goal States:
    - Should be executed
    - Should not be executed

#### Example Scheduler Definition
``` json
    {
	"type": "Juno.Scheduler.Preconditions.InProgressExperimentsProvider",
	"parameters": {
		"targetInstances": "5"
	}
     }
```

##### Parameters
The In Progress Provider only requires one Parameter: `targetInstances` this key should be assigned a string with
the number of how many in progress experiments the user would have.