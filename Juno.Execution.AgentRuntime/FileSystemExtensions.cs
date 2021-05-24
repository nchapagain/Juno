namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Text;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC.Extensions;

    /// <summary>
    /// Extension method that help with the interaction of files while
    /// operating AgentRuntime
    /// </summary>
    public static class FileSystemExtensions
    {
        /// <summary>
        /// Retrieves a file located in the 
        /// </summary>
        /// <param name="directory">Directory object that offers methods to interact with directory.</param>
        /// <param name="pfService">The pfservice directory to search under</param>
        /// <param name="file">The file to search for.</param>
        /// <param name="scenario">
        /// An optional scenario. In CRC, pf services there may be two files of the same name:
        /// i.e.
        /// JunoPayload
        ///     Upgrade
        ///         MyUpdateFile.bat
        ///     Downgrade
        ///         MyUpdateFile.bat
        /// this parameter directs which scenario to look under.
        /// </param>
        /// <param name="drive">The top level drive to search for</param>
        /// <returns>True/False is the file exists on the machine, under the pfservice and further, under the scenrario if supplied.</returns>
        public static bool FileExists(this IDirectory directory, string pfService, string file, string scenario = null, string drive = AgentRuntimeConstants.AppFolder)
        {
            directory.ThrowIfNull(nameof(directory));
            try
            {
                directory.GetFile(pfService, file, scenario, drive);
            }
            catch (FileNotFoundException)
            {
                return false;
            }

            return true;

        }

        /// <summary>
        /// Retrieves the parent directory of the file supplied
        /// </summary>
        /// <param name="directory">Instance of an IDirectory</param>
        /// <param name="pfService">The pf service under which the file is located.</param>
        /// <param name="file">The file to search for.</param>
        /// <param name="scenario">The scenario which further delimits the directory the file is under.</param>
        /// <param name="drive">The main drive to search under.</param>
        /// <returns>The path to the parent directory of the file supplied.</returns>
        public static string GetParentDirectory(this IDirectory directory, string pfService, string file, string scenario = null, string drive = AgentRuntimeConstants.AppFolder)
        {
            string fullpath = directory.GetFile(pfService, file, scenario, drive);
            IDirectoryInfo directories = directory.GetParent(fullpath);
            return directories.FullName;
        }

        /// <summary>
        /// Retrieves a file located in the 
        /// </summary>
        /// <param name="directory">Directory object that offers methods to interact with directory.</param>
        /// <param name="pfService">The pfservice directory to search under</param>
        /// <param name="file">The file to search for.</param>
        /// <param name="scenario">
        /// An optional scenario. In CRC, pf services there may be two files of the same name:
        /// i.e.
        /// JunoPayload
        ///     Upgrade
        ///         MyUpdateFile.bat
        ///     Downgrade
        ///         MyUpdateFile.bat
        /// this parameter directs which scenario to look under.
        /// </param>
        /// <param name="drive">The top level drive to search for</param>
        /// <returns>The full path to the file.</returns>
        public static string GetFile(this IDirectory directory, string pfService, string file, string scenario = null, string drive = AgentRuntimeConstants.AppFolder)
        {
            directory.ThrowIfNull(nameof(directory));
            pfService.ThrowIfNullOrWhiteSpace(nameof(pfService));
            file.ThrowIfNullOrWhiteSpace(nameof(file));
            string[] filePaths = directory.GetFiles(drive, file, SearchOption.AllDirectories);
            if (!filePaths.Any())
            {
                throw new FileNotFoundException($"The file: {file} was not found in the {pfService} under the folder: {drive}");
            }

            IEnumerable<string> pfPaths = filePaths.Where(p => p.Contains(pfService, StringComparison.OrdinalIgnoreCase));
            if (!pfPaths.Any())
            {
                throw new FileNotFoundException($"The file: {file} was found under the root folder: {drive} but not under the pfservice {pfService}");
            }

            if (scenario == null)
            {
                return pfPaths.First();
            }

            IEnumerable<string> scenarioSpecific = pfPaths.Where(p => p.Contains(scenario, StringComparison.OrdinalIgnoreCase));
            if (!scenarioSpecific.Any())
            {
                throw new FileNotFoundException($"The file: {file} was found in the {pfService} under the folder: {drive} but did not match scenario: {scenario}");
            }

            return scenarioSpecific.First();
        }
    }
}
