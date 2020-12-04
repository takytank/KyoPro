using System;
using System.Collections.Generic;

namespace AOJ.Temp.Lib
{
	public class Bitset
	{
		public static int PopCount(ulong bits)
		{
			bits = ((bits & 0xaaaaaaaaaaaaaaaa) >> 1) + (bits & 0x5555555555555555);
			bits = ((bits & 0xcccccccccccccccc) >> 2) + (bits & 0x3333333333333333);
			bits = ((bits & 0xf0f0f0f0f0f0f0f0) >> 4) + (bits & 0x0f0f0f0f0f0f0f0f);
			bits = ((bits & 0xff00ff00ff00ff00) >> 8) + (bits & 0x00ff00ff00ff00ff);
			bits = ((bits & 0xffff0000ffff0000) >> 16) + (bits & 0x0000ffff0000ffff);
			bits = ((bits & 0xffffffff00000000) >> 32) + (bits & 0x00000000ffffffff);
			return (int)bits;
		}

		private readonly int length_;
		private readonly ulong[] bits_;

		public int Size { get; private set; }
		public Bitset(int size, bool fillTrue = false)
		{
			Size = size;
			length_ = ((size - 1) >> 6) + 1;
			bits_ = new ulong[length_];
			if (fillTrue) {
				int y = size % 64;
				if (y == 0) {
					for (int i = 0; i < length_; i++) {
						bits_[i] = ulong.MaxValue;
					}
				} else {
					for (int i = 0; i < length_ - 1; i++) {
						bits_[i] = ulong.MaxValue;
					}

					bits_[length_ - 1] = (1UL << y) - 1;
				}
			}
		}
		public Bitset(int size, long initialValue)
		{
			Size = size;
			length_ = ((size - 1) >> 6) + 1;
			bits_ = new ulong[length_];
			bits_[0] = (ulong)initialValue;
		}

		public bool this[int i]
		{
			get { return ((bits_[i >> 6] >> i) & 1) != 0; }
			set { bits_[i >> 6] = (bits_[i >> 6] & (ulong.MaxValue ^ (1ul << i))) | ((ulong)(value ? 1 : 0) << i); }
		}

