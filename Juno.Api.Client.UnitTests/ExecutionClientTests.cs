namespace Juno.Execution.Client
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Rest;
    using Moq;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class ExecutionClientTests
    {
        private Fixture mockFixture;
        private ExecutionClient executionClient;
        private Mock<IRestClient> mockRestClient;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockFixture.SetUpGoalBasedScheduleMocks();

            this.mockRestClient = new Mock<IRestClient>();
            this.executionClient = new ExecutionClient(
                this.mockRestClient.Object,
                new Uri("https://anyjunoenvironment.execution"),
                Policy.NoOpAsync());
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToCreateAnExperiment()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals("/api/experiments"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.CreateExperimentAsync(this.mockFixture.Create<Experiment>(), CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToCreateAnExperimentContext()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string experimentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{experimentId}/context"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.CreateExperimentContextAsync(
                    experimentId,
                    this.mockFixture.Create<ExperimentMetadata>(),
                    CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToCreateExperimentSteps()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string experimentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{experimentId}/steps"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.CreateExperimentStepsAsync(experimentId, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToCreateExperimentStepsGivenADefinition()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string experimentId = Guid.NewGuid().ToString();
                int sequence = 0;
                ExperimentComponent definition = this.mockFixture.Create<ExperimentComponent>();

                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/experiments/{experimentId}/steps?sequence={sequence}"));
                        Assert.IsNotNull(content);
                        Assert.IsTrue(definition.Equals(content.ReadAsStringAsync().GetAwaiter().GetResult().FromJson<ExperimentComponent>()));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.CreateExperimentStepsAsync(experimentId, sequence, definition, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToCreateAnExperimentAgentStep()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.Created))
            {
                ExperimentStepInstance parentStep = this.mockFixture.Create<ExperimentStepInstance>();
                ExperimentComponent stepDefinition = this.mockFixture.Create<ExperimentComponent>();

                string agentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/experiments/{parentStep.ExperimentId}/agent-steps?agentId={agentId}&parentStepId={parentStep.Id}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.CreateExperimentAgentStepsAsync(parentStep, stepDefinition, agentId, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToCreateANotice()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string expectedQueueName = "anyqueue";

                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/notifications?workQueue={expectedQueueName}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.CreateNoticeAsync(expectedQueueName, this.mockFixture.Create<ExperimentMetadata>(), CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToCreateANoticeWithAnExplicitVisibilityDelay()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string expectedQueueName = "anyqueue";
                int expectedVisibilityDelay = 20;

                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/notifications?workQueue={expectedQueueName}&visibilityDelay={expectedVisibilityDelay}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.CreateNoticeAsync(expectedQueueName, this.mockFixture.Create<ExperimentMetadata>(), CancellationToken.None, TimeSpan.FromSeconds(expectedVisibilityDelay))
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToCreateAnExecutionGoal()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                GoalBasedSchedule executionGoal = this.mockFixture.Create<GoalBasedSchedule>();
                Item<GoalBasedSchedule> executionGoalItem = new Item<GoalBasedSchedule>(executionGoal.ExecutionGoalId, executionGoal);

                string teamName = executionGoal.TeamName;
                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/executionGoals"));
                    })
                    .Returns(Task.FromResult(response));
                this.executionClient.CreateExecutionGoalAsync(executionGoalItem, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToCreateAnExecutionGoalFromTemplate()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                GoalBasedSchedule executionGoal = this.mockFixture.Create<GoalBasedSchedule>();
                string teamName = executionGoal.TeamName;
                string templateId = executionGoal.ExecutionGoalId;
                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoals/{templateId}?teamName={teamName}"));
                    })
                    .Returns(Task.FromResult(response));
                
                ExecutionGoalSummary executionGoalMetadata = this.mockFixture.Create<ExecutionGoalSummary>();
                ExecutionGoalParameter executionGoalParameters = new ExecutionGoalParameter(templateId, executionGoal.ExperimentName, executionGoalMetadata.ParameterNames.Owner, executionGoal.Enabled, executionGoalMetadata.ParameterNames.TargetGoals, executionGoal.Parameters);
                this.executionClient.CreateExecutionGoalFromTemplateAsync(executionGoalParameters, templateId, executionGoal.TeamName, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToUpdateExecutionGoalFromTemplate()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                GoalBasedSchedule executionGoal = this.mockFixture.Create<GoalBasedSchedule>();
                string teamName = executionGoal.TeamName;
                string templateId = executionGoal.ExecutionGoalId;
                this.mockRestClient.Setup(client => client.PutAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoals/{templateId}?teamName={teamName}"));
                    })
                    .Returns(Task.FromResult(response));

                ExecutionGoalSummary executionGoalMetadata = this.mockFixture.Create<ExecutionGoalSummary>();
                ExecutionGoalParameter executionGoalParameters = new ExecutionGoalParameter(templateId, executionGoal.ExperimentName, executionGoalMetadata.ParameterNames.Owner, executionGoal.Enabled, executionGoalMetadata.ParameterNames.TargetGoals, executionGoal.Parameters);
                this.executionClient.UpdateExecutionGoalFromTemplateAsync(executionGoalParameters, templateId, executionGoal.TeamName, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToCreateAnExecutionGoalWhenTeamNameHasSpaces()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                GoalBasedSchedule executionGoal = FixtureExtensions.CreateExecutionGoalFromTemplate(teamName: "Team Name With Spaces");
                Item<GoalBasedSchedule> executionGoalItem = new Item<GoalBasedSchedule>(executionGoal.ExecutionGoalId, executionGoal);
                string teamName = Uri.EscapeUriString(executionGoal.TeamName);
                this.mockRestClient.Setup(client => client.PutAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoals?teamName={teamName}"));
                    })
                    .Returns(Task.FromResult(response));
                this.executionClient.CreateExecutionGoalAsync(executionGoalItem, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToDeleteAgentStepsForAnExperiment()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                string experimentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.DeleteAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, CancellationToken>((uri, token) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{experimentId}/agent-steps"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.DeleteExperimentAgentStepsAsync(experimentId, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToDeleteAnExperiment()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string experimentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.DeleteAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, CancellationToken>((uri, token) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{experimentId}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.DeleteExperimentAsync(experimentId, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToDeleteAnExperimentContext()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                string experimentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.DeleteAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, CancellationToken>((uri, token) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{experimentId}/context"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.DeleteExperimentContextAsync(experimentId, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToDeleteExperimentSteps()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                string experimentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.DeleteAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, CancellationToken>((uri, token) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{experimentId}/steps"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.DeleteExperimentStepsAsync(experimentId, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToDeleteExecutionGoal()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                string executionGoalId = Guid.NewGuid().ToString();
                string teamName = "teamName";

                this.mockRestClient.Setup(client => client.DeleteAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, CancellationToken>((uri, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoals/{executionGoalId}?teamName={teamName}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.DeleteExecutionGoalAsync(executionGoalId, teamName, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToDeleteExecutionGoalTemplate()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                string executionGoalId = Guid.NewGuid().ToString();
                string teamName = "teamName";

                this.mockRestClient.Setup(client => client.DeleteAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, CancellationToken>((uri, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoalTemplates/{executionGoalId}/?teamName={teamName}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.DeleteExecutionGoalTemplateAsync(executionGoalId, teamName, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExperimentAgentSteps()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string experimentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, option) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{experimentId}/agent-steps"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExperimentAgentStepsAsync(experimentId, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExperimentAgentStepsForAGivenAParentStep()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string experimentId = Guid.NewGuid().ToString();
                string parentStepId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, option) =>
                    {
                        string encodedFilter = HttpUtility.UrlEncode($"(ParentStepId eq '{parentStepId}')");
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/experiments/{experimentId}/agent-steps?filter={encodedFilter}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExperimentAgentStepsAsync(experimentId, CancellationToken.None, parentStepId: parentStepId)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExperimentAgentStepsForAGivenASetOfStatuses()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string experimentId = Guid.NewGuid().ToString();
                List<ExecutionStatus> statuses = new List<ExecutionStatus>(Enum.GetValues(typeof(ExecutionStatus)) as IEnumerable<ExecutionStatus>);

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, option) =>
                    {
                        string encodedFilter = HttpUtility.UrlEncode(
                            "(Status eq 'Pending') or (Status eq 'InProgress') or (Status eq 'InProgressContinue') or (Status eq 'Succeeded') or (Status eq 'Failed') or (Status eq 'Cancelled') or (Status eq 'SystemCancelled')");

                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/experiments/{experimentId}/agent-steps?filter={encodedFilter}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExperimentAgentStepsAsync(
                    experimentId,
                    CancellationToken.None,
                    status: statuses)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetAgentStepsForAGivenParentStepIdAndSetOfStatuses()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string experimentId = Guid.NewGuid().ToString();
                string parentStepId = Guid.NewGuid().ToString();
                List<ExecutionStatus> statuses = new List<ExecutionStatus>
                {
                    ExecutionStatus.InProgress,
                    ExecutionStatus.InProgressContinue
                };

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, option) =>
                    {
                        string encodedFilter = HttpUtility.UrlEncode($"(ParentStepId eq '{parentStepId}')" +
                        $" and ((Status eq 'InProgress') or (Status eq 'InProgressContinue'))");

                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/experiments/{experimentId}/agent-steps?filter={encodedFilter}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExperimentAgentStepsAsync(
                    experimentId,
                    CancellationToken.None,
                    parentStepId: parentStepId,
                    status: statuses)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetAnExperiment()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string experimentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, option) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{experimentId}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExperimentAsync(experimentId, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetAnExperimentContext()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                string experimentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, option) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{experimentId}/context"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExperimentContextAsync(experimentId, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExperimentStatuses()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string experimentName = "Any_Experiment";

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, option) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experimentstatus/{experimentName}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExperimentInstanceStatusesAsync(experimentName, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExperimentSteps()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                string experimentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, option) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{experimentId}/steps"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExperimentStepsAsync(experimentId, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExperimentStepsForASetOfStatuses()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string experimentId = Guid.NewGuid().ToString();
                List<ExecutionStatus> statuses = new List<ExecutionStatus>
                {
                    ExecutionStatus.InProgress,
                    ExecutionStatus.InProgressContinue
                };

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, token, option) =>
                    {
                        string encodedFilter = HttpUtility.UrlEncode("(Status eq 'InProgress') or (Status eq 'InProgressContinue')");
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/experiments/{experimentId}/steps?filter={encodedFilter}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExperimentStepsAsync(
                    experimentId,
                    CancellationToken.None,
                    status: statuses)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetANotice()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string expectedQueueName = "anyqueue";
                string expectedHideDuration = "300";

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/notifications?workQueue={expectedQueueName}&visibilityDelay={expectedHideDuration}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetNoticeAsync(expectedQueueName, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetAnExecutionGoal()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string executionGoalId = Guid.NewGuid().ToString();
                string teamName = "teamName";
                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoals?teamName={teamName}&executionGoalId={executionGoalId}&view={ExecutionGoalView.Full}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None, teamName, executionGoalId)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoals()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string executionGoalId = Guid.NewGuid().ToString();
                string teamName = "teamName";
                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoals?teamName={teamName}&view={ExecutionGoalView.Full}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None, teamName)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWithoutTeamName()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoals?view=Full"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWithoutViewAndReturnsFalse()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsFalse(uri.PathAndQuery.Equals($"/api/executionGoals"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWhenExecutionGoalViewIsTypeSummary()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        string apiRoute = $"/api/executionGoals?view=Summary";
                        Assert.IsTrue(uri.PathAndQuery.Equals(apiRoute));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None, null, null, ExecutionGoalView.Summary)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWithoutTeamNameWithIdAndReturnsFalse()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string executionGoalId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsFalse(uri.PathAndQuery.Equals($"/api/executionGoals?executionGoalId={executionGoalId}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWithoutTeamNameWithIdViewStatusAndReturnsFalse()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string executionGoalId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsFalse(uri.PathAndQuery.Equals($"/api/executionGoals?executionGoalId={executionGoalId}&view=Status"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWithoutTeamNameWithIdViewTimelineAndReturnsFalse()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string executionGoalId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsFalse(uri.PathAndQuery.Equals($"/api/executionGoals?executionGoalId={executionGoalId}&view=Timeline"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWithoutTeamNameWithIdViewSummaryAndReturnsFalse()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string executionGoalId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsFalse(uri.PathAndQuery.Equals($"/api/executionGoals?executionGoalId={executionGoalId}&view=Summary"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWithoutTeamNameAndIdViewTimelineAndReturnsFalse()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsFalse(uri.PathAndQuery.Equals($"/api/executionGoals?view=Timeline"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWithoutTeamNameAndIdViewStatusAndReturnsFalse()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsFalse(uri.PathAndQuery.Equals($"/api/executionGoals?view=Status"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWithoutIdWithTeamNameViewTimelineAndReturnsFalse()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string teamName = "teamName";
                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsFalse(uri.PathAndQuery.Equals($"/api/teamName={teamName}&executionGoals?view=Timeline"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWithoutIdWithTeamNameViewStatusAndReturnsFalse()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string teamName = "teamName";
                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsFalse(uri.PathAndQuery.Equals($"/api/teamName={teamName}&executionGoals?view=Status"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWithTeamNameWhenExecutionGoalViewIsTypeFull()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string teamName = "teamName";

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        string apiRoute = $"/api/executionGoals?teamName={teamName}&view=Full";
                        Assert.IsTrue(uri.PathAndQuery.Equals(apiRoute));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None, teamName, null, ExecutionGoalView.Full)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalsWithTeamNameWhenExecutionGoalViewIsTypeSummary()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string teamName = "teamName";

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        string apiRoute = $"/api/executionGoals?teamName={teamName}&view=Summary";
                        Assert.IsTrue(uri.PathAndQuery.Equals(apiRoute));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None, teamName, null, ExecutionGoalView.Summary)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalWhenExecutionGoalViewIsTypeFull()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string executionGoalId = Guid.NewGuid().ToString();
                string teamName = "teamName";

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        string apiRoute = $"/api/executionGoals?teamName={teamName}&executionGoalId={executionGoalId}&view=Full";
                        Assert.IsTrue(uri.PathAndQuery.Equals(apiRoute));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None, teamName, executionGoalId, ExecutionGoalView.Full)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalWhenExecutionGoalViewIsTypeEmpty()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string executionGoalId = Guid.NewGuid().ToString();
                string teamName = "teamName";

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        string apiRoute = $"/api/executionGoals?teamName={teamName}&executionGoalId={executionGoalId}&view=Full";
                        Assert.IsTrue(uri.PathAndQuery.Equals(apiRoute));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None, teamName, executionGoalId)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalWhenExecutionGoalViewIsTypeStatus()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string executionGoalId = Guid.NewGuid().ToString();
                string teamName = "teamName";

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        string apiRoute = $"/api/executionGoals?teamName={teamName}&executionGoalId={executionGoalId}&view=Status";
                        Assert.IsTrue(uri.PathAndQuery.Equals(apiRoute));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None, teamName, executionGoalId, ExecutionGoalView.Status)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalWhenExecutionGoalViewIsTypeSummary()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string executionGoalId = Guid.NewGuid().ToString();
                string teamName = "teamName";

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        string apiRoute = $"/api/executionGoals?teamName={teamName}&executionGoalId={executionGoalId}&view=Summary";
                        Assert.IsTrue(uri.PathAndQuery.Equals(apiRoute));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None, teamName, executionGoalId, ExecutionGoalView.Summary)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalWhenExecutionGoalViewIsTypeTimeline()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string executionGoalId = Guid.NewGuid().ToString();
                string teamName = "teamName";

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        string apiRoute = $"/api/executionGoals?teamName={teamName}&executionGoalId={executionGoalId}&view=Timeline";
                        Assert.IsTrue(uri.PathAndQuery.Equals(apiRoute));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalsAsync(CancellationToken.None, teamName, executionGoalId, ExecutionGoalView.Timeline)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalTemplateMetadata()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string teamName = "teamName";
                string templateId = "templateId";
                this.mockRestClient.Setup(client => client.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoalTemplates/?teamName={teamName}&templateId={templateId}&view=Summary"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalTemplatesAsync(CancellationToken.None, teamName, templateId, View.Summary)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalTemplates()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string teamName = "teamName";
                this.mockRestClient.Setup(client => client.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoalTemplates/?teamName={teamName}&view=Full"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalTemplatesAsync(CancellationToken.None, teamName)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalTemplate()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string teamName = "teamName";
                string id = Guid.NewGuid().ToString();
                this.mockRestClient.Setup(client => client.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoalTemplates/?teamName={teamName}&templateId={id}&view=Full"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalTemplatesAsync(CancellationToken.None, teamName, templateId: id)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToGetExecutionGoalTemplateMetadataWithId()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string teamName = "teamName";
                string id = Guid.NewGuid().ToString();
                this.mockRestClient.Setup(client => client.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        string route = $"/api/executionGoalTemplates/?teamName={teamName}&templateId={id}&view=Summary";
                        Assert.IsTrue(uri.PathAndQuery.Equals(route));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.GetExecutionGoalTemplatesAsync(CancellationToken.None, teamName, id, View.Summary)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToDeleteANotice()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string expectedQueueName = "anyqueue";
                string expectedMessageId = "anyId";
                string expectedPopReceipt = "anyReceipt";

                this.mockRestClient.Setup(client => client.GetAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<HttpCompletionOption>()))
                    .Callback<Uri, CancellationToken, HttpCompletionOption>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/notifications?workQueue={expectedQueueName}&messageId={expectedMessageId}&popReceipt={expectedPopReceipt}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.DeleteNoticeAsync(expectedQueueName, expectedMessageId, expectedPopReceipt, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToSetNoticeVisibility()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.OK))
            {
                string expectedQueueName = "anyqueue";
                string expectedMessageId = "anyId";
                string expectedPopReceipt = "anyReceipt";
                TimeSpan expectedDuration = TimeSpan.FromMinutes(2);

                this.mockRestClient.Setup(client => client.PutAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/notifications?workQueue={expectedQueueName}&messageId={expectedMessageId}&popReceipt={expectedPopReceipt}&visibilityDelay={expectedDuration.TotalSeconds}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.SetNoticeVisibilityAsync(expectedQueueName, expectedMessageId, expectedPopReceipt, expectedDuration, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToUpdateAnExperiment()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                ExperimentInstance experiment = this.mockFixture.Create<ExperimentInstance>();

                this.mockRestClient.Setup(client => client.PutAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{experiment.Id}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.UpdateExperimentAsync(experiment, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToUpdateAnExperimentContext()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                string experimentId = Guid.NewGuid().ToString();

                this.mockRestClient.Setup(client => client.PutAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{experimentId}/context"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.UpdateExperimentContextAsync(
                    experimentId,
                    this.mockFixture.Create<ExperimentMetadataInstance>(),
                    CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToUpdateAnExperimentStep()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                ExperimentStepInstance step = this.mockFixture.Create<ExperimentStepInstance>();

                this.mockRestClient.Setup(client => client.PutAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{step.ExperimentId}/steps/{step.Id}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.UpdateExperimentStepAsync(step, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheExpectedApiToUpdateAnExperimentAgentStep()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                ExperimentStepInstance template = this.mockFixture.Create<ExperimentStepInstance>();
                ExperimentStepInstance step = new ExperimentStepInstance(
                    template.Id,
                    template.ExperimentId,
                    template.ExperimentGroup,
                    template.StepType,
                    template.Status,
                    template.Sequence,
                    template.Attempts,
                    template.Definition,
                    agentId: "AnyAgent",
                    parentStepId: "AnyParentStep");

                this.mockRestClient.Setup(client => client.PutAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.AbsolutePath.Equals($"/api/experiments/{step.ExperimentId}/agent-steps/{step.Id}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.UpdateExperimentAgentStepAsync(step, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecutionClientCallsTheEpxectedApiToUpdateAnExecutionGoal()
        {
            using (HttpResponseMessage response = ExecutionClientTests.CreateResponseMessage(HttpStatusCode.NoContent))
            {
                GoalBasedSchedule executionGoal = this.mockFixture.Create<GoalBasedSchedule>();
                Item<GoalBasedSchedule> executionGoalItem = new Item<GoalBasedSchedule>(executionGoal.ExecutionGoalId, executionGoal);

                this.mockRestClient.Setup(client => client.PostAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<HttpContent>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<Uri, HttpContent, CancellationToken>((uri, content, token) =>
                    {
                        Assert.IsTrue(uri.PathAndQuery.Equals($"/api/executionGoals?teamName={executionGoal.TeamName}"));
                    })
                    .Returns(Task.FromResult(response));

                this.executionClient.UpdateExecutionGoalAsync(executionGoalItem, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }

        private static HttpResponseMessage CreateResponseMessage(HttpStatusCode expectedStatusCode, object expectedContent = null)
        {
            HttpResponseMessage mockResponse = new HttpResponseMessage(expectedStatusCode);

            if (expectedContent != null)
            {
                mockResponse.Content = new StringContent(expectedContent.ToJson());
            }

            return mockResponse;
        }
    }
}