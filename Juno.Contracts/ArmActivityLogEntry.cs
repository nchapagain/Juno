namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The REST Call to the Activity Log has the potential to return hundreds of log entries for a single resource group. This results
    /// in a need for pagination to get the remaining logs. This class captures the Event Data Collection (https://docs.microsoft.com/en-us/rest/api/monitor/activitylogs/list#eventdata)
    /// as well as the specified filters for the eventData objects;
    /// </summary>
    public class ArmActivityLogEntry
    {
        /// <summary>
        /// If another page is available this will allow us to store the link and fetch the logs recursively.
        /// </summary>
        public string NextLink { get; set; }

        /// <summary>
        /// The actual error logs within the Activity Log
        /// </summary>
        public IEnumerable<EventLogDataValues> Value { get; set; }
    }

    /// <summary>
    /// The EventLogDataValues (https://docs.microsoft.com/en-us/rest/api/monitor/activitylogs/list#eventdata) for the activity log. In an event to try
    /// and capture as much relevant data as possible this applies the filter to capture information for the following fields:
    /// correlationId, level, resourceGroupName, resourceType, operationName, properties, status
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "JsonObjectForSerialization")]
    public class EventLogDataValues
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventLogDataValues"/> class.
        /// </summary>
        /// <param name="eventTimestamp">The eventTimestamp from the activity log.</param>
        /// <param name="correlationId">The Correlation Id, a GUID in the string format.</param>
        /// <param name="level">The severity level of the event.</param>
        /// <param name="resourceGroupName">The resource group name</param>
        /// <param name="resourceType">The resource type</param>
        /// <param name="operationName">The operationname</param>
        /// <param name="properties">The properties including the status and error message</param>
        /// <param name="status">The status of the operation</param>
        public EventLogDataValues(DateTime eventTimestamp, string level, string correlationId, string resourceGroupName, JObject resourceType, JObject operationName, JObject properties, JObject status)
        {
            this.EventTimestamp = eventTimestamp;
            this.CorrelationId = correlationId;
            this.Level = level;
            this.ResourceGroupName = resourceGroupName;
            this.ResourceType = resourceType;
            this.OperationName = operationName;
            this.Properties = properties;
            this.Status = status;
        }

        /// <summary>
        /// The event timestamp for when the activity logged occured.
        /// </summary>
        public DateTime EventTimestamp { get; set; }

        /// <summary>
        /// The Correlation Id, usually a GUID in the string format. The correlation Id is shared among the events that belong to the same operation.
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// The severity level of the event. This consists of the 5 categories below in ascending severity.
        /// Warning, Verbose, Informational, Error, Critical
        /// https://docs.microsoft.com/en-us/rest/api/monitor/activitylogs/list#eventlevel
        /// The diagnostic provider relies on the records that are in the Error/Critical severity level.
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// the resource group name of the impacted resource.
        /// </summary>
        public string ResourceGroupName { get; set; }

        /// <summary>
        /// The resource type in localizable string format
        /// This collection contains the following items:          "resourceType": {
        ///    value, string, the invariant value                      ["value": "Microsoft.Compute/virtualMachines/extensions"],
        ///    localizedValue, string, the locale spcific value        ["localizedValue": "Microsoft.Compute/virtualMachines/extensions"]
        /// </summary>
        public JObject ResourceType { get; set; }

        /// <summary>
        /// The operationName in localizable string format
        /// This collection contains the following items:          "operationName": {
        ///    value, string, the invariant value                      ["value": "Microsoft.Compute/virtualMachines/extensions"],
        ///    localizedValue, string, the locale spcific value        ["localizedValue": "Microsoft.Compute/virtualMachines/extensions"]
        /// </summary>
        public JObject OperationName { get; set; }

        /// <summary>
        /// The set of Key Value pairs that includes details about the event.
        /// Including the following items:
        ///  "statusMessage":
        ///     "status":"Failed"
        ///     "error":
        ///         "code":"ResourceOperationFailure"
        ///         "message":"The resource operation completed with terminal provisioning state 'Failed'."
        ///         "details":
        ///             "code":"OSProvisioningTimedOut",
        ///             "message":"OS Provisioning for VM did not finish in the allotted time"}]}}",
        ///   "eventCategory": "Administrative",
        ///   "entity": "/subscriptions/.../extensions/Microsoft.Azure.Security.AntimalwareSignature.AntimalwareConfiguration",
        ///   "message": "Microsoft.Compute/virtualMachines/extensions/write",
        ///   "hierarchy":
        /// </summary>
        public JObject Properties { get; set; }

        /// <summary>
        /// a string describing the status of the operation. Some typical values are: Started, In progress, Succeeded, Failed, Resolved.
        /// "status": {
        ///    ["value": "Microsoft.Compute/virtualMachines/extensions"] - value, string, the invariant value
        ///    ["localizedValue": "Microsoft.Compute/virtualMachines/extensions"] - localizedValue, string, the locale spcific value
        /// </summary>
        public JObject Status { get; set; }
    }
}