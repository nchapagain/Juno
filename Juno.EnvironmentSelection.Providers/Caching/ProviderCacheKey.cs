namespace Juno.EnvironmentSelection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Defines a Cache Key for ESS providers.
    /// </summary>
    public class ProviderCacheKey : Dictionary<string, string>
    {
        private const char KvSeperator = '&';
        private const char EntrySeperator = ':';
        private static readonly char[] ReservedChars = { '[', '\r', '\n', ']', ' ', '\"' };

        /// <summary>
        /// Initializes a <see cref="ProviderCacheKey"/>
        /// </summary>
        public ProviderCacheKey()
        {
        }

        /// <summary>
        /// Initialies a <see cref="ProviderCacheKey"/> with the same collection
        /// of key-value pairs given by the dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary whose key-value pairs should be copied.</param>
        public ProviderCacheKey(IDictionary<string, string> dictionary) 
            : base(dictionary)
        {
        }

        /// <summary>
        /// Projects the given key-value onto each entry in the set of original keys.
        /// </summary>
        /// <param name="originalKeys">The set to be projected onto</param>
        /// <param name="key">The key to add to each entry</param>
        /// <param name="value">The value the key should map to</param>
        /// <returns>A new <see cref="IList{ProviderCacheKey}"/> that is similar to the original list, except with the addition
        /// of the key and value to each entry.</returns>
        public static IList<ProviderCacheKey> ProjectString(IEnumerable<ProviderCacheKey> originalKeys, string key, string value)
        {
            originalKeys.ThrowIfNull(nameof(originalKeys));
            key.ThrowIfNullOrWhiteSpace(nameof(key));
            value.ThrowIfNullOrWhiteSpace(nameof(value));

            if (originalKeys.Any(k => k.Keys.Contains(key)))
            {
                throw new ArgumentException($"One or more {nameof(originalKeys)} contains a {nameof(key)} : {key}");
            }

            IList<ProviderCacheKey> result = new List<ProviderCacheKey>();
            if (!originalKeys.Any())
            {
                ProviderCacheKey cacheKey = new ProviderCacheKey
                {
                    { key, value }
                };
                return new List<ProviderCacheKey>() { cacheKey };
            }

            foreach (ProviderCacheKey cacheKey in originalKeys)
            {
                ProviderCacheKey newKey = new ProviderCacheKey(cacheKey)
                {
                    { key, value }
                };
                result.Add(newKey);
            }

            return result;
        }

        /// <summary>
        /// Given a key in the dictionary, expands the entry into a set of 
        /// cache keys. The value to expand should be a list.
        /// </summary>
        /// <param name="entry">The entry that maps to a key in the <see cref="ProviderCacheKey"/></param>
        /// <param name="splitBy">A character to split the list entries by</param>
        /// <returns>
        /// A <see cref="IEnumerable{ProviderCacheKey}"/> where the entry points to one value such 
        /// that the value belonged to the original list the entry referenced.
        /// </returns>
        public IEnumerable<ProviderCacheKey> ExpandEntry(string entry, char splitBy)
        {
            entry.ThrowIfNullOrWhiteSpace(nameof(entry));
            splitBy.ThrowIfNull(nameof(splitBy));

            if (!this.ContainsKey(entry))
            {
                throw new ArgumentException($"{nameof(ProviderCacheKey)} does not contain entry {entry}");
            }

            if (ProviderCacheKey.ReservedChars.Contains(splitBy))
            {
                throw new ArgumentException($"{nameof(ProviderCacheKey)} can not split on: {splitBy}, this is one of the reserved " +
                    $"{nameof(ProviderCacheKey)} characters.");
            }

            // The value is going to be straight from kusto. This means it is carrying all of the residual list characters, that must be removed.
            string entryValue = this[entry].Replace("\r\n", string.Empty, StringComparison.Ordinal)
                .Replace("[", string.Empty, StringComparison.Ordinal)
                .Replace("]", string.Empty, StringComparison.Ordinal)
                .Replace("\"", string.Empty, StringComparison.Ordinal);
            IEnumerable<string> values = entryValue.Split(splitBy).Select(v => v.Trim());
            IList<ProviderCacheKey> expandedKeys = new List<ProviderCacheKey>();
            foreach (string value in values)
            {
                ProviderCacheKey key = new ProviderCacheKey(this)
                {
                    [entry] = value
                };
                expandedKeys.Add(key);
            }

            return expandedKeys;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            IList<string> orderedKeys = this.Keys.OrderBy(key => key).ToList();
            StringBuilder builder = new StringBuilder();
            foreach (string orderedKey in orderedKeys)
            {
                if (builder.Length != 0)
                {
                    builder.Append(ProviderCacheKey.KvSeperator);
                }

                builder.Append(orderedKey).Append(ProviderCacheKey.EntrySeperator).Append(this[orderedKey]);
            }

            return builder.ToString();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            ProviderCacheKey other = obj as ProviderCacheKey;
            if (other == null)
            {
                return false;
            }

            return this.Equals(other);
        }

        /// <summary>
        /// Compares equality between this instance and another instance
        /// of a <see cref="ProviderCacheKey"/>
        /// </summary>
        /// <param name="other">The other <see cref="ProviderCacheKey"/></param>
        /// <returns>True if equal false otherwise</returns>
        public bool Equals(ProviderCacheKey other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
        }
    }
}
