using System;
using System.Collections.Generic;
using System.Text;

namespace AtCoder.Temp.Lib
{
	public class Bitset
	{
		private const int BIT_SHIFT_64 = 6;
		private const int BITS_64 = 64;

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

		public int Count { get; }
		public Bitset(int bitCount, bool fillTrue = false)
		{
			Count = bitCount;
			length_ = ((bitCount - 1) >> BIT_SHIFT_64) + 1;
			bits_ = new ulong[length_];
			if (fillTrue) {
				int y = bitCount % BITS_64;
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
		public Bitset(int bitCount, long initialValue)
		{
			Count = bitCount;
			length_ = ((bitCount - 1) >> BIT_SHIFT_64) + 1;
			bits_ = new ulong[length_];
			bits_[0] = (ulong)initialValue;
		}

		public bool this[int i]
		{
			get { return ((bits_[i >> BIT_SHIFT_64] >> i) & 1) != 0; }
			set { bits_[i >> 6] = (bits_[i >> BIT_SHIFT_64] & (ulong.MaxValue ^ (1ul << i))) | ((ulong)(value ? 1 : 0) << i); }
		}

		public static Bitset operator &(Bitset lhs, Bitset rhs)
		{
			int min = Math.Min(lhs.length_, rhs.length_);

			var ret = new Bitset(Math.Max(lhs.Count, rhs.Count));
			for (int i = 0; i < min; i++) {
				ret.bits_[i] = lhs.bits_[i] & rhs.bits_[i];
			}

			return ret;
		}
		public static Bitset operator |(Bitset lhs, Bitset rhs)
		{
			int max = Math.Max(lhs.length_, rhs.length_);
			int min = Math.Min(lhs.length_, rhs.length_);
			var ret = new Bitset(Math.Max(lhs.Count, rhs.Count));

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
			var ret = new Bitset(Math.Max(lhs.Count, rhs.Count));

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
			var ret = new Bitset(target.Count);
			if (shift > target.length_ << BIT_SHIFT_64) {
				return ret;
			}

			int minIndex = (shift + 63) >> BIT_SHIFT_64;
			if (shift % BITS_64 == 0) {
				for (int i = target.length_ - 1; i >= minIndex; i--) {
					ret.bits_[i] = target.bits_[i - minIndex];
				}
			} else {
				for (int i = target.length_ - 1; i >= minIndex; i--) {
					ret.bits_[i] = (target.bits_[i - minIndex + 1] << shift)
						| (target.bits_[i - minIndex] >> (BITS_64 - shift));
				}

				ret.bits_[minIndex - 1] = target.bits_[0] << shift;
			}

			return ret;
		}
		public static Bitset operator >>(Bitset target, int shift)
		{
			var ret = new Bitset(target.Count);
			if (shift > target.length_ << BIT_SHIFT_64) {
				return ret;
			}

			int minIndex = (shift + 63) >> BIT_SHIFT_64;
			if (shift % BITS_64 == 0) {
				for (int i = 0; i + minIndex < ret.length_; i++) {
					ret.bits_[i] = target.bits_[i + minIndex];
				}
			} else {
				for (int i = 0; i + minIndex < ret.length_; i++) {
					ret.bits_[i] = (target.bits_[i + minIndex - 1] >> shift)
						| (target.bits_[i + minIndex] << (BITS_64 - shift));
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

		public void Or(Bitset target)
		{
			for (int i = 0; i < Math.Min(length_, target.length_); i++) {
				bits_[i] |= target.bits_[i];
			}
		}
		public void And(Bitset target)
		{
			for (int i = 0; i < Math.Min(length_, target.length_); i++) {
				bits_[i] &= target.bits_[i];
			}
		}
		public void Xor(Bitset target)
		{
			for (int i = 0; i < Math.Min(length_, target.length_); i++) {
				bits_[i] ^= target.bits_[i];
			}
		}

		public void RightShift(int count)
		{
			if (count <= 0) {
				return;
			}

			int toIndex = 0;
			int ints = GetInt64ArrayLengthFromBitLength(Count);
			if (count < Count) {
				int fromIndex = Div64Rem(count, out int shiftCount);
				Div64Rem(Count, out int extraBits);
				if (shiftCount == 0) {
					unchecked {
						ulong mask = ulong.MaxValue >> (BITS_64 - extraBits);
						bits_[ints - 1] &= mask;
					}
					Array.Copy(bits_, fromIndex, bits_, 0, ints - fromIndex);
					toIndex = ints - fromIndex;
				} else {
					int lastIndex = ints - 1;
					unchecked {
						while (fromIndex < lastIndex) {
							ulong right = bits_[fromIndex] >> shiftCount;
							fromIndex++;
							ulong left = bits_[fromIndex] << (BITS_64 - shiftCount);
							bits_[toIndex] = left | right;
							toIndex++;
						}

						ulong mask = ulong.MaxValue >> (BITS_64 - extraBits);
						mask &= bits_[fromIndex];
						bits_[toIndex] = mask >> shiftCount;
						toIndex++;
					}
				}
			}

			bits_.AsSpan(toIndex, ints - toIndex).Clear();
		}
		public void LeftShift(int count)
		{
			if (count <= 0) {
				return;
			}

			int lengthToClear;
			if (count < Count) {
				int lastIndex = (Count - 1) >> BIT_SHIFT_64;

				lengthToClear = Div64Rem(count, out int shiftCount);

				if (shiftCount == 0) {
					Array.Copy(bits_, 0, bits_, lengthToClear, lastIndex + 1 - lengthToClear);
				} else {
					int fromIndex = lastIndex - lengthToClear;
					unchecked {
						while (fromIndex > 0) {
							ulong left = bits_[fromIndex] << shiftCount;
							--fromIndex;
							ulong right = bits_[fromIndex] >> (BITS_64 - shiftCount);
							bits_[lastIndex] = left | right;
							lastIndex--;
						}

						bits_[lastIndex] = bits_[fromIndex] << shiftCount;
					}
				}
			} else {
				lengthToClear = GetInt64ArrayLengthFromBitLength(Count);
			}

			bits_.AsSpan(0, lengthToClear).Clear();
		}

		public void OrWithRightShift(int count)
		{
			if (count <= 0) {
				return;
			}

			int toIndex = 0;
			int ints = GetInt64ArrayLengthFromBitLength(Count);
			if (count < Count) {
				int fromIndex = Div64Rem(count, out int shiftCount);
				Div64Rem(Count, out int extraBits);
				if (shiftCount == 0) {
					unchecked {
						ulong mask = ulong.MaxValue >> (BITS_64 - extraBits);
						bits_[ints - 1] &= mask;
					}
					Array.Copy(bits_, fromIndex, bits_, 0, ints - fromIndex);
				} else {
					int lastIndex = ints - 1;
					unchecked {
						while (fromIndex < lastIndex) {
							ulong right = bits_[fromIndex] >> shiftCount;
							fromIndex++;
							ulong left = bits_[fromIndex] << (BITS_64 - shiftCount);
							bits_[toIndex] |= left | right;
							toIndex++;
						}

						ulong mask = ulong.MaxValue >> (BITS_64 - extraBits);
						mask &= bits_[fromIndex];
						bits_[toIndex] |= mask >> shiftCount;
					}
				}
			}
		}
		public void OrWithLeftShift(int count)
		{
			if (count <= 0) {
				return;
			}

			int lengthToClear;
			if (count < Count) {
				int lastIndex = (Count - 1) >> BIT_SHIFT_64;

				lengthToClear = Div64Rem(count, out int shiftCount);

				if (shiftCount == 0) {
					Array.Copy(bits_, 0, bits_, lengthToClear, lastIndex + 1 - lengthToClear);
				} else {
					int fromIndex = lastIndex - lengthToClear;
					unchecked {
						while (fromIndex > 0) {
							ulong left = bits_[fromIndex] << shiftCount;
							--fromIndex;
							ulong right = bits_[fromIndex] >> (BITS_64 - shiftCount);
							bits_[lastIndex] |= left | right;
							lastIndex--;
						}

						bits_[lastIndex] |= bits_[fromIndex] << shiftCount;
					}
				}
			}
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(length_, bits_, Count);
		}

		public override string ToString()
		{
			string ret = "";
			for (int i = 0; i < length_ - 1; i++) {
				string temp = Convert.ToString((long)bits_[i], 2);
				temp = (BITS_64 > temp.Length ? new string('0', BITS_64 - temp.Length) : "")
					+ temp;
				ret = temp + ret;
			}

			{
				string temp = Convert.ToString((long)bits_[length_ - 1], 2);
				temp = ((Count % BITS_64) > temp.Length ? new string('0', (Count % BITS_64) - temp.Length) : "")
					+ temp;
				ret = temp + ret;
			}

			return ret;
		}

		private static int GetInt64ArrayLengthFromBitLength(int n)
		{
			return (int)((ulong)(n - 1 + (1 << BIT_SHIFT_64)) >> BIT_SHIFT_64);
		}

		private static int Div64Rem(int number, out int remainder)
		{
			uint quotient = (uint)number / BITS_64;
			remainder = number & (BITS_64 - 1);
			return (int)quotient;
		}

	}
}
