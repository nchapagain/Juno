namespace Juno.Execution.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
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
    using NUnit.Framework;

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
        private Mock<IExperimentTemplateDataManager> mockExperimentTemplateDataManager;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks();
            this.mockDependencies = new FixtureDependencies();
            this.mockExperimentTemplateDataManager = new Mock<IExperimentTemplateDataManager>();
            this.controller = new ExperimentTemplatesController(this.mockExperimentTemplateDataManager.Object, NullLogger.Instance);

            this.mockExperiment = this.mockFixture.Create<Experiment>();
            this.mockExperimentItem = this.mockFixture.Create<ExperimentItem>();

            // Setup the default return values for the controller experiment data manager. Given there are
            // no custom setups in the test methods, the data manager will return the following values by
            // default.
            this.mockDependencies.DataManager.SetReturnsDefault(Task.FromResult(this.mockExperimentItem));
        }

        [Test]
        public void ControllerContructorsValidateParameters()
        {
            Assert.Throws<ArgumentException>(() => new ExperimentTemplatesController(null));
        }

        [Test]
        public void ControllerConstructorSetsParametersToExpectedValues()
        {
            ExperimentTemplatesController controller = new ExperimentTemplatesController(this.mockExperimentTemplateDataManager.Object);
            EqualityAssert.PropertySet(controller, "DataManager", this.mockExperimentTemplateDataManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", NullLogger.Instance);

            controller = new ExperimentTemplatesController(this.mockExperimentTemplateDataManager.Object, this.mockDependencies.Logger.Object);

            EqualityAssert.PropertySet(controller, "DataManager", this.mockExperimentTemplateDataManager.Object);
            EqualityAssert.PropertySet(controller, "Logger", this.mockDependencies.Logger.Object);
        }

        [Test]
        public async Task ControllerValidatesTheSchemaOfAnExperimentBeforeCreation()
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
        public async Task ControllerCreatesTheExpectedExperimentTemplate()
        {
            ExperimentValidation.Instance.Clear();

            this.mockExperimentTemplateDataManager.Setup(mgr => mgr.CreateExperimentTemplateAsync(It.IsAny<ExperimentItem>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExperimentItem));

            CreatedAtActionResult result = await this.controller.CreateExperimentTemplateAsync(this.mockExperiment, CancellationToken.None)
                as CreatedAtActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status201Created, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentItem, result.Value));
        }

        [Test]
        public async Task ControllerDeletesTheExpectedExperimentTemplate()
        {
            await this.controller.DeleteExperimentTemplateAsync(this.mockExperimentItem.Id, Uri.EscapeUriString(this.mockExperimentItem.Definition.Metadata["teamName"].ToString()), CancellationToken.None);
            this.mockExperimentTemplateDataManager.Verify(mgr => mgr.DeleteExperimentTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ControllerGetsTheExpectedExperimentTemplate()
        {
            await this.controller.GetExperimentTemplateAsync(this.mockExperimentItem.Id, this.mockExperimentItem.Definition.Metadata["teamName"].ToString(), CancellationToken.None);

            this.mockExperimentTemplateDataManager.Verify(mgr => mgr.GetExperimentTemplateAsync(
                this.mockExperimentItem.Id,
                this.mockExperimentItem.Definition.Metadata["teamName"].ToString(),
                It.IsAny<CancellationToken>(),
                null));
        }

        [Test]
        public async Task ControllerGetsTheExpectedExperimentTemplates()
        {
            var cosmosResult = this.mockExperimentItem;

            this.mockExperimentTemplateDataManager.Setup(mgr => mgr.GetExperimentTemplatesListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<ExperimentItem>() { cosmosResult } as IEnumerable<ExperimentItem>));

            ObjectResult result = await this.controller.GetExperimentTemplatesListAsync(this.mockExperimentItem.Definition.Metadata["teamName"].ToString(), CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentItem, (result.Value as List<ExperimentItem>)[0]));
        }

        [Test]
        public async Task ControllerReturnsTheExpectedResultWhenAnExecutionGoalItemIsUpdatedSuccessfully()
        {
            this.mockExperimentTemplateDataManager.Setup(mgr => mgr.CreateExperimentTemplateAsync(It.IsAny<ExperimentItem>(), It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.mockExperimentItem));

            ObjectResult result = await this.controller.UpdateExperimentTemplateAsync(this.mockExperimentItem, CancellationToken.None)
                as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(StatusCodes.Status200OK, result.StatusCode);
            Assert.IsTrue(object.ReferenceEquals(this.mockExperimentItem, result.Value));
        }
    }
}
