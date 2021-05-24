namespace Juno.Execution.Management
{
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.Storage;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Extension methods for experiment metadata/notice objects.
    /// </summary>
    internal static class ExperimentNoticeExtensions
    {
        /// <summary>
        /// Returns the message ID for the notification on the Azure Queue where the message
        /// exists.
        /// </summary>
        /// <param name="notice">The notification containing the message ID reference.</param>
        public static string MessageId(this ExperimentMetadataInstance notice)
        {
            notice.ThrowIfNull(nameof(notice));

            if (!notice.Extensions.TryGetValue(AzureQueueStore.MessageIdExtension, out JToken messageId))
            {
                throw new SchemaException($"Notice is missing expected '{AzureQueueStore.MessageIdExtension}' extension property/definition.");
            }

            return messageId.ToString();
        }

        /// <summary>
        /// Returns the pop receipt for the notification on the Azure Queue where the message
        /// exists.
        /// </summary>
        /// <param name="notice">The notification containing the pop receipt reference.</param>
        public static string PopReceipt(this ExperimentMetadataInstance notice)
        {
            notice.ThrowIfNull(nameof(notice));

            if (!notice.Extensions.TryGetValue(AzureQueueStore.PopReceiptExtension, out JToken popReceipt))
            {
                throw new SchemaException($"Notice is missing expected '{AzureQueueStore.PopReceiptExtension}' extension property/definition.");
            }

            return popReceipt.ToString();
        }
    }
}
