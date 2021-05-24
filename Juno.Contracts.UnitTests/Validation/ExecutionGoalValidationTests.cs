namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using AutoFixture;
    using Juno.Contracts;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ExecutionGoalValidationTests
    {
        private Fixture mockFixture;
        private GoalBasedSchedule mockExecutionGoal;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetUpGoalBasedScheduleMocks();
            this.mockExecutionGoal = this.mockFixture.Create<GoalBasedSchedule>();
        }

        [Test]
        public void ExecutionGoalValidationReturnsSameReferenceOnInstance()
        {
            ExecutionGoalValidation instance1 = ExecutionGoalValidation.Instance;
            ExecutionGoalValidation instance2 = ExecutionGoalValidation.Instance;

            Assert.IsTrue(object.ReferenceEquals(instance1, instance2));
        }

        [Test]
        public void ValidateValidatesParametersBeforeValidation()
        {
            ExecutionGoalValidation instance = ExecutionGoalValidation.Instance;
            Assert.Throws<ArgumentException>(() => instance.Validate(null));
        }

        [Test]
        public void ValiateReturnsExpectedResultWhenAllValidatesReturnValid()
        {
            ExecutionGoalValidation instance = ExecutionGoalValidation.Instance;
            instance.Add(new TestSuccessValidation());
            ValidationResult actualResult = instance.Validate(this.mockExecutionGoal);
            ValidationResult expectedResult = new ValidationResult(true);

            Assert.IsTrue(actualResult.ValidationErrors?.Any() == false);
            Assert.AreEqual(expectedResult.IsValid, actualResult.IsValid);
        }

        [Test]
        public void ValidateReturnsExpectedResultWhenAllValidatesReturnInValid()
        {
            TestFailedValidation validationRule = new TestFailedValidation();
            string errorMessage = "This is an error message";
            validationRule.OnValidate = data => { return new ValidationResult(false, new List<string> { errorMessage }); };

            ExecutionGoalValidation instance = ExecutionGoalValidation.Instance;
            instance.Add(validationRule);

            ValidationResult actualResult = instance.Validate(this.mockExecutionGoal);
            ValidationResult expectedResult = new ValidationResult(false, new List<string> { errorMessage });

            Assert.AreEqual(actualResult.ValidationErrors, expectedResult.ValidationErrors);
            Assert.AreEqual(expectedResult.IsValid, actualResult.IsValid);
        }

        private class TestSuccessValidation : IValidationRule<GoalBasedSchedule>
        {
            public Func<GoalBasedSchedule, ValidationResult> OnValidate { get; set; }

            public ValidationResult Validate(GoalBasedSchedule data)
            {
                return this.OnValidate != null ? this.OnValidate.Invoke(data)
                    : new ValidationResult(true);
            }
        }

        private class TestFailedValidation : IValidationRule<GoalBasedSchedule>
        { 
            public Func<GoalBasedSchedule, ValidationResult> OnValidate { get; set; }

            public ValidationResult Validate(GoalBasedSchedule data)
            {
                return this.OnValidate != null ? this.OnValidate.Invoke(data)
                    : new ValidationResult(false);
            }
        }
    }
}
