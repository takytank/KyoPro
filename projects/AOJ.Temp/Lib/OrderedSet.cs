using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public class OrderedSet<T> : IEnumerable<T>
		where T : IComparable
	{
		private readonly T inf_;
		private readonly bool isMulti_;
		private readonly RandomizedBinarySearchTree<T> bst_
			= new RandomizedBinarySearchTree<T>();
		private RandomizedBinarySearchTree<T>.Node root_;

		public T this[int index] { get { return ElementAt(index); } }

		public OrderedSet(bool isMulti = false, T inf = default(T))
			: this(Comparer<T>.Default, isMulti, inf) { }
		public OrderedSet(IComparer<T> comparer, bool isMulti = false, T inf = default(T))
		{
			inf_ = inf;
			isMulti_ = isMulti;
			bst_ = new RandomizedBinarySearchTree<T>(comparer, inf);
		}

		public int Count { get { return bst_.Count(root_); } }
		public int IndexOf(T v)
		{
			return bst_.UpperBound(root_, v) - bst_.LowerBound(root_, v);
		}

		public bool Contains(T value)
		{
			return bst_.Contains(root_, value);
		}

		public T Min { get { return Count == 0 ? inf_ : ElementAt(0); } }
		public T Max { get { return Count == 0 ? inf_ : ElementAt(Count - 1); } }

		public void Add(T value)
		{
			if (root_ == null) {
				root_ = new RandomizedBinarySearchTree<T>.Node(value);
			} else {
				if (bst_.Find(root_, value) != null && isMulti_ == false) {
					return;
				}

				root_ = bst_.Insert(root_, value);
			}
		}

		public void Remove(T value)
		{
			root_ = bst_.Remove(root_, value);
		}

		public void RemoveAt(int index)
		{
			root_ = bst_.RemoveAt(root_, index);
		}

		public void Clear()
		{
			root_ = null;
		}

		public T ElementAt(int index)
		{
			var node = bst_.FindByIndex(root_, index);
			if (node == null) {
				throw new IndexOutOfRangeException();
			}

			return node.Value;
		}

		public int LowerBound(T v) 
		{
			return bst_.LowerBound(root_, v);
		}

		public int UpperBound(T v)
		{
			return bst_.UpperBound(root_, v);
		}

		public T LowerBoundValue(T v)
		{
			return bst_.LowerBoundValue(root_, v);
		}

		public T UpperBoundValue(T v)
		{
			return bst_.UpperBoundValue(root_, v);
		}

		public Tuple<int, int> EqualRange(T v)
		{
			if (Contains(v) == false) {
				return new Tuple<int, int>(-1, -1);
			}

			return new Tuple<int, int>(
				bst_.LowerBound(root_, v),
				bst_.UpperBound(root_, v) - 1);
		}

		public List<T> ToList()
		{
			return new List<T>(bst_.Enumerate(root_));
		}

		public IEnumerator<T> GetEnumerator()
		{
			foreach (var v in bst_.Enumerate(root_)) {
				yield return v;
			};
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
