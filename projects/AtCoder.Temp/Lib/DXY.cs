using System;
using System.Collections.Generic;
using System.Text;

namespace AtCoder.Temp.Lib
{
	public struct DXY
	{
		public static double Length(DXY p, DXY q)
		{
			double x = p.X - q.X;
			double y = p.Y - q.Y;
			return Math.Sqrt(x * x + y * y);
		}

		public static bool IsOneLine(DXY a, DXY b, DXY c)
		{
			return a.Y * (b.X - c.X) + b.Y * (c.X - a.X) + c.Y * (a.X - b.X) == 0;
		}

		public static DXY Circumcenter(DXY A, DXY B, DXY C)
		{
			double px = B.X - A.X;
			double py = B.Y - A.Y;
			double qx = C.X - A.X;
			double qy = C.Y - A.Y;

			double x = A.X + (qy * (px * px + py * py) - py * (qx * qx + qy * qy)) / (px * qy - py * qx) / 2;
			double y = (py != 0)
				? (px * (A.X + B.X - x - x) + py * (A.Y + B.Y)) / py / 2
				: (qx * (A.X + C.X - x - x) + qy * (A.Y + C.Y)) / qy / 2;
			return new DXY(x, y);

			/*
			double a = Length(B, C);
			double b = Length(C, A);
			double c = Length(A, B);

			double a2 = a * a;
			double b2 = b * b;
			double c2 = c * c;

			double denominator = a2 * (b2 + c2 - a2) + b2 * (a2 + c2 - b2) + c2 * (a2 + b2 - c2);
			double x = a2 * (b2 + c2 - a2) * A.X + b2 * (a2 + c2 - b2) * B.X + c2 * (a2 + b2 - c2) * C.X
				/ denominator;
			double y = a2 * (b2 + c2 - a2) * A.Y + b2 * (a2 + c2 - b2) * B.Y + c2 * (a2 + b2 - c2) * C.Y
				/ denominator;

			return new DXY(x, y);
			*/
		}

		public static double RadiusOfCircumcircle(double a, double b, double c)
		{
			return a * b * c
				/ Math.Sqrt((a + b + c) * (-a + b + c) * (a - b + c) * (a + b - c));
		}

		public double X { get; set; }
		public double Y { get; set; }

		public DXY(double x, double y)
		{
			X = x;
			Y = y;
		}

		public override int GetHashCode()
		{
			var hashCode = 1861411795;
			hashCode = hashCode * -1521134295 + X.GetHashCode();
			hashCode = hashCode * -1521134295 + Y.GetHashCode();
			return hashCode;
		}
	}
}
