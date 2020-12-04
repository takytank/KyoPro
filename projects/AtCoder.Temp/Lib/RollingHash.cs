using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AtCoder.Temp.Lib
{
	///https://qiita.com/keymoon/items/11fac5627672a6d6a9f6
	public class RollingHash
	{
		private const ulong MASK30 = (1UL << 30) - 1;
		private const ulong MASK31 = (1UL << 31) - 1;
		private const ulong MOD = (1UL << 61) - 1;
		private const ulong POSITIVIZER = MOD * 4;

		private static readonly ulong base_;
		private static readonly List<ulong> basePows_ = new List<ulong>(1000) { 1 };
		static RollingHash()
		{
			var random = new Random();
			base_ = (uint)random.Next(1 << 10, int.MaxValue);
		}

		private readonly ulong[] hash_;
		public string String { get; }
		public RollingHash(string s)
		{
			String = s;
			int n = s.Length;
			for (int i = basePows_.Count; i <= n; i++) {
				basePows_.Add(CalculateMod(basePows_[i - 1] * base_));
			}

			hash_ = new ulong[n + 1];
			for (int i = 0; i < n; i++) {
				hash_[i + 1] = CalculateMod(Multiply(hash_[i], base_) + s[i]);
			}
		}

		public ulong GetHash(Range range)
		{
			var (start, offset) = range.GetOffsetAndLength(String.Length);
			return GetHash(start, start + offset);
		}
		public ulong GetHash(int a, int b)
		{
			return CalculateMod(hash_[b] + POSITIVIZER - Multiply(hash_[a], basePows_[b - a]));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ulong Multiply(ulong a, ulong b)
		{
			ulong msbA = a >> 31;
			ulong lsbA = a & MASK31;
			ulong msbB = b >> 31;
			ulong lsbB = b & MASK31;
			ulong mid = lsbA * msbB + msbA * lsbB;
			return msbA * msbB * 2 + (mid >> 30) + ((mid & MASK30) << 31) + lsbA * lsbB;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ulong CalculateMod(ulong x)
		{
			ulong res = (x >> 61) + (x & MOD);
			if (res >= MOD) {
				res -= MOD;
			}

			return res;
		}
	}
}
