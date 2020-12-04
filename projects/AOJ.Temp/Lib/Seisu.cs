using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	class Seisu
	{
		public static bool IsPrime(long value)
		{
			for (int i = 2; i * i <= value; i++) {
				if (value % i == 0) {
					return false;
				}
			}

			return true;
		}

		public static List<long> GetDivisors(long value)
		{
			long max = (long)Math.Ceiling(Math.Sqrt(value));
			var divisors = new List<long>();
			for (int i = 1; i <= max; i++) {
				if (value % i == 0) {
					divisors.Add(i);
					if (i != value / i) {
						divisors.Add(value / i);
					}
				}
			}

			divisors.Sort();
			return divisors;
		}

		public static HashSet<long> GetPrimeFactor(long value)
		{
			var factors = new HashSet<long>();
			for (long i = 2; i * i <= value; ++i) {
				if (value % i == 0) {
					factors.Add(i);
					while (value % i == 0) {
						value /= i;
					}
				}
			}

			if (value != 1) {
				factors.Add(value);
			}

			return factors;
		}

		public static Dictionary<long, long> GetPrimeFactors(long value)
		{
			var factors = new Dictionary<long, long>();
			for (long i = 2; i * i <= value; ++i) {
				while (value % i == 0) {
					if (factors.ContainsKey(i) == false) {
						factors[i] = 1;
					} else {
						factors[i] += 1;
					}

					value /= i;
				}
			}

			if (value != 1) {
				factors[value] = 1;
			}

			return factors;
		}

		public static long[] Eratosthenes(long n)
		{
			var primes = new long[n + 1];

			for (long i = 2; i <= n; i++) {
				if (primes[i] == 0) {
					for (long j = i + i; j <= n; j += i) {
						primes[j] = i;
					}
				}
			}

			return primes;
		}

		public static long GCD(long a, long b)
		{
			if (b == 0) {
				return a;
			}

			return GCD(b, a % b);
		}

		public static long GCD(long[] values)
		{
			if (values.Length == 1) {
				return values[0];
			}

			long gcd = values[0];
			for (int i = 1; i < values.Length; ++i) {
				if (gcd == 1) {
					return gcd;
				}
				gcd = GCD(values[i], gcd);
			}

			return gcd;
		}

		public static long LCM(long a, long b)
		{
			return a / GCD(a, b) * b;
		}

		public static long LCM(long[] values, long limit)
		{
			if (values.Length == 1) {
				return values[0];
			}

			long lcm = values[0];
			for (int i = 1; i < values.Length; i++) {
				lcm = LCM(lcm, values[i]);
				if (lcm > limit || lcm < 0) {
					return -1;
				}
			}

			return lcm;
		}

		static int BinarySearch(SortedList<int, long> sl, int ai)
		{
			int left = -1;
			int right = sl.Count();

			while (right - left > 1) {
				int mid = left + (right - left) / 2;

				if (sl.Keys[mid] > ai) {
					right = mid;
				} else {
					left = mid;
				}
			}

			return right;
		}
	}
}
