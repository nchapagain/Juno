namespace Juno.Contracts
{
    using System;
    using System.Linq;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension methods for working with parameters defined for experiments and 
    /// experiment components.
    /// </summary>
    public static class ExperimentParameterExtensions
    {
        private const string TimeoutParameter = "timeout";

        /// <summary>
        /// Returns a 'timeout' defined in the parameters of the experiment component.
        /// </summary>
        /// <param name="component">The experiment component.</param>
        /// <returns>
        /// A <see cref="TimeSpan"/> representing the timeout if defined.
        /// </returns>
        public static TimeSpan? Timeout(this ExperimentComponent component)
        {
            component.ThrowIfNull(nameof(component));

            TimeSpan? timeout = null;
            if (component.Parameters?.Any() == true
                && component.Parameters.ContainsKey(ExperimentParameterExtensions.TimeoutParameter))
            {
                timeout = TimeSpan.Parse(component.Parameters.GetValue<string>(ExperimentParameterExtensions.TimeoutParameter));
            }

            return timeout;
        }
    }
}
