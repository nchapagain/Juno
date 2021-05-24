namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.Threading;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SystemManagerTests
    {
        private ISystemManager testManager;

        [SetUp]
        public void SetUp()
        {
            // Due to nature of the ISystemManager, Windows and Linux could not be tested at the same time.
            this.testManager = SystemManagerFactory.Get();
        }

        [Test]
        public void WindowsSystemManagerCanGetUptime()
        {
            TimeSpan uptime1 = this.testManager.GetUptime();
            Thread.Sleep(100);
            TimeSpan uptime2 = this.testManager.GetUptime();
            Assert.IsTrue(uptime2 > uptime1);
        }

        [Test]
        public void WindowsSystemManagerCanDetermineIfElevated()
        {
            // bool can't be null, consider it passed if the method doesn't throw.
            bool elevated = this.testManager.IsRunningAsAdministrator();
        }
    }
}
