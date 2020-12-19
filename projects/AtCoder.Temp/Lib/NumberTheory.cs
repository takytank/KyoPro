using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace AtCoder.Temp.Lib
{
	public static class NumberTheory
	{
		static readonly HashSet<long> smallPrimes_
			= new long[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 }.ToHashSet();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsPrime(long value)
		{
			for (int i = 2; i * i <= value; i++) {
				if (value % i == 0) {
					return false;
				}
			}

			return true;
		}

		//ttp://joisino.hatenablog.com/entry/2017/08/03/210000
		//ttp://miller-rabin.appspot.com/
		static readonly long[] sprpBase1 = { 126401071349994536 };
		static readonly long[] sprpBase2 = { 336781006125, 9639812373923155 };
		static readonly long[] sprpBase3 = { 2, 2570940, 211991001, 3749873356 };
		static readonly long[] sprpBase4 = { 2, 325, 9375, 28178, 450775, 9780504, 1795265022 };
		public static bool IsPrimeByMillerRabin(long value)
		{
			if (value < 2) {
				return false;
			}

			if (smallPrimes_.Contains(value)) {
				return true;
			}

			long d = value - 1;
			int count2 = 0;
			while (d % 2 == 0) {
				d /= 2;
				count2++;
			}

			long[] v = value <= 291831L ? sprpBase1
				: value <= 1050535501L ? sprpBase2
				: value <= 47636622961201 ? sprpBase3
				: sprpBase4;

			foreach (var a in v) {
				if (a == value) {
					return true;
				}

				long temp = PowMod(a, d, value);
				if (temp == 1) {
					continue;
				}

				bool ok = true;
				for (int r = 0; r < count2; r++) {
					if (temp == value - 1) {
						ok = false;
						break;
					}

					temp = PowMod(temp, 2, value);
				}

				if (ok) {
					return false;
				}
			}

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HashSet<long> Divisors(long value)
		{
			var divisors = new HashSet<long>();
			for (long i = 1; i * i <= value; ++i) {
				if (value % i == 0) {
					divisors.Add(i);
					if (i != value / i) {
						divisors.Add(value / i);
					}
				}
			}

			return divisors;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HashSet<long> PrimeFactor(long value)
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Dictionary<long, int> PrimeFactors(long value)
		{
			var factors = new Dictionary<long, int>();
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

		//ttp://joisino.hatenablog.com/entry/2017/08/03/210000
		//ttps://qiita.com/gushwell/items/561afde2e00bf3380c98
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Dictionary<long, int> PrimeFactorsByPollardsRho(long value)
		{
			static long Next(long x, long p)
			{
				return (long)(((BigInteger)x * x + 1) % p);
			}

			static long FindFactor(long n)
			{
				if (n % 2 == 0) {
					return 2;
				}

				if (IsPrimeByMillerRabin(n)) {
					return n;
				}

				int seed = 0;
				while (true) {
					++seed;
					long x = seed % n;
					long y = Next(x, n);
					long d = 1;
					while (d == 1) {
						x = Next(x, n);
						y = Next(Next(y, n), n);
						d = Gcd(Math.Abs(x - y), n);
					}

					if (d == n) {
						continue;
					}

					return d;
				}
			}

			var ret = new Dictionary<long, int>();
			var que = new Queue<long>();
			que.Enqueue(value);
			while (que.Count > 0) {
				var target = que.Dequeue();
				if (target == 1) {
					continue;
				}

				if (IsPrimeByMillerRabin(target)) {
					if (ret.ContainsKey(target)) {
						ret[target]++;
					} else {
						ret.Add(target, 1);
					}

					continue;
				}

				long f = FindFactor(target);
				que.Enqueue(f);
				que.Enqueue(target / f);
			}

			return ret;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static List<long> PrimeFactorsToDivisors(
			Dictionary<long, int> factors,
			bool sorts = false)
		{
			var count = factors.Count();
			var divisors = new List<long>();
			if (count == 0) {
				return divisors;
			}

			var keys = factors.Keys.ToArray();

			void Dfs(int c, long v)
			{
				if (c == count) {
					divisors.Add(v);
					return;
				}

				Dfs(c + 1, v);
				for (int i = 0; i < factors[keys[c]]; i++) {
					v *= keys[c];
					Dfs(c + 1, v);
				}
			}

			Dfs(0, 1);

			if (sorts) {
				divisors = divisors.OrderBy(x => x).ToList();
			}

			return divisors;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Dictionary<long, (long first, long length)> FloorNK(long n)
		{
			var nk = new Dictionary<long, (long first, long length)>();
			long l = 1;
			while (l <= n) {
				long r = n / (n / l) + 1;
				nk[n / l] = (l, r - l);
				l = r;
			}

			return nk;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long Gcd(long a, long b)
		{
			if (b == 0) {
				return a;
			}

			return Gcd(b, a % b);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long Gcd(long[] values)
		{
			if (values.Length == 1) {
				return values[0];
			}

			long gcd = values[0];
			for (int i = 1; i < values.Length; ++i) {
				if (gcd == 1) {
					return gcd;
				}
				gcd = Gcd(values[i], gcd);
			}

			return gcd;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long Lcm(long a, long b)
		{
			return a / Gcd(a, b) * b;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long Lcm(long[] values, long limit)
		{
			if (values.Length == 1) {
				return values[0];
			}

			long lcm = values[0];
			for (int i = 1; i < values.Length; i++) {
				lcm = Lcm(lcm, values[i]);
				if (lcm > limit || lcm < 0) {
					return -1;
				}
			}

			return lcm;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long Mod(long x, long p)
		{
			x %= p;
			if (x < 0) {
				x += p;
			}

			return x;
		}

		// original: ttps://github.com/key-moon/ac-library-cs
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long PowMod(long x, long n, long p)
		{
			if (p == 1) {
				return 0;
			}

			if (p < int.MaxValue) {
				var barrett = new BarrettReduction((uint)p);
				uint r = 1;
				uint y = (uint)Mod(x, p);
				while (0 < n) {
					if ((n & 1) != 0) {
						r = barrett.Multilply(r, y);
					}

					y = barrett.Multilply(y, y);
					n >>= 1;
				}

				return r;
			} else {
				BigInteger ret = 1;
				BigInteger mul = x % p;
				while (n != 0) {
					if ((n & 1) == 1) {
						ret = ret * mul % p;
					}
					mul = mul * mul % p;
					n >>= 1;
				}

				return (long)ret;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long InverseMod(long a, long p)
		{
			var (_, x, _) = ExtendedEuclidean(a, p);
			return Mod(x, p);
		}

		// ax + by = gcd(a, b)
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (long gcd, long x, long y) ExtendedEuclidean(long a, long b)
		{
			if (b == 0) {
				return (a, 1, 0);
			}

			var (gcd, y, x) = ExtendedEuclidean(b, a % b);
			y -= a / b * x;
			return (gcd, x, y);
		}

		// original: ttps://github.com/key-moon/ac-library-cs
		// g=gcd(a,b),xa=g(mod b)
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (long g, long x) InverseGcd(long a, long b)
		{
			a = Mod(a, b);
			if (a == 0) {
				return (b, 0);
			}

			long s = b;
			long t = a;
			long m0 = 0;
			long m1 = 1;
			long u;
			while (true) {
				if (t == 0) {
					if (m0 < 0) {
						m0 += b / s;
					}

					return (s, m0);
				}

				u = s / t;
				s -= t * u;
				m0 -= m1 * u;

				if (s == 0) {
					if (m1 < 0) {
						m1 += b / t;
					}

					return (t, m1);
				}

				u = t / s;
				t -= s * u;
				m1 -= m0 * u;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long Garner(Span<long> values, Span<long> mods)
		{
			long coefficient = 1;
			long x = values[0] % mods[0];
			for (int i = 1; i < values.Length; i++) {
				coefficient *= mods[i - 1];
				long t = (long)((BigInteger)(values[i] - x)
					* InverseMod(coefficient, mods[i])
					% mods[i]);
				if (t < 0) {
					t += mods[i];
				}

				x += t * coefficient;
			}

			return x;
		}

		// original: ttps://github.com/key-moon/ac-library-cs
		// x=y(mod p = lcm of mods) on x=valus[i](mod[i])
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (long y, long p) ChineseRemainderTheorem(long[] values, long[] mods)
		{
			long r0 = 0, m0 = 1;
			for (int i = 0; i < mods.Length; i++) {
				long r1 = Mod(values[i], mods[i]);
				long m1 = mods[i];
				if (m0 < m1) {
					(r0, r1) = (r1, r0);
					(m0, m1) = (m1, m0);
				}
				if (m0 % m1 == 0) {
					if (r0 % m1 != r1) {
						return (0, 0);
					}

					continue;
				}
				var (g, im) = InverseGcd(m0, m1);

				long u1 = (m1 / g);
				if ((r1 - r0) % g != 0) {
					return (0, 0);
				}

				long x = (r1 - r0) / g % u1 * im % u1;
				r0 += x * m0;
				m0 *= u1;
				if (r0 < 0) {
					r0 += m0;
				}
			}

			return (r0, m0);
		}

		// original: ttps://github.com/key-moon/ac-library-cs
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long FloorSum(long n, long m, long a, long b)
		{
			long ans = 0;
			while (true) {
				if (a >= m) {
					ans += (n - 1) * n * (a / m) / 2;
					a %= m;
				}

				if (b >= m) {
					ans += n * (b / m);
					b %= m;
				}

				long yMax = (a * n + b) / m;
				long xMax = yMax * m - b;
				if (yMax == 0) {
					return ans;
				}

				ans += (n - (xMax + a - 1) / a) * yMax;
				(n, m, a, b) = (yMax, a, m, (a - xMax % a) % a);
			}
		}

		public class BarrettReduction
		{
			private readonly ulong im_;
			public uint Mod { get; private set; }

			public BarrettReduction(uint m)
			{
				Mod = m;
				im_ = unchecked((ulong)-1) / m + 1;
			}

			public uint Multilply(uint a, uint b)
			{
				ulong z = a;
				z *= b;
				if (!Bmi2.X64.IsSupported) {
					return (uint)(z % Mod);
				}

				var x = Bmi2.X64.MultiplyNoFlags(z, im_);
				var v = unchecked((uint)(z - x * Mod));
				if (Mod <= v) {
					v += Mod;
				}

				return v;
			}
		}
	}
}
