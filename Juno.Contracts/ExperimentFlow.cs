namespace Juno.Contracts
{
    using Newtonsoft.Json;

    /// <summary>
    /// Conditional flow definition used to override the default flow of an experiment.
    /// </summary>
    public class ExperimentFlow
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentFlow"/> object.
        /// </summary>
        /// <param name="blockName">Name of the experiment step block.</param>
        /// <param name="onFailureExecuteBlock">Name of the block of steps to execute when a step fails.</param>
        /// <param name="overrideDefault">Flag to override the default step selection strategy.</param>
        [JsonConstructor]
        public ExperimentFlow(string blockName, string onFailureExecuteBlock, bool overrideDefault = false)
        {
            this.BlockName = blockName;
            this.OnFailureExecuteBlock = onFailureExecuteBlock;
            this.OverrideDefault = overrideDefault;
        }

        /// <summary>
        /// Name of the experiment step block.
        /// </summary>
        [JsonProperty(PropertyName = "blockName", Order = 1)]
        public string BlockName { get; }

        /// <summary>
        /// Name of the block of steps to execute when a step fails.
        /// </summary>
        [JsonProperty(PropertyName = "onFailureExecuteBlock", Order = 2)]
        public string OnFailureExecuteBlock { get; }

        /// <summary>
        /// Used to override the default execution flow.
        /// </summary>
        [JsonProperty(PropertyName = "overrideDefault", Order = 3)]
        public bool OverrideDefault { get; set; }
    }
}
