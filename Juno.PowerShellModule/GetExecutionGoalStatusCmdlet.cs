namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Rest;
    using Polly;

    /// <summary>
    /// Powershell Module to get Execution Goal Status
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "ExecutionGoalStatus")]
    public class GetExecutionGoalStatusCmdlet : ExperimentCmdletBase
    {
        /// <summary>
        /// Constructor for GetExecutionGoalStatusCmdlet class
        /// </summary>
        public GetExecutionGoalStatusCmdlet()
        {
        }

        /// <summary>
        /// Constructor for GetExecutionGoalStatusCmdlet class that sets the retry policy
        /// </summary>
        public GetExecutionGoalStatusCmdlet(IAsyncPolicy asyncPolicy = null)
            : base(asyncPolicy)
        {
        }

        /// <summary>
        /// <para type="description">
        /// Execution Goal Id
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [Alias("Id")]
        public string ExecutionGoalId { get; set; }

        /// <summary>
        /// <para type="description">
        /// Team Name
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [Alias("Team")]
        public string TeamName { get; set; }

        /// <summary>
        /// Executes the operation to get Experiment Status.
        /// </summary>
        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            this.ValidateParameters();
            HttpResponseMessage response = this.GetExperimentStatusAsync().GetAwaiter().GetResult();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var output = response.Content.ReadAsJsonAsync<IEnumerable<ExperimentInstanceStatus>>().GetAwaiter().GetResult();

                if (this.AsJson.IsPresent)
                {
                    this.WriteResultsAsJson(output);
                }
                else
                {
                    this.WriteResults(output);
                }
            }
            else
            {
                this.WriteObject(string.Format("Experiment does not have any status reported. Please contact CRC AIR team for support."));
            }
        }

        /// <summary>
        /// Validates the parameters passed into the commandlet.
        /// </summary>
        protected override bool ValidateParameters()
        {
            base.ValidateParameters();
            bool isValid = true;
            if (string.IsNullOrEmpty(this.ExecutionGoalId) ||
                (string.IsNullOrEmpty(this.TeamName)))
            {
                throw new ArgumentException($"Invalid arguments passed to module. Required arguments are {nameof(this.TeamName).ToString()} and {nameof(this.ExecutionGoalId).ToString()}.");
            }

            return isValid;
        }

        private async Task<HttpResponseMessage> GetExperimentStatusAsync()
        {
            using (RestClientBuilder rcBuilder = new RestClientBuilder())
            {
                using (CancellationTokenSource source = new CancellationTokenSource())
                {
                    HttpResponseMessage responseMessage = null;
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        responseMessage = await this.ExperimentsClient.GetExecutionGoalsAsync(source.Token, this.TeamName, this.ExecutionGoalId, ExecutionGoalView.Status).ConfigureAwait(false);
                    }).ConfigureAwait(false);

                    return responseMessage;
                }
            }
        }
    }
}