		public static Bitset operator &(Bitset lhs, Bitset rhs)
		{
			int min = Math.Min(lhs.length_, rhs.length_);

			var ret = new Bitset(Math.Max(lhs.Size, rhs.Size));
			for (int i = 0; i < min; i++) {
				ret.bits_[i] = lhs.bits_[i] & rhs.bits_[i];
			}

			return ret;
		}
		public static Bitset operator |(Bitset lhs, Bitset rhs)
		{
			int max = Math.Max(lhs.length_, rhs.length_);
			int min = Math.Min(lhs.length_, rhs.length_);
			var ret = new Bitset(Math.Max(lhs.Size, rhs.Size));

			for (int i = 0; i < min; i++) {
				ret.bits_[i] = lhs.bits_[i] | rhs.bits_[i];
			}

			if (lhs.length_ > rhs.length_) {
				for (int i = min; i < max; i++) {
					ret.bits_[i] = lhs.bits_[i];
				}
			} else {
				for (int i = min; i < max; i++) {
					ret.bits_[i] = rhs.bits_[i];
				}
			}

			return ret;
		}
		public static Bitset operator ^(Bitset lhs, Bitset rhs)
		{
			int max = Math.Max(lhs.length_, rhs.length_);
			int min = Math.Min(lhs.length_, rhs.length_);
			var ret = new Bitset(Math.Max(lhs.Size, rhs.Size));

			for (int i = 0; i < min; i++) {
				ret.bits_[i] = lhs.bits_[i] ^ rhs.bits_[i];
			}

			if (lhs.length_ > rhs.length_) {
				for (int i = min; i < max; i++) {
					ret.bits_[i] = lhs.bits_[i];
				}
			} else {
				for (int i = min; i < max; i++) {
					ret.bits_[i] = rhs.bits_[i];
				}
			}

			return ret;
		}
		public static Bitset operator <<(Bitset target, int shift)
		{
			var ret = new Bitset(target.Size);
			if (shift > target.length_ << 6) {
				return ret;
			}

			int minIndex = (shift + 63) >> 6;
			if (shift % 64 == 0) {
				for (int i = target.length_ - 1; i >= minIndex; i--) {
					ret.bits_[i] = target.bits_[i - minIndex];
				}
			} else {
				for (int i = target.length_ - 1; i >= minIndex; i--) {
					ret.bits_[i] = (target.bits_[i - minIndex + 1] << shift)
						| (target.bits_[i - minIndex] >> (64 - shift));
				}

				ret.bits_[minIndex - 1] = target.bits_[0] << shift;
			}

			return ret;
		}
		public static Bitset operator >>(Bitset target, int shift)
		{
			var ret = new Bitset(target.Size);
			if (shift > target.length_ << 6) {
				return ret;
			}

			int minIndex = (shift + 63) >> 6;
			if (shift % 64 == 0) {
				for (int i = 0; i + minIndex < ret.length_; i++) {
					ret.bits_[i] = target.bits_[i + minIndex];
				}
			} else {
				for (int i = 0; i + minIndex < ret.length_; i++) {
					ret.bits_[i] = (target.bits_[i + minIndex - 1] >> shift)
						| (target.bits_[i + minIndex] << (64 - shift));
				}

				ret.bits_[ret.length_ - minIndex] = target.bits_[ret.length_ - 1] >> shift;
			}

			return ret;
		}
		public static bool operator ==(Bitset lhs, Bitset rhs)
		{
			int max = Math.Max(lhs.length_, rhs.length_);
			int min = Math.Min(lhs.length_, rhs.length_);
			for (int i = 0; i < min; i++) {
				if (lhs.bits_[i] != rhs.bits_[i]) {
					return false;
				}
			}

			if (lhs.length_ > rhs.length_) {
				for (int i = min; i < max; i++) {
					if (lhs.bits_[i] != 0) {
						return false;
					}
				}
			} else {
				for (int i = min; i < max; i++) {
					if (rhs.bits_[i] != 0) {
						return false;
					}
				}
			}

			return true;
		}
		public static bool operator !=(Bitset lhs, Bitset rhs)
		{
			return !(lhs == rhs);
		}
		public override bool Equals(object obj)
		{
			return false;
		}

		public override int GetHashCode()
		{
			var hashCode = -723801931;
			hashCode = hashCode * -1521134295 + length_.GetHashCode();
			hashCode = hashCode * -1521134295 + EqualityComparer<ulong[]>.Default.GetHashCode(bits_);
			hashCode = hashCode * -1521134295 + Size.GetHashCode();
			return hashCode;
		}

		public override string ToString()
		{
			string ret = "";
			for (int i = 0; i < length_ - 1; i++) {
				string temp = Convert.ToString((long)bits_[i], 2);
				temp = (64 > temp.Length ? new string('0', 64 - temp.Length) : "")
					+ temp;
				ret = temp + ret;
			}

			{
				string temp = Convert.ToString((long)bits_[length_ - 1], 2);
				temp = ((Size % 64) > temp.Length ? new string('0', (Size % 64) - temp.Length) : "")
					+ temp;
				ret = temp + ret;
			}

			return ret;
		}
	}

