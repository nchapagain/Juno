namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class GoalComponentProviderFactoryTests
    {
        private Fixture mockFixture;
        private IServiceCollection mockServices;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockServices = new ServiceCollection();
        }

        [Test]
        public void FactoryValidatesRequiredPreconditionParameters()
        {
            Precondition component = this.mockFixture.Create<Precondition>();

            Assert.Throws<ArgumentException>(
                () => GoalComponentProviderFactory.CreatePreconditionProvider(null, this.mockServices));

            Assert.Throws<ArgumentException>(
                () => GoalComponentProviderFactory.CreatePreconditionProvider(component, null));
        }

        [Test]
        public void FactoryValidatesRequiredScheduleActionParameters()
        {
            ScheduleAction component = this.mockFixture.Create<ScheduleAction>();

            Assert.Throws<ArgumentException>(
                () => GoalComponentProviderFactory.CreateScheduleActionProvider(null, this.mockServices));

            Assert.Throws<ArgumentException>(
                () => GoalComponentProviderFactory.CreateScheduleActionProvider(component, null));
        }

        [Test]
        public void FactoryCreatesTheExpectedPreconditionProvider()
        {
            Precondition component = new Precondition(
                typeof(TestPreconditionProvider).FullName,
                new Dictionary<string, IConvertible>());

            IPreconditionProvider provider = GoalComponentProviderFactory.CreatePreconditionProvider(component, this.mockServices);
            Assert.IsNotNull(provider);
            Assert.IsInstanceOf<TestPreconditionProvider>(provider);
        }

        [Test]
        public void FactoryCreatesTheExpectedScheduleActionProvider()
        {
            ScheduleAction component = new ScheduleAction(
                typeof(TestActionProvider).FullName,
                new Dictionary<string, IConvertible>());
            IScheduleActionProvider provider = GoalComponentProviderFactory.CreateScheduleActionProvider(component, this.mockServices);
            Assert.IsNotNull(provider);
            Assert.IsInstanceOf<TestActionProvider>(provider);
        }

        [Test]
        public void FactoryThrowsExceptionWhenPreconditionProviderDoesNotExist()
        {
            Precondition component = new Precondition(
                "Juno.This.Type.DoesNotExist",
                new Dictionary<string, IConvertible>());

            ProviderException exc = Assert.Throws<ProviderException>(
                () => GoalComponentProviderFactory.CreatePreconditionProvider(component, this.mockServices));

            Assert.AreEqual(ErrorReason.ProviderNotFound, exc.Reason);
            Assert.IsNotNull(exc.InnerException);
            Assert.IsInstanceOf<TypeLoadException>(exc.InnerException);
        }

        [Test]
        public void FactoryThrowsExceptionWhenScheduleActionProviderDoesNotExist()
        {
            ScheduleAction component = new ScheduleAction(
                "Juno.This.Type.DoesNotExist",
                new Dictionary<string, IConvertible>());

            ProviderException exc = Assert.Throws<ProviderException>(
                () => GoalComponentProviderFactory.CreateScheduleActionProvider(component, this.mockServices));

            Assert.AreEqual(ErrorReason.ProviderNotFound, exc.Reason);
            Assert.IsNotNull(exc.InnerException);
            Assert.IsInstanceOf<TypeLoadException>(exc.InnerException);
        }

        [SupportedParameter(Name = "ParameterOne", Required = true, Type = typeof(string))]
        private class TestPreconditionProvider : PreconditionProvider
        {
            public TestPreconditionProvider(IServiceCollection services)
                : base(services)
            {
            }

            public override Task ConfigureServicesAsync(GoalComponent component, ScheduleContext context)
            {
                return Task.CompletedTask;
            }

            protected override Task<bool> IsConditionSatisfiedAsync(Precondition component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        [SupportedParameter(Name = "ParameterOne", Required = true, Type = typeof(string))]
        private class TestActionProvider : ScheduleActionProvider
        {
            public TestActionProvider(IServiceCollection services)
                : base(services)
            { 
            }

            public override Task ConfigureServicesAsync(GoalComponent component, ScheduleContext context)
            {
                return Task.CompletedTask;
            }

            protected override Task<ExecutionResult> ExecuteActionAsync(ScheduleAction component, ScheduleContext scheduleContext, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
