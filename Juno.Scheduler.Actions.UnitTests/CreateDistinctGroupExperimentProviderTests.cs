namespace Juno.Scheduler.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class CreateDistinctGroupExperimentProviderTests
    {
        private EnvironmentQuery containerOne;
        private EnvironmentQuery containerTwo;
        private Fixture mockFixture;
        private Mock<IExperimentClient> mockClient;
        private ServiceCollection mockServices;
        private CreateDistinctGroupExperimentProvider provider;
        private ScheduleAction mockComponent;
        private ScheduleContext mockContext;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupEnvironmentSelectionMocks();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockClient = new Mock<IExperimentClient>();
            this.mockServices = new ServiceCollection();
            this.mockServices.AddSingleton<IExperimentClient>(this.mockClient.Object);
            this.provider = new CreateDistinctGroupExperimentProvider(this.mockServices);
            this.containerOne = this.mockFixture.Create<EnvironmentQuery>();
            this.containerTwo = this.mockFixture.Create<EnvironmentQuery>();
            this.mockComponent = this.mockFixture.Create<ScheduleAction>();
            this.mockComponent.Parameters.Add("experimentTemplateFile", "file");
            this.mockContext = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), new Mock<IConfiguration>().Object);
        }

        [Test]
        public void ExecuteAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteActionAsync(null, this.mockContext, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteActionAsync(this.mockComponent, null, CancellationToken.None));
        }

        [Test]
        public void ExecuteAsyncPostsCorrectQueriesToExperimentClient()
        {
            this.mockComponent.Parameters.Add("nodeListA", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("nodeListB", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));

            this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Callback<EnvironmentQuery, CancellationToken>((query, token) =>
                {
                    Assert.IsTrue(this.containerOne.Equals(query) || this.containerTwo.Equals(query));
                })
                .Returns(Task.FromResult(this.GenerateHttpResponse()));

            _ = this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            this.mockClient.Verify(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public void ExecuteAsyncThrowsExceptionWhenAnyRequestIsNotSuccessStatusCode()
        {
            this.mockComponent.Parameters.Add("nodeListA", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("nodeListB", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));

            this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

            ExecutionResult result = this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(result.Status, ExecutionStatus.Failed);
            Assert.IsInstanceOf(typeof(SchedulerException), result.Error);
        }

        [Test]
        public void ExecuteAsyncCreatesCorrectParameterKeysForExperimentTemplate()
        {
            this.mockComponent.Parameters.Add("nodeListA", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("nodeListB", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));

            this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.GenerateHttpResponse()));

            _ = this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(this.mockComponent.Parameters.ContainsKey("nodeListA"));
            Assert.IsTrue(this.mockComponent.Parameters.ContainsKey("nodeListB"));
            Assert.IsTrue(this.mockComponent.Parameters.ContainsKey("subscription"));
            Assert.IsTrue(this.mockComponent.Parameters.ContainsKey("vmSku"));
        }

        [Test]
        public void ExecuteAsyncCreatesCorrectParmaeterValeusForExperimentTemplate()
        {
            this.mockComponent.Parameters.Add("nodeListA", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("nodeListB", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));

            this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.GenerateHttpResponse()));

            _ = this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            IDictionary<string, IConvertible> dict = this.mockComponent.Parameters;
            Assert.AreEqual(dict["nodeListA"], "node1,node2");
            Assert.AreEqual(dict["nodeListB"], "node1,node2");
            Assert.AreEqual(dict["subscription"], "sub1");
            Assert.AreEqual(dict["vmSku"], "vm1,vm2");
        }

        [Test]
        public void ExecuteAsyncReturnsExpectedResultWhenNoNodesSupportSameVm()
        {
            IEnumerable<EnvironmentCandidate> candidates = new List<EnvironmentCandidate>()
            {
                new EnvironmentCandidate("sub1", vmSku: new List<string>() { "vm1", "vm2" }, node: "node1"),
                new EnvironmentCandidate("sub1", vmSku: new List<string>() { "vm3", "vm4" }, node: "node2")
            };

            this.mockComponent.Parameters.Add("nodeListA", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("nodeListB", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));

            this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.GenerateHttpResponse(candidates)));

            ExecutionResult result = this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsInstanceOf(typeof(SchedulerException), result.Error);
        }

        [Test]
        public void ValidateParametersThrowsExceptionWhenComponentDoesNotHaveCorrectParameters()
        {
            ExecutionResult result = this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsInstanceOf<SchemaException>(result.Error);
        }

        [Test]
        public void ValidateParametersThrowsExceptionWhenJunoParameterIsWrongType()
        {
            JunoParameter invalidParameter = new JunoParameter(typeof(string).FullName, "not an Environment Query :(");
            this.mockComponent.Parameters.Add("nodeListA", invalidParameter);

            ExecutionResult result = this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(ExecutionStatus.Failed, result.Status);
            Assert.IsInstanceOf<SchemaException>(result.Error);
        }

        [Test]
        public void ExecuteActionAsyncCopiesNodeListWhenRequested()
        {
            this.mockComponent.Parameters.Add("nodeListA", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("nodeListB", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("mergeLists", true);

            this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.GenerateHttpResponse()));

            _ = this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None).GetAwaiter().GetResult();

            IDictionary<string, IConvertible> dict = this.mockComponent.Parameters;
            Assert.IsTrue(dict.ContainsKey("nodeList"));
            Assert.AreEqual("node1,node2,node1,node2", dict["nodeList"]);
        }

        private HttpResponseMessage GenerateHttpResponse(IEnumerable<EnvironmentCandidate> candidates = null)
        {
            candidates = candidates ?? new List<EnvironmentCandidate>() 
            { 
                new EnvironmentCandidate("sub1", vmSku: new List<string>() { "vm1", "vm2" }, node: "node1"), 
                new EnvironmentCandidate("sub1", vmSku: new List<string>() { "vm1", "vm2" }, node: "node2")
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonConvert.SerializeObject(candidates)) };
        }
    }
}
