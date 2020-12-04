using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public struct ModInt
	{
		public static long P { get; set; }
		static ModInt()
		{
			P = (long)1e9 + 7;;
		}

		public static ModInt Pow(ModInt value, long k)
		{
			return Pow(value.Value, k);
		}

		public static ModInt Pow(long value, long k)
		{
			long ret = 1;
			for (k %= P - 1; k > 0; k >>= 1, value = value * value % P) {
				if ((k & 1) == 1) {
					ret = ret * value % P;
				}
			}
			return new ModInt(ret);
		}

		public static ModInt Inverse(ModInt value)
		{
			return Pow(value, P - 2);
		}

		public long Value;

		public ModInt(long value)
		{
			value %= P;
			if (value < 0) {
				value += P;
			}

			Value = value;
		}

		public static ModInt operator +(ModInt lhs, ModInt rhs)
		{
			lhs.Value += rhs.Value;
			if (lhs.Value >= P) {
				lhs.Value -= P;
			}
			return lhs;
		}

		public static ModInt operator -(ModInt lhs, ModInt rhs)
		{
			lhs.Value -= rhs.Value;
			if (lhs.Value < 0) {
				lhs.Value += P;
			}

			return lhs;
		}

		public static ModInt operator *(ModInt lhs, ModInt rhs)
		{
			return new ModInt(lhs.Value * rhs.Value % P);
		}
		public static ModInt operator /(ModInt lhs, ModInt rhs)
		{
			long exp = P - 2;
			while (exp > 0) {
				if (exp % 2 > 0) {
					lhs *= rhs;
				}

				rhs *= rhs;
				exp /= 2;
			}

			return lhs;
		 }

		public static implicit operator ModInt(long n)
		{
			n %= P;
			if (n < 0) {
				n += P;
			}

			return new ModInt(n);
		}

		public override string ToString()
		{
			return Value.ToString();
		}
	}

	class BinomialCoefficient
	{
		public ModInt[] fact, ifact;
		public BinomialCoefficient(int n)
		{
			fact = new ModInt[n + 1];
			ifact = new ModInt[n + 1];
			fact[0] = 1;
			for (int i = 1; i <= n; i++)
				fact[i] = fact[i - 1] * i;
			ifact[n] = ModInt.Inverse(fact[n]);
			for (int i = n - 1; i >= 0; i--)
				ifact[i] = ifact[i + 1] * (i + 1);
			ifact[0] = ifact[1];
		}
		public ModInt this[int n, int r]
		{
			get
			{
				if (n < 0 || n >= fact.Length || r < 0 || r > n)
					return 0;
				return fact[n] * ifact[n - r] * ifact[r];
			}
		}
		public ModInt RepeatedCombination(int n, int k)
		{
			if (k == 0)
				return 1;
			return this[n + k - 1, k];
		}
	}

	class Pair<T, U> : IComparable<Pair<T, U>> where T : IComparable<T> where U : IComparable<U>
	{
		public T F;
		public U S;
		public Pair(T f, U s) { this.F = f; this.S = s; }
		public int CompareTo(Pair<T, U> a) 
		{
			return F.CompareTo(a.F) != 0 ? F.CompareTo(a.F) : S.CompareTo(a.S);
		}

		public override string ToString()
		{
			return F.ToString() + " " + S.ToString();
		}
	}
}
