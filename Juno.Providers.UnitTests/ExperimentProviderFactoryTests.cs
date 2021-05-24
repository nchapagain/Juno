namespace Juno.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentProviderFactoryTests
    {
        private Fixture mockFixture;
        private ServiceCollection mockServices;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupExperimentMocks(ExperimentType.AB);
            this.mockServices = new ServiceCollection();
        }

        [Test]
        public void FactoryValidatesRequiredParameters()
        {
            ExperimentComponent validComponent = this.mockFixture.Create<ExperimentComponent>();

            Assert.Throws<ArgumentException>(
                () => ExperimentProviderFactory.CreateProvider(null, this.mockServices));

            Assert.Throws<ArgumentException>(
                () => ExperimentProviderFactory.CreateProvider(validComponent, null));
        }

        [Test]
        public void FactoryCreatesTheExperimentProvider()
        {
            ExperimentComponent component = new ExperimentComponent(
                typeof(TestExecutionProvider).FullName,
                "AnyName",
                "AnyDescription");

            IExperimentProvider provider = ExperimentProviderFactory.CreateProvider(component, this.mockServices);
            Assert.IsNotNull(provider);
            Assert.IsInstanceOf<TestExecutionProvider>(provider);
        }

        [Test]
        public void FactoryThrowsWhenAnExperimentProviderDoesNotMatchTheProviderTypeSpecified()
        {
            // Provider Type = Workload
            ExperimentComponent component = new ExperimentComponent(
                typeof(TestExecutionProvider).FullName,
                "AnyName",
                "AnyDescription");

            ProviderException exc = Assert.Throws<ProviderException>(
                () => ExperimentProviderFactory.CreateProvider(component, this.mockServices, SupportedStepType.Payload));

            Assert.IsNotNull(exc);
            Assert.AreEqual(ErrorReason.ProviderDefinitionInvalid, exc.Reason);
            Assert.IsNotNull(exc.InnerException);
            Assert.IsInstanceOf<SchemaException>(exc.InnerException);
        }

        [Test]
        public void FactoryThrowsWhenAnExperimentProviderDoesNotExist()
        {
            ExperimentComponent component = new ExperimentComponent(
                "Juno.This.Type.DoesNotExist",
                "AnyName",
                "AnyDescription");

            ProviderException exc = Assert.Throws<ProviderException>(
                () => ExperimentProviderFactory.CreateProvider(component, this.mockServices));

            Assert.AreEqual(ErrorReason.ProviderNotFound, exc.Reason);
            Assert.IsNotNull(exc.InnerException);
            Assert.IsInstanceOf<TypeLoadException>(exc.InnerException);
        }

        [Test]
        public void FactoryThrowsWhenAnExperimentProviderClassDoesNotHaveRequiredConstructors()
        {
            ExperimentComponent component = new ExperimentComponent(
                typeof(TestExecutionProviderMissingConstructor).FullName,
                "AnyName",
                "AnyDescription");

            ProviderException exc = Assert.Throws<ProviderException>(
                () => ExperimentProviderFactory.CreateProvider(component, this.mockServices));

            Assert.AreEqual(ErrorReason.ProviderDefinitionInvalid, exc.Reason);
            Assert.IsNotNull(exc.InnerException);
            Assert.IsInstanceOf<MissingMethodException>(exc.InnerException);
        }

        [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteOnVirtualMachine)]
        private class TestExecutionProvider : IExperimentProvider
        {
            public TestExecutionProvider(IServiceCollection services)
            {
                this.Services = services;
            }

            public IServiceCollection Services { get; }

            public Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
            {
                return Task.CompletedTask;
            }

            public Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        [ExecutionConstraints(SupportedStepType.Workload, SupportedStepTarget.ExecuteOnVirtualMachine)]
        private class TestExecutionProviderMissingConstructor : IExperimentProvider
        {
            public TestExecutionProviderMissingConstructor()
            {
            }

            public IServiceCollection Services { get; }

            public Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
            {
                return Task.CompletedTask;
            }

            public Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        [ExecutionConstraints(SupportedStepType.EnvironmentSetup, SupportedStepTarget.ExecuteRemotely)]
        private class TestEnvironmentProvider : IExperimentProvider
        {
            public TestEnvironmentProvider(IServiceCollection services)
            {
                this.Services = services;
            }

            public IServiceCollection Services { get; }

            public Task ConfigureServicesAsync(ExperimentContext context, ExperimentComponent component)
            {
                return Task.CompletedTask;
            }

            public Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
