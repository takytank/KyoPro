using System;
using System.Collections.Generic;
using System.Text;

namespace AtCoder.Temp.Lib
{
	public class Pair<T, U> : IComparable<Pair<T, U>>
		where T : IComparable<T> 
		where U : IComparable<U>
	{
		public T F { get; set; }
		public U S { get; set; }
		public Pair(T f, U s) { F = f; S = s; }
		public int CompareTo(Pair<T, U> a) => F.CompareTo(a.F) != 0 ? F.CompareTo(a.F) : S.CompareTo(a.S);
		public override string ToString() => $"{F} {S}";
	}
}
