using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public class PriorityQueue<T>
	{
		private readonly List<T> heap_;
		private readonly Comparison<T> comparison_;
		private readonly bool reverses_;

		public T Top { get { return heap_[0]; } }
		public int Count { get { return heap_.Count; } }

		public PriorityQueue(bool reverses = false)
			: this(Comparer<T>.Default, reverses) { }

		public PriorityQueue(int capacity, bool reverses = false)
			: this(capacity, Comparer<T>.Default.Compare, reverses) { }

		public PriorityQueue(IComparer<T> comparer, bool reverses = false)
			: this(16, comparer.Compare, reverses) { }

		public PriorityQueue(Comparison<T> comparison, bool reverses = false)
			: this(16, comparison, reverses) { }

		public PriorityQueue(
			int capacity,
			Comparison<T> comparison,
			bool reverses = false)
		{
			heap_ = new List<T>(capacity);
			comparison_ = comparison;
			reverses_ = reverses;
		}

		public void Enqueue(T item)
		{
			int c = heap_.Count;
			heap_.Add(item);
			while (c > 0) {
				int p = (c - 1) >> 1;
				if (Compare(p, c) < 0) {
					Swap(p, c);
				} else {
					break;
				}

				c = p;
			}
		}

		public T Dequeue()
		{
			T ret = heap_[0];
			int n = heap_.Count - 1;

			heap_[0] = heap_[n];
			heap_.RemoveAt(n);
			for (int p = 0, c; (c = (p << 1) + 1) < n; p = c) {
				if (c != n - 1 && Compare(c + 1, c) > 0) {
					++c;
				}

				if (Compare(p, c) < 0) {
					Swap(p, c);
				} else {
					break;
				}
			}

			return ret;
		}

		public void Clear() 
		{
			heap_.Clear();
		}

		private int Compare(int x, int y)
		{
			return comparison_(heap_[x], heap_[y]) * (reverses_ ? -1 : 1);
		}

		private void Swap(int x, int y)
		{
			var t = heap_[x];
			heap_[x] = heap_[y];
			heap_[y] = t;
		}
	}

	public struct PriorityPair<T, U> : IComparable<PriorityPair<T, U>>
			where T : IComparable<T>
	{
		public T Priority { get; set; }
		public U Item { get; set; }

		public PriorityPair(T priority, U item)
		{
			Priority = priority;
			Item = item;
		}

		public int CompareTo(PriorityPair<T, U> target)
		{
			return Priority.CompareTo(target.Priority);
		}

		public override string ToString()
		{
			return Priority.ToString() + " " + Item.ToString();
		}
	}

	/*
	public class PriorityQueue<T>
		where T : IComparable<T>
	{
		private readonly List<T> heap_;
		public bool Reverses { get; set; } = false;

		public T Top => heap_[0];
		public int Count => heap_.Count;

		public PriorityQueue(int capacity)
		{
			heap_ = new List<T>(capacity);
		}

		public void Enqueue(T elem)
		{
			int n = heap_.Count;
			heap_.Add(elem);
			while (n > 0) {
				int i = (n - 1) >> 1;
				if (Compare(n, i) > 0) {
					Swap(i, n);
				} else {
					break;
				}

				n = i;
			}
		}
		public T Dequeue()
		{
			T ret = heap_[0];
			int n = heap_.Count - 1;
			heap_[0] = heap_[n];
			heap_.RemoveAt(n);
			for (int i = 0, j; (j = (i << 1) + 1) < n; i = j) {
				if (j != n - 1 && Compare(j, j + 1) < 0) {
					++j;
				}

				if (Compare(i, j) < 0) {
					Swap(i, j);
				} else {
					break;
				}
			}
			return ret;
		}

		private int Compare(int self, int target)
			=> heap_[self].CompareTo(heap_[target]) * (Reverses ? -1 : 1);
		private void Swap(int x, int y)
		{
			var t = heap_[x];
			heap_[x] = heap_[y];
			heap_[y] = t;
		}

		public struct Pair<U> : IComparable<Pair<U>>
		{
			public T Priority { get; set; }
			public U Value { get; set; }
			public Pair(T priority, U value)
			{
				Priority = priority;
				Value = value;
			}
			public int CompareTo(Pair<U> target)
				=> Priority.CompareTo(target.Priority);
			public override string ToString()
				=> $"{Priority} {Value}";
		}
	}*/
}
