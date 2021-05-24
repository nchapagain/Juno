namespace Juno
{
    using System;
    using System.Reflection;
    using Juno.Contracts;
    using Juno.Providers;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;

    /// <summary>
    /// Common dependencies for Precondition and ScheduleAction Tests.
    /// </summary>
    public class GoalProviderFixture : FixtureDependencies
    {
        /// <summary>
        /// Initializes <see cref="GoalProviderFixture"/> for a specific provider
        /// </summary>
        /// <param name="providerType"></param>
        public GoalProviderFixture(Type providerType)
        {
            this.Initialize(providerType);
        }

        /// <summary>
        /// Mock ScheduleActionComponent
        /// </summary>
        public ScheduleAction ScheduleActionComponent { get; set; }

        /// <summary>
        /// Mock PreconditionComponent
        /// </summary>
        public Precondition PreconditionComponent { get; set; }

        /// <summary>
        /// Mock Data Client Provider
        /// </summary>
        public Mock<IProviderDataClient> DataClient { get; set; }

        private void Initialize(Type providerType)
        {
            this.SetUpGoalBasedScheduleMocks();
            this.PreconditionComponent = FixtureExtensions.CreatePreconditionComponent(providerType);
            this.ScheduleActionComponent = FixtureExtensions.CreateScheduleActionComponent(providerType);

            this.DataClient = new Mock<IProviderDataClient>();
            this.Services = new ServiceCollection();
            this.Services.AddSingleton(this.DataClient.Object);
        }
    }
}
