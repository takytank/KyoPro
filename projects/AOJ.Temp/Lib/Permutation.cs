﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	static class Permutation
	{
		public static IEnumerable<int[]> All(int n)
		{
			var src = Enumerable.Range(1, n).ToArray();
			do {
				yield return src;
			} while (Next(src, 0, n));
		}

		public static bool Next(int[] src, int index, int length)
		{
			if (length <= 1) {
				return false;
			}

			int last = index + length - 1;
			int i = last;
			while (true) {
				int ii = i;
				--i;
				if (src[i] < src[ii]) {
					int j = last;
					while (src[i] >= src[j]) {
						--j;
					}

					int temp = src[i];
					src[i] = src[j];
					src[j] = temp;
					Array.Reverse(src, ii, last - ii + 1);
					return true;
				}

				if (i == index) {
					Array.Reverse(src, index, length);
					return false;
				}
			}
		}

		public static IEnumerable<T[]> All<T>(T[] src)
			where T : IComparable<T>
		{
			int n = src.Length;
			do {
				yield return src;
			} while (Next<T>(src, 0, n));
		}

		public static bool Next<T>(T[] src) where T : IComparable<T>
		{
			return Next(src, 0, src.Length);
		}

		public static bool Next<T>(T[] src, int index, int length)
			where T : IComparable<T>
		{
			if (length <= 1) {
				return false;
			}

			int last = index + length - 1;
			int i = last;

			while (true) {
				int ii = i;
				--i;
				if (src[i].CompareTo(src[ii]) < 0) {
					int j = last;
					while (src[i].CompareTo(src[j]) >= 0) {
						--j;
					}

					T temp = src[i];
					src[i] = src[j];
					src[j] = temp;
					Array.Reverse(src, ii, last - ii + 1);
					return true;
				}

				if (i == index) {
					Array.Reverse(src, index, length);
					return false;
				}
			}
		}

		public static bool Prev<T>(T[] src) where T : IComparable<T>
		{
			return Prev(src, 0, src.Length);
		}

		public static bool Prev<T>(T[] src, int index, int length)
			where T : IComparable<T>
		{
			if (length <= 1) {
				return false;
			}

			int last = index + length - 1;
			int i = last;

			while (true) {
				int ii = i;
				--i;
				if (src[ii].CompareTo(src[i]) < 0) {
					int j = last;
					while (src[j].CompareTo(src[i]) >= 0) {
						--j;
					}

					T temp = src[i];
					src[i] = src[j];
					src[j] = temp;
					Array.Reverse(src, ii, last - ii + 1);
					return true;
				}

				if (i == index) {
					Array.Reverse(src, index, length);
					return false;
				}
			}
		}
	}
}
