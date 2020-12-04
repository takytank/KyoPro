using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AOJ
{
	class Program
	{
		static void Main()
		{
			var cin = new Scanner2();

#if DEBUG
			Console.ReadLine();
#endif
		}
	}

	public class HashMap<TKey, TValue> : Dictionary<TKey, TValue>
	{
		private readonly TValue default_;
		public HashMap(TValue defaultValue)
			: base()
		{
			default_ = defaultValue;
		}

		public HashMap(TValue defaultValue, int capacity)
			: base(capacity)
		{
			default_ = defaultValue;
		}

		new public TValue this[TKey key]
		{
			get
			{
				if (ContainsKey(key) == false) {
					base[key] = default_;
				}

				return base[key];
			}

			set { base[key] = value; }
		}
	}

	public static class Helper
	{
		public static void UpdateMin<T>(ref T target, T value) where T : IComparable<T>
			=> target = target.CompareTo(value) > 0 ? value : target;

		public static void UpdateMin<T>(ref T target, T value, Action<T> onUpdated) where T : IComparable<T>
		{
			if (target.CompareTo(value) > 0) {
				target = value;
				onUpdated(value);
			}
		}

		public static void UpdateMax<T>(ref T target, T value) where T : IComparable<T>
			=> target = target.CompareTo(value) < 0 ? value : target;
		public static void UpdateMax<T>(ref T target, T value, Action<T> onUpdated) where T : IComparable<T>
		{
			if (target.CompareTo(value) < 0) {
				target = value;
				onUpdated(value);
			}
		}

		public static T[] Array<T>(int n, Func<int, T> init)
			=> Enumerable.Range(0, n).Select(x => init(x)).ToArray();
		public static List<T> List<T>(int n, Func<int, T> init)
			=> Enumerable.Range(0, n).Select(x => init(x)).ToList();
		public static T[,] Array2<T>(int n, int m, T init)
		{
			var array = new T[n, m];
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < m; j++) {
					array[i, j] = init;
				}
			}

			return array;
		}

		public static T[,,] Array3<T>(int n1, int n2, int n3, T init)
		{
			var array = new T[n1, n2, n3];
			for (int i1 = 0; i1 < n1; i1++) {
				for (int i2 = 0; i2 < n2; i2++) {
					for (int i3 = 0; i3 < n3; i3++) {
						array[i1, i2, i3] = init;
					}
				}
			}

			return array;
		}

		private static readonly int[] delta4_ = { 1, 0, -1, 0, 1 };
		public static void DoAt4(int i, int j, int imax, int jmax, Action<int, int> action)
		{
			for (int n = 0; n < 4; n++) {
				int ii = i + delta4_[n];
				int jj = j + delta4_[n + 1];
				if ((uint)ii < (uint)imax && (uint)jj < (uint)jmax) {
					action(ii, jj);
				}
			}
		}
	}

	public class Scanner2
	{
		private readonly char[] delimiter_ = new char[] { ' ' };
		private readonly string filePath_;
		private readonly Func<string> reader_;
		private string[] buf_;
		private int index_;

		public Scanner2(string file = "")
		{
			if (string.IsNullOrWhiteSpace(file)) {
				reader_ = Console.ReadLine;
			} else {
				filePath_ = file;
				var fs = new StreamReader(file);
				reader_ = fs.ReadLine;
			}
			buf_ = new string[0];
			index_ = 0;
		}

		public string Next()
		{
			if (index_ < buf_.Length) {
				return buf_[index_++];
			}

			string st = reader_();
			while (st == "") {
				st = reader_();
			}

			buf_ = st.Split(delimiter_, StringSplitOptions.RemoveEmptyEntries);
			if (buf_.Length == 0) {
				return Next();
			}

			index_ = 0;
			return buf_[index_++];
		}

		public int Int() => int.Parse(Next());
		public long Long() => long.Parse(Next());
		public double Double() => double.Parse(Next());

		public int[] ArrayInt(int N, int add = 0)
		{
			int[] Array = new int[N];
			for (int i = 0; i < N; i++) {
				Array[i] = Int() + add;
			}
			return Array;
		}

		public long[] ArrayLong(int N, long add = 0)
		{
			long[] Array = new long[N];
			for (int i = 0; i < N; i++) {
				Array[i] = Long() + add;
			}
			return Array;
		}

		public double[] ArrayDouble(int N, double add = 0)
		{
			double[] Array = new double[N];
			for (int i = 0; i < N; i++) {
				Array[i] = Double() + add;
			}
			return Array;
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
