using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public struct Vector2
	{
		private const double EPS = 1e-10;

		private static double Add(double a, double b)
		{
			return Math.Abs(a + b) < EPS * (Math.Abs(a) + Math.Abs(b)) ? 0 : a + b;
		}

		/*
		public static bool IsOnSegment(Vector2 p1, Vector2 p2, Vector2 q)
			=> (p1 - q).Det(p2 - q) == 0 && (p1 - q).Dot(p2 - q) <= 0;
		public static Vector2 GetIntersection(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
			=> p1 + (p2 - p1) * ((q2 - q1).Det(q1 - p1) / (q2 - q1).Det(p2 - p1));
		public static bool IsCross2(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
		{
			var intersection = GetIntersection(p1, p2, q1, q2);
		0	return IsOnSegment(p1, p2, intersection) && IsOnSegment(q1, q2, intersection);
		}*/

		public static bool IsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
		{
			{
				var l = q1 - p1;
				var c = p2 - p1;
				var r = q2 - p1;

				double s1 = c.Det(l);
				double s2 = c.Det(r);
				if (s1 * s2 > 0) {
					return false;
				}
			}

			{
				var l = p1 - q1;
				var c = q2 - q1;
				var r = p2 - q1;

				double s1 = c.Det(l);
				double s2 = c.Det(r);
				if (s1 * s2 > 0) {
					return false;
				}
			}

			return true;
		}

		public double X { get; private set; }
		public double Y { get; private set; }

		public Vector2(double x, double y)
		{
			X = x;
			Y = y;
		}

		public static Vector2 operator +(Vector2 lhs, Vector2 rhs)
		{
			return new Vector2(Add(lhs.X, rhs.X), Add(lhs.Y, rhs.Y));
		}

		public static Vector2 operator -(Vector2 lhs, Vector2 rhs)
		{
			return new Vector2(Add(lhs.X, -rhs.X), Add(lhs.Y, -rhs.Y));
		}

		public static Vector2 operator *(Vector2 src, double value)
		{
			return new Vector2(src.X * value, src.Y * value);
		}

		public static Vector2 operator *(double value, Vector2 src)
		{
			return new Vector2(src.X * value, src.Y * value);
		}

		public double Dot(Vector2 target)
		{
			return Add(X * target.X, Y * target.Y);
		}

		public double Det(Vector2 target)
		{
			return Add(X * target.Y, -Y * target.X);
		}
	}
}
