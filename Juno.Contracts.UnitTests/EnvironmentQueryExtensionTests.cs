namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using AutoFixture;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class EnvironmentQueryExtensionTests
    {
        private Fixture mockFixture;
        private EnvironmentQuery parameterizedQuery;

        [SetUp]
        public void SetupTests()
        {
            this.mockFixture = new Fixture();
            this.mockFixture.SetupEnvironmentSelectionMocks();
            this.parameterizedQuery = new EnvironmentQuery("foobar", 8, new List<EnvironmentFilter>()
            { 
                new EnvironmentFilter("filter1", new Dictionary<string, IConvertible>()
                { 
                    ["parameter1"] = "$.external.vmSku",
                    ["parameter2"] = "$.subscription.region"
                })
            });
        }

        [Test]
        public void HasExternalReferenceValidatesParameters()
        {
            EnvironmentQuery nullQuery = null;
            Assert.Throws<ArgumentException>(() => nullQuery.HasExternalReferences());
        }

        [Test]
        public void HasSubscriptionReferenceValidatesParameters()
        {
            EnvironmentQuery nullQuery = null;
            Assert.Throws<ArgumentException>(() => nullQuery.HasSubscriptionReferences());
        }

        [Test]
        public void ReplaceExternalReferenceValidatesParameters()
        {
            EnvironmentQuery nullQuery = null;
            Assert.Throws<ArgumentException>(() => nullQuery.ReplaceExternalReferences());
        }

        [Test]
        public void ReplaceSubscriptionReferenceValidatesParameters()
        {
            EnvironmentQuery nullQuery = null;
            Assert.Throws<ArgumentException>(() => nullQuery.ReplaceSubscriptionReferences());
        }

        [Test]
        public void HasExternalReferenceReturnsExpectedResultWhenReferenceIsAbsent()
        {
            EnvironmentQuery query = this.mockFixture.Create<EnvironmentQuery>();
            bool result = query.HasExternalReferences();

            Assert.IsFalse(result);
        }

        [Test]
        public void HasSubscriptionResferenceReturnsExpectedResutlWhenReferenceIsAbsent()
        {
            EnvironmentQuery query = this.mockFixture.Create<EnvironmentQuery>();
            bool result = query.HasSubscriptionReferences();

            Assert.IsFalse(result);
        }

        [Test]
        public void HasExternalReferenceReturnsExpectedResultWhenReferenceIsPresent()
        {
            bool result = this.parameterizedQuery.HasExternalReferences();
            Assert.IsTrue(result);
        }

        [Test]
        public void HasSubscriptionReferenceReturnsExpectedResultWhenReferenceIsPresent()
        {
            bool result = this.parameterizedQuery.HasSubscriptionReferences();
            Assert.IsTrue(result);
        }

        [Test]
        public void ReplaceExternalReferenceDoesNotChangeObjectWhenReferenceIsAbsent()
        {
            EnvironmentQuery query = this.mockFixture.Create<EnvironmentQuery>();
            EnvironmentQuery otherQuery = new EnvironmentQuery(query);

            query = query.ReplaceExternalReferences();

            Assert.AreEqual(otherQuery, query);
        }

        [Test]
        public void ReplaceSubscriptionReferenceDoesNotChangeObjectWhenReferenceIsAbsent()
        {
            EnvironmentQuery query = this.mockFixture.Create<EnvironmentQuery>();
            EnvironmentQuery otherQuery = new EnvironmentQuery(query);

            query = query.ReplaceSubscriptionReferences();

            Assert.AreEqual(otherQuery, query);
        }

        [Test]
        public void ReplaceExternalReferencesReturnsExpectedResultWhenReferenceIsPresent()
        {
            const string value = "Standard_D2_v3";
            this.parameterizedQuery.Parameters.Add("vmSku", value);

            EnvironmentQuery expectedQuery = new EnvironmentQuery(this.parameterizedQuery);
            expectedQuery.Filters.First().Parameters["parameter1"] = value;

            this.parameterizedQuery = this.parameterizedQuery.ReplaceExternalReferences();

            Assert.AreEqual(expectedQuery, this.parameterizedQuery);
        }

        [Test]
        public void ReplaceSubscriptionReferencesReturnsExpectedResultWhenReferenceIsPresent()
        {
            const string value = "useast,usesat2";
            this.parameterizedQuery.Parameters.Add("region", value);

            EnvironmentQuery expectedQuery = new EnvironmentQuery(this.parameterizedQuery);
            expectedQuery.Filters.First().Parameters["parameter2"] = value;

            this.parameterizedQuery = this.parameterizedQuery.ReplaceSubscriptionReferences();

            Assert.AreEqual(expectedQuery, this.parameterizedQuery);
        }
    }
}
