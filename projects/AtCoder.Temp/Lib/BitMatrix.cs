using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AtCoder.Temp.Lib
{
	public class BitMatrix
	{
		public static BitMatrix Identity(int n)
		{
			var identity = new BitMatrix(n, n);
			for (int i = 0; i < identity.internalN_; ++i) {
				identity.internalMatrix_[i, i] = 0x8040201008040201;
			}

			return identity;
		}

		public static BitMatrix Mul(BitMatrix a, BitMatrix b)
		{
			//bit and
			var c = new BitMatrix(a.N, b.M);
			for (int i = 0; i < a.internalM_; ++i) {
				for (int k = 0; k < b.internalM_; ++k) {
					for (int j = 0; j < b.internalN_; ++j) {
						c.internalMatrix_[i, j] |= BitMatrix.Mul(a.internalMatrix_[i, k], b.internalMatrix_[k, j]);
					}
				}
			}

			return c;
		}

		public static BitMatrix Pow(BitMatrix a, int k)
		{
			BitMatrix x = Identity(a.N);
			for (; k > 0; k >>= 1) {
				if ((k & 1) != 0) {
					x = Mul(x, a);
				}

				a = Mul(a, a);
			}

			return x;
		}

		private static ulong Mul(ulong a, ulong b)
		{
			// C[i][j] |= A[i][k] & B[k][j]
			ulong u = 0xff;
			ulong v = 0x101010101010101;
			ulong c = 0;
			for (; a != 0 && b != 0; a >>= 1, b >>= 8) {
				c |= (((a & v) * u) & ((b & u) * v));
			}

			return c;
		}

		private static ulong Transpose(ulong a)
		{
			ulong t = (a ^ (a >> 7)) & 0xaa00aa00aa00aa;
			a = a ^ t ^ (t << 7);
			t = (a ^ (a >> 14)) & 0xcccc0000cccc;
			a = a ^ t ^ (t << 14);
			t = (a ^ (a >> 28)) & 0xf0f0f0f0;
			a = a ^ t ^ (t << 28);
			return a;
		}

		private readonly int internalM_;
		private readonly int internalN_;
		private readonly ulong[,] internalMatrix_;

		public int M { get; }
		public int N { get; }

		public BitMatrix(int m, int n)
		{
			M = m;
			N = n;
			internalM_ = 1 + M / 8;
			internalN_ = 1 + N / 8;
			internalMatrix_ = new ulong[internalM_, internalN_];
		}

		public bool this[int i, int j]
		{
			get
			{
				return (internalMatrix_[i / 8, j / 8] & (1UL << (8 * (i % 8) + (j % 8)))) != 0;
			}

			set
			{
				if (value) {
					internalMatrix_[i / 8, j / 8] |= (1UL << (8 * (i % 8) + (j % 8)));
				} else {
					internalMatrix_[i / 8, j / 8] &= ~(1UL << (8 * (i % 8) + (j % 8)));
				}
			}
		}

		public void Add(BitMatrix b)
		{
			//bit or
			for (int i = 0; i < internalM_; ++i) {
				for (int j = 0; j < internalN_; ++j) {
					internalMatrix_[i, j] |= b.internalMatrix_[i, j];
				}
			}
		}

		public BitMatrix Transpose(BitMatrix a)
		{
			var b = new BitMatrix(a.M, a.N);
			for (int i = 0; i < a.internalM_; ++i) {
				for (int j = 0; j < a.internalN_; ++j) {
					b.internalMatrix_[j, i] = Transpose(a.internalMatrix_[i, j]);
				}
			}

			return b;
		}

		public void Print(TextWriter s)
		{
			var sb = new StringBuilder();
			for (int i = 0; i < M; ++i) {
				for (int j = 0; j < N; ++j) {
					sb.Append(this[i, j] == false ? 0 : 1);
				}

				sb.AppendLine();
			}

			s.Write(sb.ToString());
		}

	}
}
