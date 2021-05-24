namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Rest;
    using Polly;

    /// <summary>
    /// Powershell Module to get Experiment Resources given the Experiment Id 
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "ExperimentResources")]
    public class GetExperimentResourcesCmdlet : ExperimentCmdletBase
    {
        /// <summary>
        /// Constructor for GetExperimentStepsCmdlet class
        /// </summary>
        public GetExperimentResourcesCmdlet()
            : base()
        {
        }

        /// <summary>
        /// Constructor for GetExperimentResourcesCmdlet class that sets the retry policy
        /// </summary>
        public GetExperimentResourcesCmdlet(IAsyncPolicy asyncPolicy = null)
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
        /// Executes the operation to get Experiment resources.
        /// </summary>
        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            this.ValidateParameters();
            IEnumerable<EnvironmentEntity> response = this.GetExperimentContextAsync().GetAwaiter().GetResult();
            if (response != null)
            {
                if (this.AsJson.IsPresent)
                {
                    this.WriteResultsAsJson(response);
                }
                else
                {
                    this.WriteResults(response);
                }
            }
            else
            {
                this.WriteObject(string.Format("Experiment does not have any resources associated."));
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

        private async Task<IEnumerable<EnvironmentEntity>> GetExperimentContextAsync()
        {
            using (RestClientBuilder rcBuilder = new RestClientBuilder())
            {
                using (CancellationTokenSource source = new CancellationTokenSource())
                {
                    IEnumerable<EnvironmentEntity> responseMessage = null;
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        responseMessage = await this.ExperimentsClient.GetExperimentResourcesAsync(this.ExperimentId, source.Token).ConfigureAwait(false);
                    }).ConfigureAwait(false);

                    return responseMessage;
                }
            }
        }
    }
}
