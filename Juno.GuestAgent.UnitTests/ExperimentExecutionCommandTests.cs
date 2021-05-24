namespace Juno.GuestAgent
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.Invocation;
    using System.CommandLine.Parsing;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Juno.Execution.AgentRuntime.CommandLine;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentExecutionCommandTests
    {
        private FixtureDependencies mockFixture;
        private string[] exampleCommandLine;

        [SetUp]
        public void SetupTest()
        {
            this.mockFixture = new FixtureDependencies();
            this.exampleCommandLine = new string[]
            {
                "--environment", "\"anyEnvironment\"",
                "--agentId", "\"cluster,node,vm,tip\"",
                "--vmSku", "\"Standard_Issue_v3\"",
                "--region", "\"South Africa North\""
            };
        }

        [Test]
        [TestCase("--version")]
        public void ExperimentExecutionCommandSupportsExpectedVersionArguments(string argument)
        {
            string[] args = new string[] { argument };

            CommandLineBuilder commandBuilder = ExperimentExecutionCommand.CreateBuilder(args, CancellationToken.None)
                .WithDefaults();

            commandBuilder.Command.Handler = CommandHandler.Create<ExperimentExecutionCommand>((handler) => handler.ExecuteAsync(args, CancellationToken.None));

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
        public void ExperimentExecutionCommandSupportsExpectedHelpArguments(string argument)
        {
            string[] args = new string[] { argument };

            CommandLineBuilder commandBuilder = ExperimentExecutionCommand.CreateBuilder(args, CancellationToken.None)
                .WithDefaults();

            commandBuilder.Command.Handler = CommandHandler.Create<ExperimentExecutionCommand>((handler) => handler.ExecuteAsync(args, CancellationToken.None));

            ParseResult parseResults = commandBuilder.Build().Parse(args);
            int exitCode = parseResults.Invoke();

            Assert.IsTrue(parseResults.Tokens.Count == 1);
            Assert.IsTrue(parseResults.Tokens[0].Value == argument);
            Assert.IsTrue(exitCode == 0);
        }

        [Test]
        public void ExperimentExecutionCommandSetsExpectedPropertiesToMatchTheCommandLineArgument()
        {
            CommandLineBuilder commandBuilder = ExperimentExecutionCommand.CreateBuilder(this.exampleCommandLine, CancellationToken.None)
                .WithDefaults();

            // To get a reference to the command underneath, we are adding it to the 'services' collection
            // passed to the builder method above. This allows us to get it from there so that we can verify the
            // properties are set to expected values on the command.
            commandBuilder.Command.Handler = CommandHandler.Create<TestExperimentExecutionCommand>(
                (handler) => handler.ExecuteAsync(this.exampleCommandLine, CancellationToken.None, this.mockFixture.Services));

            ParseResult parseResults = commandBuilder.Build().Parse(this.exampleCommandLine);

            Assert.AreEqual(this.exampleCommandLine.Length, parseResults.Tokens.Count);
            Assert.DoesNotThrow(() => parseResults.InvokeAsync().GetAwaiter().GetResult());

            // Get the command from the services collection as noted above.
            TestExperimentExecutionCommand command = this.mockFixture.Services.GetService<TestExperimentExecutionCommand>();
            ExperimentExecutionCommandTests.AssertCommandPropertiesSet(parseResults, command);
        }

        [Test]
        public void ExperimentExecutionCommandSupportsResponseFiles()
        {
            string[] args = new string[] { "@Startup.rsp" };

            CommandLineBuilder commandBuilder = ExperimentExecutionCommand.CreateBuilder(args, CancellationToken.None)
                .WithDefaults();

            // To get a reference to the command underneath, we are adding it to the 'services' collection
            // passed to the builder method above. This allows us to get it from there so that we can verify the
            // properties are set to expected values on the command.
            commandBuilder.Command.Handler = CommandHandler.Create<TestExperimentExecutionCommand>(
                (handler) => handler.ExecuteAsync(args, CancellationToken.None, this.mockFixture.Services));

            ParseResult parseResults = commandBuilder.Build().Parse(args);
            Assert.IsFalse(parseResults.Errors.Any());
            Assert.DoesNotThrow(() => parseResults.InvokeAsync().GetAwaiter().GetResult());

            // Get the command from the services collection as noted above.
            TestExperimentExecutionCommand command = this.mockFixture.Services.GetService<TestExperimentExecutionCommand>();
            ExperimentExecutionCommandTests.AssertCommandPropertiesSet(parseResults, command);
        }

        [Test]
        public void InstallationCommandValidatesRequiredParameters()
        {
            string[] args = new string[]
            {
                "--agentId", "anyAgentId",
            };

            CommandLineBuilder commandBuilder = ExperimentExecutionCommand.CreateBuilder(args, CancellationToken.None)
                .WithDefaults();

            ParseResult parseResults = commandBuilder.Build().Parse(args);
            int exitCode = parseResults.Invoke();

            Assert.NotZero(parseResults.Errors.Count());
            Assert.IsTrue(exitCode > 0);
        }

        private static void AssertCommandPropertiesSet(ParseResult parserResults, TestExperimentExecutionCommand command)
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

        private class TestExperimentExecutionCommand : ExperimentExecutionCommand
        {
            public override Task<int> ExecuteAsync(string[] args, CancellationToken token, IServiceCollection services = null)
            {
                services.AddSingleton(this);
                return Task.FromResult(0);
            }
        }
    }
}
