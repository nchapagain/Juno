﻿namespace Juno.PowerShellModule
{
    using System;
    using System.IO;
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
    /// Powershell Module to Update an Execution Goal given JSON string/file path
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Template")]
    public class NewTemplateCmdlet : ExperimentCmdletBase
    {
        /// <summary>
        /// Constructor for NewTemplateCmdlet class
        /// </summary>
        public NewTemplateCmdlet()
            : this(null)
        {
        }

        /// <summary>
        /// Constructor for NewTemplateCmdlet class that sets the retry policy
        /// </summary>
        public NewTemplateCmdlet(IAsyncPolicy asyncPolicy = null)
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
        /// Executes the operation to update an execution goal in cosmos.
        /// </summary>
        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            this.ValidateParameters();

            HttpResponseMessage response = this.CreateTemplateAsync(this.FilePath).GetAwaiter().GetResult();
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
        /// Gets an Item with <see cref="GoalBasedSchedule"/> from a provided file location.
        /// </summary>
        /// <param name="filePath">The path of the file to be parsed into an Item with a <see cref="GoalBasedSchedule"/>.</param>
        /// <returns>An Item with a <see cref="GoalBasedSchedule"/></returns>
        protected virtual Item<GoalBasedSchedule> GetExecutionGoalFromFile(string filePath)
        {
            return JsonConvert.DeserializeObject<Item<GoalBasedSchedule>>(File.ReadAllTextAsync(filePath).GetAwaiter().GetResult());
        }

        private async Task<HttpResponseMessage> CreateTemplateAsync(string filePath)
        {
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                Item<GoalBasedSchedule> executionGoalTemplate = this.GetExecutionGoalFromFile(filePath);

                HttpResponseMessage responseMessage = null;
                await this.RetryPolicy.ExecuteAsync(async () =>
                {
                    responseMessage = await this.ExperimentsClient.CreateExecutionGoalTemplateAsync(executionGoalTemplate, source.Token).ConfigureAwait(false);
                }).ConfigureAwait(false);

                return responseMessage;
            }
        }
    }
}
