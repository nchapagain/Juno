namespace Juno.PowerShellModule
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using Juno.Api.Client;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Rest;
    using Newtonsoft.Json;
    using Polly;

    /// <summary>
    /// Based Cmdlet file to be drived by all classes implementing a new PS Module
    /// </summary>
    public abstract class ExperimentCmdletBase : PSCmdlet, IDisposable
    {
        private const int DefaultRetryCount = 5;
        private const int DefaultRetryWaitInSecs = 3;
        private static readonly JsonSerializerSettings DefaultJsonSerializationSettings = new JsonSerializerSettings
        {
            // Format: 2012-03-21T05:40:12.340Z
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,

            // We tried using PreserveReferenceHandling.All and Object, but ran into issues
            // when deserializing string arrays and read only dictionaries
            ReferenceLoopHandling = ReferenceLoopHandling.Error,

            // This is the default setting, but to avoid remote code execution bugs do NOT change
            // this to any other setting.
            TypeNameHandling = TypeNameHandling.None
        };

        private static IAsyncPolicy defaultRetryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(ExperimentCmdletBase.DefaultRetryCount, (retries) => TimeSpan.FromSeconds(retries + ExperimentCmdletBase.DefaultRetryWaitInSecs));

        private IAsyncPolicy retryPolicy;
        private IExperimentClient experimentsClient;

        static ExperimentCmdletBase()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ExperimentCmdletBase.OnAssemblyResolve;
            ExperimentCmdletBase.ModulePath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(ExperimentCmdletBase)).Location);
        }

        /// <summary>
        /// Constructor for ExperimentCmdletBase class
        /// </summary>
        public ExperimentCmdletBase(IAsyncPolicy retryPolicy = null)
        {
            this.retryPolicy = retryPolicy ?? ExperimentCmdletBase.defaultRetryPolicy;
        }

        /// <summary>
        /// Gets the path to the PowerShell module assembly.
        /// </summary>
        public static string ModulePath { get; }

        /// <summary>
        /// <para type="description">
        /// Team Name
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter AsJson { get; set; }

        /// <summary>
        /// Gets the experiment client for experiment api calls.
        /// </summary>
        public IExperimentClient ExperimentsClient 
        { 
            get
            {
                if (this.experimentsClient == null)
                {
                    this.experimentsClient = ExperimentCmdletBase.GetExperimentsClient();
                }

                return this.experimentsClient;
            }

            set
            {
                this.experimentsClient = value ?? ExperimentCmdletBase.GetExperimentsClient();
            } 
        }

        /// <summary>
        /// Gets the retry policy for experiment api calls.
        /// </summary>
        public IAsyncPolicy RetryPolicy
        {
            get
            {
                return this.retryPolicy;
            }
        }

        /// <summary>
        /// Gets the encoding of the file contents. Default is set to the UTF-8 format.
        /// </summary>
        internal static Encoding ContentEncoding { get; } = Encoding.UTF8;

        /// <summary>
        /// Gets or sets the cancellation token used to cancel module commandlet operation.  Cancellation
        /// is requested when the user presses Ctrl-C.  Commandlets are expected to handle cancellation
        /// requests individually.
        /// </summary>
        protected CancellationTokenSource CancellationTokenSource { get; set; }

        /// <summary>
        /// Disposes of resources used by the class instance.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of resources used by the class instance.
        /// </summary>
        /// <param name="disposing">A flag determining whether to dispose of the managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Removes any server-side data tracking extensions from the item (e.g. _eTag, _rid).
        /// </summary>
        /// <param name="item">The item containing data tracking extensions.</param>
        protected virtual void RemoveServerSideDataTags(ItemBase item)
        {
            if (item != null && item.Extensions?.Any() == true)
            {
                List<string> extensionsToRemove = new List<string>();
                item.Extensions.ToList().ForEach(ext =>
                {
                    if (ext.Key.StartsWith('_'))
                    {
                        extensionsToRemove.Add(ext.Key);
                    }
                });

                if (extensionsToRemove.Any())
                {
                    extensionsToRemove.ForEach(ext => item.Extensions.Remove(ext));
                }
            }
        }

        /// <summary>
        /// When overridden in a derived class, executes custom parameter validation.
        /// </summary>
        protected virtual bool ValidateParameters()
        {
            return true;
        }

        /// <summary>
        /// Entry point.  When overridden in a derived class, this executes the cmdlet operation.
        /// </summary>
        protected override void ProcessRecord()
        {
        }

        /// <summary>
        /// Writes the exception/error message to the console/PowerShell pipeline.
        /// </summary>
        /// <param name="exception">The exception object to write to the pipeline.</param>
        /// <param name="context">The context.</param>
        /// <param name="category">An error category.</param>
        protected virtual void WriteErrorMessage(Exception exception, string context, ErrorCategory category)
        {
            this.WriteError(new ErrorRecord(exception, context, category, exception));
        }

        /// <summary>
        /// Writes the results to the console/PowerShell pipeline.
        /// </summary>
        /// <param name="activityId">The activity ID.</param>
        /// <param name="activity">The name of the activity.</param>
        /// <param name="description">The description of the activity.</param>
        protected virtual void WriteProgressMessage(int activityId, string activity, string description)
        {
            // Note:
            // If order to have the ability to unit test the logic, the WriteProgress method
            // cannot be called and must be stubbed out
            this.WriteProgress(new ProgressRecord(activityId, activity, description));
        }

        /// <summary>
        /// Writes the results to the console/PowerShell pipeline.
        /// </summary>
        /// <param name="results">The results object to write to the pipeline.</param>
        protected virtual void WriteResults(object results)
        {
            this.WriteObject(results);
        }

        /// <summary>
        /// Writes the results to the console/PowerShell pipeline.
        /// </summary>
        /// <param name="results">The results object to write to the pipeline.</param>
        protected virtual void WriteResultsAsJson(object results)
        {
            this.WriteObject(results.ToJson(ExperimentCmdletBase.DefaultJsonSerializationSettings));
        }

        /// <summary>
        /// Writes a verbose message to the console/PowerShell pipeline.
        /// </summary>
        /// <param name="text">The message to output.</param>
        protected virtual void WriteVerboseMessage(string text)
        {
            this.WriteVerbose(text);
        }

        /// <summary>
        /// Writes a warning message to the console/PowerShell pipeline.
        /// </summary>
        /// <param name="text">The message to output.</param>
        protected virtual void WriteWarningMessage(string text)
        {
            this.WriteWarning(text);
        }

        private static ExperimentsClient GetExperimentsClient()
        {
            using (RestClientBuilder rcBuilder = new RestClientBuilder())
            {
                IRestClient restClient = rcBuilder.AddAcceptedMediaType(MediaType.Json).WithTokenAuthentication(GetAccessToken.AccessToken).Build();
                return new ExperimentsClient(restClient, new Uri(GetAccessToken.ServiceEndpoint));
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            // PowerShell seems to have no understanding of assembly binding redirects.  In any case, the assemblies
            // we care about are in the directory alongside the module DLL and we will look there for them.
            Assembly assembly = null;

            if (!string.IsNullOrWhiteSpace(args.Name))
            {
                string assemblyName = args.Name.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                string localAssemblyPath = Path.Combine(ExperimentCmdletBase.ModulePath, assemblyName + ".dll");

                assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.Location.ToLower().Contains(localAssemblyPath, StringComparison.OrdinalIgnoreCase));
                if ((assembly == null) && File.Exists(localAssemblyPath))
                {
                    assembly = Assembly.LoadFile(localAssemblyPath);
                }
            }

            return assembly;
        }
    }
}
