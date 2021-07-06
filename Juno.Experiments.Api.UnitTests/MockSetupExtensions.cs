namespace Juno.Experiments.Api
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Rest;
    using Moq;
    using Moq.Language;
    using Moq.Language.Flow;

    /// <summary>
    /// Extension methods to help ease the setup of mocks/mock behaviors used
    /// by tests within the project.
    /// </summary>
    internal static class MockSetupExtensions
    {
        /// <summary>
        /// Setup mock call (HTTP GET) to Execution API to get an experiment
        /// instance/document.
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupGetExperiment(this Mock<IRestClient> apiRestClient, string experimentId)
        {
            return apiRestClient.Setup(client => client.GetAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/experiments/{experimentId}"),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        /// <summary>
        /// Setup mock call (HTTP GET) to Execution API to get experiment execution
        /// steps
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupGetExperimentSteps(this Mock<IRestClient> apiRestClient, string experimentId)
        {
            return apiRestClient.Setup(client => client.GetAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/experiments/{experimentId}/steps"),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        /// <summary>
        /// Setup mock call (HTTP GET) to Execution API to get experiment
        /// template
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupGetExperimentTemplate(this Mock<IRestClient> apiRestClient, string teamName, string experimentTemplateId)
        {
            ////It.IsAny<Uri>(),
            return apiRestClient.Setup(client => client.GetAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == ($"/api/experimentTemplates/{teamName}/{experimentTemplateId}")),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        /// <summary>
        /// Setup mock call (HTTP GET) to Execution API to get experiment
        /// templates
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupGetExperimentTemplates(this Mock<IRestClient> apiRestClient, string teamName)
        {
            return apiRestClient.Setup(client => client.GetAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/experimentTemplates/{teamName}"),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        /// <summary>
        /// Setup mock call (HTTP POST) to Execution API to create an experiment
        /// instance/document.
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupPostExperiment(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.PostAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == "/api/experiments"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Setup mock call (HTTP POST) to Execution API to create an experiment context
        /// document.
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupPostExperimentContext(this Mock<IRestClient> apiRestClient, string experimentId)
        {
            return apiRestClient.Setup(client => client.PostAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/experiments/{experimentId}/context"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Setup mock call (HTTP GET) to Execution API to get experiment history
        /// template
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupGetExperimentHistory(this Mock<IRestClient> apiRestClient, string experimentHistoryId)
        {
            ////It.IsAny<Uri>(),
            return apiRestClient.Setup(client => client.GetAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == ($"/api/experiments/histories/{experimentHistoryId}")),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        /// <summary>
        /// Setup mock call (HTTP GET) to Execution API to get experiment history list
        /// templates
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupGetExperimentHistories(this Mock<IRestClient> apiRestClient, string partitionKey)
        {
            return apiRestClient.Setup(client => client.GetAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/experiments/histories/list/{partitionKey}"),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        /// <summary>
        /// Setup mock call (HTTP GET) to Execution API to get user setting
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupGetPortalUserSetting(this Mock<IRestClient> apiRestClient, string documentId)
        {
            return apiRestClient.Setup(client => client.GetAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/userSetting/{documentId}"),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        /// <summary>
        /// Setup mock call (HTTP POST) to Execution API to create experiment steps.
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupPostExperimentSteps(this Mock<IRestClient> apiRestClient, string experimentId)
        {
            return apiRestClient.Setup(client => client.PostAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/experiments/{experimentId}/steps"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Setup mock call (HTTP POST) to Execution API to create an experiment template
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupPostExperimentTemplate(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.PostAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == "/api/experimentTemplates"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Setup mock call (HTTP POST) to Execution API to create experiment steps.
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupPostNotification(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.PostAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/notifications"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Setup mock call (HTTP POST) to Execution API to create an experiment history
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupPostExperimentHistory(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.PostAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == "/api/experiments/histories"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Setup mock call (HTTP POST) to Execution API to create an user setting
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupPostPortalUserSetting(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.PostAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == "/api/userSetting"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupGetExecutionGoal(this Mock<IRestClient> apiRestClient, string executionGoalId, string teamName, ExecutionGoalView view = ExecutionGoalView.Full)
        {
            return apiRestClient.Setup(client => client.GetAsync(
                It.Is<Uri>(uri => uri.PathAndQuery == $"/api/executionGoals?teamName={teamName}&executionGoalId={executionGoalId}&view={view}"),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupGetExecutionGoals(this Mock<IRestClient> apiRestClient, string teamName, ExecutionGoalView view = ExecutionGoalView.Full)
        {
            return apiRestClient.Setup(client => client.GetAsync(
                It.Is<Uri>(uri => uri.PathAndQuery == $"/api/executionGoals?teamName={teamName}&view={view}"),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupGetExecutionGoalTemplate(this Mock<IRestClient> apiRestClient, View view = View.Full, string teamName = null, string templateId = null)
        {
            string expectedPath = string.Empty;

            if (teamName == null)
            {
                expectedPath = templateId == null
                    ? $"/api/executionGoalTemplates/?view={view}"
                    : $"/api/executionGoalTemplates/?teamName={teamName}&templateId={templateId}&view={view}";
            }
            else
            {
                expectedPath = templateId == null
                    ? $"/api/executionGoalTemplates/?teamName={teamName}&view={view}"
                    : $"/api/executionGoalTemplates/?teamName={teamName}&templateId={templateId}&view={view}";
            }

            return apiRestClient.Setup(client => client.GetAsync(
                It.Is<Uri>(uri => uri.PathAndQuery == expectedPath),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupGetEnvironmentCandidates(this Mock<IRestClient> client)
        {
            return client.Setup(cl => cl.PostAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == "/api/environments"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetUpPutExperimentHistory(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.PutAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/experiments/histories"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetUpPostExecutionGoal(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.PostAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/executionGoals"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetUpPostExecutionGoalTemplate(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.PostAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/executionGoalTemplates"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetUpPutExecutionGoalTemplate(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.PutAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/executionGoalTemplates"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetUpPutExperimentTemplate(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.PutAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/experimentTemplates"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetUpPutPortalUserSetting(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.PutAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/userSetting"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetUpPostExecutionGoal(this Mock<IRestClient> apiRestClient, string templateId)
        {
            return apiRestClient.Setup(client => client.PostAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/executionGoals/{templateId}"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupPutExecutionGoal(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.PutAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/executionGoals"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupPutExecutionGoalFromTemplate(this Mock<IRestClient> apiRestClient, string templateId, string executionGoalId, string teamName)
        {
            return apiRestClient.Setup(client => client.PutAsync(
                It.Is<Uri>(uri => uri.PathAndQuery == $"/api/executionGoals/{templateId}?teamName={teamName}&executionGoalId={executionGoalId}"),
                It.IsAny<HttpContent>(),
                It.IsAny<CancellationToken>()));
        }

        /// <summary>
        /// Create a mock <see cref="HttpResponseMessage"/> with the status code and content (as JSON).
        /// </summary>
        public static HttpResponseMessage ToHttpResponse(this object content, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content.ToJson())
            };
        }

        /// <summary>
        /// Setup mock call (HTTP POST) to Execution API to create an experiment context
        /// document.
        /// </summary>
        public static ISetup<IRestClient, Task<HttpResponseMessage>> SetupGetExperimentsSummaries(this Mock<IRestClient> apiRestClient)
        {
            return apiRestClient.Setup(client => client.GetAsync(
                It.Is<Uri>(uri => uri.PathAndQuery == $"/api/experimentSummary/"),
                It.IsAny<CancellationToken>(),
                It.IsAny<HttpCompletionOption>()));
        }
    }
}