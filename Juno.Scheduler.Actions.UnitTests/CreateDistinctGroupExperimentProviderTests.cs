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
    using Juno.DataManagement;
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
        private Mock<IExperimentTemplateDataManager> mockDataManager;
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
            this.mockDataManager = new Mock<IExperimentTemplateDataManager>();
            this.mockServices = new ServiceCollection();
            this.mockServices.AddSingleton<IExperimentClient>(this.mockClient.Object);
            this.mockServices.AddSingleton<IExperimentTemplateDataManager>(this.mockDataManager.Object);
            this.provider = new CreateDistinctGroupExperimentProvider(this.mockServices);
            this.containerOne = this.mockFixture.Create<EnvironmentQuery>();
            this.containerTwo = this.mockFixture.Create<EnvironmentQuery>();
            this.mockComponent = this.mockFixture.Create<ScheduleAction>();
            this.mockComponent.Parameters.Add("experimentTemplateFile", "file");
            this.mockContext = new ScheduleContext(this.mockFixture.Create<GoalBasedSchedule>(), this.mockFixture.Create<TargetGoalTrigger>(), new Mock<IConfiguration>().Object);

            this.mockClient.Setup(client => client.CreateExperimentFromTemplateAsync(It.IsAny<ExperimentTemplate>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(this.CreateResponseMessage(HttpStatusCode.OK, this.mockFixture.Create<ExperimentItem>()));
        }

        [Test]
        public void ExecuteAsyncValidatesParameters()
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteActionAsync(null, this.mockContext, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentException>(() => this.provider.ExecuteActionAsync(this.mockComponent, null, CancellationToken.None));
        }

        [Test]
        public async Task ExecuteAsyncPostsCorrectQueriesToExperimentClient()
        {
            this.mockComponent.Parameters.Add("nodeListA", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("nodeListB", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));

            this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Callback<EnvironmentQuery, CancellationToken>((query, token) =>
                {
                    Assert.IsTrue(this.containerOne.Equals(query) || this.containerTwo.Equals(query));
                })
                .Returns(Task.FromResult(this.CreateResponseMessage(HttpStatusCode.OK)));

            await this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None);

            this.mockClient.Verify(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public async Task ExecuteAsyncThrowsExceptionWhenAnyRequestIsNotSuccessStatusCode()
        {
            this.mockComponent.Parameters.Add("nodeListA", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("nodeListB", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));

            this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

            Assert.ThrowsAsync<SchedulerException>(() => this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None));
        }

        [Test]
        public async Task ExecuteAsyncCreatesCorrectParameterKeysForExperimentTemplate()
        {
            this.mockComponent.Parameters.Add("nodeListA", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("nodeListB", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));

            this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.CreateResponseMessage(HttpStatusCode.OK)));

            await this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None);

            Assert.IsTrue(this.mockComponent.Parameters.ContainsKey("nodeListA"));
            Assert.IsTrue(this.mockComponent.Parameters.ContainsKey("nodeListB"));
            Assert.IsTrue(this.mockComponent.Parameters.ContainsKey("subscription"));
            Assert.IsTrue(this.mockComponent.Parameters.ContainsKey("vmSku"));
        }

        [Test]
        public async Task ExecuteAsyncCreatesCorrectParmaeterValeusForExperimentTemplate()
        {
            this.mockComponent.Parameters.Add("nodeListA", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("nodeListB", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));

            this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.CreateResponseMessage(HttpStatusCode.OK)));

            await this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None);

            IDictionary<string, IConvertible> dict = this.mockComponent.Parameters;
            Assert.AreEqual(dict["nodeListA"], "node1,node2");
            Assert.AreEqual(dict["nodeListB"], "node1,node2");
            Assert.AreEqual(dict["subscription"], "sub1");
            Assert.AreEqual(dict["vmSku"], "vm1,vm2");
        }

        [Test]
        public void ValidateParameterThrowsExceptionWhenComponentDoesNotHaveCorrectParameters()
        {
            Assert.ThrowsAsync<SchemaException>(() => this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None));
        }

        [Test]
        public void ValidateParametersDoesThrowsExceptionWhenJunoParameterIsWrongType()
        {
            JunoParameter invalidParameter = new JunoParameter(typeof(string).FullName, "not an Environment Query :(");
            this.mockComponent.Parameters.Add("nodeListA", invalidParameter);

            Assert.ThrowsAsync<SchemaException>(() => this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None));
        }

        [Test]
        public async Task ExecuteActionAsyncCopiesNodeListWhenRequested()
        {
            this.mockComponent.Parameters.Add("nodeListA", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("nodeListB", new JunoParameter(typeof(EnvironmentQuery).FullName, this.containerOne));
            this.mockComponent.Parameters.Add("mergeLists", true);

            this.mockClient.Setup(client => client.ReserveEnvironmentsAsync(It.IsAny<EnvironmentQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.CreateResponseMessage(HttpStatusCode.OK)));

            await this.provider.ExecuteActionAsync(this.mockComponent, this.mockContext, CancellationToken.None);

            IDictionary<string, IConvertible> dict = this.mockComponent.Parameters;
            Assert.IsTrue(dict.ContainsKey("nodeList"));
            Assert.AreEqual("node1,node2", dict["nodeList"]);
        }

        private HttpResponseMessage CreateResponseMessage(HttpStatusCode code, object candidates = null)
        {
            candidates = candidates ?? new List<EnvironmentCandidate>() 
            { 
                new EnvironmentCandidate("sub1", vmSku: new List<string>() { "vm1", "vm2" }, node: "node1"), 
                new EnvironmentCandidate("sub1", vmSku: new List<string>() { "vm1", "vm2" }, node: "node2")
            };

            return new HttpResponseMessage(code)
            { Content = new StringContent(JsonConvert.SerializeObject(candidates)) };
        }
    }
}
