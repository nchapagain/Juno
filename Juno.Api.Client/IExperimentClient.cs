namespace Juno.Api.Client
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;

    /// <summary>
    /// Juno Experiment REST API client.
    /// </summary>
    public interface IExperimentClient
    {
        /// <summary>
        /// Cancels the execution of an experiment running in the system.
        /// </summary>
        /// <param name="experimentId">The unique ID of the experiment to delete.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> indicating whether the experiment was cancelled (Cancelled = 204).
        /// </returns>
        Task<HttpResponseMessage> CancelExperimentAsync(string experimentId, CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to create a new experiment.
        /// </summary>
        /// <param name="experiment">The experiment definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="workQueue">Optional parameter allows the experiment to be queued up on the specified work queue.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentInstance"/> created.
        /// </returns>
        Task<HttpResponseMessage> CreateExperimentAsync(Experiment experiment, CancellationToken cancellationToken, string workQueue = null);

        /// <summary>
        /// Makes an API request to create a new experiment with override.
        /// </summary>
        /// <param name="experimentTemplate">The experiment template definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="workQueue">Optional parameter allows the experiment to be queued up on the specified work queue.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentTemplate"/> created.
        /// </returns>
        Task<HttpResponseMessage> CreateExperimentFromTemplateAsync(ExperimentTemplate experimentTemplate, CancellationToken cancellationToken, string workQueue = null);

        /// <summary>
        /// Create an execution goal template and post to system.
        /// </summary>
        /// <param name="executionGoalTemplate">The execution goal template definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentTemplate"/> created.
        /// </returns>
        Task<HttpResponseMessage> CreateExecutionGoalTemplateAsync(Item<GoalBasedSchedule> executionGoalTemplate, CancellationToken cancellationToken);

        /// <summary>
        /// Create an execution goal from template and post to system
        /// </summary>
        /// <param name="parameters">Parameters that was provided by user</param>
        /// <param name="templateId">Template Id</param>
        /// <param name="teamName">Team that owns the template</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        Task<HttpResponseMessage> CreateExecutionGoalFromTemplateAsync(ExecutionGoalParameter parameters, string templateId, string teamName, CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to create a new experiment template.
        /// </summary>
        /// <param name="experiment">The experiment definition.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentItem"/> created.
        /// </returns>
        Task<HttpResponseMessage> CreateExperimentTemplateAsync(Experiment experiment, CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to get Experiment Definition.
        /// </summary>
        /// <param name="definitionId">Definition Id.</param>
        /// <param name="teamName">Team Name</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentTemplate"/> created.
        /// </returns>
        Task<HttpResponseMessage> GetExperimentTemplateAsync(string definitionId, string teamName, CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to get Experiment Definition List.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentTemplateInfo"/> created.
        /// </returns>
        Task<HttpResponseMessage> GetExperimentTemplateListAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to get Experiment Definition List.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentTemplateInfo"/> created.
        /// </returns>
        Task<HttpResponseMessage> GetExperimentProviderListAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to get the steps for an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <param name="view">A flag to indicate Summary or Full response.</param>
        /// <param name="status">Optional parameter defines the set of statuses on which to filter the results.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the set of <see cref="ExperimentStepInstance"/>
        /// definitions.
        /// </returns>
        Task<HttpResponseMessage> GetExperimentStepsAsync(
            string experimentId,
            CancellationToken cancellationToken,
            View view = View.Summary,
            IEnumerable<ExecutionStatus> status = null);

        /// <summary>
        /// Makes an API request to get experiment instance statuses for end-to-end experiments.
        /// </summary>
        /// <param name="experimentName">The name of the experiment/experiment instances.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the individual experiment status objects.
        /// </returns>
        Task<HttpResponseMessage> GetExperimentInstanceStatusesAsync(string experimentName, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve environment candidates from the system.
        /// </summary>
        /// <param name="query">filters to apply to the set of possible environments</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<HttpResponseMessage> ReserveEnvironmentsAsync(EnvironmentQuery query, CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to update an experiment template.
        /// </summary>
        /// <param name="experimentTemplate">The experiment template to update.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentItem"/> created.
        /// </returns>
        Task<HttpResponseMessage> UpdateExperimentTemplateAsync(ExperimentItem experimentTemplate, CancellationToken cancellationToken);

        /// <summary>
        /// Validates the experiment against schema and functional requirements of the system.
        /// </summary>
        /// <param name="experiment">The experiment to validate.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> with a status of 200/OK if the experiment is valid or 400/Bad Request if not.
        /// </returns>
        Task<HttpResponseMessage> ValidateExperimentAsync(Experiment experiment, CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to get the context for an experiment.
        /// </summary>
        /// <param name="experimentId">The ID of the experiment.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the set of <see cref="ExperimentStepInstance"/>
        /// definitions.
        /// </returns>
        Task<IEnumerable<EnvironmentEntity>> GetExperimentResourcesAsync(
            string experimentId,
            CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve an execution goal from the system
        /// </summary>
        /// <param name="executionGoalId">The id of the execution goal</param>
        /// <param name="teamName">Name of the team that owns the execution goal</param>
        /// <param name="view">The view format of the execution goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="GoalBasedSchedule"/>.
        /// </returns>        
        Task<HttpResponseMessage> GetExecutionGoalsAsync(CancellationToken cancellationToken, string teamName = null, string executionGoalId = null, ExecutionGoalView view = ExecutionGoalView.Full);

        /// <summary>
        /// Deletes an execution goal from the system
        /// </summary>
        /// <param name="executionGoalId">The id of the execution goal</param>
        /// <param name="teamName">Name of the team that owns the execution goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/>.
        /// </returns>        
        Task<HttpResponseMessage> DeleteExecutionGoalAsync(string executionGoalId, string teamName, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes an execution goal from the system
        /// </summary>
        /// <param name="executionGoalTemplateId">The id of the execution goal</param>
        /// <param name="teamName">Name of the team that owns the execution goal</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/>.
        /// </returns>        
        Task<HttpResponseMessage> DeleteExecutionGoalTemplateAsync(string executionGoalTemplateId, string teamName, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves a list execution goal template metadata 
        /// </summary>
        /// <param name="teamName">Name of the team that owns the templates</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <param name="view"></param>
        /// <param name="templateId"></param>
        /// <returns></returns>
        Task<HttpResponseMessage> GetTemplatesAsync(CancellationToken cancellationToken, string teamName = null, View view = View.Full, string templateId = null);

        /// <summary>
        /// Makes an API request to get Experiment Definition List.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="ExperimentTemplateInfo"/> created.
        /// </returns>
        Task<HttpResponseMessage> GetProvidersListAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to update an execution goal.
        /// </summary>
        /// <param name="executionGoal">The execution goal to update.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="GoalBasedSchedule"/> created.
        /// </returns>
        Task<HttpResponseMessage> UpdateExecutionGoalAsync(Item<GoalBasedSchedule> executionGoal, CancellationToken cancellationToken);

        /// <summary>
        /// Makes an API request to update an execution goal template.
        /// </summary>
        /// <param name="executionGoalTemplate">The execution goal template to update.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>
        /// An <see cref="HttpResponseMessage"/> containing the <see cref="GoalBasedSchedule"/> created.
        /// </returns>
        Task<HttpResponseMessage> UpdateExecutionGoalTemplateAsync(Item<GoalBasedSchedule> executionGoalTemplate, CancellationToken cancellationToken);
    }
}
