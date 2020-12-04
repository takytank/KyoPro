using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public static class Lca
	{
		static int[,] parents_;
		static int[] depth_;
		static int log_;
		static List<int>[] to_;

		public static void Initialize(int n, int root, List<int>[] to)
		{
			int m = n;
			log_ = 0;
			while (m > 0) {
				++log_;
				m /= 2;
			}

			to_ = to;
			parents_ = new int[log_, n];
			depth_ = new int[n];

			DFS(root, -1, 0);
			for (int k = 0; k + 1 < log_; k++) {
				for (int v = 0; v < n; v++) {
					if (parents_[k, v] < 0) {
						parents_[k + 1, v] = -1;
					} else {
						parents_[k + 1, v] = parents_[k, parents_[k, v]];
					}
				}
			}
		}

		public static int GetLCA(int u, int v)
		{
			if (depth_[u] > depth_[v]) {
				int temp = u;
				u = v;
				v = temp;
			}

			for (int k = 0; k < log_; k++) {
				if (((depth_[v] - depth_[u]) >> k & 1) != 0) {
					v = parents_[k, v];
				}
			}

			if (u == v) {
				return u;
			}

			for (int k = log_ - 1; k >= 0; k--) {
				if (parents_[k, u] != parents_[k, v]) {
					u = parents_[k, u];
					v = parents_[k, v];
				}
			}

			return parents_[0, u];
		}

		private static void DFS(int v, int p, int d)
		{
			parents_[0, v] = p;
			depth_[v] = d;
			foreach (var pp in to_[v]) {
				if (pp != p) {
					DFS(pp, v, d + 1);
				}
			}
		}
	}
}
