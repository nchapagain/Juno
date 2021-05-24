namespace Juno.GuestAgent.Installer
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.Invocation;
    using System.CommandLine.Parsing;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Execution.AgentRuntime;
    using Juno.Execution.AgentRuntime.CommandLine;
    using Juno.Execution.NuGetIntegration;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Identity;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.KeyVault.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Rest.Azure;
    using Moq;
    using NuGet.Versioning;
    using NUnit.Framework;
    using Polly;

    [TestFixture]
    [Category("Unit")]
    public class AgentInstallationCommandTests
    {
        private FixtureDependencies mockFixture;
        private Mock<ISystemManager> mockSystemManager;
        private string[] exampleCommandLine1;
        private string[] exampleCommandLine2;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new FixtureDependencies();
            this.mockFixture.SetupCertificateMocks();
            EventContext.PersistentProperties.Clear();

            this.mockSystemManager = new Mock<ISystemManager>();

            // For the sake of good scenario testing, make sure some of these
            // arguements below have spaces in them.
            string expectedAccessToken = "\"JwtToken12345xxxx==\"";
            string expectedInstrumentationKey = "\"anInstrumentationKey\"";
            string expectedCertificateName = "\"juno-dev01-cert\"";
            string expectedClusterName = "\"cs08prdapp03e\"";
            string expectedContextId = "\"aContextId\"";
            string expectedEnvironment = "\"juno-dev10\"";
            string expectedInstallPath = "\"C:\\Install\\Path Will Do\"";
            string expectedKeyVaultUri = "\"https://aKeyVault/Certificate-url\"";
            string expectedNodeName = "\"1234-5678-9123-4567\"";
            string expectedNuGetFeed = "\"http://aNuGet/feed_url\"";
            string expectedNuGetPatSecret = "\"http://aNuGetPAT.secret/url\"";
            string expectedPackageVersion = "\"1.0.97\"";
            string expectedRegionValue = "\"South Africa North\"";
            string expectedVmSkuValue = "\"Standard_Issue_v3\"";

            this.exampleCommandLine1 = new string[]
            {
                "--environment", expectedEnvironment,
                "--agentId", $"{expectedClusterName},{expectedNodeName},anyVm,{expectedContextId}",
                "--packageVersion", expectedPackageVersion,
                "--appInsightsInstrumentationKey", expectedInstrumentationKey,
                "--keyVaultUri", expectedKeyVaultUri,
                "--certificateName", expectedCertificateName,
                "--nugetFeedUri", expectedNuGetFeed,
                "--nugetPat", expectedNuGetPatSecret,
                "--vmSku", expectedVmSkuValue,
                "--region", expectedRegionValue,
                "--installPath", expectedInstallPath,
                "--accessToken", expectedAccessToken
            };

            this.exampleCommandLine2 = new string[]
            {
                "--env", expectedEnvironment,
                "--agentId", $"{expectedClusterName},{expectedNodeName},anyVm,{expectedContextId}",
                "--packageVersion", expectedPackageVersion,
                "--instrumentationKey", expectedInstrumentationKey,
                "--keyVault", expectedKeyVaultUri,
                "--cert", expectedCertificateName,
                "--feed", expectedNuGetFeed,
                "--pat", expectedNuGetPatSecret,
                "--sku", expectedVmSkuValue,
                "--region", expectedRegionValue,
                "--installPath", expectedInstallPath,
                "--token", expectedAccessToken
            };

            this.SetupDefaultMockBehaviors();
        }

        [Test]
        [TestCase("--version")]
        public void InstallationCommandSupportsExpectedVersionArguments(string argument)
        {
            string[] args = new string[] { argument };

            CommandLineBuilder commandBuilder = AgentInstallationCommand.CreateBuilder(args, CancellationToken.None)
                .WithDefaults();

            commandBuilder.Command.Handler = CommandHandler.Create<TestAgentInstallationCommand>((handler) => handler.ExecuteAsync(args, CancellationToken.None));

            ParseResult parseResults = commandBuilder.Build().Parse(args);
            int exitCode = parseResults.Invoke();

            Assert.IsTrue(parseResults.Tokens.Count == 1);
            Assert.IsTrue(parseResults.Tokens[0].Value == argument);
            Assert.IsTrue(exitCode == 0);
        }

        [Test]
        [TestCase("--help")]
        [TestCase("-h")]
        [TestCase("/h")]
        [TestCase("-?")]
        [TestCase("/?")]
        public void InstallationCommandSupportsExpectedHelpArguments(string argument)
        {
            string[] args = new string[] { argument };

            CommandLineBuilder commandBuilder = AgentInstallationCommand.CreateBuilder(args, CancellationToken.None)
                .WithDefaults();

            commandBuilder.Command.Handler = CommandHandler.Create<TestAgentInstallationCommand>((handler) => handler.ExecuteAsync(args, CancellationToken.None));

            ParseResult parseResults = commandBuilder.Build().Parse(args);
            int exitCode = parseResults.Invoke();

            Assert.IsTrue(parseResults.Tokens.Count == 1);
            Assert.IsTrue(parseResults.Tokens[0].Value == argument);
            Assert.IsTrue(exitCode == 0);
        }

        [Test]
        public void InstallationCommandSetsExpectedPropertiesToMatchTheCommandLineArgument()
        {
            CommandLineBuilder commandBuilder = AgentInstallationCommand.CreateBuilder(this.exampleCommandLine1, CancellationToken.None)
                .WithDefaults();

            // To get a reference to the command underneath, we are adding it to the 'services' collection
            // passed to the builder method above. This allows us to get it from there so that we can verify the
            // properties are set to expected values on the command.
            commandBuilder.Command.Handler = CommandHandler.Create<TestAgentInstallationCommand>(
                (handler) => handler.ExecuteAsync(this.exampleCommandLine1, CancellationToken.None, this.mockFixture.Services));

            ParseResult parseResults = commandBuilder.Build().Parse(this.exampleCommandLine1);

            Assert.AreEqual(this.exampleCommandLine1.Length, parseResults.Tokens.Count);
            Assert.DoesNotThrow(() => parseResults.InvokeAsync().GetAwaiter().GetResult());

            // Get the command from the services collection as noted above.
            TestAgentInstallationCommand command = this.mockFixture.Services.GetService<TestAgentInstallationCommand>();
            AgentInstallationCommandTests.AssertCommandPropertiesSet(parseResults, command);
        }

        [Test]
        public void InstallationCommandSetsExpectedPropertiesToMatchTheCommandLineArguments_GuestAgentInstaller_Scenario()
        {
            /* This is the EXACT command-line scenario for the Juno Guest Agent Installer. The command line
             * below is passed to the installer on the command-line. This allows us to verify the correct handling
             * of the command line options.
             */

            CommandLineBuilder commandBuilder = AgentInstallationCommand.CreateBuilder(this.exampleCommandLine2, CancellationToken.None)
                .WithDefaults();

            // To get a reference to the command underneath, we are adding it to the 'services' collection
            // passed to the builder method above. This allows us to get it from there so that we can verify the
            // properties are set to expected values on the command.
            commandBuilder.Command.Handler = CommandHandler.Create<TestAgentInstallationCommand>(
                (handler) => handler.ExecuteAsync(this.exampleCommandLine2, CancellationToken.None, this.mockFixture.Services));

            ParseResult parseResults = commandBuilder.Build().Parse(this.exampleCommandLine2);

            Assert.AreEqual(this.exampleCommandLine2.Length, parseResults.Tokens.Count);
            Assert.DoesNotThrow(() => parseResults.InvokeAsync().GetAwaiter().GetResult());

            // Get the command from the services collection as noted above.
            TestAgentInstallationCommand command = this.mockFixture.Services.GetService<TestAgentInstallationCommand>();
            AgentInstallationCommandTests.AssertCommandPropertiesSet(parseResults, command);
        }

        [Test]
        public void InstallationCommandValidatesRequiredParameters()
        {
            string[] args = new string[]
            {
                "--accessToken", "anyAccessToken",
            };

            CommandLineBuilder commandBuilder = AgentInstallationCommand.CreateBuilder(args, CancellationToken.None)
                .WithDefaults();

            ParseResult parseResults = commandBuilder.Build().Parse(args);
            int exitCode = parseResults.Invoke();

            Assert.NotZero(parseResults.Errors.Count());
            Assert.IsTrue(exitCode > 0);
        }

        [Test]
        [TestCase(PlatformID.Win32NT)]
        [TestCase(PlatformID.Unix)]
        public Task InstallationCommandInstallsCertificatesInTheExpectedLocation(PlatformID platform)
        {
            X509Certificate2 certificate = this.mockFixture.Create<X509Certificate2>();
            string certificateBase64 = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));

            // Certificates are downloaded from a Key Vault.
            this.mockFixture.KeyVaultClient
                .Setup(kv => kv.GetSecretWithHttpMessagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AzureOperationResponse<SecretBundle> { Body = new SecretBundle(certificateBase64, contentType: "application/x-pkcs12") });
            this.mockFixture.KeyVaultClient
                .Setup(kv => kv.GetCertificateWithHttpMessagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AzureOperationResponse<CertificateBundle> { Body = new CertificateBundle() });

            // Certificates are installed in a specific store location depending upon the platform
            // (Windows vs. Linux).
            this.mockFixture.CertificateManager
                .Setup(mgr => mgr.InstallCertificateToStoreAsync(It.IsAny<X509Certificate2>(), It.IsAny<StoreName>(), It.IsAny<StoreLocation>()))
                .Callback<X509Certificate2, StoreName, StoreLocation>((cert, store, location) =>
                {
                    Assert.IsNotNull(cert);
                    Assert.AreEqual(StoreName.My, store);
                    Assert.AreEqual(platform == PlatformID.Win32NT ? StoreLocation.LocalMachine : StoreLocation.CurrentUser, location);
                })
                .Returns(Task.CompletedTask);

            TestAgentInstallationCommand installationCommand = new TestAgentInstallationCommand
            {
                KeyVaultUri = new Uri("https://any/key/vault"),
                CertificateName = "AnyCertificate"
            };

            return installationCommand.InstallCertificatesAsync(
                this.mockFixture.KeyVaultClient.Object,
                this.mockFixture.CertificateManager.Object,
                platform,
                CancellationToken.None);
        }

        [Test]
        [TestCase(PlatformID.Win32NT)]
        [TestCase(PlatformID.Unix)]
        public void GuestAgentRetriesOnFailedAttemptsToCommunicateWithTheKeyVaultToDownloadRequiredCertificates(PlatformID platform)
        {
            int retrycount = 5;
            int retrycounter = 0;

            TestAgentInstallationCommand installationCommand = new TestAgentInstallationCommand
            {
                KeyVaultUri = new Uri("https://any/key/vault"),
                CertificateName = "AnyCertificate"
            };

            // adding new retry policy to save time on unit test.
            installationCommand.RetryPolicy = Policy.Handle<Exception>().RetryAsync(retrycount);

            // Certificates are downloaded from a Key Vault.
            this.mockFixture.KeyVaultClient
                .Setup(kv => kv.GetSecretWithHttpMessagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, string, Dictionary<string, List<string>>, CancellationToken>((a, b, c, d, e) =>
                {
                    retrycounter++;
                }).Throws(new Exception());
            this.mockFixture.KeyVaultClient
                .Setup(kv => kv.GetCertificateWithHttpMessagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AzureOperationResponse<CertificateBundle> { Body = new CertificateBundle() });

            try
            {
                installationCommand.InstallCertificatesAsync(
                this.mockFixture.KeyVaultClient.Object,
                this.mockFixture.CertificateManager.Object,
                platform,
                CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // Since we are testing the retry policy on the event of failure, we are expecting the call to throw exception everytime.
                // do nothing
            }

            Assert.AreEqual((retrycount + 1), retrycounter);
        }

        [Test]
        [TestCase("1.2.3.4")]
        [TestCase("latest")]
        public Task InstallationCommandDownloadsTheExpectedGuestAgentNuGetPackage(string packageVersion)
        {
            TestAgentInstallationCommand installationCommand = new TestAgentInstallationCommand
            {
                KeyVaultUri = new Uri("https://any/key/vault"),
                NuGetFeedUri = new Uri("https://any/nuget/feed"),
                NuGetPat = "NuGetPatSecret",
                PackageVersion = packageVersion,
                InstallPath = "C:\\any\\install\\path"
            };

            // The NuGet personal access token used to authenticate with the store/feed is
            // stored in a key vault.
            this.mockFixture.KeyVaultClient
                .Setup(kv => kv.GetSecretWithHttpMessagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AzureOperationResponse<SecretBundle> { Body = new SecretBundle("anyPersonalAccessToken") });

            // The Juno Guest Agent is downloaded from a NuGet package store/feed.
            this.mockFixture.NuGetInstaller
                .Setup(nuget => nuget.InstallPackageAsync(
                    It.IsAny<NuGetPackageInfo>(),
                    It.IsAny<CancellationToken>()))
                .Callback<NuGetPackageInfo, CancellationToken>((packageInfo, token) =>
                {
                    Assert.AreEqual("Juno.GuestAgent", packageInfo.PackageName);
                    Assert.AreEqual(installationCommand.PackageVersion, packageInfo.PackageVersion);
                    Assert.AreEqual(installationCommand.InstallPath, packageInfo.DownloadPath);
                    Assert.AreEqual(installationCommand.NuGetFeedUri.AbsoluteUri, packageInfo.FeedUri.AbsoluteUri);
                })
                .ReturnsAsync(new NuGetVersion(new Version(1, 2, 3, 4)));

            return installationCommand.DownloadAgentNuGetPackageAsync(
                this.mockFixture.KeyVaultClient.Object,
                this.mockFixture.NuGetInstaller.Object,
                CancellationToken.None);
        }

        [Test]
        [TestCase("1.2.3.4")]
        [TestCase("latest")]
        public void InstallationCommandRetriesOnFailedAttemptToCommunicateWithKeyVaultToDownloadRequiredCertificates(string packageVersion)
        {
            int retrycounter = 0;
            int retrycount = 5;

            TestAgentInstallationCommand installationCommand = new TestAgentInstallationCommand
            {
                KeyVaultUri = new Uri("https://any/key/vault"),
                NuGetFeedUri = new Uri("https://any/nuget/feed"),
                NuGetPat = "NuGetPatSecret",
                PackageVersion = packageVersion,
                InstallPath = "C:\\any\\install\\path"
            };

            // adding new retry policy to save time on unit test.
            installationCommand.RetryPolicy = Policy.Handle<Exception>().RetryAsync(retrycount);

            // The NuGet personal access token used to authenticate with the store/feed is
            // stored in a key vault.
            this.mockFixture.KeyVaultClient
                .Setup(kv => kv.GetSecretWithHttpMessagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, string, Dictionary<string, List<string>>, CancellationToken>((a, b, c, d, e) =>
                {
                    retrycounter++;
                }).Throws(new Exception());

            try
            {
                installationCommand.DownloadAgentNuGetPackageAsync(
                this.mockFixture.KeyVaultClient.Object,
                this.mockFixture.NuGetInstaller.Object,
                CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // Since we are testing the retry policy on the event of failure, we are expecting the call to throw exception everytime.
                // do nothing
            }

            Assert.AreEqual((retrycount + 1), retrycounter);
        }

        [Test]
        [TestCase("1.2.3.4")]
        [TestCase("latest")]
        public void InstallationCommandRetriesOnFailedAttemptToInstallNuGetPackages(string packageVersion)
        {
            int retrycounter = 0;
            int retrycount = 5;

            TestAgentInstallationCommand installationCommand = new TestAgentInstallationCommand
            {
                KeyVaultUri = new Uri("https://any/key/vault"),
                NuGetFeedUri = new Uri("https://any/nuget/feed"),
                NuGetPat = "NuGetPatSecret",
                PackageVersion = packageVersion,
                InstallPath = "C:\\any\\install\\path"
            };

            // adding new retry policy to save time on unit test.
            installationCommand.RetryPolicy = Policy.Handle<Exception>().RetryAsync(retrycount);

            // The NuGet personal access token used to authenticate with the store/feed is
            // stored in a key vault.
            this.mockFixture.KeyVaultClient
                .Setup(kv => kv.GetSecretWithHttpMessagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AzureOperationResponse<SecretBundle> { Body = new SecretBundle("anyPersonalAccessToken") });

            // The Juno Guest Agent is downloaded from a NuGet package store/feed.
            this.mockFixture.NuGetInstaller
                .Setup(nuget => nuget.InstallPackageAsync(
                    It.IsAny<NuGetPackageInfo>(),
                    It.IsAny<CancellationToken>()))
                .Callback<NuGetPackageInfo, CancellationToken>((packageInfo, token) =>
                {
                    retrycounter++;
                }).Throws(new Exception());

            try
            {
                installationCommand.DownloadAgentNuGetPackageAsync(
               this.mockFixture.KeyVaultClient.Object,
               this.mockFixture.NuGetInstaller.Object,
               CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // Since we are testing the retry policy on the event of failure, we are expecting the call to throw exception everytime.
                // do nothing
            }

            Assert.AreEqual((retrycount + 1), retrycounter);
        }

        [Test]
        public async Task InstallationCommandCreatesTheExpectedResponseFileForTheGuestAgentServiceStartup()
        {
            string expectedWorkingDir = "C:\\any\\working\\dir";
            string expectedFileName = "Startup.rsp";
            IDictionary<string, IConvertible> expectedResponseFileArguments = new Dictionary<string, IConvertible>
            {
                { "--environment", "anyEnvironment" },
                { "--agentId", "cluster,node,vm,tip" },
                { "--vmSku", "Standard_Issue_v3" },
                { "--region", "South Africa North" }
            };

            string expectedContent = string.Join(Environment.NewLine, expectedResponseFileArguments.Select(entry => $"{entry.Key}=\"{entry.Value}\"")).Trim();

            Mock<IFile> mockfile = new Mock<IFile>();
            this.mockFixture.FileSystem.Setup(fs => fs.File).Returns(mockfile.Object);

            mockfile.Setup(file => file.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((path, content, token) =>
                {
                    Assert.AreEqual(Path.Combine(expectedWorkingDir, expectedFileName), path);
                    Assert.AreEqual(expectedContent, content);
                })
                .Returns(Task.CompletedTask);

            TestAgentInstallationCommand installationCommand = new TestAgentInstallationCommand();

            string responseFilePath = await installationCommand.CreateAgentResponseFileAsync(
                this.mockFixture.FileSystem.Object,
                expectedWorkingDir,
                expectedResponseFileArguments);

            Assert.AreEqual(Path.Combine(expectedWorkingDir, expectedFileName),  responseFilePath);
        }

        [Test]
        public async Task InstallationCommandInstallsTheGuestAgentByTheExpectedServiceName()
        {
            TestAgentInstallationCommand installationCommand = new TestAgentInstallationCommand
            {
                AgentId = "cluster01,node01,vm01,tip01",
                InstallPath = @"C:\any\path"
            };

            await installationCommand.InstallAgentAsServiceAsync(
                this.mockSystemManager.Object,
                this.mockFixture.FileSystem.Object,
                "1.2.3.4",
                CancellationToken.None);

            this.mockSystemManager.Verify(mgr => mgr.InstallServiceAsync(
                "Juno.GuestAgent",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()));
        }

        [Test]
        public async Task InstallationCommandInstallsTheGuestAgentServiceInTheExpectedLocation()
        {
            TestAgentInstallationCommand installationCommand = new TestAgentInstallationCommand
            {
                AgentId = "cluster01,node01,vm01,tip01",
                InstallPath = @"C:\any\path"
            };

            await installationCommand.InstallAgentAsServiceAsync(
                this.mockSystemManager.Object,
                this.mockFixture.FileSystem.Object,
                "1.2.3.4",
                CancellationToken.None);

            this.mockSystemManager.Verify(mgr => mgr.InstallServiceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(path => path.StartsWith($"{installationCommand.InstallPath}\\Juno.GuestAgent\\1.2.3.4")),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()));
        }

        [Test]
        public async Task InstallationCommandInstallsTheGuestAgentUsingTheExpectedRunAsAccountCredentials()
        {
            TestAgentInstallationCommand installationCommand = new TestAgentInstallationCommand
            {
                AgentId = "cluster01,node01,vm01,tip01",
                InstallPath = @"C:\any\path"
            };

            await installationCommand.InstallAgentAsServiceAsync(
                this.mockSystemManager.Object,
                this.mockFixture.FileSystem.Object,
                "1.2.3.4",
                CancellationToken.None);

            this.mockSystemManager.Verify(mgr => mgr.InstallServiceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                $"vm01\\{VmAdminAccounts.Default}",
                It.Is<string>(pwd => !string.IsNullOrWhiteSpace(pwd))));
        }

        [Test]
        public async Task InstallationCommandRetriesOnFailedAttemptToInstallsTheGuestAgent()
        {
            int retrycounter = 0;
            int retrycount = 5;

            // GuestAgentRetriesOnFailedAttemptsToCommunicateWithTheKeyVaultToDownloadRequiredCertificates
            TestAgentInstallationCommand installationCommand = new TestAgentInstallationCommand
            {
                AgentId = "cluster01,node01,vm01,tip01",
                InstallPath = @"C:\any\path"
            };

            installationCommand.RetryPolicy = Policy.Handle<Exception>().RetryAsync(retrycount);
            this.mockSystemManager
                .Setup(x => x.InstallServiceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>())).Callback<string, string, string, string, string, string, string>((a, b, c, d, e, f, g) =>
                    {
                        retrycounter++;
                    }).Throws(new Exception());

            try
            {
                await installationCommand.InstallAgentAsServiceAsync(
                this.mockSystemManager.Object,
                this.mockFixture.FileSystem.Object,
                "1.2.3.4",
                CancellationToken.None);
            }
            catch (Exception)
            {
                // Since we are testing the retry policy on the event of failure, we are expecting the call to throw exception everytime.
                // do nothing
            }

            Assert.AreEqual((retrycount + 1), retrycounter);
        }

        private static void AssertCommandPropertiesSet(ParseResult parserResults, TestAgentInstallationCommand command)
        {
            IEnumerable<Token> options = parserResults.Tokens.Where(token => token.Type == TokenType.Option);
            IEnumerable<Token> arguments = parserResults.Tokens.Where(token => token.Type == TokenType.Argument);

            for (int i = 0; i < options.Count(); i++)
            {
                string expectedOption = options.ElementAt(i).Value;
                string expectedArgument = arguments.ElementAt(i).Value;

                // By convention, the name of the option (when defined) must match the name on the underlying
                // command class.
                IOption optionDefinition = parserResults.Parser.Configuration.RootCommand.Options.First(option => option.HasAlias(expectedOption));
                EqualityAssert.PropertySet(command, optionDefinition.Name, expectedArgument, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void SetupDefaultMockBehaviors()
        {
            Mock<IFile> mockFile = new Mock<IFile>();
            mockFile.Setup(file => file.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

            this.mockFixture.FileSystem.Setup(fs => fs.File).Returns(mockFile.Object);
        }

        internal class TestAgentInstallationCommand : AgentInstallationCommand
        {
            public override Task<int> ExecuteAsync(string[] args, CancellationToken token, IServiceCollection services = null)
            {
                services.AddSingleton(this);
                return Task.FromResult(0);
            }

            // Expose these method(s) for test validation.
            public new Task<string> CreateAgentResponseFileAsync(IFileSystem fileSystem, string workingDirectory, IDictionary<string, IConvertible> responseFileArguments)
            {
                return base.CreateAgentResponseFileAsync(fileSystem, workingDirectory, responseFileArguments);
            }

            public new Task<NuGetVersion> DownloadAgentNuGetPackageAsync(IKeyVaultClient keyVaultClient, INuGetPackageInstaller nugetInstaller, CancellationToken cancellationToken)
            {
                return base.DownloadAgentNuGetPackageAsync(keyVaultClient, nugetInstaller, cancellationToken);
            }

            public new Task InstallCertificatesAsync(IKeyVaultClient keyVaultClient, ICertificateManager certificateManager, PlatformID platform, CancellationToken cancellationToken)
            {
                return base.InstallCertificatesAsync(keyVaultClient, certificateManager, platform, cancellationToken);
            }

            public new Task InstallAgentAsServiceAsync(ISystemManager systemManager, IFileSystem fileSystem, string installedVersion, CancellationToken cancellationToken)
            {
                return base.InstallAgentAsServiceAsync(systemManager, fileSystem, installedVersion, cancellationToken);
            }

            protected override Task<SecureString> GetServiceLogonPasswordAsync(string vmName, CancellationToken cancellationToken)
            {
                return Task.FromResult("AnyCredential".ToSecureString());
            }
        }
    }
}
