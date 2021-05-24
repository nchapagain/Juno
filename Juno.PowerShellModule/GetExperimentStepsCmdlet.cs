namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Rest;
    using Newtonsoft.Json.Linq;
    using Polly;

    /// <summary>
    /// Powershell Module to get Experiment Steps given the Experiment Id 
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "ExperimentSteps")]
    public class GetExperimentStepsCmdlet : ExperimentCmdletBase 
    {
        /// <summary>
        /// Constructor for GetExperimentStepsCmdlet class
        /// </summary>
        public GetExperimentStepsCmdlet()
            : base()
        {
            this.ViewType = View.Summary;
        }

        /// <summary>
        /// Constructor for GetExperimentStepsCmdlet class that sets the retry policy
        /// </summary>
        public GetExperimentStepsCmdlet(IAsyncPolicy asyncPolicy = null)
            : base(asyncPolicy)
        {
        }

        /// <summary>
        /// <para type="description">
        /// Experiment Id
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [Alias("Id")]
        public string ExperimentId { get; set; }

        /// <summary>
        /// <para type="description">
        /// Verbosity Level of Steps View - Summary (default) or Full
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [Alias("View")]
        public View ViewType { get; set; }

        /// <summary>
        /// Executes the operation to get Experiment steps.
        /// </summary>
        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            this.ValidateParameters();

            HttpResponseMessage response = this.GetExperimentStepsAsync().GetAwaiter().GetResult();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                if (this.ViewType == View.Full)
                {
                    IEnumerable<ExperimentStepInstance> output = response.Content.ReadAsJsonAsync<IEnumerable<ExperimentStepInstance>>().GetAwaiter().GetResult();
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
                    JArray jArray = JArray.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                    var postTitles =
                         from p in jArray
                         select new
                         {
                             id = p["id"].ToString(),
                             name = p["name"].ToString(),
                             experimentGroup = p["experimentGroup"].ToString(),
                             status = p["status"].ToString(),
                             startTime = p["startTime"].ToString(),
                             endTime = p["endTime"].ToString()
                         };
                    if (this.AsJson.IsPresent)
                    {
                        this.WriteResultsAsJson(postTitles.ToList());
                    }
                    else
                    {
                        this.WriteResults(postTitles.ToList());
                    }
                }
            }
            else
            {
                this.WriteObject(string.Format("Get-ExperimentSteps request received response of : {0}", response.StatusCode.ToString()));
            }
        }

        /// <summary>
        /// Validates the parameters passed into the commandlet.
        /// </summary>
        protected override bool ValidateParameters()
        {
            base.ValidateParameters();
            if (string.IsNullOrEmpty(this.ExperimentId))
            {
                throw new ArgumentException($"Invalid arguments passed to module. Required argument is {nameof(this.ExperimentId).ToString()}.");
            }
            
            return true;
        }

        private async Task<HttpResponseMessage> GetExperimentStepsAsync()
        {
            using (RestClientBuilder rcBuilder = new RestClientBuilder())
            {
                using (CancellationTokenSource source = new CancellationTokenSource())
                {
                    HttpResponseMessage responseMessage = null;
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        responseMessage = await this.ExperimentsClient.GetExperimentStepsAsync(this.ExperimentId, source.Token, this.ViewType).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                    return responseMessage;
                }
            }
        }
    }
}
