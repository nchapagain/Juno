namespace Juno.Extensions.Telemetry
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;

    /// <summary>
    /// Stores details of a diagnostics message.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class DiagnosticsEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticsEntry"/> class.
        /// </summary>
        /// <param name="source">Specifies the diagnostics source.</param>
        /// <param name="message">Specifies the diagnostics message.</param>
        [JsonConstructor]
        public DiagnosticsEntry(string source, Dictionary<string, IConvertible> message)
        {
            source.ThrowIfNullOrWhiteSpace(nameof(source));
            message.ThrowIfNull(nameof(message));

            this.Source = source;
            this.Message = message;
        }

        /// <summary>
        /// Specifies the source of diagnostics.
        /// </summary>
        [JsonProperty("source")]
        public string Source { get; }

        /// <summary>
        /// Specifies the diagnostics details.
        /// </summary>
        [JsonProperty("message")]
        public Dictionary<string, IConvertible> Message { get; }
    }
}
