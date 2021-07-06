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
    /// Powershell Module to get Execution Goals  
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "ExecutionGoals")]
    public class GetExecutionGoalsCmdlet : ExperimentCmdletBase
    {
        /// <summary>
        /// Constructor for GetExecutionGoalCmdlet class
        /// </summary>
        public GetExecutionGoalsCmdlet()
            : this(null)
        {
        }

        /// <summary>
        /// Constructor for GetExecutionGoalCmdlet class that sets the retry policy
        /// </summary>
        public GetExecutionGoalsCmdlet(IAsyncPolicy asyncPolicy = null)
            : base(asyncPolicy)
        {
        }

        /// <summary>
        /// <para type="description">
        /// Execution Goal Id
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [Alias("Id")]
        public string ExecutionGoalId { get; set; }

        /// <summary>
        /// <para type="description">
        /// Team Name
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
            if (string.IsNullOrEmpty(this.ExecutionGoalId))
            {
                // Default: Returns a list of [id, TeamName, ExperimentName] for all Execution Goals available
                HttpResponseMessage response = this.GetExecutionGoalAsync(this.ExecutionGoalId, this.TeamName).GetAwaiter().GetResult();
                response.ThrowOnError<ExperimentException>();

                IEnumerable<Item<GoalBasedSchedule>> executionGoal = response.Content.ReadAsJsonAsync<IEnumerable<Item<GoalBasedSchedule>>>().GetAwaiter().GetResult();
                returnValue = executionGoal.Select(i => new { i.Id, i.Definition.TeamName }).ToList();
            }

            // A specific Template Id and Team Name is provided 
            else
            {
                // Only a summary view is expected
                if (this.ViewType == Contracts.View.Summary)
                {
                    // Returns [id, TeamName, ExperimentName] for the particular Execution Goal Template
                    HttpResponseMessage response = this.GetExecutionGoalAsync(this.ExecutionGoalId, this.TeamName).GetAwaiter().GetResult();
                    response.ThrowOnError<ExperimentException>();

                    Item<GoalBasedSchedule> executionGoal = response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>().GetAwaiter().GetResult();
                    returnValue = new { Id = executionGoal.Id, TeamName = executionGoal.Definition.TeamName };
                }

                // Full View is expected for the given templateId and Team Name
                else
                {
                    // Returns the full JSON for the particular Execution Goal
                    HttpResponseMessage response = this.GetExecutionGoalAsync(this.ExecutionGoalId, this.TeamName).GetAwaiter().GetResult();
                    response.ThrowOnError<ExperimentException>();

                    Item<GoalBasedSchedule> executionGoal = response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>().GetAwaiter().GetResult();
                    this.RemoveServerSideDataTags(executionGoal);
                    returnValue = executionGoal;
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
            if ((string.IsNullOrEmpty(this.ExecutionGoalId) ^ string.IsNullOrEmpty(this.TeamName)) == true)
            {
                throw new ArgumentException($"Invalid arguments passed to module. The parameters {nameof(this.TeamName).ToString()} and {nameof(this.ExecutionGoalId).ToString()} must be used together.");
            }

            return true;
        }
    }
}
