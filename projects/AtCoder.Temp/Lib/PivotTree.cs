using System;

namespace AtCoder.Temp.Lib
{
	/// <summary>
	/// https://qiita.com/Kiri8128/items/6256f8559f0026485d90
	/// </summary>
	public class PivotTree
	{
		private readonly int height_;
		private readonly Node root_;
		private int count_ = 0;

		public PivotTree(int height)
		{
			height_ = height;
			root_ = new Node(1L << height, 1L << height);
		}

		public int Count => count_;
		public long Max => FindL((1L << height_) - 1);
		public long Min => FindR(-1);

		public void Add(long v)
		{
			v++;
			var node = root_;
			while (true) {
				if (v == node.Value) {
					return;
				} else {
					long min = Math.Min(v, node.Value);
					long max = Math.Max(v, node.Value);
					if (min < node.Pivot) {
						node.Value = max;
						if (node.Left != null) {
							node = node.Left;
							v = min;
						} else {
							long p = node.Pivot;
							node.Left = new Node(min, p - (p & -p) / 2);
							break;
						}
					} else {
						node.Value = min;
						if (node.Right != null) {
							node = node.Right;
							v = max;
						} else {
							long p = node.Pivot;
							node.Right = new Node(max, p + (p & -p) / 2);
							break;
						}
					}
				}
			}

			++count_;
		}

		public void Remove(long v, Node node = null, Node prev = null)
		{
			v++;
			if (node == null) {
				node = root_;
			}

			if (prev == null) {
				prev = node;
			}

			while (v != node.Value) {
				prev = node;
				if (v <= node.Value) {
					if (node.Left != null) {
						node = node.Left;
					} else {
						return;
					}
				} else {
					if (node.Right != null) {
						node = node.Right;
					} else {
						return;
					}
				}
			}

			if (node.Left == null && node.Right == null) {
				if (node.Value < prev.Value) {
					prev.Left = null;
				} else {
					prev.Right = null;
				}

				--count_;
			} else if (node.Left == null) {
				if (node.Value < prev.Value) {
					prev.Left = node.Right;
				} else {
					prev.Right = node.Right;
				}

				--count_;
			} else if (node.Right == null) {
				if (node.Value < prev.Value) {
					prev.Left = node.Left;
				} else {
					prev.Right = node.Left;
				}

				--count_;
			} else {
				node.Value = LeftMost(node.Right).Value;
				Remove(node.Value - 1, node.Right, node);
			}
		}

		public Node LeftMost(Node node)
		{
			if (node.Left != null) {
				return LeftMost(node.Left);
			}

			return node;
		}

		public Node RightMost(Node node)
		{
			if (node.Right != null) {
				return RightMost(node.Right);
			}

			return node;
		}

		public long FindL(long v)
		{
			v++;
			var node = root_;
			long prev = 0;
			if (node.Value < v) {
				prev = node.Value;
			}

			while (true) {
				if (v <= node.Value) {
					if (node.Left != null) {
						node = node.Left;
					} else {
						return prev - 1;
					}
				} else {
					if (node.Value < v) {
						prev = node.Value;
					}

					if (node.Right != null) {
						node = node.Right;
					} else {
						return prev - 1;
					}
				}
			}
		}

		public long FindR(long v)
		{
			v++;
			var node = root_;
			long prev = 0;
			if (node.Value > v) {
				prev = node.Value;
			}

			while (true) {
				if (v <= node.Value) {
					if (node.Value > v) {
						prev = node.Value;
					}

					if (node.Left != null) {
						node = node.Left;
					} else {
						return prev - 1;
					}
				} else {
					if (node.Right != null) {
						node = node.Right;
					} else {
						return prev - 1;
					}
				}
			}
		}

		public class Node
		{
			public long Value { get; set; }
			public long Pivot { get; set; }
			public Node Left { get; set; }
			public Node Right { get; set; }
			public Node(long value, long pivot)
			{
				Value = value;
				Pivot = pivot;
			}
		}
	}
}
