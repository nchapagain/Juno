namespace Juno.Execution.AgentRuntime.CommandLine
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.Invocation;
    using System.CommandLine.Parsing;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.CRC;
    using Microsoft.Azure.CRC.Extensions;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class CommandExecutionTests
    {
        private IServiceCollection services;
        private string[] exampleCommandLine1;
        private string[] exampleCommandLine2;

        [SetUp]
        public void SetupTest()
        {
            this.services = new ServiceCollection();
            this.exampleCommandLine1 = new string[]
            {
                "--environment", "\"AnyEnvironment\"",
                "--region", "\"AnyRegion\""
            };

            this.exampleCommandLine2 = new string[]
            {
                "--environment", "\"AnyEnvironment\"",
                "--configurationPath", "\"C:\\any\\path\\to\\environmentsettings.json\"",
                "--region", "\"AnyRegion\""
            };
        }

        [Test]
        [TestCase("--version")]
        public void CommandSupportsExpectedVersionArguments(string argument)
        {
            string[] args = new string[] { argument };

            CommandLineBuilder commandBuilder = TestCommandExecution.CreateBuilder(args, CancellationToken.None, this.services)
                .WithDefaults();

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
        public void CommandSupportsExpectedHelpArguments(string argument)
        {
            string[] args = new string[] { argument };

            CommandLineBuilder commandBuilder = TestCommandExecution.CreateBuilder(args, CancellationToken.None, this.services)
                .WithDefaults();

            ParseResult parseResults = commandBuilder.Build().Parse(args);
            int exitCode = parseResults.Invoke();

            Assert.IsFalse(parseResults.Errors.Any());
            Assert.IsTrue(parseResults.Tokens.Count == 1);
            Assert.IsTrue(parseResults.Tokens[0].Value == argument);
            Assert.IsTrue(exitCode == 0);
        }

        [Test]
        public void CommandReturnsExpectedResultsWhenARequiredOptionIsMissing()
        {
            string[] invalidCommandLine = this.exampleCommandLine1.Skip(2).ToArray();

            CommandLineBuilder commandBuilder = TestCommandExecution.CreateBuilder(invalidCommandLine, CancellationToken.None, this.services)
                .WithDefaults();

            ParseResult parseResults = commandBuilder.Build().Parse(invalidCommandLine);
            int exitCode = parseResults.Invoke();

            Assert.IsTrue(parseResults.Errors.Any());
            Assert.IsTrue(exitCode > 0);
        }

        [Test]
        public void CommandSetsExpectedPropertiesToMatchTheCommandLineArgument_WhenEnvironmentOptionIsProvided()
        {
            // To get a reference to the command underneath, we are adding it to the 'services' collection
            // passed to the builder method above. This allows us to get it from there so that we can verify the
            // properties are set to expected values on the command.
            CommandLineBuilder commandBuilder = TestCommandExecution.CreateBuilder(this.exampleCommandLine1, CancellationToken.None, this.services)
              .WithDefaults();

            ParseResult parseResults = commandBuilder.Build().Parse(this.exampleCommandLine1);

            Assert.IsFalse(parseResults.Errors.Any());
            Assert.AreEqual(this.exampleCommandLine1.Length, parseResults.Tokens.Count);
            Assert.DoesNotThrow(() => parseResults.InvokeAsync().GetAwaiter().GetResult());

            // Get the command from the services collection as noted above.
            TestCommandExecution command = this.services.GetService<TestCommandExecution>();
            CommandExecutionTests.AssertCommandPropertiesSet(parseResults, command);
        }

        [Test]
        public void CommandSetsExpectedPropertiesToMatchTheCommandLineArgument_WhenConfigurationPathOptionIsProvided()
        {
            // To get a reference to the command underneath, we are adding it to the 'services' collection
            // passed to the builder method above. This allows us to get it from there so that we can verify the
            // properties are set to expected values on the command.
            CommandLineBuilder commandBuilder = TestCommandExecution.CreateBuilder(this.exampleCommandLine2, CancellationToken.None, this.services)
              .WithDefaults();

            ParseResult parseResults = commandBuilder.Build().Parse(this.exampleCommandLine2);

            Assert.IsFalse(parseResults.Errors.Any());
            Assert.AreEqual(this.exampleCommandLine2.Length, parseResults.Tokens.Count);
            Assert.DoesNotThrow(() => parseResults.InvokeAsync().GetAwaiter().GetResult());

            // Get the command from the services collection as noted above.
            TestCommandExecution command = this.services.GetService<TestCommandExecution>();
            CommandExecutionTests.AssertCommandPropertiesSet(parseResults, command);
        }

        [Test]
        public void CommandHandlesSpacesInArgumentValues_WithQuotes()
        {
            this.exampleCommandLine1[1] = "\"Any Environment\"";
            this.exampleCommandLine1[3] = "\"Any  Region\"";

            // To get a reference to the command underneath, we are adding it to the 'services' collection
            // passed to the builder method above. This allows us to get it from there so that we can verify the
            // properties are set to expected values on the command.
            CommandLineBuilder commandBuilder = TestCommandExecution.CreateBuilder(this.exampleCommandLine1, CancellationToken.None, this.services)
              .WithDefaults();

            ParseResult parseResults = commandBuilder.Build().Parse(this.exampleCommandLine1);

            Assert.IsFalse(parseResults.Errors.Any());
            Assert.AreEqual(this.exampleCommandLine1.Length, parseResults.Tokens.Count);
            Assert.DoesNotThrow(() => parseResults.InvokeAsync().GetAwaiter().GetResult());

            // Get the command from the services collection as noted above.
            TestCommandExecution command = this.services.GetService<TestCommandExecution>();
            CommandExecutionTests.AssertCommandPropertiesSet(parseResults, command);
        }

        private static void AssertCommandPropertiesSet(ParseResult parserResults, TestCommandExecution command)
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

        private class TestCommandExecution : CommandExecution
        {
            public TestCommandExecution()
            {
            }

            /// <summary>
            /// The Azure region in which the VM is running.
            /// </summary>
            public string Region { get; set; }

            public static CommandLineBuilder CreateBuilder(string[] args, CancellationToken cancellationToken, IServiceCollection services = null)
            {
                RootCommand rootCommand = new RootCommand("A root command for testing.")
                {
                    OptionFactory.CreateEnvironmentOption(true),
                    OptionFactory.CreateConfigurationPathOption(),
                    OptionFactory.CreateRegionOption()
                };

                rootCommand.Handler = CommandHandler.Create<TestCommandExecution>((handler) => handler.ExecuteAsync(args, cancellationToken, services));

                return new CommandLineBuilder(rootCommand);
            }

            public override Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken, IServiceCollection services = null)
            {
                services?.AddSingleton(this);
                return Task.FromResult(0);
            }
        }
    }
}
