namespace Juno.EnvironmentSelection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ProviderCacheKeyTests
    {
        private ProviderCacheKey key;

        [SetUp]
        public void SetUpTests()
        {
            this.key = new ProviderCacheKey()
            {
                [Guid.NewGuid().ToString()] = Guid.NewGuid().ToString(),
                [Guid.NewGuid().ToString()] = Guid.NewGuid().ToString()
            };
        }

        [Test]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void ProjectStringValidatesParameters(string invalidParameter)
        {
            Assert.Throws<ArgumentException>(() => ProviderCacheKey.ProjectString(new List<ProviderCacheKey>() { this.key }, Guid.NewGuid().ToString(), invalidParameter));
            Assert.Throws<ArgumentException>(() => ProviderCacheKey.ProjectString(new List<ProviderCacheKey>() { this.key }, invalidParameter, Guid.NewGuid().ToString()));
        }

        [Test]
        public void ProjectStringThrowsErrorWhenKeyAlreadyExists()
        {
            string presentKey = Guid.NewGuid().ToString();
            this.key.Add(presentKey, Guid.NewGuid().ToString());
            Assert.Throws<ArgumentException>(() => ProviderCacheKey.ProjectString(new List<ProviderCacheKey>() { this.key }, presentKey, Guid.NewGuid().ToString()));
        }

        [Test]
        public void ProjectStringReturnsExpectedValueWhenOriginalKeysAreEmpty()
        {
            string key = Guid.NewGuid().ToString();
            string value = Guid.NewGuid().ToString();
            List<ProviderCacheKey> originalList = new List<ProviderCacheKey>();
            IList<ProviderCacheKey> result = ProviderCacheKey.ProjectString(originalList, key, value);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);

            ProviderCacheKey resultKey = result[0];
            Assert.IsTrue(resultKey.ContainsKey(key));
            Assert.AreEqual(value, resultKey[key]);
        }

        [Test]
        public void ProjectStringReturnsListWhenOriginalKeysAreNotEmpty()
        {
            string key = Guid.NewGuid().ToString();
            string value = Guid.NewGuid().ToString();
            List<ProviderCacheKey> originalList = new List<ProviderCacheKey>() { this.key };
            IList<ProviderCacheKey> result = ProviderCacheKey.ProjectString(originalList, key, value);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);

            ProviderCacheKey actualResult = result[0];
            ProviderCacheKey expectedResult = new ProviderCacheKey(this.key)
            {
                { key, value }
            };
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void ExpandEntryValidatesStringParameters(string invalidParameter)
        {
            Assert.Throws<ArgumentException>(() => this.key.ExpandEntry(invalidParameter, 'o'));
        }

        [Test]
        public void ExpandEntryThrowsErrorWhenEntryIsNotPresent()
        {
            Assert.Throws<ArgumentException>(() => this.key.ExpandEntry(Guid.NewGuid().ToString(), 'o'));
        }

        [Test]
        public void ExpandEntryReturnsExpectedResultWhenValueIsNotAList()
        {
            string key = Guid.NewGuid().ToString();
            string value = "Im not a list";

            ProviderCacheKey cacheKey = new ProviderCacheKey(this.key)
            { { key, value } };

            List<ProviderCacheKey> result = cacheKey.ExpandEntry(key, ',').ToList();
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);

            ProviderCacheKey actualKeyResult = result[0];
            ProviderCacheKey expectedKeyResult = new ProviderCacheKey(cacheKey);
            Assert.AreEqual(expectedKeyResult, actualKeyResult);
        }

        [Test]
        public void ExpandEntryReturnsExpectedResultWhenValueIsAList()
        {
            string key = Guid.NewGuid().ToString();
            string value = "i, am, a, list";
            ProviderCacheKey cacheKey = new ProviderCacheKey()
            { { key, value } };

            List<ProviderCacheKey> expectedResult = new List<ProviderCacheKey>()
            {
                new ProviderCacheKey() { { key, "i" } },
                new ProviderCacheKey() { { key, "am" } },
                new ProviderCacheKey() { { key, "a" } },
                new ProviderCacheKey() { { key, "list" } }
            };

            List<ProviderCacheKey> actualResult = cacheKey.ExpandEntry(key, ',').ToList();
            Assert.AreEqual(expectedResult.Count, actualResult.Count);
            Assert.IsTrue(expectedResult.All(actualResult.Contains));
        }

        [Test]
        public void ExpandEntryPreservesOtherThatShouldNotBeExpanded()
        {
            string key = Guid.NewGuid().ToString();
            string value = "i, am, a, list";
            ProviderCacheKey cacheKey = new ProviderCacheKey(this.key)
            { { key, value } };

            List<ProviderCacheKey> expectedResult = new List<ProviderCacheKey>()
            {
                new ProviderCacheKey(this.key) { { key, "i" } },
                new ProviderCacheKey(this.key) { { key, "am" } },
                new ProviderCacheKey(this.key) { { key, "a" } },
                new ProviderCacheKey(this.key) { { key, "list" } }
            };

            List<ProviderCacheKey> actualResult = cacheKey.ExpandEntry(key, ',').ToList();
            Assert.AreEqual(expectedResult.Count, actualResult.Count);
            Assert.IsTrue(expectedResult.All(actualResult.Contains));
        }

        [Test]
        public void ExpandEntryReturnsExpectedResultWhenStringHasReservedChars()
        {
            string key = Guid.NewGuid().ToString();
            string value = "[ \r\n i, am, a, list \r\n ]";
            ProviderCacheKey cacheKey = new ProviderCacheKey(this.key)
            { { key, value } };

            List<ProviderCacheKey> expectedResult = new List<ProviderCacheKey>()
            {
                new ProviderCacheKey(this.key) { { key, "i" } },
                new ProviderCacheKey(this.key) { { key, "am" } },
                new ProviderCacheKey(this.key) { { key, "a" } },
                new ProviderCacheKey(this.key) { { key, "list" } }
            };

            List<ProviderCacheKey> actualResult = cacheKey.ExpandEntry(key, ',').ToList();
            Assert.AreEqual(expectedResult.Count, actualResult.Count);
            Assert.IsTrue(expectedResult.All(actualResult.Contains));
        }

        [Test]
        [TestCase('[')]
        [TestCase(']')]
        [TestCase('\r')]
        [TestCase('\n')]
        [TestCase('\"')]
        [TestCase(' ')]
        public void ExpandEntryThrowsErrorWhenSplitByCharIsAReservedChar(char invalidParam)
        {
            Assert.Throws<ArgumentException>(() => this.key.ExpandEntry("someentry", invalidParam));
        }
    }
}
