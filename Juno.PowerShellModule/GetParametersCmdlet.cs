namespace Juno.PowerShellModule
{
    using System;
    using System.Management.Automation;
    using System.Net.Http;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Polly;

    /// <summary>
    /// Powershell Module to get the parameters required to be assigned a value to in order to create an Execution Goal
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Parameters")]
    public class GetParametersCmdlet : ExperimentCmdletBase
    {
        /// <summary>
        /// Constructor for GetParametersCmdlet class
        /// </summary>
        public GetParametersCmdlet()
            : this(null)
        {
        }

        /// <summary>
        /// Constructor for GetParametersCmdlet class that sets the retry policy
        /// </summary>
        public GetParametersCmdlet(IAsyncPolicy asyncPolicy = null)
            : base(asyncPolicy)
        {
        }

        /// <summary>
        /// <para type="description">
        /// Execution Goal Template Id
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [Alias("Id")]
        public string TemplateId { get; set; }

        /// <summary>
        /// <para type="description">
        /// Team Name that owns the template identified by the TemplateId
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [Alias("Team")]
        public string TeamName { get; set; }

        /// <summary>
        /// Pulls out the parameters from an execution goal schedule object.
        /// </summary>
        protected virtual dynamic GetParametersFromGoalBasedSchedule(Item<GoalBasedSchedule> goalBasedScheduleItem)
        {
            goalBasedScheduleItem.ThrowIfNull(nameof(goalBasedScheduleItem));

            ExecutionGoalParameter parameters = GoalBasedScheduleExtensions.GetParametersFromTemplate(goalBasedScheduleItem.Definition);

            return parameters;
        }

        /// <summary>
        /// Validates the parameters passed into the commandlet.
        /// </summary>
        protected override bool ValidateParameters()
        {
            base.ValidateParameters();
            if (string.IsNullOrEmpty(this.TemplateId) || string.IsNullOrEmpty(this.TeamName))
            {
                throw new ArgumentException($"Invalid arguments passed to module. Required parameters are {nameof(this.TeamName).ToString()} and {nameof(this.TemplateId).ToString()}.");
            }

            return true;
        }

        /// <summary>
        /// Executes the operation to get the Experiment parameters.
        /// </summary>
        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            this.ValidateParameters();

            HttpResponseMessage response = this.GetExecutionGoalTemplateListAsync(this.TeamName, this.TemplateId, View.Full).GetAwaiter().GetResult();
            response.ThrowOnError<ExperimentException>();

            var responseContent = this.GetParametersFromGoalBasedSchedule(response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>().GetAwaiter().GetResult());

            this.WriteResultsAsJson(responseContent);
        }
    }
}
