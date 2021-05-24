namespace Juno.Providers
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    /// <summary>
    /// Implements a hash set/collection that provides notifications when items
    /// are either added or removed from the collection.
    /// </summary>
    /// <typeparam name="T">The data type of the items stored in the collection.</typeparam>
    public class ObservableHashSet<T> : ObservableCollection<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableHashSet{T}"/> class.
        /// </summary>
        public ObservableHashSet()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableHashSet{T}"/> class.
        /// </summary>
        public ObservableHashSet(IEnumerable<T> collection)
            : base(collection)
        {
        }

        /// <summary>
        /// Override checks to see if the item already exists in the collection (by hash code)
        /// before adding it.
        /// </summary>
        /// <param name="index">The index at which the item should be added.</param>
        /// <param name="item">The item to be added.</param>
        protected override void InsertItem(int index, T item)
        {
            if (!this.ItemExists(item))
            {
                base.InsertItem(index, item);
            }
        }

        private bool ItemExists(T item)
        {
            return this.Any(existing => existing?.GetHashCode() == item?.GetHashCode());
        }
    }
}
