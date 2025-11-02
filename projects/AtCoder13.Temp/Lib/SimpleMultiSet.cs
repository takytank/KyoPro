using System;
using System.Collections.Generic;
using System.Text;

namespace AtCoder.Temp.Lib
{
	public class SimpleMultiSet<T> :
		IEnumerable<T>,
		System.Collections.IEnumerable,
		ICollection<T>,
		System.Collections.ICollection,
		IReadOnlyCollection<T>
	{
		private readonly SortedSet<T> set_;
		private readonly Dictionary<T, int> counts_ = new Dictionary<T, int>();

		public SimpleMultiSet(
			int capacity,
			IEnumerable<T> collection = null,
			IComparer<T> comparer = null)
		{
			counts_ = new Dictionary<T, int>(capacity);
			if (collection == null && comparer == null) {
				set_ = new SortedSet<T>();
			} else if (collection == null) {
				set_ = new SortedSet<T>(comparer);
			} else if (comparer == null) {
				set_ = new SortedSet<T>(collection);
			} else {
				set_ = new SortedSet<T>(collection, comparer);
			}
		}

		public IComparer<T> Comparer => set_.Comparer;
		public int Count { get; private set; }
		public T Max => set_.Max;
		public T Min => set_.Min;

		public bool IsReadOnly => false;

		object System.Collections.ICollection.SyncRoot
			=> (set_ as System.Collections.ICollection).SyncRoot;

		bool System.Collections.ICollection.IsSynchronized
			=> (set_ as System.Collections.ICollection).IsSynchronized;

		public bool Add(T item)
		{
			if (counts_.ContainsKey(item) == false) {
				counts_[item] = 1;
			} else {
				counts_[item]++;
			}

			set_.Add(item);
			++Count;
			return true;
		}

		public bool Remove(T item)
		{
			if (counts_.ContainsKey(item) == false) {
				return false;
			}

			counts_[item]--;
			Count--;
			if (counts_[item] == 0) {
				counts_.Remove(item);
				set_.Remove(item);
			}

			return true;
		}

		public void Clear()
		{
			counts_.Clear();
			Count = 0;
			set_.Clear();
		}

		public bool Contains(T item) => set_.Contains(item);

		public IEnumerator<T> GetEnumerator()
		{
			foreach (var s in set_) {
				for (int i = 0; i < counts_[s]; i++) {
					yield return s;
				}
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> GetEnumerator();

		void ICollection<T>.Add(T item) => Add(item);

		public void CopyTo(T[] array, int arrayIndex)
		{
			int index = arrayIndex;
			foreach (var s in set_) {
				for (int i = 0; i < counts_[s]; i++) {
					array[index] = s;
					++index;
				}
			}
		}

		void System.Collections.ICollection.CopyTo(Array array, int index)
		{
		}
	}
}
