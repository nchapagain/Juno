namespace Juno.Contracts
{
    using Newtonsoft.Json;

    /// <summary>
    /// History info object
    /// </summary>
    public partial class ExperimentHistoryInfo
    {
        /// <summary>
        ///  Gets or sets experiment Id
        /// </summary>
        [JsonProperty("experimentId")]
        public string ExperimentId { get; set; }

        /// <summary>
        ///  Gets or sets experiment name
        /// </summary>
        [JsonProperty("experimentName")]
        public string ExperimentName { get; set; }

        /// <summary>
        ///  Gets or sets created data 
        /// </summary>
        [JsonProperty("createdDate")]
        public string CreatedDate { get; set; }
    }
}
