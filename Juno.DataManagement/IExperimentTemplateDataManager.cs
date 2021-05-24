namespace Juno.DataManagement
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;

    /// <summary>
    /// Provides methods for managing Juno Experiment Template operations.
    /// </summary>
    public interface IExperimentTemplateDataManager
    {
        /// <summary>
        /// Gets the experiment definition from cosmos
        /// </summary>
        /// <param name="definitionId">The ID of the experiment definition/file.</param>
        /// <param name="teamName">TeamName</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="experimentName">Optional parameter defines an override to the name of the experiment to use instead of the one in the definition.</param>
        /// <returns>ExperimentTemplateDefinition</returns>
        Task<ExperimentItem> GetExperimentTemplateAsync(string definitionId, string teamName, CancellationToken cancellationToken, string experimentName = null);

        /// <summary>
        /// Gets the list of experiment definition Ids from cosmos
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="teamName">Name of the team who owns the Experiment Template</param>
        /// <returns>List of ExperimentTemplateId</returns>
        Task<IEnumerable<ExperimentItem>> GetExperimentTemplatesListAsync(string teamName, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the list of experiment definition Ids from cosmos
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of ExperimentTemplateId</returns>
        Task<IEnumerable<ExperimentItem>> GetExperimentTemplatesListAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the experiment definition from COSMOS
        /// </summary>
        /// <param name="experiment">The Experiment object to create</param>
        /// <param name="documentId">The documentId using which the Template would be saved</param>
        /// <param name="replaceIfExists">Replace file is already present</param>  
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Experiment Template Id</returns>
        Task<ExperimentItem> CreateExperimentTemplateAsync(ExperimentItem experiment, string documentId, bool replaceIfExists, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the experiment definition from cosmos
        /// </summary>
        /// <param name="documentId">The documentId using which the Template would be saved</param>
        /// <param name="teamName">Name of the team that owns the template</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        Task<string> DeleteExperimentTemplateAsync(string documentId, string teamName, CancellationToken cancellationToken);
    }
}
