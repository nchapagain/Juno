namespace Juno.Execution.AgentRuntime
{
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SystemManagerFactoryTests
    {
        [Test]
        public void SystemManagerFactoryReturnsSameInstanceOfSystemManager()
        {
            ISystemManager manager1 = SystemManagerFactory.Get();
            ISystemManager manager2 = SystemManagerFactory.Get();
            Assert.AreEqual(manager1, manager2);
        }
    }
}
