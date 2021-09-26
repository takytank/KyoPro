using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AOJ.Temp.Lib
{
	public static class Helper
	{
		public static void UpdateMin<T>(ref T target, T value) where T : IComparable<T>
		{
			target = target.CompareTo(value) > 0 ? value : target;
		}

		public static void UpdateMin<T>(ref T target, T value, Action<T> onUpdated) where T : IComparable<T>
		{
			if (target.CompareTo(value) > 0) {
				target = value;
				onUpdated(value);
			}
		}

		public static void UpdateMax<T>(ref T target, T value) where T : IComparable<T>
		{
			target = target.CompareTo(value) < 0 ? value : target;
		}
		public static void UpdateMax<T>(ref T target, T value, Action<T> onUpdated) where T : IComparable<T>
		{
			if (target.CompareTo(value) < 0) {
				target = value;
				onUpdated(value);
			}
		}

		public static T[] Array<T>(int n, Func<int, T> init)
		{
			return Enumerable.Range(0, n).Select(x => init(x)).ToArray();
		}

		public static List<T> List<T>(int n, Func<int, T> init)
		{
			return Enumerable.Range(0, n).Select(x => init(x)).ToList();
		}
		public static T[,] Array2<T>(int n, int m, T init)
		{
			var array = new T[n, m];
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < m; j++) {
					array[i, j] = init;
				}
			}

			return array;
		}

		public static T[,,] Array3<T>(int n1, int n2, int n3, T init)
		{
			var array = new T[n1, n2, n3];
			for (int i1 = 0; i1 < n1; i1++) {
				for (int i2 = 0; i2 < n2; i2++) {
					for (int i3 = 0; i3 < n3; i3++) {
						array[i1, i2, i3] = init;
					}
				}
			}

			return array;
		}

		private static readonly int[] delta4_ = { 1, 0, -1, 0, 1 };
		public static void DoAt4(int i, int j, int imax, int jmax, Action<int, int> action)
		{
			for (int n = 0; n < 4; n++) {
				int ii = i + delta4_[n];
				int jj = j + delta4_[n + 1];
				if ((uint)ii < (uint)imax && (uint)jj < (uint)jmax) {
					action(ii, jj);
				}
			}
		}
	}
}
