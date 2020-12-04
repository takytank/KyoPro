using System;
using System.Collections.Generic;
using System.Text;
using TakyTank.KyoProLib.CSharp;

namespace AtCoder.Temp.Lib
{
	public static class Matrix
	{
		public static VModInt[,] Mul(int n, VModInt[,] a, VModInt[,] b)
		{
			var ret = new VModInt[n, n];
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					for (int k = 0; k < n; k++) {
						ret[i, j] += a[i, k] * b[k, j];
					}
				}
			}

			return ret;
		}

		public static VModInt[,] Pow(int n, VModInt[,] a, long k)
		{
			var ret = new VModInt[n, n];
			for (int i = 0; i < n; i++) {
				ret[i, i] = 1;
			}

			var mul = a;
			while (k > 0) {
				if ((k & 1) != 0) {
					ret = Mul(n, ret, mul);
				}

				mul = Mul(n, mul, mul);
				k >>= 1;
			}

			return ret;
		}
	}
}
