namespace Juno.Execution.AgentRuntime.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Juno.Execution.AgentRuntime.Contract;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.KeyVault.Models;
    using Moq;
    using NuGet.ContentModel;
    using NuGet.Packaging;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class SsdReaderWmicTests
    {
        private static readonly string[] Attributes = { "FirmwareRevision", "Model", "SerialNumber" }; 
        private SsdReaderWmic reader;
        private Mock<IProcessExecution> mockExecution;

        [OneTimeSetUp]
        public void SetupTests()
        {
            this.mockExecution = new Mock<IProcessExecution>();
            this.reader = new SsdReaderWmic(this.mockExecution.Object);
        }

        [SetUp]
        public void SetupDefaultMockBehavior()
        {
            ProcessExecutionResult result = new ProcessExecutionResult() { ExitCode = 0 };
            result.Output.AddRange(SsdReaderWmicTests.Attributes.Select(a => SsdReaderWmicTests.GenerateWmicLine(a, Guid.NewGuid().ToString())));
            this.mockExecution.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(result);
        }

        [Test]
        public void ReadPostsCorrectParametersToExecuteProcess()
        {
            ProcessExecutionResult result = new ProcessExecutionResult() { ExitCode = 0 };
            result.Output.AddRange(SsdReaderWmicTests.Attributes.Select(a => SsdReaderWmicTests.GenerateWmicLine(a, Guid.NewGuid().ToString())));
            this.mockExecution.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Callback<string, string, TimeSpan?, string, bool>((cmd, args, timeout, cwd, redirect) =>
                {
                    Assert.AreEqual("wmic", cmd);
                    Assert.AreEqual("diskdrive GET FirmwareRevision, Model, SerialNumber /VALUE", args);
                    Assert.IsNull(cwd);
                    Assert.IsNull(timeout);
                })
                .ReturnsAsync(result);

            IEnumerable<SsdWmicInfo> info = this.reader.Read();
            Assert.IsNotNull(info);

            this.mockExecution.Verify(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void ReadReturnsExpectedResultWhenOneSsdIsFound()
        {
            string expectedFirmware = "firmware";
            string expectedModel = "model";
            string expectedSerialNumber = "serialNumber";
            ProcessExecutionResult result = new ProcessExecutionResult() { ExitCode = 0 };
            result.Output.AddRange(new string[]
                {
                    SsdReaderWmicTests.GenerateWmicLine(SsdReaderWmicTests.Attributes[0], expectedFirmware), 
                    SsdReaderWmicTests.GenerateWmicLine(SsdReaderWmicTests.Attributes[1], expectedModel),
                    SsdReaderWmicTests.GenerateWmicLine(SsdReaderWmicTests.Attributes[2], expectedSerialNumber)
                });
            this.mockExecution.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(result);
            SsdWmicInfo expectedInfo = new SsdWmicInfo(expectedFirmware, expectedModel, expectedSerialNumber);
            IEnumerable<SsdWmicInfo> actualInfo = this.reader.Read();
            Assert.IsNotEmpty(actualInfo);
            Assert.AreEqual(expectedInfo, actualInfo.First());
        }

        [Test]
        public void ReadReturnsExpectedResultWhenMoreThanOneSsdAreFound()
        {
            string expectedFirmware = "firmware";
            string expectedModel = "model";
            string expectedSerialNumber = "serialNumber";
            ProcessExecutionResult result = new ProcessExecutionResult() { ExitCode = 0 };
            result.Output.AddRange(new string[]
                {
                    SsdReaderWmicTests.GenerateWmicLine(SsdReaderWmicTests.Attributes[0], expectedFirmware),
                    SsdReaderWmicTests.GenerateWmicLine(SsdReaderWmicTests.Attributes[1], expectedModel),
                    SsdReaderWmicTests.GenerateWmicLine(SsdReaderWmicTests.Attributes[2], expectedSerialNumber),
                    SsdReaderWmicTests.GenerateWmicLine(SsdReaderWmicTests.Attributes[0], $"{expectedFirmware}2"),
                    SsdReaderWmicTests.GenerateWmicLine(SsdReaderWmicTests.Attributes[1], $"{expectedModel}2"),
                    SsdReaderWmicTests.GenerateWmicLine(SsdReaderWmicTests.Attributes[2], $"{expectedSerialNumber}2")
                });

            this.mockExecution.Setup(e => e.ExecuteProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(result);
            SsdWmicInfo expectedInfo = new SsdWmicInfo(expectedFirmware, expectedModel, expectedSerialNumber);
            SsdWmicInfo expectedInfo2 = new SsdWmicInfo($"{expectedFirmware}2", $"{expectedModel}2", $"{expectedSerialNumber}2");
            List<SsdWmicInfo> actualInfo = this.reader.Read().ToList();
            Assert.AreEqual(2, actualInfo.Count);
            Assert.AreEqual(expectedInfo, actualInfo[0]);
            Assert.AreEqual(expectedInfo2, actualInfo[1]);
        }

        private static string GenerateWmicLine(string key, string value)
        {
            return string.Concat(key, "=", value);
        }
    }
}
