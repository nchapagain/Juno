namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class StringExtensionsTests
    {
        [Test]
        public void ToListValidatesParameters()
        {
            string nullString = null;
            Assert.Throws<ArgumentException>(() => nullString.ToList(','));
        }

        [Test]
        public void ToListReturnsExpectedListWhenThereAreMultipleElements()
        {
            string originalString = "one, two, three, four";
            IList<string> actualResult = originalString.ToList(',');

            IList<string> expectedResult = new List<string> { "one", "two", "three", "four" };
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void ToListReturnsExpectedListWhenThreIsOneElement()
        {
            string originalString = "one";
            IList<string> actualResult = originalString.ToList(',');

            IList<string> expectedResult = new List<string> { "one" };
            Assert.AreEqual(expectedResult, actualResult);
        }
    }
}
