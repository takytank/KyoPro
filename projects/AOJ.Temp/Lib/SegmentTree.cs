using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public class SegmentTree<T>
	{
		private readonly int n_;
		private readonly T unit_;
		private readonly T[] tree_;
		private readonly Func<T, T, T> merge_;

		public int Count { get; private set; }
		public T Top { get { return tree_[1]; } }

		public SegmentTree(int count, T unit, Func<T, T, T> merge)
		{
			merge_ = merge;
			unit_ = unit;

			Count = count;
			n_ = 1;
			while(n_ < count) {
				n_ <<= 1;
			}

			tree_ = new T[n_ << 1];
			for (int i = 0; i < tree_.Length; i++) {
				tree_[i] = unit;
			}
		}

		public SegmentTree(T[] src, T unit, Func<T, T, T> merge)
			: this(src.Length, unit, merge)
		{
			for (int i = 0; i < src.Length; ++i) {
				tree_[i + n_] = src[i];
			}

			for (int i = n_ - 1; i > 0; --i) {
				tree_[i] = merge_(tree_[i << 1], tree_[(i << 1) + 1]);
			}
		}

		public T this[int index]
		{
			get { return tree_[index + n_]; }
			set { Update(index, value); }
		}

		public void Update(int index, T val)
		{
			if (index >= Count) {
				return;
			}

			index += n_;
			tree_[index] = val;
			index >>= 1;
			while (index != 0) {
				tree_[index] = merge_(tree_[index << 1], tree_[(index << 1) + 1]);
				index >>= 1;
			}
		}

		public T Query(int left, int right)
		{
			if (left > right || right < 0 || left >= Count) {
				return unit_;
			}

			int l = left + n_;
			int r = right + n_;
			T valL = unit_;
			T valR = unit_;
			while (l < r) {
				if ((l & 1) != 0) {
					valL = merge_(valL, tree_[l]);
					++l;
				}
				if ((r & 1) != 0) {
					--r;
					valR = merge_(tree_[r], valR);
				}

				l >>= 1;
				r >>= 1;
			}

			return merge_(valL, valR);
		}
	}

	public class LazySegmentTree<T>
	{
		private readonly int n_;
		private readonly T unit_;
		private readonly T[] tree_;
		private readonly bool[] should_;
		private readonly Func<T, T, T> merge_;

		public int Count { get; private set; }

		public LazySegmentTree(int count, T unit, Func<T, T, T> merge)
		{
			merge_ = merge;
			unit_ = unit;

			Count = count;
			n_ = 1;
			while (n_ < count) {
				n_ <<= 1;
			}

			tree_ = new T[n_ << 1];
			should_ = new bool[n_ << 1];
			for (int i = 0; i < tree_.Length; i++) {
				tree_[i] = unit;
			}
		}

		public T this[int i]
		{
			get { return Query(i); }
		}

		public void Update(int left, int right, T item)
		{
			UpdateCore(left, right, 1, 0, n_, item);
		}

		public T Query(int i)
		{
			int l = 0;
			int r = n_;
			int k = 1;
			while (r - l > 1) {
				Evaluate(k, l, r);
				int m = (l + r) / 2;
				if (i < m) {
					r = m;
					k <<= 1;
				} else {
					l = m;
					k = (k << 1) + 1;
				}
			}

			return tree_[k];
		}

		private void Evaluate(int k, int l, int r)
		{
			if (should_[k]) {
				if (r - l > 1) {
					tree_[k << 1] = merge_(tree_[k << 1], tree_[k]);
					should_[k << 1] = true;
					tree_[(k << 1) + 1] = merge_(tree_[(k << 1) + 1], tree_[k]);
					should_[(k << 1) + 1] = true;
					tree_[k] = unit_;
				}

				should_[k] = false;
			}
		}

		private void UpdateCore(int left, int right, int k, int l, int r, T item)
		{
			Evaluate(k, l, r);
			if (r <= left || right <= l) {
				return;
			}

			if (left <= l && r <= right) {
				tree_[k] = merge_(tree_[k], item);
				should_[k] = true;
				Evaluate(k, l, r);
				return;
			}

			UpdateCore(left, right, k << 1, l, (l + r) / 2, item);
			UpdateCore(left, right, (k << 1) + 1, (l + r) / 2, r, item);
		}
	}
}
