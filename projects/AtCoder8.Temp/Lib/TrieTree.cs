using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AtCoder.Temp.Lib
{
	public class TrieTree<TValue>
	{
		private const int ROOT_NODE = 1;
		private readonly int childCount_;
		private readonly Func<char, int> toIndex_;
		private readonly TValue initialValue_;
		private readonly Node[] nodes_;
		private readonly int[][] wordNodeNumbers_;
		private int nextNode_;

		public TrieTree(
			int maxWordCount,
			int maxNode,
			int childCount,
			Func<char, int> toIndex,
			TValue initialValue)
		{
			childCount_ = childCount;
			toIndex_ = toIndex;
			initialValue_ = initialValue;
			nodes_ = new Node[maxNode];
			nodes_[0] = null;
			nodes_[ROOT_NODE] = new Node(initialValue_, childCount_);
			nextNode_ = 2;

			wordNodeNumbers_ = new int[maxWordCount][];
		}

		public Node Root => nodes_[ROOT_NODE];
		public Node GetNode(int wordID, int count) => nodes_[wordNodeNumbers_[wordID][count]];
		public Node GetChild(Node node, char c) => nodes_[node.Children[toIndex_(c)]];

		public int WordCount => nodes_[ROOT_NODE].AllCount;
		public int NodeCount => nextNode_ - 1;

		public void Add(string word)
		{
			var numbers = new int[word.Length + 1];
			numbers[0] = 1;

			Node node = nodes_[ROOT_NODE];
			for (int i = 0; i < word.Length; i++) {
				int c = toIndex_(word[i]);
				if (node.Children[c] == 0) {
					node.Children[c] = nextNode_;
					nodes_[nextNode_] = new Node(initialValue_, childCount_);
					++nextNode_;
				}

				numbers[i + 1] = node.Children[c];
				++node.AllCount;
				node = nodes_[node.Children[c]];
			}

			++node.AllCount;
			++node.EndCount;

			wordNodeNumbers_[nodes_[ROOT_NODE].AllCount - 1] = numbers;
		}

		public (bool exists, Node node) Find(string word, bool prefix = false)
		{
			Node node = nodes_[ROOT_NODE];
			for (int i = 0; i < word.Length; i++) {
				int c = toIndex_(word[i]);
				if (node.Children[c] == 0) {
					return (false, null);
				}

				node = nodes_[node.Children[c]];
			}

			return prefix || node.EndCount > 0
				? (true, node)
				: (false, null);
		}

		public class Node
		{
			public TValue Value { get; set; }
			public int[] Children { get; }
			public int AllCount { get; set; } = 0;
			public int EndCount { get; set; } = 0;

			public Node(TValue value, int childCount)
			{
				Value = value;
				Children = new int[childCount];
			}
		}
	}

	public class TrieTree<TKey, TValue>
	{
		private const int ROOT_NODE = 1;
		private readonly int childCount_;
		private readonly Func<TKey, int> toIndex_;
		private readonly TValue initialValue_;
		private readonly Node[] nodes_;
		private readonly int[][] wordNodeNumbers_;
		private int nextNode_;

		public TrieTree(
			int maxWordCount,
			int maxNode,
			int childCount,
			Func<TKey, int> toIndex,
			TValue initialValue)
		{
			childCount_ = childCount;
			toIndex_ = toIndex;
			initialValue_ = initialValue;
			nodes_ = new Node[maxNode];
			nodes_[0] = null;
			nodes_[ROOT_NODE] = new Node(initialValue_, childCount_);
			nextNode_ = 2;

			wordNodeNumbers_ = new int[maxWordCount][];
		}

		public Node Root => nodes_[ROOT_NODE];
		public Node GetNode(int wordID, int count) => nodes_[wordNodeNumbers_[wordID][count]];
		public Node GetChild(Node node, TKey c) => nodes_[node.Children[toIndex_(c)]];

		public int WordCount => nodes_[ROOT_NODE].AllCount;
		public int NodeCount => nextNode_ - 1;

		public void Add(IEnumerable<TKey> word, int length)
		{
			var numbers = new int[length + 1];
			numbers[0] = 1;

			Node node = nodes_[ROOT_NODE];
			int i = 0;
			foreach (var w in word) {
				int c = toIndex_(w);
				if (node.Children[c] == 0) {
					node.Children[c] = nextNode_;
					nodes_[nextNode_] = new Node(initialValue_, childCount_);
					++nextNode_;
				}

				numbers[i + 1] = node.Children[c];
				++node.AllCount;
				node = nodes_[node.Children[c]];
				++i;
			}

			++node.AllCount;
			++node.EndCount;

			wordNodeNumbers_[nodes_[ROOT_NODE].AllCount - 1] = numbers;
		}

		public (bool exists, Node node) Find(IEnumerable<TKey> word, bool prefix = false)
		{
			Node node = nodes_[ROOT_NODE];
			int i = 0;
			foreach (var w in word) {
				int c = toIndex_(w);
				if (node.Children[c] == 0) {
					return (false, null);
				}

				node = nodes_[node.Children[c]];
				++i;
			}

			return prefix || node.EndCount > 0
				? (true, node)
				: (false, null);
		}

		public class Node
		{
			public TValue Value { get; set; }
			public int[] Children { get; }
			public int AllCount { get; set; } = 0;
			public int EndCount { get; set; } = 0;

			public Node(TValue value, int childCount)
			{
				Value = value;
				Children = new int[childCount];
			}
		}
	}

	public class TrieTreeNC<TKey, TValue>
	{
		private readonly int childCount_;
		private readonly Func<TKey, int> toIndex_;
		private readonly TValue initialValue_;
		private readonly Node root_;
		private readonly Node[][] wordNodePathes_;
		private int nodeCount_;

		public TrieTreeNC(
			int maxWordCount,
			int childCount,
			Func<TKey, int> toIndex,
			TValue initialValue)
		{
			childCount_ = childCount;
			toIndex_ = toIndex;
			initialValue_ = initialValue;
			root_ = new Node(initialValue_, childCount_);
			nodeCount_ = 1;

			wordNodePathes_ = new Node[maxWordCount][];
		}

		public Node Root => root_;
		public Node GetNode(int wordID, int count) => wordNodePathes_[wordID][count];
		public Node GetChild(Node node, TKey c) => node?.Children[toIndex_(c)];

		public int WordCount => root_.AllCount;
		public int NodeCount => nodeCount_;

		public void Add(IEnumerable<TKey> word, int length)
		{
			var paths = new Node[length + 1];
			paths[0] = root_;

			Node node = root_;
			int i = 0;
			foreach (var w in word) {
				int c = toIndex_(w);
				if (node.Children[c] is null) {
					node.Children[c] = new Node(initialValue_, childCount_);
					++nodeCount_;
				}

				paths[i + 1] = node.Children[c];
				++node.AllCount;
				node = node.Children[c];
				++i;
			}

			++node.AllCount;
			++node.EndCount;

			wordNodePathes_[root_.AllCount - 1] = paths;
		}

		public (bool exists, Node node) Find(IEnumerable<TKey> word, bool prefix = false)
		{
			Node node = root_;
			int i = 0;
			foreach (var w in word) {
				int c = toIndex_(w);
				if (node.Children[c] is null) {
					return (false, null);
				}

				node = node.Children[c];
				++i;
			}

			return prefix || node.EndCount > 0
				? (true, node)
				: (false, null);
		}

		public class Node
		{
			public TValue Value { get; set; }
			public Node[] Children { get; }
			public int AllCount { get; set; } = 0;
			public int EndCount { get; set; } = 0;

			public Node(TValue value, int childCount)
			{
				Value = value;
				Children = new Node[childCount];
			}
		}
	}
}
