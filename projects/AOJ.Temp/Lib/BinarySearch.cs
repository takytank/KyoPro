using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public static class BinarySearch
	{
		public static int BinarySearchIntR(int ng, int ok, Func<int, bool> check)
		{
			while (ok - ng > 1) {
				int mid = (ok + ng) / 2;
				if (check(mid)) {
					ok = mid;
				} else {
					ng = mid;
				}
			}

			return ok;
		}

		public static int BinarySearchIntL(int ok, int ng, Func<int, bool> check)
		{
			while (ng - ok > 1) {
				int mid = (ok + ng) / 2;
				if (check(mid)) {
					ok = mid;
				} else {
					ng = mid;
				}
			}

			return ok;
		}

		public static long BinarySearchLongR(long ng, long ok, Func<long, bool> check)
		{
			while (ok - ng > 1) {
				long mid = (ok + ng) / 2;
				if (check(mid)) {
					ok = mid;
				} else {
					ng = mid;
				}
			}

			return ok;
		}

		public static long BinarySearchLongL(long ok, long ng, Func<long, bool> check)
		{
			while (ng - ok > 1) {
				long mid = (ok + ng) / 2;
				if (check(mid)) {
					ok = mid;
				} else {
					ng = mid;
				}
			}

			return ok;
		}

		public static bool Exists(long[] list, long target)
		{
			int ok = 0;
			int ng = list.Length;

			while (Math.Abs(ok - ng) > 1) {
				int mid = (ok + ng) / 2;
				if (list[mid] == target) {
					return true;
				} else if (list[mid] > target) {
					ng = mid;
				} else {
					ok = mid;
				}
			}

			return false;
		}

		public static int LowerBound(long[] list, long target)
		{
			int left = -1;
			int right = list.Length;

			while (right - left > 1) {
				int mid = (right + left) / 2;
				if (list[mid] >= target) {
					right = mid;
				} else {
					left = mid;
				}
			}

			return right;
		}

		public static int UpperBound(long[] list, long target)
		{
			int left = -1;
			int right = list.Count();

			while (right - left > 1) {
				int mid = (right + left) / 2;
				if (list[mid] > target) {
					right = mid;
				} else {
					left = mid;
				}
			}

			return right;
		}
	}

	public static class TernarySearch
	{
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


	public class BinarySearch<T>
	{
		private readonly Comparison<T> comparison_;

		public BinarySearch()
			: this(Comparer<T>.Default.Compare) { }
		public BinarySearch(IComparer<T> comparer)
			: this(comparer.Compare) { }

		public BinarySearch(Comparison<T> comparison)
		{
			comparison_ = comparison;
		}


		public bool Exists(T[] list, T target)
		{
			int ok = 0;
			int ng = list.Count();

			while (Math.Abs(ok - ng) > 1) {
				int mid = (ok + ng) / 2;
				int ret = comparison_(list[mid], target);
				if (ret == 0) {
					return true;
				} else if (ret > 0) {
					ng = mid;
				} else {
					ok = mid;
				}
			}

			return false;
		}

		public int LowerBound(T[] list, T target)
		{
			int left = -1;
			int right = list.Count();

			while (right - left > 1) {
				int mid = (right + left) / 2;
				int ret = comparison_(list[mid], target);
				if (ret >= 0) {
					right = mid;
				} else {
					left = mid;
				}
			}

			return right;
		}

		public int UpperBound(T[] list, T target)
		{
			int left = -1;
			int right = list.Count();

			while (right - left > 1) {
				int mid = (right + left) / 2;
				int ret = comparison_(list[mid], target);
				if (ret > 0) {
					right = mid;
				} else {
					left = mid;
				}
			}

			return right;
		}
	}

	public class BinarySearch<T, S>
		where S : IComparable, IComparable<S>
	{
		private readonly Func<T, S> converter_;

		public BinarySearch(Func<T, S> converter)
		{
			converter_ = converter;
		}

		public bool Exists(T[] list, T target)
		{
			return Exists(list, converter_(target));
		}

		public bool Exists(T[] list, S target)
		{
			int ok = 0;
			int ng = list.Count();

			while (Math.Abs(ok - ng) > 1) {
				int mid = (ok + ng) / 2;
				int ret = converter_(list[mid]).CompareTo(target);
				if (ret == 0) {
					return true;
				} else if (ret > 0) {
					ng = mid;
				} else {
					ok = mid;
				}
			}

			return false;
		}

		public int LowerBound(T[] list, T target)
		{
			return LowerBound(list, converter_(target));
		}

		public int LowerBound(T[] list, S target)
		{
			int left = -1;
			int right = list.Count();

			while (right - left > 1) {
				int mid = (right + left) / 2;
				int ret = converter_(list[mid]).CompareTo(target);
				if (ret >= 0) {
					right = mid;
				} else {
					left = mid;
				}
			}

			return right;
		}

		public int UpperBound(T[] list, T target)
		{
			return UpperBound(list, converter_(target));
		}

		public int UpperBound(T[] list, S target)
		{
			int left = -1;
			int right = list.Count();

			while (right - left > 1) {
				int mid = (right + left) / 2;
				int ret = converter_(list[mid]).CompareTo(target);
				if (ret > 0) {
					right = mid;
				} else {
					left = mid;
				}
			}

			return right;
		}
	}
}
