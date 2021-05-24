namespace Juno.Contracts
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extensions class for object type string.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Translates the string into a list of strings delimited by the 
        /// characters given. Trims whitespace around any list values.
        /// </summary>
        /// <param name="value">The value to map onto a list</param>
        /// <param name="delimiters">The delimiters of the value</param>
        /// <returns>A list of strings mapped from the string given.</returns>
        public static IList<string> ToList(this string value, params char[] delimiters)
        {
            value.ThrowIfNullOrWhiteSpace(nameof(value));
            IList<string> split = value.Split(delimiters);
            return split.Select(sub => sub.Trim()).ToList();
        }
    }
}
