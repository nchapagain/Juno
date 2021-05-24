namespace Juno.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using NUnit.Framework;

    [TestFixture]
    [Category("Unit")]
    public class ObservableHashSetTests
    {
        [Test]
        public void ObservableHashSetAddsUniqueItemsToTheCollection_PrimitiveTypeScenario()
        {
            ObservableHashSet<int> collection = new ObservableHashSet<int>();

            collection.Add(1);
            collection.Add(2);

            Assert.IsTrue(collection.Count == 2);
            Assert.IsTrue(collection[0] == 1);
            Assert.IsTrue(collection[1] == 2);
        }

        [Test]
        public void ObservableHashSetAddsUniqueItemsToTheCollection_ComplexTypeScenario()
        {
            ObservableHashSet<ComplexObject> collection = new ObservableHashSet<ComplexObject>();

            collection.Add(new ComplexObject { Id = "Item1" });
            collection.Add(new ComplexObject { Id = "Item2" });

            Assert.IsTrue(collection.Count == 2);
            Assert.IsTrue(collection[0].Id == "Item1");
            Assert.IsTrue(collection[1].Id == "Item2");
        }

        [Test]
        public void ObservableHashSetPreventsDuplicateItemsFromBeingAddedToTheCollection_PrimitiveTypeScenario()
        {
            ObservableHashSet<int> collection = new ObservableHashSet<int>();

            collection.Add(1);
            collection.Add(2);

            // Attempt to add duplicates
            collection.Add(1);
            collection.Add(2);

            Assert.IsTrue(collection.Count == 2);
            Assert.IsTrue(collection[0] == 1);
            Assert.IsTrue(collection[1] == 2);
        }

        [Test]
        public void ObservableHashSetPreventsDuplicateItemsFromBeingAddedToTheCollection_ComplexTypeScenario()
        {
            List<ComplexObject> items = new List<ComplexObject>
            {
                new ComplexObject { Id = "Item1" },
                new ComplexObject { Id = "Item2" }
            };

            ObservableHashSet<ComplexObject> collection = new ObservableHashSet<ComplexObject>(items);

            // Attempt to add duplicates
            collection.Add(items[0]);
            collection.Add(items[1]);

            Assert.IsTrue(collection.Count == 2);
            Assert.IsTrue(collection[0].Id == "Item1");
            Assert.IsTrue(collection[1].Id == "Item2");
        }

        [Test]
        public void ObservableHashSetInvokesTheExpectedEventHandlersWhenNewItemsAreAdded()
        {
            ObservableHashSet<int> collection = new ObservableHashSet<int>();

            bool expectedEventHandlerInvoked = false;
            collection.CollectionChanged += (sender, args) =>
            {
                expectedEventHandlerInvoked = args.Action == NotifyCollectionChangedAction.Add
                    && args.NewItems != null
                    && args.NewItems.Count == 1;
            };

            collection.Add(1);
            Assert.IsTrue(expectedEventHandlerInvoked);
        }

        [Test]
        public void ObservableHashSetInvokesTheExpectedEventHandlersWhenItemsAreRemoved()
        {
            ObservableHashSet<int> collection = new ObservableHashSet<int>();
            collection.Add(1);

            bool expectedEventHandlerInvoked = false;
            collection.CollectionChanged += (sender, args) =>
            {
                expectedEventHandlerInvoked = args.Action == NotifyCollectionChangedAction.Remove
                    && args.OldItems != null
                    && args.OldItems.Count == 1;
            };

            collection.Remove(1);
            Assert.IsTrue(expectedEventHandlerInvoked);
        }

        [Test]
        public void ObservableHashSetInvokesTheExpectedEventHandlersWhenItemsAreReplaced()
        {
            ObservableHashSet<int> collection = new ObservableHashSet<int>();
            collection.Add(1);

            bool expectedEventHandlerInvoked = false;
            collection.CollectionChanged += (sender, args) =>
            {
                expectedEventHandlerInvoked = args.Action == NotifyCollectionChangedAction.Replace
                    && args.NewItems != null
                    && args.NewItems.Count == 1
                    && args.OldItems != null
                    && args.OldItems.Count == 1;
            };

            collection[0] = 2;
            Assert.IsTrue(expectedEventHandlerInvoked);
        }

        private class ComplexObject
        {
            public string Id { get; set; }

            public override int GetHashCode()
            {
                return this.Id.GetHashCode(StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
