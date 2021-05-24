namespace Juno.Experiments.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Api.Client;
    using Juno.Contracts;
    using Juno.Contracts.Validation;
    using Juno.DataManagement;
    using Juno.Providers.Workloads;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Repository;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using NuGet.Protocol;
    using NUnit.Framework;
    using Polly;
    using CosmosTable = Microsoft.Azure.Cosmos.Table;
    using Storage = Microsoft.Azure.Storage;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentsTemplatesControllerTests
    {
        private ExperimentTemplatesController controller;
        private Fixture mockFixture;
        private FixtureDependencies mockDependencies;
        private Experiment mockExperiment;
        private ExperimentItem mockExperimentItem;
        private ExecutionClient mockExecutionClient;
        private Mock<IExperimentTemplateDataManager> mockExperimentTemplateDataManager;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockDependencies = new FixtureDependencies();
            this.mockExperimentTemplateDataManager = new Mock<IExperimentTemplateDataManager>();
            this.mockExecutionClient = new ExecutionClient(this.mockDependencies.RestClient.Object, new Uri("https://any/uri"), Policy.NoOpAsync());
            this.controller = new ExperimentTemplatesController(this.mockExecutionClient, NullLogger.Instance);

            this.mockExperiment = this.mockFixture.Create<Experiment>();
            this.mockExperimentItem = this.mockFixture.Create<ExperimentItem>();

            // Setup the default return values for the controller experiment template data manager. Given there are
            // no custom setups in the test methods, the data manager will return the following values by
            // default.
            this.mockDependencies.DataManager.SetReturnsDefault(Task.FromResult(this.mockExperimentItem));
        }

        [Test]
        public void COntrollerConstructorsValidateParameters()
        {
            Assert.Throws<ArgumentException>(() => new ExperimentTemplatesController(null, null));
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            ExperimentTemplatesController controller = new ExperimentTemplatesController(this.mockExecutionClient);
            EqualityAssert.PropertySet(controller, "Client", this.mockExecutionClient);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);

            controller = new ExperimentTemplatesController(this.mockExecutionClient, this.mockDependencies.Logger.Object);
            EqualityAssert.PropertySet(controller, "Client", this.mockExecutionClient);
            EqualityAssert.PropertySet(controller, "Logger", this.mockDependencies.Logger.Object);
        }

        [Test]
        public async Task ControllerValidatesTheExperimentTemplateBeforeCreation()
        {
            Mock<IValidationRule<Experiment>> mockValidation = new Mock<IValidationRule<Experiment>>();
            mockValidation.Setup(validation => validation.Validate(It.IsAny<Experiment>()))
                .Returns(new ValidationResult(false));

            ExperimentValidation.Instance.Add(mockValidation.Object);
            ObjectResult result = await this.controller.CreateExperimentTemplateAsync(this.mockExperiment, CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExperimentIsCreatedSuccessfully()
        {
            this.mockDependencies.RestClient.SetupPostExperimentTemplate()
                .Returns(Task.FromResult(this.mockExperimentItem.ToHttpResponse()));

            CreatedAtActionResult result = await this.controller.CreateExperimentTemplateAsync(this.mockExperiment, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.AreEqual(this.mockExperimentItem.Id, (result.Value as ExperimentItem).Id);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenGettingAnExperimentTemplate()
        {
            this.mockDependencies.RestClient.SetupGetExperimentTemplate(Uri.EscapeUriString(this.mockExperimentItem.Definition.Metadata["teamName"].ToString()), this.mockExperimentItem.Id)
               .Returns(Task.FromResult(this.mockExperimentItem.ToHttpResponse()));

            ObjectResult result = await this.controller.GetExperimentTemplateAsync(Uri.EscapeUriString(this.mockExperimentItem.Definition.Metadata["teamName"].ToString()), this.mockExperimentItem.Id, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.AreEqual(this.mockExperimentItem.Id, (result.Value as ExperimentItem).Id);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedReponseWhenGettingExperimentTemplates()
        {
            this.mockDependencies.RestClient.SetupGetExperimentTemplates(Uri.EscapeUriString(this.mockExperimentItem.Definition.Metadata["teamName"].ToString()))
                .Returns(Task.FromResult((new List<ExperimentItem>() { this.mockExperimentItem } as IEnumerable<ExperimentItem>).ToHttpResponse()));

            ObjectResult result = await this.controller.GetExperimentTemplatesListAsync(Uri.EscapeUriString(this.mockExperimentItem.Definition.Metadata["teamName"].ToString()), CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenUpdatingExperimentTemplate()
        {
            this.mockDependencies.RestClient.SetUpPutExperimentTemplate()
                .Returns(Task.FromResult(this.mockExperimentItem.ToHttpResponse()));

            ObjectResult result = await this.controller.UpdateExperimentTemplateAsync(this.mockExperimentItem, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.AreEqual(this.mockExperimentItem, result.Value);
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResponseWhenDeletingAnExperimentTemplate()
        {
            this.mockDependencies.RestClient.Setup(client => client.DeleteAsync(
                It.Is<Uri>(uri => uri.AbsolutePath == $"/api/experimentTemplates/{this.mockExperimentItem.Definition.Metadata["teamName"].ToString()}/{this.mockExperimentItem.Id}"),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

            StatusCodeResult result = await this.controller.DeleteExperimentTemplateAsync(this.mockExperimentItem.Definition.Metadata["teamName"].ToString(), this.mockExperimentItem.Id, CancellationToken.None)
                as StatusCodeResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status204NoContent, result.StatusCode);
        }
    }
}
