namespace Juno.Contracts.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension methods for configuration settings-related classes.
    /// </summary>
    public static class SettingsExtensions
    {
        /// <summary>
        /// Extension gets the settings definition from the set whose ID matches.
        /// </summary>
        /// <typeparam name="TSettings">The data type of the settings instance.</typeparam>
        /// <param name="settings">The set of settings containing the target definition.</param>
        /// <param name="id">The ID of the settings.</param>
        /// <exception cref="ArgumentException">A settings definition with the ID provided does not exist.</exception>
        public static TSettings Get<TSettings>(this IEnumerable<TSettings> settings, string id)
            where TSettings : SettingsBase
        {
            settings.ThrowIfNull(nameof(settings));
            id.ThrowIfNullOrWhiteSpace(nameof(id));

            TSettings match = settings.FirstOrDefault(s => s.Id == id);
            if (match == null)
            {
                throw new ArgumentException($"Settings with ID '{id}' do not exist in the configuration settings provided.");
            }

            return match;
        }
    }
}
