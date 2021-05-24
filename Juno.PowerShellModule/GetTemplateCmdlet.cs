namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Net.Http;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Rest;
    using Polly;

    /// <summary>
    /// Powershell Module to get Execution Goal Templates 
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Templates")]
    public class GetTemplateCmdlet : ExperimentCmdletBase
    {
        /// <summary>
        /// Constructor for GetTemplateCmdlet class
        /// </summary>
        public GetTemplateCmdlet()
            : this(null)
        {
        }

        /// <summary>
        /// Constructor for GetTemplateCmdlet class that sets the retry policy
        /// </summary>
        public GetTemplateCmdlet(IAsyncPolicy asyncPolicy = null)
            : base(asyncPolicy)
        {
        }

        /// <summary>
        /// <para type="description">
        /// Execution Goal Template Id
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [Alias("Id")]
        public string TemplateId { get; set; }

        /// <summary>
        /// <para type="description">
        /// Team Name that owns the template identified by the TemplateId
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [Alias("Team")]
        public string TeamName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Verbosity Level of the response - Summary or Full (Default)
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [Alias("View")]
        public View ViewType { get; set; } = Contracts.View.Full;

        /// <summary>
        /// Executes the operation to get an execution goal.
        /// </summary>
        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            this.ValidateParameters();
            var returnValue = new object();

            // No specific Template Id and Team Name is provided
            if (string.IsNullOrEmpty(this.TemplateId))
            {
                // Default: Returns a list of [id, TeamName, Intent] for all Execution Goal Templates available
                HttpResponseMessage response = this.GetExecutionGoalTemplateListAsync(this.TeamName, this.TemplateId, View.Summary).GetAwaiter().GetResult();
                response.ThrowOnError<ExperimentException>();

                IEnumerable<ExecutionGoalSummary> templatesSummary = response.Content.ReadAsJsonAsync<IEnumerable<ExecutionGoalSummary>>().GetAwaiter().GetResult();
                returnValue = templatesSummary.Select(i => new { i.Id, i.TeamName, Intent = i.Metadata.ContainsKey("intent") ? i.Metadata["intent"] : string.Empty }).ToList();
            }

            // A specific Template Id and Team Name is provided 
            else
            {
                // Only a summary view is expected
                if (this.ViewType == Contracts.View.Summary)
                {
                    // Returns [id, TeamName, Intent] for the particular Execution Goal Template
                    HttpResponseMessage response = this.GetExecutionGoalTemplateListAsync(this.TeamName, this.TemplateId, View.Summary).GetAwaiter().GetResult();
                    response.ThrowOnError<ExperimentException>();

                    ExecutionGoalSummary templateSummary = response.Content.ReadAsJsonAsync<ExecutionGoalSummary>().GetAwaiter().GetResult();
                    returnValue = new { Id = templateSummary.Id, TeamName = templateSummary.TeamName, Intent = templateSummary.Metadata.ContainsKey("intent") ? templateSummary.Metadata["intent"] : string.Empty };
                }

                // Full View is expected for the given templateId and Team Name
                else
                {
                    // Returns the full JSON for the particular Execution Goal Template
                    HttpResponseMessage response = this.GetExecutionGoalTemplateListAsync(this.TeamName, this.TemplateId, View.Full).GetAwaiter().GetResult();
                    response.ThrowOnError<ExperimentException>();

                    returnValue = response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>().GetAwaiter().GetResult();
                }
            }

            if (this.AsJson.IsPresent)
            {
                this.WriteResultsAsJson(returnValue);
            }
            else
            {
                this.WriteResults(returnValue);
            }
        }

        /// <summary>
        /// Validates the parameters passed into the commandlet.
        /// </summary>
        protected override bool ValidateParameters()
        {
            base.ValidateParameters();
            if ((string.IsNullOrEmpty(this.TemplateId) ^ string.IsNullOrEmpty(this.TeamName)) == true)
            {
                throw new ArgumentException($"Invalid arguments passed to module. The parameters {nameof(this.TeamName).ToString()} and {nameof(this.TemplateId).ToString()} must be used together.");
            }

            return true;
        }
    }
}