	public class BitsetOriginal
	{
		public int n, l;
		public ulong[] he;
		public BitsetOriginal(int n)
		{
			this.n = n;
			l = (n >> 6) + 1;
			he = new ulong[l];
		}
		public BitsetOriginal(int n, long a)
		{
			this.n = n;
			l = (n >> 6) + 1;
			he = new ulong[l];
			he[0] = (ulong)a;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage(
			"Style", "IDE0060:未使用のパラメーターを削除します", Justification = "")]
		public BitsetOriginal(int n, bool b)
		{
			this.n = n;
			l = (n >> 6) + 1;
			he = new ulong[l];
			int y = n % 64;
			if (y == 0) { for (int i = 0; i < l; i++) { he[i] = ulong.MaxValue; } } else {
				for (int i = 0; i < l - 1; i++) { he[i] = ulong.MaxValue; }
				he[l - 1] = (1UL << y) - 1;
			}
		}
		public int this[int i]
		{
			set { he[i >> 6] = (he[i >> 6] & (ulong.MaxValue ^ (1UL << i))) | ((ulong)value << i); }
			get { return (int)((he[i >> 6] >> i) & 1); }
		}
		public static BitsetOriginal operator &(BitsetOriginal a, BitsetOriginal b)
		{
			BitsetOriginal c = new BitsetOriginal(a.n);
			int d = Math.Min(a.l, b.l);
			for (int i = 0; i < d; i++) { c.he[i] = a.he[i] & b.he[i]; }
			return c;
		}
		public static BitsetOriginal operator |(BitsetOriginal a, BitsetOriginal b)
		{
			BitsetOriginal c;
			if (a.l < b.l) { c = a; a = b; b = c; }
			c = new BitsetOriginal(a.n);
			int i = 0;
			for (; i < b.l; i++) { c.he[i] = a.he[i] | b.he[i]; }
			for (; i < a.l; i++) { c.he[i] = a.he[i]; }
			return c;
		}
		public static BitsetOriginal operator ^(BitsetOriginal a, BitsetOriginal b)
		{
			BitsetOriginal c;
			if (a.l < b.l) { c = a; a = b; b = c; }
			c = new BitsetOriginal(a.n);
			int i = 0;
			for (; i < b.l; i++) { c.he[i] = a.he[i] ^ b.he[i]; }
			for (; i < a.l; i++) { c.he[i] = a.he[i]; }
			return c;
		}
		public static BitsetOriginal operator <<(BitsetOriginal a, int b)
		{
			BitsetOriginal c = new BitsetOriginal(a.n);
			if (b > a.l << 6) { return c; }
			int d = (b + 63) >> 6;
			if (b % 64 == 0) { for (int i = a.l - 1; i >= d; i--) { c.he[i] = a.he[i - d]; } } else {
				for (int i = a.l - 1; i >= d; i--) { c.he[i] = (a.he[i - d + 1] << b) | (a.he[i - d] >> (64 - b)); }
				c.he[d - 1] = a.he[0] << b;
			}
			return c;
		}
		public static BitsetOriginal operator >>(BitsetOriginal a, int b)
		{
			BitsetOriginal c = new BitsetOriginal(a.n);
			if (b > a.l << 6) { return c; }
			int d = (b + 63) >> 6;
			if (b % 64 == 0) { for (int i = 0; i + d < c.l; i++) { c.he[i] = a.he[i + d]; } } else {
				for (int i = 0; i + d < c.l; i++) { c.he[i] = (a.he[i + d - 1] >> b) | (a.he[i + d] << (64 - b)); }
				c.he[c.l - d] = a.he[c.l - 1] >> b;
			}
			return c;
		}
		public static bool operator ==(BitsetOriginal a, BitsetOriginal b)
		{
			int mx = Math.Max(a.l, b.l), mn = Math.Min(a.l, b.l);
			for (int i = 0; i < mn; i++) { if (a.he[i] != b.he[i]) { return false; } }
			if (a.l > b.l) { for (int i = mn; i < mx; i++) { if (a.he[i] != 0) { return false; } } } else { for (int i = mn; i < mx; i++) { if (b.he[i] != 0) { return false; } } }
			return true;
		}
		public static bool operator !=(BitsetOriginal a, BitsetOriginal b)
		{
			int mx = Math.Max(a.l, b.l), mn = Math.Min(a.l, b.l);
			for (int i = 0; i < mn; i++) { if (a.he[i] != b.he[i]) { return true; } }
			if (a.l > b.l) { for (int i = mn; i < mx; i++) { if (a.he[i] != 0) { return true; } } } else { for (int i = mn; i < mx; i++) { if (b.he[i] != 0) { return true; } } }
			return false;
		}
		public override bool Equals(object obj) { return false; }
		public override int GetHashCode() { return 0; }
		public string St()
		{
			string t = "", s;
			for (int i = 0; i < l - 1; i++) {
				s = Convert.ToString((long)he[i], 2);
				s = (64 > s.Length ? new string('0', 64 - s.Length) : "") + s;
				t = s + t;
			}
			s = Convert.ToString((long)he[l - 1], 2);
			s = ((n % 64) > s.Length ? new string('0', (n % 64) - s.Length) : "") + s;
			return s + t;
		}
	}
}
