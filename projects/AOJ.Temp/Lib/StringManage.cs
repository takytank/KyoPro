using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public class StringComparator : IComparer<string>
	{
		int IComparer<string>.Compare(string a, string b)
		{
			int length = Math.Min(a.Length, b.Length);
			for (int i = 0; i < length; i++) {
				if (a[i] < b[i]) {
					return -1;
				} else if (a[i] > b[i]) {
					return 1;
				}
			}

			if (a.Length < b.Length) {
				return -1;
			}

			if (a.Length > b.Length) {
				return 1;
			}

			return 0;
		}
	}

	class StringManage
	{
		public static int[] ZAlogorithm(string s, int start)
		{
			int n = s.Length - start;
			int[] a = new int[n];
			int c = 0;
			for (int i = 1; i < n; i++) {
				if (i + a[i - c] < c + a[c]) {
					a[i] = a[i - c];
				} else {
					int j = Math.Max(0, c + a[c] - i);
					while (i + j < n && s[j + start] == s[i + j + start]) {
						++j;
					}
					a[i] = j;
					c = i;
				}
			}

			a[0] = n;

			return a;
		}

		public static int[] Temp(string s)
		{
			int n = s.Length;
			int[] a = new int[n];
			int c = 0;
			for (int i = 1; i < n; i++) {
				if (i + a[i - c] < c + a[c]) {
					a[i] = a[i - c];
				} else {
					int j = Math.Max(0, c + a[c] - i);
					while (i + j < n && s[j] == s[i + j]) {
						++j;
					}
					a[i] = j;
					c = i;
				}
			}

			return a;
		}
	}
}
