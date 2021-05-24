# Juno steps execution workflows
The steps execution workflows on EOS, host agent and guest agents are verify similar with few differences


#### Execution workflow on EOS
The execution of experiment workflow steps in Juno happens essentially in a loop. Juno experiment providers that handle the runtime details of
step execution are designed to be short-running as a general rule and to be re-entrant/idempotent. This design mechanic enables any step to 
be executed and re-executed any number of times until it can be confirmed to be completed. This design also enables the execution engine to
scale well with minimal resources.

1.  Get the experiment notification from the azure queue
2.  Get all steps for the experiment
3.  Check if any step is failed, if yes mark the experiment as **Failed** and exit
4.  Check if any step is cancelled, if yes mark the experiment as **Cancelled** and exit
5.  Check if all steps are succeeded, if yes mark the experiment as **Succeeded** and exit
6.  Select steps to execute: 
  * Select steps whose status is **InProgress** or **InProgressContinue**, and add them to selected steps collection.
  * Check if selected steps contains any step with Status=InProgress, if no add the next **Pending** step to collection based on sequence ranking.
7.  Execute all the selected states in parallel and update the status of each steps
8.  Update the experiment status based on the status of executed steps
9.  Update the notice on the Azure Queue if experiment is not completed so that one of the instances of the Execution Service will pick it up for the next round of processing.

#### Execution workflow on Host/Guest agent

1.  Get all steps for agents with status: **InProgress**, **InProgressContinue** or **Pending**
2.  Filter the steps to execute: 
  * Select steps whose status is  **InProgress** or **InProgressContinue**, and add them to selected steps collection.
  * Check if selected steps contains any step with status=**InProgress**, if no add the next **Pending** step to collection based on sequence ranking.
3.  Execute all the selected states in parallel and update the status of each steps


