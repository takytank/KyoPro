using System;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace AtCoder
{
	class Program
	{
		static void Main()
		{
			using var cin = new Scanner();
		}
	}

	public struct BitFlag
	{
		public static BitFlag Begin() => 0;
		public static BitFlag End(int bitCount) => 1 << bitCount;
		public static BitFlag FromBit(int bitNumber) => 1 << bitNumber;
		public static BitFlag Fill(int count) => (1 << count) - 1;

		public static IEnumerable<BitFlag> All(int n)
		{
			for (var f = Begin(); f < End(n); ++f) {
				yield return f;
			}
		}

		private readonly int flags_;
		public int Flag => flags_;
		public bool this[int bitNumber] => (flags_ & (1 << bitNumber)) != 0;
		public BitFlag(int flags) { flags_ = flags; }

		public bool Has(BitFlag target) => (flags_ & target.flags_) == target.flags_;
		public bool Has(int target) => (flags_ & target) == target;
		public bool HasBit(int bitNumber) => (flags_ & (1 << bitNumber)) != 0;
		public BitFlag OrBit(int bitNumber) => (flags_ | (1 << bitNumber));
		public BitFlag AndBit(int bitNumber) => (flags_ & (1 << bitNumber));
		public BitFlag XorBit(int bitNumber) => (flags_ ^ (1 << bitNumber));
		public BitFlag ComplementOf(BitFlag sub) => flags_ ^ sub.flags_;
		public int PopCount() => BitOperations.PopCount((uint)flags_);

		public static BitFlag operator ++(BitFlag src) => new BitFlag(src.flags_ + 1);
		public static BitFlag operator --(BitFlag src) => new BitFlag(src.flags_ - 1);
		public static BitFlag operator |(BitFlag lhs, BitFlag rhs)
			=> new BitFlag(lhs.flags_ | rhs.flags_);
		public static BitFlag operator |(BitFlag lhs, int rhs)
			=> new BitFlag(lhs.flags_ | rhs);
		public static BitFlag operator |(int lhs, BitFlag rhs)
			=> new BitFlag(lhs | rhs.flags_);
		public static BitFlag operator &(BitFlag lhs, BitFlag rhs)
			=> new BitFlag(lhs.flags_ & rhs.flags_);
		public static BitFlag operator &(BitFlag lhs, int rhs)
			=> new BitFlag(lhs.flags_ & rhs);
		public static BitFlag operator &(int lhs, BitFlag rhs)
			=> new BitFlag(lhs & rhs.flags_);
		public static BitFlag operator ^(BitFlag lhs, BitFlag rhs)
			=> new BitFlag(lhs.flags_ ^ rhs.flags_);
		public static BitFlag operator ^(BitFlag lhs, int rhs)
			=> new BitFlag(lhs.flags_ ^ rhs);
		public static BitFlag operator ^(int lhs, BitFlag rhs)
			=> new BitFlag(lhs ^ rhs.flags_);

		public static bool operator <(BitFlag lhs, BitFlag rhs) => lhs.flags_ < rhs.flags_;
		public static bool operator <(BitFlag lhs, int rhs) => lhs.flags_ < rhs;
		public static bool operator <(int lhs, BitFlag rhs) => lhs < rhs.flags_;
		public static bool operator >(BitFlag lhs, BitFlag rhs) => lhs.flags_ > rhs.flags_;
		public static bool operator >(BitFlag lhs, int rhs) => lhs.flags_ > rhs;
		public static bool operator >(int lhs, BitFlag rhs) => lhs > rhs.flags_;
		public static bool operator <=(BitFlag lhs, BitFlag rhs) => lhs.flags_ <= rhs.flags_;
		public static bool operator <=(BitFlag lhs, int rhs) => lhs.flags_ <= rhs;
		public static bool operator <=(int lhs, BitFlag rhs) => lhs <= rhs.flags_;
		public static bool operator >=(BitFlag lhs, BitFlag rhs) => lhs.flags_ >= rhs.flags_;
		public static bool operator >=(BitFlag lhs, int rhs) => lhs.flags_ >= rhs;
		public static bool operator >=(int lhs, BitFlag rhs) => lhs >= rhs.flags_;

		public static implicit operator BitFlag(int t) => new BitFlag(t);
		public static implicit operator int(BitFlag t) => t.flags_;

		public override string ToString() => $"{Convert.ToString(flags_, 2).PadLeft(32, '0')} ({flags_})";

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ForEachSubBits(Action<BitFlag> action)
		{
			for (BitFlag sub = flags_; sub > 0; sub = --sub & flags_) {
				action(sub);
			}
		}

		public SubBitsEnumerator SubBits => new SubBitsEnumerator(flags_);
		public struct SubBitsEnumerator : IEnumerable<BitFlag>
		{
			private readonly int flags_;
			public SubBitsEnumerator(int flags)
			{
				flags_ = flags;
			}

			IEnumerator<BitFlag> IEnumerable<BitFlag>.GetEnumerator() => new Enumerator(flags_);
			IEnumerator IEnumerable.GetEnumerator() => new Enumerator(flags_);
			public Enumerator GetEnumerator() => new Enumerator(flags_);
			public struct Enumerator : IEnumerator<BitFlag>
			{
				private readonly int src_;
				public BitFlag Current { get; private set; }
				object IEnumerator.Current => Current;

				public Enumerator(int flags)
				{
					src_ = flags;
					Current = flags + 1;
				}

				public void Dispose() { }
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public bool MoveNext() => (Current = --Current & src_) > 0;
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public void Reset() => Current = src_;
			}
		}
	}

	public class HashMap<TKey, TValue> : Dictionary<TKey, TValue>
	{
		private readonly Func<TKey, TValue> initialzier_;
		public HashMap(Func<TKey, TValue> initialzier)
			: base()
		{
			initialzier_ = initialzier;
		}

		public HashMap(Func<TKey, TValue> initialzier, int capacity)
			: base(capacity)
		{
			initialzier_ = initialzier;
		}

		new public TValue this[TKey key]
		{
			get
			{
				if (TryGetValue(key, out TValue value)) {
					return value;
				} else {
					var init = initialzier_(key);
					base[key] = init;
					return init;
				}
			}

			set { base[key] = value; }
		}

		public HashMap<TKey, TValue> Merge(
			HashMap<TKey, TValue> src,
			Func<TValue, TValue, TValue> mergeValues)
		{
			foreach (var key in src.Keys) {
				this[key] = mergeValues(this[key], src[key]);
			}

			return this;
		}
	}

	public class JagList2<T> where T : struct
	{
		private readonly int n_;
		private readonly List<T>[] tempValues_;
		private T[][] values_;

		public int Count => n_;
		public List<T>[] Raw => tempValues_;
		public T[][] Values => values_;
		public T[] this[int index] => values_[index];

		public JagList2(int n)
		{
			n_ = n;
			tempValues_ = new List<T>[n];
			for (int i = 0; i < n; ++i) {
				tempValues_[i] = new List<T>();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(int i, T value) => tempValues_[i].Add(value);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Build()
		{
			values_ = new T[n_][];
			for (int i = 0; i < values_.Length; ++i) {
				values_[i] = tempValues_[i].ToArray();
			}
		}
	}

	public class DijkstraQ
	{
		private int count_ = 0;
		private long[] distanceHeap_;
		private int[] vertexHeap_;

		public int Count => count_;
		public DijkstraQ()
		{
			distanceHeap_ = new long[8];
			vertexHeap_ = new int[8];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Enqueue(long distance, int v)
		{
			if (distanceHeap_.Length == count_) {
				var newDistanceHeap = new long[distanceHeap_.Length << 1];
				var newVertexHeap = new int[vertexHeap_.Length << 1];
				Unsafe.CopyBlock(
					ref Unsafe.As<long, byte>(ref newDistanceHeap[0]),
					ref Unsafe.As<long, byte>(ref distanceHeap_[0]),
					(uint)(8 * count_));
				Unsafe.CopyBlock(
					ref Unsafe.As<int, byte>(ref newVertexHeap[0]),
					ref Unsafe.As<int, byte>(ref vertexHeap_[0]),
					(uint)(4 * count_));
				distanceHeap_ = newDistanceHeap;
				vertexHeap_ = newVertexHeap;
			}

			ref var dRef = ref distanceHeap_[0];
			ref var vRef = ref vertexHeap_[0];
			Unsafe.Add(ref dRef, count_) = distance;
			Unsafe.Add(ref vRef, count_) = v;
			++count_;

			int c = count_ - 1;
			while (c > 0) {
				int p = (c - 1) >> 1;
				var tempD = Unsafe.Add(ref dRef, p);
				if (tempD <= distance) {
					break;
				} else {
					Unsafe.Add(ref dRef, c) = tempD;
					Unsafe.Add(ref vRef, c) = Unsafe.Add(ref vRef, p);
					c = p;
				}
			}

			Unsafe.Add(ref dRef, c) = distance;
			Unsafe.Add(ref vRef, c) = v;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (long distance, int v) Dequeue()
		{
			ref var dRef = ref distanceHeap_[0];
			ref var vRef = ref vertexHeap_[0];
			(long distance, int v) ret = (dRef, vRef);
			int n = count_ - 1;

			var distance = Unsafe.Add(ref dRef, n);
			var vertex = Unsafe.Add(ref vRef, n);
			int p = 0;
			int c = (p << 1) + 1;
			while (c < n) {
				if (c != n - 1 && Unsafe.Add(ref dRef, c + 1) < Unsafe.Add(ref dRef, c)) {
					++c;
				}

				var tempD = Unsafe.Add(ref dRef, c);
				if (distance > tempD) {
					Unsafe.Add(ref dRef, p) = tempD;
					Unsafe.Add(ref vRef, p) = Unsafe.Add(ref vRef, c);
					p = c;
					c = (p << 1) + 1;
				} else {
					break;
				}
			}

			Unsafe.Add(ref dRef, p) = distance;
			Unsafe.Add(ref vRef, p) = vertex;
			--count_;

			return ret;
		}
	}

	public struct ModInt
	{
		//public const long P = 1000000007;
		public const long P = 998244353;
		//public const long P = 2;
		public const long ROOT = 3;

		// (924844033, 5)
		// (998244353, 3)
		// (1012924417, 5)
		// (167772161, 3)
		// (469762049, 3)
		// (1224736769, 3)

		private long value_;

		public static ModInt New(long value, bool mods) => new ModInt(value, mods);
		public ModInt(long value) => value_ = value;
		public ModInt(long value, bool mods)
		{
			if (mods) {
				value %= P;
				if (value < 0) {
					value += P;
				}
			}

			value_ = value;
		}

		public static ModInt operator +(ModInt lhs, ModInt rhs)
		{
			lhs.value_ = (lhs.value_ + rhs.value_) % P;
			return lhs;
		}
		public static ModInt operator +(long lhs, ModInt rhs)
		{
			rhs.value_ = (lhs + rhs.value_) % P;
			return rhs;
		}
		public static ModInt operator +(ModInt lhs, long rhs)
		{
			lhs.value_ = (lhs.value_ + rhs) % P;
			return lhs;
		}

		public static ModInt operator -(ModInt lhs, ModInt rhs)
		{
			lhs.value_ = (P + lhs.value_ - rhs.value_) % P;
			return lhs;
		}
		public static ModInt operator -(long lhs, ModInt rhs)
		{
			rhs.value_ = (P + lhs - rhs.value_) % P;
			return rhs;
		}
		public static ModInt operator -(ModInt lhs, long rhs)
		{
			lhs.value_ = (P + lhs.value_ - rhs) % P;
			return lhs;
		}

		public static ModInt operator *(ModInt lhs, ModInt rhs)
		{
			lhs.value_ = lhs.value_ * rhs.value_ % P;
			return lhs;
		}
		public static ModInt operator *(long lhs, ModInt rhs)
		{
			rhs.value_ = lhs * rhs.value_ % P;
			return rhs;
		}
		public static ModInt operator *(ModInt lhs, long rhs)
		{
			lhs.value_ = lhs.value_ * rhs % P;
			return lhs;
		}

		public static ModInt operator /(ModInt lhs, ModInt rhs)
			=> lhs * Inverse(rhs);

		public static implicit operator ModInt(long n) => new ModInt(n, true);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ModInt Inverse(ModInt value) => Pow(value, P - 2);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ModInt Pow(ModInt value, long k) => Pow(value.value_, k);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ModInt Pow(long value, long k)
		{
			long ret = 1;
			while (k > 0) {
				if ((k & 1) != 0) {
					ret = ret * value % P;
				}

				value = value * value % P;
				k >>= 1;
			}

			return new ModInt(ret);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long ToLong() => value_;
		public override string ToString() => value_.ToString();
	}

	public static class Helper
	{
		public static long INF => 1L << 60;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T Clamp<T>(this T value, T min, T max) where T : struct, IComparable<T>
		{
			if (value.CompareTo(min) <= 0) {
				return min;
			}

			if (value.CompareTo(max) >= 0) {
				return max;
			}

			return value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void UpdateMin<T>(this ref T target, T value) where T : struct, IComparable<T>
			=> target = target.CompareTo(value) > 0 ? value : target;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void UpdateMin<T>(this ref T target, T value, Action<T> onUpdated)
			where T : struct, IComparable<T>
		{
			if (target.CompareTo(value) > 0) {
				target = value;
				onUpdated(value);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void UpdateMax<T>(this ref T target, T value) where T : struct, IComparable<T>
			=> target = target.CompareTo(value) < 0 ? value : target;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void UpdateMax<T>(this ref T target, T value, Action<T> onUpdated)
			where T : struct, IComparable<T>
		{
			if (target.CompareTo(value) < 0) {
				target = value;
				onUpdated(value);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[] Array1<T>(int n, T initialValue) where T : struct
			=> new T[n].Fill(initialValue);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[] Array1<T>(int n, Func<int, T> initializer)
			=> Enumerable.Range(0, n).Select(x => initializer(x)).ToArray();
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[] Fill<T>(this T[] array, T value)
			where T : struct
		{
			array.AsSpan().Fill(value);
			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[,] Array2<T>(int n, int m, T initialValule) where T : struct
			=> new T[n, m].Fill(initialValule);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[,] Array2<T>(int n, int m, Func<int, int, T> initializer)
		{
			var array = new T[n, m];
			for (int i = 0; i < n; ++i) {
				for (int j = 0; j < m; ++j) {
					array[i, j] = initializer(i, j);
				}
			}

			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[,] Fill<T>(this T[,] array, T initialValue)
			where T : struct
		{
			MemoryMarshal.CreateSpan<T>(ref array[0, 0], array.Length).Fill(initialValue);
			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<T> AsSpan<T>(this T[,] array, int i)
			=> MemoryMarshal.CreateSpan<T>(ref array[i, 0], array.GetLength(1));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[,,] Array3<T>(int n1, int n2, int n3, T initialValue)
			where T : struct
			=> new T[n1, n2, n3].Fill(initialValue);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[,,] Fill<T>(this T[,,] array, T initialValue)
			where T : struct
		{
			MemoryMarshal.CreateSpan<T>(ref array[0, 0, 0], array.Length).Fill(initialValue);
			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<T> AsSpan<T>(this T[,,] array, int i, int j)
			=> MemoryMarshal.CreateSpan<T>(ref array[i, j, 0], array.GetLength(2));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[,,,] Array4<T>(int n1, int n2, int n3, int n4, T initialValue)
			where T : struct
			=> new T[n1, n2, n3, n4].Fill(initialValue);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[,,,] Fill<T>(this T[,,,] array, T initialValue)
			where T : struct
		{
			MemoryMarshal.CreateSpan<T>(ref array[0, 0, 0, 0], array.Length).Fill(initialValue);
			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<T> AsSpan<T>(this T[,,,] array, int i, int j, int k)
			=> MemoryMarshal.CreateSpan<T>(ref array[i, j, k, 0], array.GetLength(3));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[] Merge<T>(ReadOnlySpan<T> first, ReadOnlySpan<T> second) where T : IComparable<T>
		{
			var ret = new T[first.Length + second.Length];
			int p = 0;
			int q = 0;
			while (p < first.Length || q < second.Length) {
				if (p == first.Length) {
					ret[p + q] = second[q];
					q++;
					continue;
				}

				if (q == second.Length) {
					ret[p + q] = first[p];
					p++;
					continue;
				}

				if (first[p].CompareTo(second[q]) < 0) {
					ret[p + q] = first[p];
					p++;
				} else {
					ret[p + q] = second[q];
					q++;
				}
			}

			return ret;
		}

		private static readonly int[] delta4_ = { 1, 0, -1, 0, 1 };
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DoIn4(int i, int j, int imax, int jmax, Action<int, int> action)
		{
			for (int dn = 0; dn < 4; ++dn) {
				int d4i = i + delta4_[dn];
				int d4j = j + delta4_[dn + 1];
				if ((uint)d4i < (uint)imax && (uint)d4j < (uint)jmax) {
					action(d4i, d4j);
				}
			}
		}

		private static readonly int[] delta8_ = { 1, 0, -1, 0, 1, 1, -1, -1, 1 };
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DoIn8(int i, int j, int imax, int jmax, Action<int, int> action)
		{
			for (int dn = 0; dn < 8; ++dn) {
				int d8i = i + delta8_[dn];
				int d8j = j + delta8_[dn + 1];
				if ((uint)d8i < (uint)imax && (uint)d8j < (uint)jmax) {
					action(d8i, d8j);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ForEachSubBits(int bit, Action<int> action)
		{
			for (int sub = bit; sub >= 0; --sub) {
				sub &= bit;
				action(sub);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string Reverse(string src)
		{
			var chars = src.ToCharArray();
			for (int i = 0, j = chars.Length - 1; i < j; ++i, --j) {
				var tmp = chars[i];
				chars[i] = chars[j];
				chars[j] = tmp;
			}

			return new string(chars);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string Exchange(string src, char a, char b)
		{
			var chars = src.ToCharArray();
			for (int i = 0; i < chars.Length; i++) {
				if (chars[i] == a) {
					chars[i] = b;
				} else if (chars[i] == b) {
					chars[i] = a;
				}
			}

			return new string(chars);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Swap(this string str, int i, int j)
		{
			var span = str.AsWriteableSpan();
			(span[i], span[j]) = (span[j], span[i]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char Replace(this string str, int index, char c)
		{
			var span = str.AsWriteableSpan();
			char old = span[index];
			span[index] = c;
			return old;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<char> AsWriteableSpan(this string str)
		{
			var span = str.AsSpan();
			return MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), span.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string Join<T>(this IEnumerable<T> values, string separator = "")
			=> string.Join(separator, values);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string JoinNL<T>(this IEnumerable<T> values)
			=> string.Join(Environment.NewLine, values);
	}

	public static class Extensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<T> AsSpan<T>(this List<T> list)
		{
			return Unsafe.As<FakeList<T>>(list).Array.AsSpan(0, list.Count);
		}

		private class FakeList<T>
		{
			public T[] Array = null;
		}
	}

	public class Scanner : IDisposable
	{
		private const int BUFFER_SIZE = 1024;
		private const int ASCII_CHAR_BEGIN = 33;
		private const int ASCII_CHAR_END = 126;
		private readonly string filePath_;
		private readonly Stream stream_;
		private readonly byte[] buf_ = new byte[BUFFER_SIZE];
		private int length_ = 0;
		private int index_ = 0;
		private bool isEof_ = false;

		public Scanner(string file = "")
		{
			if (string.IsNullOrWhiteSpace(file)) {
				stream_ = Console.OpenStandardInput();
			} else {
				filePath_ = file;
				stream_ = new FileStream(file, FileMode.Open);
			}

			Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) {
				AutoFlush = false
			});
		}

		public void Dispose()
		{
			Console.Out.Flush();
			stream_.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public char Char()
		{
			byte b;
			do {
				b = Read();
			} while (b < ASCII_CHAR_BEGIN || ASCII_CHAR_END < b);

			return (char)b;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string Next()
		{
			var sb = new StringBuilder();
			for (var b = Char(); b >= ASCII_CHAR_BEGIN && b <= ASCII_CHAR_END; b = (char)Read()) {
				sb.Append(b);
			}

			return sb.ToString();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string[] ArrayString(int length)
		{
			var array = new string[length];
			for (int i = 0; i < length; ++i) {
				array[i] = Next();
			}

			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Int() => (int)Long();
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Int(int offset) => Int() + offset;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (int, int) Int2(int offset = 0)
			=> (Int(offset), Int(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (int, int, int) Int3(int offset = 0)
			=> (Int(offset), Int(offset), Int(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (int, int, int, int) Int4(int offset = 0)
			=> (Int(offset), Int(offset), Int(offset), Int(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (int, int, int, int, int) Int5(int offset = 0)
			=> (Int(offset), Int(offset), Int(offset), Int(offset), Int(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int[] ArrayInt(int length, int offset = 0)
		{
			var array = new int[length];
			for (int i = 0; i < length; ++i) {
				array[i] = Int(offset);
			}

			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long Long()
		{
			long ret = 0;
			byte b;
			bool ng = false;
			do {
				b = Read();
			} while (b != '-' && (b < '0' || '9' < b));

			if (b == '-') {
				ng = true;
				b = Read();
			}

			for (; true; b = Read()) {
				if (b < '0' || '9' < b) {
					return ng ? -ret : ret;
				} else {
					ret = ret * 10 + b - '0';
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long Long(long offset) => Long() + offset;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (long, long) Long2(long offset = 0)
			=> (Long(offset), Long(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (long, long, long) Long3(long offset = 0)
			=> (Long(offset), Long(offset), Long(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (long, long, long, long) Long4(long offset = 0)
			=> (Long(offset), Long(offset), Long(offset), Long(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (long, long, long, long, long) Long5(long offset = 0)
			=> (Long(offset), Long(offset), Long(offset), Long(offset), Long(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long[] ArrayLong(int length, long offset = 0)
		{
			var array = new long[length];
			for (int i = 0; i < length; ++i) {
				array[i] = Long(offset);
			}

			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public BigInteger Big() => new BigInteger(Long());
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public BigInteger Big(long offset) => Big() + offset;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (BigInteger, BigInteger) Big2(long offset = 0)
			=> (Big(offset), Big(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (BigInteger, BigInteger, BigInteger) Big3(long offset = 0)
			=> (Big(offset), Big(offset), Big(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (BigInteger, BigInteger, BigInteger, BigInteger) Big4(long offset = 0)
			=> (Big(offset), Big(offset), Big(offset), Big(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (BigInteger, BigInteger, BigInteger, BigInteger, BigInteger) Big5(long offset = 0)
			=> (Big(offset), Big(offset), Big(offset), Big(offset), Big(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public BigInteger[] ArrayBig(int length, long offset = 0)
		{
			var array = new BigInteger[length];
			for (int i = 0; i < length; ++i) {
				array[i] = Big(offset);
			}

			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double Double() => double.Parse(Next(), CultureInfo.InvariantCulture);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double Double(double offset) => Double() + offset;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (double, double) Double2(double offset = 0)
			=> (Double(offset), Double(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (double, double, double) Double3(double offset = 0)
			=> (Double(offset), Double(offset), Double(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (double, double, double, double) Double4(double offset = 0)
			=> (Double(offset), Double(offset), Double(offset), Double(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (double, double, double, double, double) Double5(double offset = 0)
			=> (Double(offset), Double(offset), Double(offset), Double(offset), Double(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double[] ArrayDouble(int length, double offset = 0)
		{
			var array = new double[length];
			for (int i = 0; i < length; ++i) {
				array[i] = Double(offset);
			}

			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public decimal Decimal() => decimal.Parse(Next(), CultureInfo.InvariantCulture);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public decimal Decimal(decimal offset) => Decimal() + offset;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (decimal, decimal) Decimal2(decimal offset = 0)
			=> (Decimal(offset), Decimal(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (decimal, decimal, decimal) Decimal3(decimal offset = 0)
			=> (Decimal(offset), Decimal(offset), Decimal(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (decimal, decimal, decimal, decimal) Decimal4(decimal offset = 0)
			=> (Decimal(offset), Decimal(offset), Decimal(offset), Decimal(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (decimal, decimal, decimal, decimal, decimal) Decimal5(decimal offset = 0)
			=> (Decimal(offset), Decimal(offset), Decimal(offset), Decimal(offset), Decimal(offset));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public decimal[] ArrayDecimal(int length, decimal offset = 0)
		{
			var array = new decimal[length];
			for (int i = 0; i < length; ++i) {
				array[i] = Decimal(offset);
			}

			return array;
		}

		private byte Read()
		{
			if (isEof_) {
				throw new EndOfStreamException();
			}

			if (index_ >= length_) {
				index_ = 0;
				if ((length_ = stream_.Read(buf_, 0, BUFFER_SIZE)) <= 0) {
					isEof_ = true;
					return 0;
				}
			}

			return buf_[index_++];
		}

		public void Save(string text)
		{
			if (string.IsNullOrWhiteSpace(filePath_)) {
				return;
			}

			File.WriteAllText(filePath_ + "_output.txt", text);
		}
	}
}
