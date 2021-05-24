namespace Juno.PowerShellModule
{
    using System;
    using System.Management.Automation;
    using System.Net.Http;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Rest;
    using Polly;

    /// <summary>
    /// Powershell Module to get Execution Goal Steps given the Experiment Id, Target End Point and Verbosity Level of Steps 
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "Template")]
    public class RemoveTemplateCmdlet : ExperimentCmdletBase
    {
        /// <summary>
        /// Constructor for GetExecutionGoalCmdlet class that sets the retry policy
        /// </summary>
        public RemoveTemplateCmdlet()
            : this(null)
        {
        }

        /// <summary>
        /// Constructor for GetExecutionGoalCmdlet class that sets the retry policy
        /// </summary>
        public RemoveTemplateCmdlet(IAsyncPolicy asyncPolicy = null)
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
        /// Team Name
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [Alias("Team")]
        public string TeamName { get; set; }

        /// <summary>
        /// Executes the operation to get an execution goal.
        /// </summary>
        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            this.ValidateParameters();

            HttpResponseMessage response = this.RemoveExecutionGoalTemplateAsync(this.TemplateId, this.TeamName).GetAwaiter().GetResult();
            response.ThrowOnError<ExperimentException>();
        }

        /// <summary>
        /// Validates the parameters passed into the commandlet.
        /// </summary>
        protected override bool ValidateParameters()
        {
            base.ValidateParameters();
            if (string.IsNullOrEmpty(this.TeamName))
            {
                throw new ArgumentException($"Invalid arguments passed to module. Required arguments are {nameof(this.TeamName).ToString()} and {nameof(this.TemplateId).ToString()}.");
            }

            return true;
        }
    }
}
