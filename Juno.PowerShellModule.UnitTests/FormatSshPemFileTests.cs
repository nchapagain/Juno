namespace Juno.PowerShellModule
{
    using System.Diagnostics.CodeAnalysis;
    using System.IO.Abstractions;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Will dispose in teardown")]
    public class FormatSshPemFileTests
    {
        private TestFormatSshPemFileCmdlet cmdlet;
        private Mock<IFileSystem> mockFileSystem;
        private Mock<IFileInfo> mockFileInfo;
        private Mock<IFile> mockFile;
        private Mock<IFileInfoFactory> mockFileInfoFactory;

        [SetUp]
        public void SetupTest()
        {
            this.mockFileSystem = new Mock<IFileSystem>();
            this.mockFileInfo = new Mock<IFileInfo>();
            this.mockFile = new Mock<IFile>();
            this.mockFileInfoFactory = new Mock<IFileInfoFactory>();
            this.mockFileSystem.Setup(s => s.FileInfo).Returns(this.mockFileInfoFactory.Object);
            this.mockFileSystem.Setup(s => s.File).Returns(this.mockFile.Object);
            
            this.cmdlet = new TestFormatSshPemFileCmdlet(this.mockFileSystem.Object);
        }

        [TearDown]
        public void TearDown()
        {
            this.cmdlet.Dispose();
        }

        [Test]
        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Juno Powershell only supports Windows currently")]
        public void CmdletWritesToExpectedFileAndWritesExpectedString()
        {
            FileSecurity mockSecurity = new FileSecurity();
            mockSecurity.SetOwner(new NTAccount("Users"));
            string expectedPath = @"x:\Test.pem";
            string expectedKey = "-----BEGIN PRIVATE KEY-----\r\nVerylongString00000000000000000000000000000000000000000000000000000000000000\r\n0000000000000000000000000000000000000000000000000000000000000000000000000000\r\n0000000000000000000000000000000000000000000000VerylongString\r\n-----END PRIVATE KEY-----\r\n";

            this.mockFile.Setup(f => f.WriteAllTextAsync(expectedPath, expectedKey, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();
            this.mockFileInfoFactory.Setup(f => f.FromFileName(expectedPath)).Returns(this.mockFileInfo.Object).Verifiable();
            this.mockFileInfo.Setup(i => i.GetAccessControl(AccessControlSections.All)).Returns(mockSecurity).Verifiable();
            
            this.cmdlet.FilePath = @"x:\Test.pem";
            this.cmdlet.PrivateKey = "VerylongString0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000VerylongString";
            this.cmdlet.ProcessInternal();

            this.mockFile.Verify();
            this.mockFileInfo.Verify();
            this.mockFileInfoFactory.Verify();
        }

        private class TestFormatSshPemFileCmdlet : FormatSshPemFileCmdlet
        {
            public TestFormatSshPemFileCmdlet(IFileSystem fileSystem = null)
                : base(fileSystem)
            {
            }

            public object Results { get; set; }

            public void ProcessInternal()
            {
                this.ProcessRecord();
            }

            protected override void WriteResults(object results)
            {
                this.Results = results;
            }

            protected override void WriteResultsAsJson(object results)
            {
                this.Results = results;
            }
        }
    }
}
