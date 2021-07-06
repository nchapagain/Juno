namespace Juno.PowerShellModule
{
    using System;
    using System.IO;
    using System.IO.Abstractions;
    using System.Management.Automation;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Newtonsoft.Json;
    using Polly;

    /// <summary>
    /// Powershell Module to update an Execution Goal
    /// </summary>
    [Cmdlet(VerbsData.Update, "ExecutionGoalFromTemplate")]
    public class UpdateExecutionGoalFromTemplateCmdlet : ExperimentCmdletBase
    {
        /// <summary>
        /// Constructor for UpdateExecutionGoalFromTemplateCmdlet class
        /// </summary>
        public UpdateExecutionGoalFromTemplateCmdlet()
            : this(null)
        {
        }

        /// <summary>
        /// Constructor for UpdateExecutionGoalFromTemplateCmdlet class that sets the retry policy
        /// </summary>
        public UpdateExecutionGoalFromTemplateCmdlet(IAsyncPolicy asyncPolicy = null)
            : base(asyncPolicy)
        {
        }

        /// <summary>
        /// <para type="description">
        /// Full path of JSON input file
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [Alias("Path")]
        public string FilePath { get; set; }

        /// <summary>
        /// <para type="description">
        /// Template Id
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        public string TemplateId { get; set; }

        /// <summary>
        /// <para type="description">
        /// Team Name that owns the template identified by the TemplateId
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        public string TeamName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Experiment Name
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        public string ExecutionGoalId { get; set; }

        /// <summary>
        /// Executes the operation to update an execution goal in cosmos.
        /// </summary>
        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            this.ValidateParameters();

            HttpResponseMessage response = this.UpdateExecutionGoalAsync(this.FilePath).GetAwaiter().GetResult();
            response.ThrowOnError<ExperimentException>();

            Item<GoalBasedSchedule> item = response.Content.ReadAsJsonAsync<Item<GoalBasedSchedule>>().GetAwaiter().GetResult();
            this.RemoveServerSideDataTags(item);

            if (this.AsJson.IsPresent)
            {
                this.WriteResultsAsJson(item);
            }
            else
            {
                this.WriteResults(item);
            }
        }

        /// <summary>
        /// Validates the parameters passed into the commandlet.
        /// </summary>
        protected override bool ValidateParameters()
        {
            base.ValidateParameters();

            if (string.IsNullOrEmpty(this.FilePath))
            {
                throw new ArgumentException($"Invalid arguments passed to module. Required parameter is {nameof(this.FilePath).ToString()}.");
            }

            return true;
        }

        /// <summary>
        /// Gets an Item with <see cref="ExecutionGoalParameter"/> from a provided file location.
        /// </summary>
        /// <param name="filePath">The path of the file to be parsed into an Item with a <see cref="ExecutionGoalParameter"/>.</param>
        /// <returns>An Item with a <see cref="ExecutionGoalParameter"/></returns>
        protected async virtual Task<ExecutionGoalParameter> GetExecutionGoalParameterFromFileAsync(string filePath)
        {
            FileSystem fileSystem = new FileSystem();
            return JsonConvert.DeserializeObject<ExecutionGoalParameter>(await fileSystem.File.ReadAllTextAsync(filePath).ConfigureAwait(false));
        }

        private async Task<HttpResponseMessage> UpdateExecutionGoalAsync(string filePath)
        {
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                ExecutionGoalParameter executionGoalParameter = await this.GetExecutionGoalParameterFromFileAsync(filePath).ConfigureAwait(false);

                HttpResponseMessage responseMessage = null;
                await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    responseMessage = await this.ExperimentsClient.UpdateExecutionGoalFromTemplateAsync(executionGoalParameter, this.TemplateId, this.ExecutionGoalId, this.TeamName, source.Token).ConfigureAwait(false);
                }).ConfigureAwait(false);

                return responseMessage;
            }
        }
    }
}
