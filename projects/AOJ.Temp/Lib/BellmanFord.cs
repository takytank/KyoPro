using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public class BellmanFord
	{
		private const long INFINITY = long.MaxValue;

		private readonly int count_;
		private readonly List<Edge> edges_;
		private readonly List<int>[] to_;
		private readonly List<int>[] from_;

		public BellmanFord(int n)
		{
			count_ = n;
			edges_ = new List<Edge>(n);
			to_ = Enumerable.Range(0, n).Select(x => new List<int>()).ToArray();
			from_ = Enumerable.Range(0, n).Select(x => new List<int>()).ToArray();
		}

		public void AddEdge(int from, int to, int cost)
		{
			edges_.Add(new Edge { From = from, To = to, Cost = cost });
			to_[from].Add(to);
			from_[to].Add(from);
		}

		public long[] CalculateDistance(int startIndex, out bool existsNegativeCycle)
		{
			long[] distances = new long[count_];
			for (int i = 0; i < count_; i++) {
				if (i != startIndex) {
					distances[i] = INFINITY;
				}
			}

			existsNegativeCycle = false;
			for (int i = 0; i < count_; i++) {
				bool changes = false;
				foreach (var edge in edges_) {
					if (distances[edge.From] != INFINITY) {
						long newDistance = distances[edge.From] + edge.Cost;
						if (newDistance < distances[edge.To]) {
							changes = true;
							distances[edge.To] = newDistance;
						}
					}
				}

				if (i == count_ - 1) {
					existsNegativeCycle = changes;
				}
			}

			return distances;
		}

		public long CalculateDistance(int startIndex, int endIndex, out bool existsNegativeCycle)
		{
			long[] distances = new long[count_];
			for (int i = 0; i < count_; i++) {
				if (i != startIndex) {
					distances[i] = INFINITY;
				}
			}

			bool[] reachableFromStart = new bool[count_];
			bool[] reachableFromEnd = new bool[count_];
			DfsForTo(startIndex, reachableFromStart);
			DfsForFrom(endIndex, reachableFromEnd);

			bool[] enableds = new bool[count_];
			for (int i = 0; i < count_; i++) {
				enableds[i] = reachableFromStart[i] & reachableFromEnd[i];
			}

			existsNegativeCycle = false;
			for (int i = 0; i < count_; i++) {
				bool changes = false;
				foreach (var edge in edges_) {
					if (enableds[edge.From] == false || enableds[edge.To] == false) {
						continue;
					}

					if (distances[edge.From] != INFINITY) {
						long newDistance = distances[edge.From] + edge.Cost;
						if (newDistance < distances[edge.To]) {
							changes = true;
							distances[edge.To] = newDistance;
						}
					}
				}

				if (i == count_ - 1) {
					existsNegativeCycle = changes;
				}
			}

			return distances[endIndex];
		}

		private void DfsForTo(int index, bool[] reachableFromStart)
		{
			if (reachableFromStart[index]) {
				return;
			}

			reachableFromStart[index] = true;
			foreach (int next in to_[index]) {
				DfsForTo(next, reachableFromStart);
			}
		}

		private void DfsForFrom(int index, bool[] reachableFromEnd)
		{
			if (reachableFromEnd[index]) {
				return;
			}

			reachableFromEnd[index] = true;
			foreach (int next in from_[index]) {
				DfsForFrom(next, reachableFromEnd);
			}
		}

		private struct Edge
		{
			public int From;
			public int To;
			public long Cost;
		}
	}

	public class WarshallFloyd
	{
		/*
		 * var d = new long[n, n];
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					if (i != j) {
						d[i, j] = inf;
					}
				}
			}

			for (int i = 0; i < m; i++) {
				int a = cin.Int() - 1;
				int b = cin.Int() - 1;
				long c = cin.Long();
				d[a, b] = c;
				d[b, a] = c;
			}

			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					for (int k = 0; k < n; k++) {
						d[j, k] = Math.Min(d[j, k], d[j, i] + d[i, k]);
					}
				}
			}
			*/
	}
}
