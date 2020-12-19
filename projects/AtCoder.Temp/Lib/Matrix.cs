using System;
using System.Collections.Generic;
using System.Text;
using TakyTank.KyoProLib.CSharp;

namespace AtCoder.Temp.Lib
{
	public static class Matrix
	{
		public static ModInt[,] Mul(int n, ModInt[,] a, ModInt[,] b)
		{
			var ret = new ModInt[n, n];
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					for (int k = 0; k < n; k++) {
						ret[i, j] += a[i, k] * b[k, j];
					}
				}
			}

			return ret;
		}

		public static ModInt[,] Pow(int n, ModInt[,] a, long k)
		{
			var ret = new ModInt[n, n];
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
