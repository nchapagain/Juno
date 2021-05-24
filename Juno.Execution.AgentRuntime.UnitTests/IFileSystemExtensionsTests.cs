namespace Juno.Execution.AgentRuntime
{
    using System;
    using System.IO;
    using System.IO.Abstractions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class IFileSystemExtensionsTests
    {
        private Mock<IDirectory> directory;

        [OneTimeSetUp]
        public void SetupTests()
        {
            this.directory = new Mock<IDirectory>();
        }

        [Test]
        public void GetFileValidatesNonStringParameters()
        {
            IDirectory system = null;
            Assert.Throws<ArgumentException>(() => system.GetFile("service", "file"));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void GetFileValidatesStringParmaters(string invalidParam)
        {
            Assert.Throws<ArgumentException>(() => FileSystemExtensions.GetFile(this.directory.Object, invalidParam, "notinvalid"));
            Assert.Throws<ArgumentException>(() => FileSystemExtensions.GetFile(this.directory.Object, "notinvalid", invalidParam));
        }

        [Test]
        public void GetFileThrowsErrorWhenNoDirectoryWasFound()
        {
            this.directory.Setup(d => d.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Returns(Array.Empty<string>());

            Assert.Throws<FileNotFoundException>(() => FileSystemExtensions.GetFile(this.directory.Object, "Source", "file"));
        }

        [Test]
        public void GetFileThrowsErrorWhenNoDirectoryContainsPfServiceString()
        {
            this.directory.Setup(d => d.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Returns(new string[] { @"C:\App\NotTheRightPfService\" });

            Assert.Throws<FileNotFoundException>(() => FileSystemExtensions.GetFile(this.directory.Object, "Source", "file"));
        }

        [Test]
        public void GetFileThrowwsErrorWhenNoDirectoryContainsScenarioString()
        {
            string pfService = "JunoPfService";
            this.directory.Setup(d => d.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Returns(new string[] { $@"C:\App\{pfService}\downgrade" });

            Assert.Throws<FileNotFoundException>(() => FileSystemExtensions.GetFile(this.directory.Object, pfService, "file", "upgrade"));
        }

        [Test]
        public void GetFileReturnsExpectedResultWhenNoScenarioIsGiven()
        {
            string pfService = "JunoPfService";
            string path = $@"C:\App\{pfService}\downgrade\file.txt";
            this.directory.Setup(d => d.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Returns(new string[] { path });

            string actualpath = FileSystemExtensions.GetFile(this.directory.Object, pfService, "file.txt");

            Assert.AreEqual(path, actualpath);
        }

        [Test]
        public void GetFileReturnsExpectedResultWhenScenarioIsGivenAndPresent()
        {
            string pfService = "JunoPfService";
            string scenario = "downgrade";
            string path = $@"C:\App\{pfService}\{scenario}\file.txt";
            this.directory.Setup(d => d.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Returns(new string[] { path });

            string actualpath = FileSystemExtensions.GetFile(this.directory.Object, pfService, "file.txt", scenario);

            Assert.AreEqual(path, actualpath);
        }

        [Test]
        public void GetFilePostsToDifferentRootDirectoryIfSupplied()
        {
            string otherDrive = "D:\\";
            this.directory.Setup(d => d.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Returns(new string[] { "mypath\\service" })
                .Callback<string, string, SearchOption>((drive, file, option) =>
                {
                    Assert.AreEqual(otherDrive, drive);
                });

            _ = FileSystemExtensions.GetFile(this.directory.Object, "service", "file.txt", drive: otherDrive);
        }
    }
}
