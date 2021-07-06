namespace Juno.Execution.Providers.Workloads
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Juno.Contracts;
    using Juno.Contracts.Configuration;
    using Juno.Execution.AgentRuntime;
    using Juno.Execution.ArmIntegration;
    using Juno.Execution.Providers.Workloads.VCContracts;
    using Juno.Providers;
    using Microsoft.Azure.CRC.Contracts;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Azure.CRC.Repository.KeyVault;
    using Microsoft.Azure.CRC.Telemetry;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class VirtualClientWorkloadProviderTests
    {
        private static int currentProcessId = 1234;

        private VmResourceGroupDefinition mockVmResourceGroup;
        private ProviderFixture mockFixture;
        private TestVirtualClientWorkloadProvider provider;
        private VirtualClientWorkloadProvider.State providerState;
        private Mock<IProcessProxy> mockProcess;
        private Mock<IFileSystem> mockFileSystem;
        private Mock<IFile> mockFileInterface;
        private Mock<IAzureKeyVault> mockKeyVault;
        private IEnumerable<EnvironmentEntity> mockVmEntities;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new ProviderFixture(typeof(VirtualClientWorkloadProvider), "Cluster01,Node01,VM01,TipSession01", Guid.NewGuid().ToString());
            this.mockFixture.SetupExperimentMocks();

            this.provider = new TestVirtualClientWorkloadProvider(this.mockFixture.Services);
            this.providerState = new VirtualClientWorkloadProvider.State();
            this.mockVmResourceGroup = this.mockFixture.Create<VmResourceGroupDefinition>();

            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "command", @"{NuGetPackagePath}\virtualclient\1.2.3\content\win-x64\VirtualClient.exe" },
                { "commandArguments", "--profile=any.profile --platform=Juno" },
                { "duration", "12:00:00" },
                { "timeout", "13:00:00" },
                { "platform", "win-x64" },
                { "EventHubConnectionString", "EventHubString" },
                { "PackageVersion", "1.2.3.4" }
            });

            this.mockVmEntities = new List<EnvironmentEntity>
            {
                EnvironmentEntity.VirtualMachine(
                    "VM01",
                    this.mockFixture.Context.ExperimentStep.ExperimentGroup,
                    new Dictionary<string, IConvertible>
                    {
                        ["AgentId"] = this.mockFixture.Context.ExperimentStep.AgentId
                    }),
                EnvironmentEntity.VirtualMachine(
                    "VM02",
                    this.mockFixture.Context.ExperimentStep.ExperimentGroup,
                    new Dictionary<string, IConvertible>
                    {
                        ["AgentId"] = this.mockFixture.Context.ExperimentStep.AgentId.Replace("VM01", "VM02")
                    })
            };

            // Mock Setup:
            // Setup the process/proxy mock for the common properties and methods that will be
            // called as part of the typical path of the provider operation.
            this.mockProcess = new Mock<IProcessProxy>();
            this.mockFileSystem = new Mock<IFileSystem>();
            this.mockKeyVault = new Mock<IAzureKeyVault>();
            this.mockFileInterface = new Mock<IFile>();
            this.mockFileSystem.SetupGet(fs => fs.File).Returns(this.mockFileInterface.Object);

            // Mock services/dependencies used by provider.
            this.mockFixture.Services.AddSingleton<IFileSystem>(this.mockFileSystem.Object);
            this.mockFixture.Services.AddSingleton<IAzureKeyVault>(this.mockKeyVault.Object);
            this.SetupMockDefaults();
        }

        [Test]
        public async Task VirtualClientWorkloadProviderValidatesRequiredComponentParameters()
        {
            this.mockFixture.Component.Parameters.Clear();
            Dictionary<string, IConvertible> requiredParameters = new Dictionary<string, IConvertible>
            {
                { "command", @"VirtualClient.exe" },
                { "commandArguments", "--profile=any.profile --platform=Juno" },
                { "duration", "12:00:00" },
                { "platform", "win-x64" },
                { "EventHubConnectionString", "EventHubString" },
                { "PackageVersion", "1.2.3.4" }
            };

            ExecutionResult result = null;
            foreach (var entry in requiredParameters)
            {
                result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

                Assert.IsNotNull(result);
                Assert.IsTrue(result.Status == ExecutionStatus.Failed);
                Assert.IsNotNull(result.Error);
                Assert.IsInstanceOf<SchemaException>(result.Error);

                this.mockFixture.Component.Parameters.Add(entry.Key, entry.Value);
            }

            result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status != ExecutionStatus.Failed);
        }

        [Test]
        public void VirtualClientWorkloadProviderMaintainsStateInItsOwnIndividualStateObject()
        {
            this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, new CancellationToken(false))
                .GetAwaiter().GetResult();

            this.mockFixture.DataClient.Verify(client => client.GetOrCreateStateAsync<VirtualClientWorkloadProvider.State>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.Is<string>(stateId => !string.IsNullOrEmpty(stateId))));

            this.mockFixture.DataClient.Verify(client => client.SaveStateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VirtualClientWorkloadProvider.State>(),
                It.IsAny<CancellationToken>(),
                It.Is<string>(stateId => !string.IsNullOrEmpty(stateId))));
        }

        [Test]
        public void VirtualClientWorkloadProviderCreatesTheExpectedMetadataToProvideToTheVirtualClientProcess()
        {
            ExperimentContext context = this.mockFixture.Context;
            ExperimentComponent component = this.mockFixture.Component;
            AgentIdentification agentId = this.mockFixture.Create<AgentIdentification>();
            EventContext telemetryContext = new EventContext(Guid.NewGuid());

            IDictionary<string, string> expectedMetadata = new Dictionary<string, string>
            {
                { "agentId", agentId.ToString() },
                { "agentType", AgentType.GuestAgent.ToString() },
                { "containerId", string.Empty },
                { "tipSessionId", agentId.Context },
                { "nodeId", agentId.NodeName },
                { "nodeName", agentId.NodeName },
                { "experimentId", context.Experiment.Id },
                { "experimentStepId", context.ExperimentStep.Id },
                { "experimentGroup", context.ExperimentStep.ExperimentGroup },
                { "groupId", context.ExperimentStep.ExperimentGroup },
                { "virtualMachineName", agentId.VirtualMachineName },
                { "clusterName", agentId.ClusterName }
            };

            // All experiment metadata properteis are supplied to the Virtual Client as well.
            expectedMetadata.AddRange(context.Experiment.Definition.Metadata.ToDictionary(m => m.Key, (entry) => entry.Value.ToString()));

            IDictionary<string, string> actualMetadata = TestVirtualClientWorkloadProvider2.CreateMetadata(context, component, agentId, telemetryContext);

            CollectionAssert.AreEquivalent(
                expectedMetadata.Select(entry => $"{entry.Key}={entry.Value}"),
                actualMetadata.Select(entry => $"{entry.Key}={entry.Value}"));
        }

        [Test]
        public void VirtualClientWorkloadProviderIncludesTheVMLogContainerIdToTheExpectedMetadataWhenItIsDefined()
        {
            ExperimentContext context = this.mockFixture.Context;
            ExperimentComponent component = this.mockFixture.Component;
            AgentIdentification agentId = this.mockFixture.Create<AgentIdentification>();
            EventContext telemetryContext = new EventContext(Guid.NewGuid());

            // If the VM log container ID is included in the telemetry context, pass it to the
            // VC in the metadata properties.
            string expectedContainerId = Guid.NewGuid().ToString();
            telemetryContext.Properties["containerId"] = expectedContainerId;

            IDictionary<string, string> actualMetadata = TestVirtualClientWorkloadProvider2.CreateMetadata(context, component, agentId, telemetryContext);

            Assert.IsTrue(actualMetadata.ContainsKey("containerId"));
            Assert.AreEqual(expectedContainerId, actualMetadata["containerId"]);
        }

        [Test]
        public async Task VirtualClientWorkloadProviderCreatesTheExpectedProcessToRunTheVirtualClientWorkload()
        {
            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);

            using (IProcessProxy process = await testProvider.CreateProcessAsync(
                this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None))
            {
                string expectedFileName = Path.Combine(DependencyPaths.NuGetPackages, @"virtualclient\1.2.3\content\win-x64\VirtualClient.exe");
                string expectedWorkingDirectory = Path.GetDirectoryName(expectedFileName);

                Assert.AreEqual(expectedFileName, process.StartInfo.FileName);
                Assert.AreEqual(expectedWorkingDirectory, process.StartInfo.WorkingDirectory);
                Assert.IsTrue(process.StartInfo.CreateNoWindow);
                Assert.IsFalse(process.StartInfo.UseShellExecute);
                Assert.IsTrue(process.StartInfo.RedirectStandardError);
                Assert.IsFalse(process.StartInfo.RedirectStandardOutput);
            }
        }

        [Test]
        public async Task VirtualClientWorkloadProviderCreatesTheExpectedProcessToRunTheWindowsVirtualClientWorkloadWithParametersSpecified()
        {
            this.mockFixture.Component = new ExperimentComponent(
            this.mockFixture.Component.ComponentType,
            this.mockFixture.Component.Name,
            this.mockFixture.Component.Description,
            this.mockFixture.Component.Group);

            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "platform", "win-x64" },
                { "packageVersion", "1.2.3" },
                { "commandArguments", "--profile=any.profile --platform=Juno" },
                { "duration", "12:00:00" },
                { "timeout", "13:00:00" }
            });

            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);

            using (IProcessProxy process = await testProvider.CreateProcessAsync(
                this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None))
            {
                string expectedFileName = Path.Combine(DependencyPaths.NuGetPackages, @"virtualclient\1.2.3\content\win-x64\VirtualClient.exe");
                string expectedWorkingDirectory = Path.GetDirectoryName(expectedFileName);

                Assert.AreEqual(expectedFileName, process.StartInfo.FileName);
                Assert.AreEqual(expectedWorkingDirectory, process.StartInfo.WorkingDirectory);
                Assert.IsTrue(process.StartInfo.CreateNoWindow);
                Assert.IsFalse(process.StartInfo.UseShellExecute);
                Assert.IsTrue(process.StartInfo.RedirectStandardError);
                Assert.IsFalse(process.StartInfo.RedirectStandardOutput);
            }
        }

        [Test]
        public async Task VirtualClientWorkloadProviderCreatesTheExpectedProcessToRunTheWindowsVirtualClientWorkloadWithParametersSpecifiedForARM64()
        {
            this.mockFixture.Component = new ExperimentComponent(
            this.mockFixture.Component.ComponentType,
            this.mockFixture.Component.Name,
            this.mockFixture.Component.Description,
            this.mockFixture.Component.Group);

            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "platform", "win-arm64" },
                { "packageVersion", "1.2.3" },
                { "commandArguments", "--profile=any.profile --platform=Juno" },
                { "duration", "12:00:00" },
                { "timeout", "13:00:00" }
            });

            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);

            using (IProcessProxy process = await testProvider.CreateProcessAsync(
                this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None))
            {
                string expectedFileName = Path.Combine(DependencyPaths.NuGetPackages, @"virtualclient.arm64\1.2.3\content\win-arm64\VirtualClient.exe");
                string expectedWorkingDirectory = Path.GetDirectoryName(expectedFileName);

                Assert.AreEqual(expectedFileName, process.StartInfo.FileName);
                Assert.AreEqual(expectedWorkingDirectory, process.StartInfo.WorkingDirectory);
                Assert.IsTrue(process.StartInfo.CreateNoWindow);
                Assert.IsFalse(process.StartInfo.UseShellExecute);
                Assert.IsTrue(process.StartInfo.RedirectStandardError);
                Assert.IsFalse(process.StartInfo.RedirectStandardOutput);
            }
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        public void VirtualClientWorkloadProviderCatchesMissingPlatformParameter(string missingParameter)
        {
            this.mockFixture.Component = new ExperimentComponent(
            this.mockFixture.Component.ComponentType,
            this.mockFixture.Component.Name,
            this.mockFixture.Component.Description,
            this.mockFixture.Component.Group);

            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {   
                { "platform", missingParameter },
                { "packageVersion", "1.2.3" },
                { "commandArguments", "--profile=any.profile --platform=Juno" },
                { "duration", "12:00:00" },
                { "timeout", "13:00:00" }
            });
            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);
            Assert.ThrowsAsync<ProviderException>(
                async () => 
                await testProvider.CreateProcessAsync(this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        public void VirtualClientWorkloadProviderCatchesMissingPackageVersionParameter(string missingParameter)
        {
            this.mockFixture.Component = new ExperimentComponent(
            this.mockFixture.Component.ComponentType,
            this.mockFixture.Component.Name,
            this.mockFixture.Component.Description,
            this.mockFixture.Component.Group);

            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "platform", "win-x64" },
                { "packageVersion", missingParameter },
                { "commandArguments", "--profile=any.profile --platform=Juno" },
                { "duration", "12:00:00" },
                { "timeout", "13:00:00" }
            });
            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);
            Assert.ThrowsAsync<ProviderException>(
                async () => await testProvider.CreateProcessAsync(this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        public void VirtualClientWorkloadProviderCatchesMissingCommandParameter(string missingParameter)
        {
            this.mockFixture.Component = new ExperimentComponent(
            this.mockFixture.Component.ComponentType,
            this.mockFixture.Component.Name,
            this.mockFixture.Component.Description,
            this.mockFixture.Component.Group);

            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "command", missingParameter },
                { "commandArguments", "--profile=any.profile --platform=Juno" },
                { "duration", "12:00:00" },
                { "timeout", "13:00:00" }
            });
            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);
            Assert.ThrowsAsync<ProviderException>(
                async () => await testProvider.CreateProcessAsync(this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None));
        }

        [Test]
        public async Task VirtualClientWorkloadProviderCreatesTheExpectedProcessToRunTheLinuxVirtualClientWorkloadWithParametersSpecified()
        {
            this.mockFixture.Component = new ExperimentComponent(
            this.mockFixture.Component.ComponentType,
            this.mockFixture.Component.Name,
            this.mockFixture.Component.Description,
            this.mockFixture.Component.Group);

            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "platform", "linux-x64" },
                { "packageVersion", "1.2.3" },
                { "commandArguments", "--profile=any.profile --platform=Juno" },
                { "duration", "12:00:00" },
                { "timeout", "13:00:00" }
            });

            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);

            using (IProcessProxy process = await testProvider.CreateProcessAsync(
                this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None))
            {
                string expectedFileName = DependencyPaths.NuGetPackages + "/virtualclient/1.2.3/content/linux-x64/VirtualClient";
                string expectedWorkingDirectory = Path.GetDirectoryName(expectedFileName);

                Assert.AreEqual(expectedFileName, process.StartInfo.FileName);
                Assert.AreEqual(expectedWorkingDirectory, process.StartInfo.WorkingDirectory);
                Assert.IsTrue(process.StartInfo.CreateNoWindow);
                Assert.IsFalse(process.StartInfo.UseShellExecute);
                Assert.IsTrue(process.StartInfo.RedirectStandardError);
                Assert.IsFalse(process.StartInfo.RedirectStandardOutput);
            }
        }

        [Test]
        public async Task VirtualClientWorkloadProviderSupportsExpectedCommandPathFormats()
        {
            Dictionary<string, string> supportedCommandPaths = new Dictionary<string, string>
            {
                // Paths using well-known path references.
                { @"{NuGetPackagePath}\virtualclient\VirtualClient.exe", Path.Combine(DependencyPaths.NuGetPackages, @"virtualclient\VirtualClient.exe") },

                // Paths relative to the dependencies root path.
                { @"virtualclient\VirtualClient.exe", Path.Combine(DependencyPaths.RootPath, @"virtualclient\VirtualClient.exe") },

                // Fully qualified paths.
                { @"C:\Temp\virtualclient\VirtualClient.exe", @"C:\Temp\virtualclient\VirtualClient.exe" }
            };

            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);

            foreach (var entry in supportedCommandPaths)
            {
                this.mockFixture.Component.Parameters["command"] = entry.Key;
                using (IProcessProxy process = await testProvider.CreateProcessAsync(
                    this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None))
                {
                    string expectedFileName = entry.Value;
                    string expectedWorkingDirectory = Path.GetDirectoryName(expectedFileName);

                    Assert.AreEqual(expectedFileName, process.StartInfo.FileName);
                    Assert.AreEqual(expectedWorkingDirectory, process.StartInfo.WorkingDirectory);
                }
            }
        }

        [Test]
        public async Task VirtualClientWorkloadProviderRunsTheVirtualClientForTheDurationDefined()
        {
            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);

            using (IProcessProxy process = await testProvider.CreateProcessAsync(
                this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None))
            {
                TimeSpan expectedDuration = this.mockFixture.Component.Parameters.GetTimeSpanValue(StepParameters.Duration);
                Assert.IsTrue(process.StartInfo.Arguments.Contains($"--timeout={expectedDuration.TotalMinutes}"));
            }
        }

        [Test]
        public async Task VirtualClientWorkloadProviderRunsTheVirtualClientWithExpectedSeed()
        {
            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);

            using (IProcessProxy process = await testProvider.CreateProcessAsync(
                this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None))
            {
                int expectedSeed = Guid.Parse(this.mockFixture.Context.Experiment.Id).GetHashCode();
                Assert.IsTrue(process.StartInfo.Arguments.Contains($"--seed={expectedSeed}"));
            }
        }

        [Test]
        public async Task VirtualClientWorkloadProviderRunsTheVirtualClientWithAppInsightsKeyAndEventHubConnectionString()
        {
            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "command", @"{NuGetPackagePath}\virtualclient\1.2.3\content\win-x64\VirtualClient.exe" },
                { "commandArguments", "--profile=any.profile --platform=Juno" },
                { "duration", "12:00:00" },
                { "timeout", "13:00:00" },
                { "eventHubConnectionString", "[secret:keyvault]=testConnectionString" },
                { "applicationInsightsInstrumentationKey", "[secret:keyvault]=testAppInsightsKey" }
            });

            string expectedKey = "RealKey";
            string expectedConnectionString = "RealConnectionString";
            SecureString secretKey = expectedKey.ToSecureString();
            SecureString secretString = expectedConnectionString.ToSecureString();

            this.mockKeyVault.Setup(kv => kv.GetSecretAsync("testAppInsightsKey", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(secretKey))
                .Verifiable();

            this.mockKeyVault.Setup(kv => kv.GetSecretAsync("testConnectionString", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(secretString))
                .Verifiable();

            using (IProcessProxy process = await testProvider.CreateProcessAsync(
                this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None))
            {
                Assert.IsTrue(process.StartInfo.Arguments.Contains($"--applicationInsightsInstrumentationKey={expectedKey}"));
                Assert.IsTrue(process.StartInfo.Arguments.Contains($"--eventHubConnectionString={expectedConnectionString}"));
            }

            this.mockKeyVault.Verify();
        }

        [Test]
        public void VirtualClientWorkloadProviderThrowsIfDuplicateAppInsightsKeyParametersExist()
        {
            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "command", @"{NuGetPackagePath}\virtualclient\1.2.3\content\win-x64\VirtualClient.exe" },

                // parameter is duplicated...defined on both the command line arguments AND as a separate step parameter. This creates an ambiguous
                // situation where we cannot know which one to use.
                { "commandArguments", "--profile=any.profile --platform=Juno --applicationInsightsInstrumentationKey=duplicateParameterValue" },
                { "duration", "12:00:00" },
                { "timeout", "13:00:00" },
                { "applicationInsightsInstrumentationKey", "[secret:keyvault]=testAppInsightsKey" }
            });

            this.mockKeyVault.Setup(kv => kv.GetSecretAsync("testAppInsightsKey", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult("AnySecret".ToSecureString()));

            ProviderException error = Assert.ThrowsAsync<ProviderException>(() => testProvider.CreateProcessAsync(
                this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None));

            Assert.IsNotNull(error);
            Assert.AreEqual(ErrorReason.InvalidUsage, error.Reason);
        }

        [Test]
        public void VirtualClientWorkloadProviderThrowsIfDuplicateEventHubConnectionStringParametersExist()
        {
            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);
            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "command", @"{NuGetPackagePath}\virtualclient\1.2.3\content\win-x64\VirtualClient.exe" },

                // parameter is duplicated...defined on both the command line arguments AND as a separate step parameter. This creates an ambiguous
                // situation where we cannot know which one to use.
                { "commandArguments", "--profile=any.profile --platform=Juno --eventHubConnectionString=duplicateParameterValue" },
                { "duration", "12:00:00" },
                { "timeout", "13:00:00" },
                { "eventHubConnectionString", "[secret:keyvault]=testEventHubConnectionString" }
            });

            this.mockKeyVault.Setup(kv => kv.GetSecretAsync("testEventHubConnectionString", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult("AnySecret".ToSecureString()));

            ProviderException error = Assert.ThrowsAsync<ProviderException>(() => testProvider.CreateProcessAsync(
                this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None));

            Assert.IsNotNull(error);
            Assert.AreEqual(ErrorReason.InvalidUsage, error.Reason);
        }

        [Test]
        public async Task VirtualClientWorkloadProviderProvidesTheExpectedCommandLineParametersToTheVirtualClient()
        {
            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);

            using (IProcessProxy process = await testProvider.CreateProcessAsync(
                this.mockFixture.Context, this.mockFixture.Component, new Dictionary<string, string>(), "Specifications.json", "layout.json", CancellationToken.None))
            {
                string expectedFileName = Path.Combine(DependencyPaths.NuGetPackages, @"virtualclient\1.2.3\content\win-x64\VirtualClient.exe");
                string expectedWorkingDirectory = Path.GetDirectoryName(expectedFileName);
                string expectedArguments = "--profile=any.profile --platform=Juno --timeout=720 --metadata=";

                Assert.AreEqual(expectedFileName, process.StartInfo.FileName);
                Assert.AreEqual(expectedWorkingDirectory, process.StartInfo.WorkingDirectory);
                Assert.IsTrue(process.StartInfo.Arguments.Contains(expectedArguments));
            }
        }

        [Test]
        public async Task VirtualClientWorkloadProviderPassesTheExpectedMetadataToTheVirtualClientCommandLine()
        {
            ExperimentContext context = this.mockFixture.Context;
            ExperimentComponent component = this.mockFixture.Component;
            AgentIdentification agentId = this.mockFixture.Create<AgentIdentification>();

            IDictionary<string, string> expectedMetadata = new Dictionary<string, string>
            {
                { "agentId", agentId.ToString() },
                { "experimentId", context.Experiment.Id },
                { "experimentStepId", context.ExperimentStep.Id },
                { "experimentGroup", context.ExperimentStep.ExperimentGroup },
                { "groupId", context.ExperimentStep.ExperimentGroup },
                { "virtualMachineName", agentId.VirtualMachineName },
                { "nodeName", agentId.NodeName },
                { "nodeId", agentId.NodeName },
                { "clusterName", agentId.ClusterName },
                { "context", agentId.Context }
            };

            TestVirtualClientWorkloadProvider2 testProvider = new TestVirtualClientWorkloadProvider2(this.mockFixture.Services);

            using (IProcessProxy process = await testProvider.CreateProcessAsync(
                this.mockFixture.Context, this.mockFixture.Component, expectedMetadata, "Specifications.json", "layout.json", CancellationToken.None))
            {
                // Expected Format:
                // --metadata=key1=value1,,,key2=value2,,,key3=value3
                string expectedMetadataArgument = $"--metadata=\"{string.Join(",,,", expectedMetadata.Select(entry => $"{entry.Key}={entry.Value}"))}\"";
                Assert.IsTrue(process.StartInfo.Arguments.Contains(expectedMetadataArgument));
            }
        }

        [Test]
        public async Task VirtualClientWorkloadProviderWritesASpecificationFileWhenRequiredDataIsAvailable()
        {
            // Move to step where VC is started.
            this.providerState.DependenciesInstalled = true;

            // Ensure we are requesting the specification file be created.
            this.mockFixture.Component.Parameters["includeSpecifications"] = true;

            // OS and Data disk SKU information is defined on the VM entities when they are registered
            // with the experiment.
            this.mockVmEntities.First().Metadata["osDiskSku"] = "Standard_LRS";
            this.mockVmEntities.First().Metadata["dataDisks"] = VmDisk.ToString(VirtualClientWorkloadProviderTests.CreateDataDisks());

            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(this.mockVmEntities);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            this.mockFileInterface.Verify(file => file.WriteAllTextAsync(
                It.IsRegex("Specifications.json"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task VirtualClientWorkloadProviderWritesLayoutFileWithExpectedContent()
        {
            // Move to step where VC is started.
            this.providerState.DependenciesInstalled = true;
            int index = 0;
            foreach (VmDefinition vm in this.mockVmResourceGroup.VirtualMachines)
            {
                // Mock fixture creates invalid IP address which will fail validation, so overriding those.
                index++;
                vm.PrivateIPAddress = $"10.{index}.{index}.{index}";
            }

            this.mockFixture.DataClient
                .Setup(c => c.GetOrCreateStateAsync<VmResourceGroupDefinition>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(this.mockVmResourceGroup);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);
            EnvironmentLayout layout;
            this.mockFileInterface.Setup(file => file.WriteAllTextAsync(
                It.IsRegex("layout.json"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((filePath, content, token) =>
            {
                layout = JsonConvert.DeserializeObject<EnvironmentLayout>(content);
                Assert.AreEqual(3, layout.Clients.Count());
            })
            .Returns(Task.CompletedTask);

            this.mockFileInterface.Verify(file => file.WriteAllTextAsync(
                It.IsRegex("layout.json"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task VirtualClientWorkloadProviderDoesNotWriteASpecificationFileWhenRequiredDataIsNotAvailable()
        {
            // Move to step where VC is started.
            this.providerState.DependenciesInstalled = true;

            // Ensure we are requesting the specification file be created. However, assume that the requisite
            // information is not included in the VM entities (e.g. the OS and Data disk SKU info).
            this.mockFixture.Component.Parameters["includeSpecifications"] = true;

            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(this.mockVmEntities);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            this.mockFileInterface.Verify(file => file.WriteAllTextAsync(
                It.IsRegex("Specifications.json"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task VirtualClientWorkloadProviderWritesASpecificationFileByDefault()
        {
            // Move to step where VC is started.
            this.providerState.DependenciesInstalled = true;

            // OS and Data disk SKU information is defined on the VM entities when they are registered
            // with the experiment.
            IEnumerable<VmDisk> expectedDataDisks = VirtualClientWorkloadProviderTests.CreateDataDisks();
            this.mockVmEntities.First().Metadata["osDiskSku"] = "Standard_LRS";
            this.mockVmEntities.First().Metadata["dataDisks"] = VmDisk.ToString(expectedDataDisks);

            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(this.mockVmEntities);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            this.mockFileInterface.Verify(file => file.WriteAllTextAsync(
                It.IsRegex("Specifications.json"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task VirtualClientWorkloadProviderDoesNotWriteASpecificationFileWhenInstructedNotTo()
        {
            // Move to step where VC is started.
            this.providerState.DependenciesInstalled = true;

            // Ensure we are explicitly requesting the specification file NOT be created.
            this.mockFixture.Component.Parameters["includeSpecifications"] = false;

            // OS and Data disk SKU information is defined on the VM entities when they are registered
            // with the experiment.
            IEnumerable<VmDisk> expectedDataDisks = VirtualClientWorkloadProviderTests.CreateDataDisks();
            this.mockVmEntities.First().Metadata["osDiskSku"] = "Standard_LRS";
            this.mockVmEntities.First().Metadata["dataDisks"] = VmDisk.ToString(expectedDataDisks);

            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(this.mockVmEntities);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            this.mockFileInterface.Verify(file => file.WriteAllTextAsync(
                It.IsRegex("Specifications.json"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public void VirtualClientWorkloadProviderWritesTheSpecificationFileToTheExpectedLocation()
        {
            // Move to step where VC is started.
            this.providerState.DependenciesInstalled = true;

            // OS and Data disk SKU information is defined on the VM entities when they are registered
            // with the experiment.
            this.mockVmEntities.First().Metadata["osDiskSku"] = "Standard_LRS";
            this.mockVmEntities.First().Metadata["dataDisks"] = VmDisk.ToString(VirtualClientWorkloadProviderTests.CreateDataDisks());

            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(this.mockVmEntities);

            string[] expectedLocation = this.mockFixture.Component.Parameters["command"].ToString().Split("\\");

            this.mockFileInterface
                .Setup(file => file.WriteAllText(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((path, content) =>
                {
                    string[] actualLocation = path.Split("\\");
                    // Directory location (expected to be the same directory as the VirtualClient.exe)
                    CollectionAssert.AreEqual(expectedLocation.TakeLast(4).SkipLast(1), actualLocation.TakeLast(4).SkipLast(1));

                    // File name
                    Assert.AreEqual("Specifications.json", actualLocation.Last());
                });

            this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [Test]
        public async Task VirtualClientWorkloadProviderWritesTheExpectedInformationToTheSpecificationFile()
        {
            // Move to step where VC is started.
            this.providerState.DependenciesInstalled = true;

            // OS and Data disk SKU information is defined on the VM entities when they are registered
            // with the experiment.
            IEnumerable<VmDisk> expectedDataDisks = VirtualClientWorkloadProviderTests.CreateDataDisks();
            this.mockVmEntities.First().Metadata["osDiskSku"] = "Standard_LRS";
            this.mockVmEntities.First().Metadata["dataDisks"] = VmDisk.ToString(expectedDataDisks);

            this.mockFixture.DataClient.OnGetEntitiesProvisioned().ReturnsAsync(this.mockVmEntities);
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            List<object> expectedDiskInfo = new List<object>()
            {
                new { id = -1, name = "osDiskSku",  type = "Standard_LRS" }
            };

            expectedDataDisks.ToList().ForEach(disk => expectedDiskInfo.Add(new { id = disk.Lun, name = "dataDiskSku", type = disk.Sku }));

            string expectedContent = new { diskMapping = expectedDiskInfo }.ToJson();

            this.mockFileInterface.Verify(file => file.WriteAllTextAsync(
                It.IsRegex("Specifications.json"),
                It.Is<string>(content => content.RemoveWhitespaces() == expectedContent.RemoveWhitespaces()),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task VirtualClientWorkloadProviderInstallsAnyDependenciesDefinedForTheComponentStep()
        {
            this.mockFixture.Component = new ExperimentComponent(
                this.mockFixture.Component.ComponentType,
                this.mockFixture.Component.Name,
                this.mockFixture.Component.Description,
                this.mockFixture.Component.Group,
                parameters: this.mockFixture.Component.Parameters,
                dependencies: new List<ExperimentComponent>
                {
                    FixtureExtensions.CreateExperimentComponent(typeof(TestDependencyProvider))
                });

            // Behavior Setup:
            // We are passing the list of dependencies installed to the 'TestDependencyProvider' via the provider services so that
            // we can validate the exact individual dependency providers installed.
            IList<TestDependencyProvider> dependenciesInstalled = new List<TestDependencyProvider>();
            this.mockFixture.Services.AddSingleton(dependenciesInstalled);

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsTrue(dependenciesInstalled.Count == 1);
        }

        [Test]
        public async Task VirtualClientWorkloadProviderRunsTheVirtualClientProcessAfterDependenciesAreInstalled()
        {
            this.providerState.DependenciesInstalled = true;

            this.mockFixture.Component = new ExperimentComponent(
                this.mockFixture.Component.ComponentType,
                this.mockFixture.Component.Name,
                this.mockFixture.Component.Description,
                this.mockFixture.Component.Group,
                parameters: this.mockFixture.Component.Parameters,
                dependencies: new List<ExperimentComponent>
                {
                    // Add dependencies to the component/step definition
                    FixtureExtensions.CreateExperimentComponent(typeof(TestDependencyProvider))
                });

            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Behavior Setup:
            // If the Virtual Client process should be started if ALL dependencies
            // get installed.
            this.mockProcess.Verify(process => process.Start());
        }

        [Test]
        public async Task VirtualClientWorkloadProviderDoesNotRunTheVirtualClientProcessIfDependencyInstallationFails()
        {
            this.mockFixture.Component = new ExperimentComponent(
                this.mockFixture.Component.ComponentType,
                this.mockFixture.Component.Name,
                this.mockFixture.Component.Description,
                this.mockFixture.Component.Group,
                parameters: this.mockFixture.Component.Parameters,
                dependencies: new List<ExperimentComponent>
                {
                    // Add dependencies to the component/step definition
                    FixtureExtensions.CreateExperimentComponent(typeof(TestDependencyProvider))
                });

            // Behavior Setup:
            // If the Virtual Client process should NOT be started if any dependencies fail to
            // get installed.
            bool virtualClientProcessStarted = false;
            this.mockProcess.Setup(process => process.Start())
                .Callback(() => virtualClientProcessStarted = true)
                .Returns(true);

            // Behavior Setup:
            // Cause the dependency provider to fail.
            //
            // We are passing the execution result to the 'TestDependencyProvider' via the provider services so that
            // we can control the behavior of the individual dependency provider.
            this.mockFixture.Services.AddSingleton(new ExecutionResult(ExecutionStatus.Failed));

            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.Failed);
            Assert.IsFalse(virtualClientProcessStarted);
        }

        [Test]
        public void VirtualClientWorkloadProviderParsesCommandForWinX64()
        {
            this.mockFixture.Component = new ExperimentComponent(
                this.mockFixture.Component.ComponentType,
                this.mockFixture.Component.Name,
                this.mockFixture.Component.Description,
                this.mockFixture.Component.Group);

            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "platform", "win-x64" },
                { "packageVersion", "1.2.3" },
                { "commandArguments", "--profile=any.profile --platform=Juno" },
                { "duration", "12:00:00" },
                { "timeout", "13:00:00" }
            });

            string command = VirtualClientWorkloadProviderTests.GetVirtualClientExePath(this.mockFixture.Component);
            Console.WriteLine(command);
        }

        [Test]
        public void VirtualClientWorkloadProviderParsesCommandForLinux64()
        {
            this.mockFixture.Component = new ExperimentComponent(
                this.mockFixture.Component.ComponentType,
                this.mockFixture.Component.Name,
                this.mockFixture.Component.Description,
                this.mockFixture.Component.Group);

            this.mockFixture.Component.Parameters.Clear();
            this.mockFixture.Component.Parameters.AddRange(new Dictionary<string, IConvertible>
            {
                { "platform", "linux-x64" },
                { "packageVersion", "1.2.3" },
                { "commandArguments", "--profile=any.profile --platform=Juno" },
                { "duration", "12:00:00" },
                { "timeout", "13:00:00" }
            });

            string command = VirtualClientWorkloadProviderTests.GetVirtualClientExePath(this.mockFixture.Component);
            Console.WriteLine(command);
        }

        [Test]
        public async Task VirtualClientWorkloadProviderCanBeCancelledBeforeRunningTheVirtualClient()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                VirtualClientWorkloadProviderTests.RequestCancellation(tokenSource);

                // Behavior Setup:
                // If the Virtual Client process should NOT be started if cancellation is requested.
                bool virtualClientProcessStarted = false;
                this.mockProcess.Setup(process => process.Start())
                    .Callback(() => virtualClientProcessStarted = true)
                    .Returns(true);

                await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, tokenSource.Token);

                Assert.IsFalse(virtualClientProcessStarted);
            }
        }

        [Test]
        public async Task VirtualClientWorkloadProviderReturnsTheExpectedResultAfterStartingTheVirtualClientProcess()
        {
            // First execution starts the process.
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgressContinue);
        }

        [Test]
        public async Task VirtualClientWorkloadProviderReturnsTheExpectedResultWhenTheVirtualClientProcessIsRunning()
        {
            // First execution starts the process.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            // Second (and subsequent) executions monitor the process for completion.
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgressContinue);
        }

        [Test]
        public async Task VirtualClientWorkloadProviderReturnsTheExpectedResultWhenTheVirtualClientProcessExit()
        {
            // Behavior Setup:
            // Setup the case where the VirtualClient.exe process has exited.
            this.mockProcess.Setup(process => process.HasExited)
                .Returns(true);

            // First execution starts the process.
            await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            this.mockProcess.Setup(process => process.Id)
              .Returns(VirtualClientWorkloadProviderTests.currentProcessId++);

            // Second call should start the new process
            ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Status == ExecutionStatus.InProgressContinue);
        }

        [Test]
        public async Task VirtualClientWorkloadProviderReturnsTheExpectedResultWhenCancelled()
        {
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                VirtualClientWorkloadProviderTests.RequestCancellation(tokenSource);

                // Second (and subsequent) executions monitor the process for completion.
                ExecutionResult result = await this.provider.ExecuteAsync(this.mockFixture.Context, this.mockFixture.Component, tokenSource.Token);

                Assert.IsNotNull(result);
                Assert.IsTrue(result.Status == ExecutionStatus.Cancelled);
            }
        }

        [Test]
        public void VirtualClientWorkloadProviderSupportsFeatureFlags()
        {
            string expectedFlag = "AnyFeatureFlag";
            this.mockFixture.Component.Parameters[StepParameters.FeatureFlag] = expectedFlag;

            Assert.IsTrue(this.provider.HasFeatureFlag(this.mockFixture.Component, expectedFlag));
            Assert.DoesNotThrow(() => this.provider.ValidateParameters(this.mockFixture.Component));
        }

        private static IEnumerable<VmDisk> CreateDataDisks(string dataDiskSku = null)
        {
            string effectiveDiskSku = dataDiskSku ?? "Standard_LRS";
            return new List<VmDisk>
            {
                new VmDisk(0, effectiveDiskSku, 32, effectiveDiskSku),
                new VmDisk(1, effectiveDiskSku, 64, effectiveDiskSku)
            };
        }

        private static string GetVirtualClientExePath(ExperimentComponent component)
        {
            string command = component.Parameters.GetValue<string>(StepParameters.Command, string.Empty);
            // Construct command based on platform and version if command is not explicitly provided.
            // TODO [8601409]: Remove command parameter and make platform and version parameter required
            if (string.IsNullOrEmpty(command))
            {
                string platform = component.Parameters.GetValue<string>(StepParameters.Platform, string.Empty);
                string packageVersion = component.Parameters.GetValue<string>(StepParameters.PackageVersion, string.Empty);
                if (string.IsNullOrEmpty(platform))
                {
                    throw new ProviderException($"Required parameter {nameof(StepParameters.Platform)} is missing.", ErrorReason.ProviderStateInvalid);
                }

                if (string.IsNullOrEmpty(packageVersion))
                {
                    throw new ProviderException($"Required parameter {nameof(StepParameters.PackageVersion)} is missing.", ErrorReason.ProviderStateInvalid);
                }

                if (platform == VmPlatform.LinuxArm64 || platform == VmPlatform.LinuxX64)
                {
                    command = $"{{NuGetPackagePath}}/virtualclient/{packageVersion}/content/{platform}/VirtualClient";
                }
                else
                {
                    command = $"{{NuGetPackagePath}}\\virtualclient\\{packageVersion}\\content\\win-x64\\VirtualClient.exe";
                }
            }

            string commandFullPath = Path.Combine(DependencyPaths.ReplacePathReferences(command));

            if (!Path.IsPathRooted(commandFullPath))
            {
                commandFullPath = Path.Combine(DependencyPaths.RootPath, commandFullPath);
            }

            return commandFullPath;
        }

        private static void RequestCancellation(CancellationTokenSource tokenSource)
        {
            try
            {
                tokenSource.Cancel();
            }
            catch
            {
                // Throws an OperationCancelledException
            }
        }

        private void SetupMockDefaults()
        {
            // Mock Setup:
            // Setup the mocks for the typical set of "happy path" operations in the 
            // flow of the provider.
            this.mockFixture.DataClient.OnGetState<VirtualClientWorkloadProvider.State>()
                .Returns(Task.FromResult(this.providerState));

            // Because a static collection is used in the provider, individual process IDs must be 
            // unique or we will hit race conditions that cause the unit tests to fail with false negative
            // results.
            this.mockProcess.Setup(process => process.Id)
                .Returns(VirtualClientWorkloadProviderTests.currentProcessId++);

            this.mockProcess.Setup(process => process.HasExited)
                .Returns(false);

            this.mockProcess.Setup(process => process.ExitCode)
                .Returns(-1);

            this.mockProcess.Setup(process => process.StartInfo)
                .Returns(new ProcessStartInfo
                {
                    FileName = @"C:\users\any\temp\NuGet\Packages\virtualclient\1.2.3\content\win-x64\VirtualClient.exe",
                    Arguments = "--profile=any.profile --platform=Juno --metadata=key=value",
                    WorkingDirectory = @"C:\users\any\temp\NuGet\Packages\virtualclient\1.2.3\content\win-x64"
                });

            this.mockProcess.Setup(process => process.Start())
                .Returns(true);

            this.provider.OnCreateProcess = () => this.mockProcess.Object;
        }

        /// <summary>
        /// Used to supply overrides to the creation of the process proxy
        /// </summary>
        private class TestVirtualClientWorkloadProvider : VirtualClientWorkloadProvider
        {
            public TestVirtualClientWorkloadProvider(IServiceCollection services)
                : base(services)
            {
            }

            public Func<IProcessProxy> OnCreateProcess { get; set; }

            public new void ValidateParameters(ExperimentComponent component)
            {
                base.ValidateParameters(component);
            }

            protected async override Task<IProcessProxy> CreateProcessAsync(
                ExperimentContext context, ExperimentComponent component, IDictionary<string, string> metadata, string specificationFilePath, string layoutFilePath, CancellationToken cancellationToken)
            {
                await Task.Delay(1);
                return this.OnCreateProcess?.Invoke();
            }
        }

        /// <summary>
        /// Used to expose protected methods of the underlying provider
        /// </summary>
        private class TestVirtualClientWorkloadProvider2 : VirtualClientWorkloadProvider
        {
            public TestVirtualClientWorkloadProvider2(IServiceCollection services)
                : base(services)
            {
            }

            public Action<AadPrincipalSettings, Uri, string> OnGetAdminCredential { get; set; }

            public static new IDictionary<string, string> CreateMetadata(ExperimentContext context, ExperimentComponent component, AgentIdentification agentId, EventContext telemetryContext)
            {
                return VirtualClientWorkloadProvider.CreateMetadata(context, component, agentId, telemetryContext);
            }

            public new Task<IProcessProxy> CreateProcessAsync(ExperimentContext context, ExperimentComponent component, IDictionary<string, string> metadata, string specificationFilePath, string layoutFilePath, CancellationToken cancellationToken)
            {
                return base.CreateProcessAsync(context, component, metadata, specificationFilePath, layoutFilePath, cancellationToken);
            }
        }

        [ExecutionConstraints(SupportedStepType.Dependency, SupportedStepTarget.ExecuteOnVirtualMachine)]
        private class TestDependencyProvider : ExperimentProvider
        {
            public TestDependencyProvider(IServiceCollection services)
                : base(services)
            {
            }

            protected override Task<ExecutionResult> ExecuteAsync(ExperimentContext context, ExperimentComponent component, EventContext telemetryContext, CancellationToken cancellationToken)
            {
                // Because we create the dependenty providers through factory -> reflection, we need
                // a way to confirm when a dependency is executed/installed and a way to control the
                // execution result in unit tests above. The services collection allows us to pass instance-level
                // objects from the unit tests to the individual dependency provider.
                IList<TestDependencyProvider> dependenciesInstalled;
                if (this.Services.TryGetService<IList<TestDependencyProvider>>(out dependenciesInstalled))
                {
                    // Track the dependencies executed/installed.
                    dependenciesInstalled.Add(this);
                }

                ExecutionResult result = null;
                if (!this.Services.TryGetService<ExecutionResult>(out result))
                {
                    result = new ExecutionResult(ExecutionStatus.Succeeded);
                }

                return Task.FromResult(result);
            }
        }
    }
}
