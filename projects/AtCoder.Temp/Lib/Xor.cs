using System;
using System.Collections.Generic;
using System.Text;

namespace AtCoder.Temp.Lib
{
	// noshi 基底
	public static class Xor
	{
		public static List<long> BasisOf(Span<long> values)
		{
			int n = values.Length;
			var basis = new List<long>();
			for (int j = n - 1; j >= 0; j--) {
				long v = values[j];
				foreach (var b in basis) {
					v = Math.Min(v, v ^ b);
				}

				if (v > 0) {
					basis.Add(v);
				}
			}

			return basis;
		}
	}
}
