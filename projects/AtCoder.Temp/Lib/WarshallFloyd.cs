using System;
using System.Collections.Generic;
using System.Text;

namespace AtCoder.Temp.Lib
{
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
