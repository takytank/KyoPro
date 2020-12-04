using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	/// <summary>
	/// 平衡二分木
	/// </summary>
	/// <remarks>
	/// http://yambe2002.hatenablog.com/entry/2017/02/07/122421
	/// </remarks>
	/// <typeparam name="T"></typeparam>
	public class RandomizedBinarySearchTree<T>
		where T : IComparable
	{
		private static readonly Random random_ = new Random();

		private readonly IComparer<T> comparer_;
		private readonly T inf_;

		public RandomizedBinarySearchTree(T inf = default(T))
			: this(Comparer<T>.Default, inf) { }
		public RandomizedBinarySearchTree(IComparer<T> comparer, T inf = default(T))
		{
			comparer_ = comparer;
			inf_ = inf;
		}

		public int Count(Node node)
		{
			return node == null ? 0 : node.SubTreeSize;
		}

		public bool Contains(Node node, T value)
		{
			return Find(node, value) != null;
		}

		public Node Insert(Node node, T value)
		{
			var index = LowerBound(node, value);
			return InsertAt(node, index, value);
		}

		private Node InsertAt(Node node, int k, T value)
		{
			var s = Split(node, k);
			return Merge(Merge(s.Left, new Node(value)), s.Right);
		}

		public Node Remove(Node node, T value)
		{
			if (Find(node, value) == null) {
				return node;
			}

			return RemoveAt(node, LowerBound(node, value));
		}

		public Node RemoveAt(Node node, int k)
		{
			var s1 = Split(node, k);
			var s2 = Split(s1.Right, 1);
			return Merge(s1.Left, s2.Right);
		}

		public Node Find(Node node, T target)
		{
			while (node != null) {
				var cmp = comparer_.Compare(node.Value, target);
				if (cmp > 0) {
					node = node.LeftChild;
				} else if (cmp < 0) {
					node = node.RightChild;
				} else {
					break;
				}
			}
			return node;
		}

		public Node FindByIndex(Node node, int index)
		{
			if (node == null) {
				return null;
			}

			var currentIndex = Count(node) - Count(node.RightChild) - 1;
			while (node != null) {
				if (currentIndex == index) {
					return node;
				}

				if (currentIndex > index) {
					node = node.LeftChild;
					currentIndex -= (Count(node == null ? null : node.RightChild) + 1);
				} else {
					node = node.RightChild;
					currentIndex += (Count(node == null ? null : node.LeftChild) + 1);
				}
			}

			return null;
		}

		public Node Merge(Node left, Node right)
		{
			if (left == null || right == null) {
				return left ?? right;
			}

			if ((double)Count(left) / (double)(Count(left) + Count(right)) > random_.NextDouble()) {
				left.RightChild = Merge(left.RightChild, right);
				return Update(left);
			} else {
				right.LeftChild = Merge(left, right.LeftChild);
				return Update(right);
			}
		}

		public Splited Split(Node node, int k)
		{
			if (node == null) {
				return new Splited(null, null);
			}

			//[0, k), [k, n)
			if (k <= Count(node.LeftChild)) {
				var s = Split(node.LeftChild, k);
				node.LeftChild = s.Right;
				return new Splited(s.Left, Update(node));
			} else {
				var s = Split(node.RightChild, k - Count(node.LeftChild) - 1);
				node.RightChild = s.Left;
				return new Splited(Update(node), s.Right);
			}
		}

		public int UpperBound(Node node, T value)
		{
			if (node == null) {
				return -1;
			}

			var ret = 0;
			while (node != null) {
				if (comparer_.Compare(node.Value, value) > 0) {
					node = node == null ? null : node.LeftChild;
				} else {
					ret += Count(node == null ? null : node.LeftChild) + 1;
					node = node == null ? null : node.RightChild;
				}
			}

			return ret;
		}

		public T UpperBoundValue(Node node, T value)
		{
			T ret = inf_;
			while (node != null) {
				if (comparer_.Compare(node.Value, value) > 0) {
					ret = node.Value;
					node = node == null ? null : node.LeftChild;
				} else {
					node = node == null ? null : node.RightChild;
				}
			}

			return ret;
		}

		public int LowerBound(Node node, T value)
		{
			if (node == null) {
				return -1;
			}

			var ret = 0;
			while (node != null) {
				if (comparer_.Compare(node.Value, value) >= 0) {
					node = node == null ? null : node.LeftChild;
				} else {
					ret += Count(node == null ? null : node.LeftChild) + 1;
					node = node == null ? null : node.RightChild;
				}
			}

			return ret;
		}

		public T LowerBoundValue(Node node, T value)
		{
			T ret = inf_;
			while (node != null) {
				if (comparer_.Compare(node.Value, value) >= 0) {
					ret = node.Value;
					node = node == null ? null : node.LeftChild;
				} else {
					node = node == null ? null : node.RightChild;
				}
			}

			return ret;
		}

		private Node Update(Node node)
		{
			node.SubTreeSize = Count(node.LeftChild) + Count(node.RightChild) + 1;
			return node;
		}

		public IEnumerable<T> Enumerate(Node node)
		{
			var ret = new List<T>();
			Enumerate(node, ret);
			return ret;
		}

		private void Enumerate(Node node, List<T> ret)
		{
			if (node == null) {
				return;
			}

			Enumerate(node.LeftChild, ret);
			ret.Add(node.Value);
			Enumerate(node.RightChild, ret);
		}

		public class Node
		{
			public T Value { get; set; }
			public Node LeftChild { get; set; }
			public Node RightChild { get; set; }
			public int SubTreeSize { get; set; }
			public Node(T value)
			{
				Value = value;
				SubTreeSize = 1;
			}
		}

		public class Splited
		{
			public Node Left { get; set; }
			public Node Right { get; set; }
			public Splited(Node left, Node right)
			{
				Left = left;
				Right = right;
			}
		}
	}
}
