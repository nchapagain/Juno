namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Well-known paths for dependencies and tooling on nodes and virtual machines
    /// that are part of Juno experiments.
    /// </summary>
    public static class DependencyPaths
    {
        // Functions/delegates to replace supported placeholders in a given path.
        private static readonly List<Func<string, string>> PlaceholderHandlers = new List<Func<string, string>>
        {
            (path) => Regex.Replace(path, "{NuGetPackagePath}", DependencyPaths.NuGetPackages, RegexOptions.IgnoreCase)
        };

        /// <summary>
        /// Gets the path to the root directory where NuGet packages are installed.
        /// </summary>
        public static string NuGetPackages { get; } = Path.Combine(DependencyPaths.RootPath, "NuGet", "Packages");

        /// <summary>
        /// Gets the path to the root directory where dependencies
        /// are installed.
        /// </summary>
        public static string RootPath => Path.Combine(Path.GetTempPath(), "Juno");

        /// <summary>
        /// Replaces any well-known path placeholders (e.g. {NuGetPackagePath} with actual path
        /// values.
        /// </summary>
        /// <param name="commandPath">A path containing path placeholders.</param>
        /// <returns>
        /// A path having all placeholders replaced.
        /// </returns>
        public static string ReplacePathReferences(string commandPath)
        {
            string updatedPath = commandPath;
            foreach (Func<string, string> replacementHandler in DependencyPaths.PlaceholderHandlers)
            {
                updatedPath = replacementHandler.Invoke(updatedPath);
            }

            return updatedPath;
        }
    }
}
