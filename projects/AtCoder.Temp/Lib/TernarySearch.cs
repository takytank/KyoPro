using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace AtCoder.Temp.Lib
{
	public static class TernarySearch
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double MinDouble(double left, double right, Func<double, double> f, double delta)
		{
			while (right - left > delta) {
				double cl = (left * 2 + right) / 3;
				double cr = (left + right * 2) / 3;
				if (f(cl) > f(cr)) {
					left = cl;
				} else {
					right = cr;
				}
			}

			return left;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double MaxDouble(double left, double right, Func<double, double> f, double delta)
		{
			while (right - left > delta) {
				double cl = (left * 2 + right) / 3;
				double cr = (left + right * 2) / 3;
				if (f(cl) < f(cr)) {
					left = cl;
				} else {
					right = cr;
				}
			}

			return left;
		}
	}
}
