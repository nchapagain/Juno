namespace Juno.Contracts.Validation
{
    using System;
    using System.Linq;
    using AutoFixture;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExperimentOwnerEmailValidationTests
    {
        private Fixture mockFixture;
        private GoalBasedSchedule validExecutionGoal;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();

            this.validExecutionGoal = this.mockFixture.Create<GoalBasedSchedule>();
        }

        [Test]
        [TestCase("experimentowner@microsoft.com")]
        [TestCase("EXPERIMENTOWNER1@MICROSOFT.COM")]
        [TestCase("experiment.owner-2@Microsoft.com")]
        [TestCase("exper!ment_Owner3@MiCrOsOfT.CoM")]
        [TestCase("3xp3r1m3nt_0wn3r@microsoft.com")]
        [TestCase("experimentowner@microsoft.com; experimentOwner3@MiCrOsOfT.CoM")]
        [TestCase(" experimentowner2@Microsoft.com experimentOwner3@MiCrOsOfT.CoM ")]
        [TestCase("experimentOwner3@MiCrOsOfT.CoM, experimentOwner4@microsoft.com  ")]
        [TestCase("experimentOwner4@microsoft.com\texperimentowner@microsoft.com")]
        [TestCase("   experimentowner@microsoft.com; experimentowner2@Microsoft.com\texperimentOwner3@MiCrOsOfT.CoM, experimentOwner4@microsoft.com")]
        public void ExperimentOwnerEmailValidationValidatesExperimentOwnerEmails(string validEmails)
        {
            GoalBasedSchedule executionGoal = this.CreateExecutionGoalWithGivenExperimentOwnerDistributionList(validEmails);
            ValidationResult result = ExperimentOwnerEmailRules.Instance.Validate(executionGoal);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Any() != true);

            Assert.AreEqual(executionGoal.Owner, validEmails);
        }

        [Test]
        [TestCase("www.Microsoft.com")]
        [TestCase("CRC AIR")]
        [TestCase("v-alias")]
        [TestCase("alias@microsoft")]
        [TestCase("user@example.com")]
        [TestCase("user@AME.gbl")]
        [TestCase("EXPERIMENTOWNER1@MICROSOFT.COM.CO.UK")]
        [TestCase("experimentowner@microsoft.com! experimentOwner3@MiCrOsOfT.Co M")]
        [TestCase(" experimentowner2@Microsoft.com/experimentOwner3@MiCrOsOfT.CoM ")]
        [TestCase("experimentOwner3@MiCrOsOfT.CoMexperimentOwner4@microsoft.com")]
        public void ExperimentOwnerEmailValidationRejectsInvalidEmailFormats(string invalidEmail)
        {
            GoalBasedSchedule executionGoal = this.CreateExecutionGoalWithGivenExperimentOwnerDistributionList(invalidEmail);
            ValidationResult result = ExperimentOwnerEmailRules.Instance.Validate(executionGoal);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ValidationErrors?.Any() == true);
            Assert.IsTrue(result.ValidationErrors?.Count() >= 1);
        }

        [Test]
        [TestCase("www.Microsoft.com")]
        [TestCase("CRC AIR")]
        [TestCase("v-alias")]
        [TestCase("alias@microsoft")]
        [TestCase("user@example.com")]
        [TestCase("user@AME.gbl")]
        [TestCase("experimentOwner @microsoft.com")]
        [TestCase("experimentOwner@microsoft.net")]
        [TestCase("experimentOwner@micorsoft.net")]
        public void ExperimentOwnerEmailValidationIdentifiesInvalidEmailsAmongMultiple(string invalidEmail)
        {
            string emails = string.Concat("experimentOwner@microsoft.com; ", invalidEmail, "\tEXPERIMENTOWNER!@microsoft.com");
            GoalBasedSchedule executionGoal = this.CreateExecutionGoalWithGivenExperimentOwnerDistributionList(emails);
            ValidationResult result = ExperimentOwnerEmailRules.Instance.Validate(executionGoal);

            char[] delimiterChars = { ' ', ',', ';', '\t' };
            string[] distributionList = invalidEmail.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
            foreach (string recipient in distributionList)
            {
                string expectedError = "The execution goal provided failed schema validation. An experiment owner email address is in an invalid format. Please use a " +
                    $"valid email address on the Microsoft domain. {Environment.NewLine}Expected format: alias@microsoft.com. {Environment.NewLine}" +
                    $"Email provided: {recipient}";

                Assert.IsNotNull(result);
                Assert.IsFalse(result.IsValid);
                Assert.IsTrue(result.ValidationErrors?.Any() == true);
                Assert.IsTrue(result.ValidationErrors.Contains(expectedError));
            }
        }

        private GoalBasedSchedule CreateExecutionGoalWithGivenExperimentOwnerDistributionList(string distributionList)
        {
            GoalBasedSchedule mockSchedule = this.mockFixture.Create<GoalBasedSchedule>();

            if (mockSchedule.Metadata.ContainsKey(ExecutionGoalMetadata.Owner))
            {
                mockSchedule.Metadata.Remove(ExecutionGoalMetadata.Owner);
            }

            mockSchedule.Metadata.Add(ExecutionGoalMetadata.Owner, distributionList);

            return new GoalBasedSchedule(
                    mockSchedule.ExperimentName,
                    mockSchedule.Description,
                    mockSchedule.Experiment,
                    mockSchedule.TargetGoals,
                    mockSchedule.ControlGoals,
                    mockSchedule.Metadata);
        }
    }
}