namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Management.Automation;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Rest;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Polly;

    /// <summary>
    /// Powershell Module to Start an Experiment given Template JSON string/file path,  optional Overwrite JSON string/file path,
    /// Target End Point and optional Work Queue
    /// </summary>
    [Cmdlet(VerbsCommon.New, "ExecutionGoal")]
    public class NewExecutionGoalCmdlet : ExperimentCmdletBase
    {
        /// <summary>
        /// Constructor for NewExecutionGoalCmdlet class
        /// </summary>
        public NewExecutionGoalCmdlet()
            : base()
        {
        }

        /// <summary>
        /// Constructor for NewExecutionGoalCmdlet class that sets the retry policy
        /// </summary>
        public NewExecutionGoalCmdlet(IAsyncPolicy asyncPolicy = null)
            : base(asyncPolicy)
        {
        }

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
        public string ExperimentName { get; set; }

        /// <summary>
        /// <para type="description">
        /// Full path of populated Parameter input file
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        public string ParameterFilePath { get; set; }

        /// <summary>
        /// <para type="description">
        /// Flag to enable/disable the Execution Goal on creation - defaults to true
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// <para type="description">
        /// Verbosity Level of the response - Summary or Full (Default)
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [Alias("View")]
        public View ViewType { get; set; } = Contracts.View.Summary;

        /// <summary>
        /// Executes the operation to create the OS Imaging job.
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();
                if (this.ValidateParameters())
                {
                    HttpResponseMessage response = this.CreateExecutionGoalAsync().GetAwaiter().GetResult();
                    response.ThrowOnError<ExperimentException>();

                    object output = this.FormatOutput(response.Content);

                    if (this.AsJson.IsPresent)
                    {
                        this.WriteResultsAsJson(output);
                    }
                    else
                    {
                        this.WriteResults(output);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Validates the parameters passed into the commandlet.
        /// OverrideJSONFile/OverrideJSONString is mandatory with TemplateId
        /// </summary>
        protected override bool ValidateParameters()
        {
            base.ValidateParameters();
            if (string.IsNullOrEmpty(this.TemplateId) ||
               string.IsNullOrEmpty(this.TeamName) ||
               string.IsNullOrEmpty(this.ParameterFilePath))
            {
                throw new ArgumentException($"Invalid arguments passed to module. Required parameters are {nameof(this.TeamName).ToString()}, {nameof(this.TemplateId).ToString()}, {nameof(this.ExperimentName).ToString()}, and {nameof(this.ParameterFilePath).ToString()}.");
            }

            return true;
        }

        /// <summary>
        /// Reads the parameter file.
        /// </summary>
        protected async virtual Task<ExecutionGoalParameter> GetParameterFileAsync()
        {
            FileSystem fileSystem = new FileSystem();
            return JsonConvert.DeserializeObject<ExecutionGoalParameter>(await fileSystem.File.ReadAllTextAsync(this.ParameterFilePath).ConfigureAwait(false));
        }

        /// <summary>
        /// Creates console friendly output from <see cref="HttpContent"/>.
        /// </summary>
        /// <param name="content">The content that the friendly console output will be created from.</param>
        /// <returns>An object for console output.</returns>
        protected object FormatOutput(HttpContent content)
        {
            Item<GoalBasedSchedule> item = content.ReadAsJsonAsync<Item<GoalBasedSchedule>>().GetAwaiter().GetResult();
            this.RemoveServerSideDataTags(item);

            object result = item;

            // Only a summary view is expected
            if (this.ViewType == Contracts.View.Summary)
            {
                result = new
                {
                    Id = item.Id,
                    Created = item.Created,
                    LastModified = item.LastModified
                };
            }

            return result;
        }

        private async Task<HttpResponseMessage> CreateExecutionGoalAsync()
        {
            using (RestClientBuilder rcBuilder = new RestClientBuilder())
            {
                using (CancellationTokenSource source = new CancellationTokenSource())
                {
                    ExecutionGoalParameter parameters = await this.GetParameterFileAsync().ConfigureAwait(false);

                    HttpResponseMessage responseMessage = null;
                    await this.RetryPolicy.ExecuteAsync(async () =>
                    {
                        responseMessage = await this.ExperimentsClient.CreateExecutionGoalFromTemplateAsync(parameters, this.TemplateId, this.TeamName, source.Token).ConfigureAwait(false);
                    }).ConfigureDefaults();

                    return responseMessage;
                }
            }
        }
    }
}