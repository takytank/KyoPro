using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{

	public class Scanner
	{
		private readonly char[] delimiter_ = new char[] { ' ' };
		private string[] buf_;
		private int index_;

		public Scanner()
		{
			buf_ = new string[0];
			index_ = 0;
		}

		public string Next()
		{
			if (index_ < buf_.Length) {
				return buf_[index_++];
			}

			string st = Console.ReadLine();
			while (st == "") {
				st = Console.ReadLine();
			}

			buf_ = st.Split(delimiter_, StringSplitOptions.RemoveEmptyEntries);
			if (buf_.Length == 0) {
				return Next();
			}

			index_ = 0;
			return buf_[index_++];
		}

		public int Int()
		{
			return int.Parse(Next());
		}

		public long Long()
		{
			return long.Parse(Next());
		}

		public double Double()
		{
			return double.Parse(Next());
		}

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

		public int Int()
		{
			return int.Parse(Next());
		}

		public long Long()
		{
			return long.Parse(Next());
		}

		public double Double()
		{
			return double.Parse(Next());
		}

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
