namespace Juno.Scheduler.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.DataManagement;
    using Juno.EnvironmentSelection.ClusterSelectionFilters;
    using Juno.EnvironmentSelection.NodeSelectionFilters;
    using Juno.Providers;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NuGet.Protocol;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SelectEnvironmentAndCreateExperimentProviderTests
    {
        private static string environmentQuery = "environmentQuery";
        private static string experimentTemplateFile = "experimentTemplateFile";
        private static string vmSku = "vmSku";

        private IScheduleActionProvider provider;
        private Mock<IExperimentClient> mockClient;
        private IServiceCollection mockServices;
        private Fixture mockFixture;
        private ScheduleContext mockContext;
        private ScheduleAction mockAction;
        private EnvironmentQuery mockQuery;

        [SetUp]
        public void SetupTests()
        {
            this.mockClient = new Mock<IExperimentClient>();
            this.mockServices = new ServiceCollection();
            this.mockServices.AddSingleton(this.mockClient.Object);
            this.provider = new SelectEnvironmentAndCreateExperimentProvider(this.mockServices);

            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockFixture.SetupEnvironmentSelectionMocks();

            this.mockContext = new ScheduleContext(
                this.mockFixture.Create<GoalBasedSchedule>(), 
                this.mockFixture.Create<TargetGoalTrigger>(), 
                new Mock<IConfiguration>().Object);

            this.mockQuery = this.mockFixture.Create<EnvironmentQuery>();

            this.mockAction = new ScheduleAction(typeof(SelectEnvironmentAndCreateExperimentProvider).FullName, new Dictionary<string, IConvertible>()
            { 
                [SelectEnvironmentAndCreateExperimentProviderTests.environmentQuery] = new JunoParameter(typeof(EnvironmentQuery).FullName, this.mockQuery),
                [SelectEnvironmentAndCreateExperimentProviderTests.experimentTemplateFile] = "experimentTemplateFile",
                [SelectEnvironmentAndCreateExperimentProviderTests.vmSku] = "Standard_A3"
            });
        }

        [Test]
        public void ExecuteActionAsyncValidatesParameters()
        { 
            ScheduleAction component = this.mockFixture.Create<ScheduleAction>();
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteActionAsync(null, this.mockContext, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteActionAsync(component, null, CancellationToken.None));
        }

        [Test]
        public void ExecuteActionPostsExpectedEnvironmentFilters()
        {
            IEnumerable<EnvironmentFilter> expectedFilters = new List<EnvironmentFilter>()
            { 
                new EnvironmentFilter(typeof(HealthyNodeProvider).FullName, new Dictionary<string, IConvertible>()
                { 
                    ["includeRegion"] = "useast2, uswest"
                }),
                new EnvironmentFilter(typeof(VmSkuFilterProvider).FullName, new Dictionary<string, IConvertible>()
                { 
                    ["includeVmSku"] = "Standard_D2s_v3, Standard_D4s_v3",
                    ["includeRegion"] = "useast2, uswest"
                })
            };

            EnvironmentQuery query = new EnvironmentQuery("name", 6, expectedFilters);
            JunoParameter container = new JunoParameter(typeof(EnvironmentQuery).FullName, query);
            this.mockAction.Parameters[SelectEnvironmentAndCreateExperimentProviderTests.environmentQuery] = container;

            var returnContent = new List<EnvironmentCandidate>() { this.mockFixture.Create<EnvironmentCandidate>(), this.mockFixture.Create<EnvironmentCandidate>() };

            using (HttpResponseMessage message = SelectEnvironmentAndCreateExperimentProviderTests.CreateResponseMessage(HttpStatusCode.OK, returnContent))
            {
                this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                    .Callback<EnvironmentQuery, CancellationToken>((filters, token) =>
                    {
                        Assert.AreEqual(query, filters);
                    })
                    .Returns(Task.FromResult(message));

                ExecutionResult result = this.provider.ExecuteActionAsync(this.mockAction, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

                this.mockClient.Verify(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()), Times.Once());
            }
        }

        [Test]
        public void ExecuteActionReturnsExpectedResultWhenStatusCodeIsNotOK()
        {
            EnvironmentQuery query = this.mockFixture.Create<EnvironmentQuery>();

            JunoParameter container = new JunoParameter(typeof(EnvironmentQuery).FullName, query);
            this.mockAction.Parameters[SelectEnvironmentAndCreateExperimentProviderTests.environmentQuery] = container;
            this.mockAction.Parameters.Remove("vmSku");

            using (HttpResponseMessage response = SelectEnvironmentAndCreateExperimentProviderTests.CreateResponseMessage(HttpStatusCode.BadRequest, new ProblemDetails()))
            {
                this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(response));

                ExecutionResult result = this.provider.ExecuteActionAsync(this.mockAction, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

                Assert.IsNotNull(result);
                Assert.AreEqual(ExecutionStatus.Failed, result.Status);
                Assert.IsInstanceOf(typeof(SchedulerException), result.Error);
            }
        }

        [Test]
        public void ExecuteActionCopiesCorrectParameterToFilterParameters()
        {
            KeyValuePair<string, IConvertible> queryParameter = new KeyValuePair<string, IConvertible>("query.vmSku", "Stadndard_D2_v3");
            this.mockAction.Parameters.Add(queryParameter.Key, queryParameter.Value);

            EnvironmentQuery query = this.mockFixture.Create<EnvironmentQuery>();
            JunoParameter container = new JunoParameter(typeof(EnvironmentQuery).FullName, query);
            this.mockAction.Parameters[SelectEnvironmentAndCreateExperimentProviderTests.environmentQuery] = container;

            var returnContent = new List<EnvironmentCandidate>() { this.mockFixture.Create<EnvironmentCandidate>(), this.mockFixture.Create<EnvironmentCandidate>() };
            using (HttpResponseMessage response = SelectEnvironmentAndCreateExperimentProviderTests.CreateResponseMessage(HttpStatusCode.OK, returnContent))
            {
                this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(response));

                ExecutionResult result = this.provider.ExecuteActionAsync(this.mockAction, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();
            }

            Assert.IsTrue(query.Parameters.ContainsKey("vmSku"));
            Assert.AreEqual("Stadndard_D2_v3", query.Parameters["vmSku"]);
        }

        [Test]
        public void ExecuteActionUnionsAllGivenVmskusFromEnvironmentsApi()
        {
            ScheduleAction action = new ScheduleAction(this.mockAction.Type, this.mockAction.Parameters);
            EnvironmentQuery query = this.mockFixture.Create<EnvironmentQuery>();
            JunoParameter container = new JunoParameter(typeof(EnvironmentQuery).FullName, query);
            action.Parameters[SelectEnvironmentAndCreateExperimentProviderTests.environmentQuery] = container;
            action.Parameters.Remove("vmSku");

            List<string> expectedVmSkuHalf = new List<string>() { "vm1", "vm2" };
            List<string> expectedVmSkuOtherHalf = new List<string> { "vm3", "vm4" };
            EnvironmentCandidate candidate1 = new EnvironmentCandidate("sub1", node: "node1", vmSku: expectedVmSkuHalf);
            EnvironmentCandidate candidate2 = new EnvironmentCandidate("sub1", node: "node1", vmSku: expectedVmSkuOtherHalf);

            var returnContent = new List<EnvironmentCandidate>() { candidate1, candidate2 };
            using (HttpResponseMessage response = SelectEnvironmentAndCreateExperimentProviderTests.CreateResponseMessage(HttpStatusCode.OK, returnContent))
            {
                this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(response));

                ExecutionResult result = this.provider.ExecuteActionAsync(action, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();
            }

            Assert.IsTrue(action.Parameters.ContainsKey("vmSku"));
            CollectionAssert.AreEquivalent(expectedVmSkuHalf.Union(expectedVmSkuOtherHalf), action.Parameters["vmSku"].ToString().ToList(',', ';'));
        }

        [Test]
        public void ExecuteActionReplacesStarsWithRegionSearchSpace()
        {
            EnvironmentQuery query = this.mockFixture.Create<EnvironmentQuery>();
            JunoParameter container = new JunoParameter(typeof(EnvironmentQuery).FullName, query);
            this.mockAction.Parameters[SelectEnvironmentAndCreateExperimentProviderTests.environmentQuery] = container;

            var returnContent = new List<EnvironmentCandidate>() { this.mockFixture.Create<EnvironmentCandidate>(), this.mockFixture.Create<EnvironmentCandidate>() };
            using (HttpResponseMessage response = SelectEnvironmentAndCreateExperimentProviderTests.CreateResponseMessage(HttpStatusCode.OK, returnContent))
            {
                this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(response));

                ExecutionResult result = this.provider.ExecuteActionAsync(this.mockAction, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();
            }

            foreach (EnvironmentFilter filter in query.Filters)
            {
                if (filter.Parameters.ContainsKey("includeRegion") && !filter.Parameters["includeRegion"].Equals("*"))
                {
                    Assert.IsTrue(filter.Parameters.ContainsKey("includeRegion"));
                    Assert.AreEqual("$.subscription.regionSearchSpace", filter.Parameters["includeRegion"]);
                }
            }
        }

        [Test]
        public void ExecuteActionCheckForStarsInsteadOfRegionSearchSpace()
        {
            EnvironmentQuery query = this.mockFixture.Create<EnvironmentQuery>();
            JunoParameter container = new JunoParameter(typeof(EnvironmentQuery).FullName, query);
            this.mockAction.Parameters[SelectEnvironmentAndCreateExperimentProviderTests.environmentQuery] = container;

            var returnContent = new List<EnvironmentCandidate>() { this.mockFixture.Create<EnvironmentCandidate>(), this.mockFixture.Create<EnvironmentCandidate>() };
            using (HttpResponseMessage response = SelectEnvironmentAndCreateExperimentProviderTests.CreateResponseMessage(HttpStatusCode.OK, returnContent))
            {
                this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(response));

                ExecutionResult result = this.provider.ExecuteActionAsync(this.mockAction, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();
            }

            string value = string.Empty;

            foreach (EnvironmentFilter filter in query.Filters)
            {
                if (filter.Parameters.ContainsKey("includeRegion") && filter.Parameters["includeRegion"].Equals("*"))
                {
                    value = filter.Parameters["includeRegion"].ToString();
                }
            }

            Assert.AreNotEqual("*", value);
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
