namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExecutionResultTests
    {
        [Test]
        public void ExpectedStatusesAreConsideredCompleted()
        {
            List<ExecutionStatus> expectedCompletedStatuses = new List<ExecutionStatus>
            {
                ExecutionStatus.Cancelled,
                ExecutionStatus.Failed,
                ExecutionStatus.Succeeded,
                ExecutionStatus.SystemCancelled
            };

            foreach (ExecutionStatus status in Enum.GetValues(typeof(ExecutionStatus)))
            {
                if (expectedCompletedStatuses.Contains(status))
                {
                    Assert.IsTrue(ExecutionResult.CompletedStatuses.Contains(status));
                }
                else
                {
                    Assert.IsFalse(ExecutionResult.CompletedStatuses.Contains(status));
                }
            }
        }

        [Test]
        public void ExpectedStatusesAreConsideredTerminal()
        {
            List<ExecutionStatus> expectedTerminalStatuses = new List<ExecutionStatus>
            {
                ExecutionStatus.Cancelled,
                ExecutionStatus.Failed,
                ExecutionStatus.SystemCancelled
            };

            foreach (ExecutionStatus status in Enum.GetValues(typeof(ExecutionStatus)))
            {
                if (expectedTerminalStatuses.Contains(status))
                {
                    Assert.IsTrue(ExecutionResult.TerminalStatuses.Contains(status));
                }
                else
                {
                    Assert.IsFalse(ExecutionResult.TerminalStatuses.Contains(status));
                }
            }
        }

        [Test]
        public void ExecutionResultReturnsTheExpectedDefaultTimeExtension()
        {
            List<ExecutionResult> results = new List<ExecutionResult>
            {
                new ExecutionResult(ExecutionStatus.InProgress),
                new ExecutionResult(ExecutionStatus.InProgress)
            };

            TimeSpan expectedExtension = TimeSpan.FromSeconds(1);
            TimeSpan actualExtension = ExecutionResult.GetRelativeTimeExtension(results);

            Assert.AreEqual(expectedExtension, actualExtension);
        }

        [Test]
        public void ExecutionResultReturnsTheExpectedTimeExtensionWhenRequested_Scenario1()
        {
            TimeSpan expectedExtension = TimeSpan.FromSeconds(30);
            List<ExecutionResult> results = new List<ExecutionResult>
            {
                new ExecutionResult(ExecutionStatus.InProgress)
                {
                    Extension = expectedExtension
                },
                new ExecutionResult(ExecutionStatus.InProgress),
            };

            TimeSpan actualExtension = ExecutionResult.GetRelativeTimeExtension(results);

            Assert.AreEqual(expectedExtension, actualExtension);
        }

        [Test]
        public void ExecutionResultReturnsTheExpectedTimeExtensionWhenRequested_Scenario2()
        {
            TimeSpan expectedExtension = TimeSpan.FromSeconds(30);
            List<ExecutionResult> results = new List<ExecutionResult>
            {
                new ExecutionResult(ExecutionStatus.InProgress),
                new ExecutionResult(ExecutionStatus.InProgress)
                {
                    Extension = expectedExtension
                }
            };

            TimeSpan actualExtension = ExecutionResult.GetRelativeTimeExtension(results);

            Assert.AreEqual(expectedExtension, actualExtension);
        }

        [Test]
        public void ExecutionResultReturnsTheExpectedTimeExtensionWhenRequested_Scenario3()
        {
            TimeSpan expectedExtension = TimeSpan.FromSeconds(30);
            List<ExecutionResult> results = new List<ExecutionResult>
            {
                new ExecutionResult(ExecutionStatus.InProgress)
                {
                    Extension = expectedExtension.Subtract(TimeSpan.FromSeconds(1))
                },
                new ExecutionResult(ExecutionStatus.InProgress)
                {
                    // Given multiple timestamp extensions, the largest one will be used.
                    Extension = expectedExtension
                }
            };

            TimeSpan actualExtension = ExecutionResult.GetRelativeTimeExtension(results);

            Assert.AreEqual(expectedExtension, actualExtension);
        }
    }
}
