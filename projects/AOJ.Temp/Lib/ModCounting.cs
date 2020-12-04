using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public static class ModCounting
	{
		private static long p_;

		private static long[] factorial_;
		private static long[] inverseFactorial_;
		private static long[] inverse_;

		public static void InitializeFactorial(long max, long p = 1000000007)
		{
			p_ = p;

			factorial_ = new long[max + 1];
			inverseFactorial_ = new long[max + 1];
			inverse_ = new long[max + 1];

			factorial_[0] = factorial_[1] = 1;
			inverseFactorial_[0] = inverseFactorial_[1] = 1;
			inverse_[1] = 1;
			for (int i = 2; i <= max; i++) {
				factorial_[i] = factorial_[i - 1] * i % p;
				inverse_[i] = p - inverse_[p % i] * (p / i) % p;
				inverseFactorial_[i] = inverseFactorial_[i - 1] * inverse_[i] % p;
			}
		}

		public static long Factorial(long n)
		{
			if (n < 0) {
				return 0;
			}

			return factorial_[n];
		}

		public static long InverseFactorial(long n)
		{
			if (n < 0) {
				return 0;
			}

			return inverseFactorial_[n];
		}

		public static long Inverse(long n)
		{
			if (n < 0) {
				return 0;
			}

			return inverse_[n];
		}

		public static long Permutation(long n, long k)
		{
			if (n < k || (n < 0 || k < 0)) {
				return 0;
			}

			return factorial_[n] * inverseFactorial_[n - k] % p_;
		}

		public static long RepeatedPermutation(long n, long k) {
			long ret = 1;
			for (k %= p_ - 1; k > 0; k >>= 1, n = n * n % p_) {
				if ((k & 1) == 1) {
					ret = ret * n % p_;
				}
			}

			return ret;
		}

		public static long Combination(long n, long k)
		{
			if (n < k || (n < 0 || k < 0)) {
				return 0;
			}

			return factorial_[n] * (inverseFactorial_[k] * inverseFactorial_[n - k] % p_) % p_;
		}

		public static long CombinationK(long n, long k)
		{
			long ret = 1;
			for (int i = 0; i < k; i++) {
				ret = (ret * (n - i)) % p_;
				ret = (ret * inverse_[i + 1]) % p_;
			}

			return ret;
		}

		public static long HomogeneousProduct(long n, long k)
		{
			if (n < 0 || k < 0) {
				return 0;
			}

			return Combination(n + k - 1, k);
		}
	}
}
