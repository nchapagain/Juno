namespace Juno.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class CronTabValidationUnitTests
    {
        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ValidateValidatesParameters(string invalidParameter)
        {
            Assert.Throws<ArgumentException>(() => CronTabValidation.Validate(invalidParameter, out string error));
        }

        [Test]
        [TestCase("Not a cron expression")]
        [TestCase("* * * * * * *")]
        [TestCase("1*/ * * * *")]
        [TestCase("/ * / * / *")]
        [TestCase("Monday tuesday")]
        [TestCase("*/61 * * * *")]
        [TestCase("* */25 * * *")]
        [TestCase("* * * */13 *")]
        public void ValidateReturnsAnErrorWhenGivenInvalidCronExpression(string cronExpression)
        {
            bool result = CronTabValidation.Validate(cronExpression, out string message);
            Assert.IsFalse(result);
            Assert.IsFalse(string.IsNullOrEmpty(message));
        }

        [Test]
        [TestCase("* * * * *")]
        [TestCase("*/10 * * * *")]
        [TestCase("* */5 * * *")]
        [TestCase("* * */4 * *")]
        [TestCase("* * */8 * *")]
        [TestCase("5 4 * * sun")]
        [TestCase("0 22 * * 1-5")]
        public void ValidateReturnsNoErrorWhenGivenCorrectCronExpression(string cronExpression)
        {
            bool result = CronTabValidation.Validate(cronExpression, out string message);
            Assert.IsTrue(result);
            Assert.IsTrue(string.IsNullOrEmpty(message));
        }
    }
}
