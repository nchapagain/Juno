# Timer Trigger Precondition Provider

#### Timer Trigger Precondition Provider
The Timer Trigger Precondition provider evaluates the Cron expression that is located in the cosmos
schedule table for the target goal. 

This allows to have target goals run at different frequencies, and allow the ability to increase or decreaese the
frequency a target goal is ran.

#### Scope
- Data Availability Delay: N/A
- Dependencies: NCronTab
- Target Goal States:
    - Should be executed
    - Should not be executed

#### Example Scheduler Definition
``` json
    {
        "type": "Juno.Scheduler.Preconditions.TimerTriggerProvider",
        "parameters": {
            "cronExpression": "* * * * *"
        }
    }
```

##### Parameters
The Timer Trigger Provider only requires one Parameter: `cronExpression` this key should be assigned a string with
the format of a Cron Expression. A Cron Expression is a way to autmotate the frequency of a trigger (think execute every second, 
every minute, every 5 hours etc...). To find out more about cron expressions visit [CronTab Guru](https://crontab.guru/) for more 
information regarding cron expressions.