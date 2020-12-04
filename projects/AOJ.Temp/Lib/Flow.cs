using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public static class Flow
	{
		public static void AddEdge(List<Flow.Edge>[] edges, int from, int to, long capacity)
		{
			edges[from].Add(new Edge(to, capacity, edges[to].Count));
			edges[to].Add(new Edge(from, 0, edges[from].Count - 1));
		}

		public static long FordFulkerson(int n, List<Flow.Edge>[] edges, int s, int t)
		{
			long flow = 0;
			while (true) {
				var done = new bool[n];
				long f = FordFulkersonDfs(edges, s, t, long.MaxValue, done);
				if (f == 0 || f == long.MaxValue) {
					break;
				}

				flow += f;
			}

			return flow;
		}

		private static long FordFulkersonDfs(List<Flow.Edge>[] edges, int s, int t, long f, bool[] done)
		{
			if (s == t) {
				return f;
			}

			done[s] = true;
			foreach (var edge in edges[s]) {
				if (done[edge.To] == false && edge.Capacity > 0) {
					long d = FordFulkersonDfs(edges, edge.To, t, Math.Min(f, edge.Capacity), done);
					if (d > 0) {
						edge.Capacity -= d;
						edges[edge.To][edge.ReverseEdgeIndex].Capacity += d;
						return d;
					}
				}
			}

			return 0;
		}

		public static long Dinic(int n, List<Flow.Edge>[] edges, int s, int t)
		{
			long flow = 0;
			while (true) {
				var distance = DinicBfs(n, edges, s);
				if (distance[t] < 0) {
					break;
				}

				var done = new int[n];
				while (true) {
					long f = DinicDfs(n, edges, s, t, long.MaxValue, done, distance);
					if (f == 0 || f == long.MaxValue) {
						break;
					}

					flow += f;
				}
			}

			return flow;
		}

		private static long[] DinicBfs(int n, List<Flow.Edge>[] edges, int s)
		{
			var d = new long[n];
			for (int i = 0; i < n; i++) {
				d[i] = -1;
			}

			var q = new Queue<int>();
			d[s] = 0;
			q.Enqueue(s);
			while (q.Count > 0) {
				int v = q.Dequeue();
				foreach (var edge in edges[v]) {
					if (edge.Capacity > 0 && d[edge.To] < 0) {
						d[edge.To] = d[v] + 1;
						q.Enqueue(edge.To);
					}
				}
			}

			return d;
		}

		private static long DinicDfs(
			int n, List<Flow.Edge>[] edges, int s, int t, long f, int[] done, long[] distance)
		{
			if (s == t) {
				return f;
			}

			for (; done[s] < edges[s].Count; done[s]++) {
				var edge = edges[s][done[s]];
				if (edge.Capacity > 0 && distance[s] < distance[edge.To]) {
					long d = DinicDfs(n, edges, edge.To, t, Math.Min(f, edge.Capacity), done, distance);
					if (d > 0) {
						edge.Capacity -= d;
						edges[edge.To][edge.ReverseEdgeIndex].Capacity += d;
						return d;
					}
				}
			}

			return 0;
		}

		public class Edge
		{
			public int To { get; set; }
			public long Capacity { get; set; }
			public int ReverseEdgeIndex { get; set; }

			public Edge(int to, long capacity, int reverse)
			{
				To = to;
				Capacity = capacity;
				ReverseEdgeIndex = reverse;
			}
		}
	}
}
