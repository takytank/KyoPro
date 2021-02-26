using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace AtCoder.Temp.Lib
{
	public class Rerooting<T>
	{
		private readonly int n_;
		private readonly Func<T, T, T> merge_;
		private readonly Func<int, T, T> calculate_;
		private readonly T unit_;
		private readonly List<int>[] to_;
		private readonly T[][] dp_;
		private readonly T[] result_;
		private int[] reverseIndexes_;

		public Rerooting(
			int n,
			Func<T, T, T> merge,
			Func<int, T, T> calculate,
			T unit)
		{
			n_ = n;
			merge_ = merge;
			calculate_ = calculate;
			unit_ = unit;

			to_ = new List<int>[n];
			dp_ = new T[n][];
			result_ = new T[n];
			for (int i = 0; i < n; i++) {
				to_[i] = new List<int>();
				result_[i] = unit_;
			}
		}

		public void AddEdge(int a, int b)
		{
			to_[a].Add(b);
		}

		public T[] Build(int root = 0, bool doseOnlyTreeDp = false)
		{
			DfsForTreeDP(root);
			if (doseOnlyTreeDp == false) {
				BfsForReroot(root);
			}

			return result_;
		}

		private void DfsForTreeDP(int v)
		{
			var parents = new int[n_];
			var indexes = new int[n_];
			reverseIndexes_ = new int[n_];
			var visited = new bool[n_];
			var routeStack = new Stack<int>(n_);
			var tempStack = new Stack<int>(n_);
			routeStack.Push(v);
			tempStack.Push(v);
			visited[v] = true;
			parents[v] = -1;

			while (tempStack.Count > 0) {
				int current = tempStack.Pop();
				dp_[current] = new T[to_[current].Count];
				for (int i = 0; i < to_[current].Count; i++) {
					int next = to_[current][i];
					if (visited[next]) {
						reverseIndexes_[current] = i;
						continue;
					}

					visited[next] = true;
					parents[next] = current;
					indexes[next] = i;
					routeStack.Push(next);
					tempStack.Push(next);
				}
			}

			while (routeStack.Count > 0) {
				int target = routeStack.Pop();
				T accum = unit_;
				for (int i = 0; i < to_[target].Count; i++) {
					int child = to_[target][i];
					if (child == parents[target]) {
						continue;
					}

					accum = merge_(accum, dp_[target][i]);
				}

				result_[target] = calculate_(target, accum);
				if (parents[target] >= 0) {
					dp_[parents[target]][indexes[target]] = result_[target];
				}
			}
		}

		private void BfsForReroot(int first)
		{
			int max = 0;
			for (int i = 0; i < n_; i++) {
				max = Math.Max(max, to_[i].Count);
			}

			var accumL = new T[max + 1];
			var accumR = new T[max + 1];

			var q = new Queue<BfsInfo>(n_);
			q.Enqueue(new BfsInfo(first, -1));
			while (q.Count > 0) {
				var current = q.Dequeue();
				int count = to_[current.V].Count;

				accumL[0] = unit_;
				for (int i = 0; i < count; i++) {
					accumL[i + 1] = merge_(accumL[i], dp_[current.V][i]);
				}

				accumR[count] = unit_;
				for (int i = count - 1; i >= 0; i--) {
					accumR[i] = merge_(accumR[i + 1], dp_[current.V][i]);
				}

				result_[current.V] = calculate_(current.V, accumL[count]);

				for (int i = 0; i < count; i++) {
					int next = to_[current.V][i];
					if (next == current.P) {
						continue;
					}

					T reverse = calculate_(current.V, merge_(accumL[i], accumR[i + 1]));
					dp_[next][reverseIndexes_[next]] = reverse;

					q.Enqueue(new BfsInfo(next, current.V));
				}
			}
		}

		private struct BfsInfo
		{
			public int V { get; set; }
			public int P { get; set; }

			public BfsInfo(int v, int p)
			{
				V = v;
				P = p;
			}
		}
	}
}
