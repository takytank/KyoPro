using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AOJ.Temp.Lib
{
	public class UnionFindTree
	{
		private readonly List<int> sizes_;
		private readonly List<int> parents_;

		public int Count { get; private set; }
		public int GroupCount { get; private set; }

		public UnionFindTree(int count)
		{
			parents_ = new List<int>(count);
			sizes_ = new List<int>(count);

			Count = 0;
			GroupCount = 0;

			ExtendSize(count);
		}

		public bool IsUnited(int x, int y)
		{
			int xRoot = Find(x);
			int yRoot = Find(y);

			return xRoot == yRoot;
		}

		public bool TryUnite(int x, int y)
		{
			int xRoot = Find(x);
			int yRoot = Find(y);
			if (yRoot == xRoot) {
				return false;
			}

			int parent = sizes_[xRoot] < sizes_[yRoot] ? yRoot : xRoot;
			int child = sizes_[xRoot] < sizes_[yRoot] ? xRoot : yRoot;

			GroupCount--;
			parents_[child] = parent;
			sizes_[parent] += sizes_[child];

			return true;
		}
		public IEnumerable<int> AllRepresents
		{
			get { return parents_.Where(x => x == parents_[x]); }
		}

		public int GetSize(int x)
		{
			return sizes_[Find(x)];
		}

		public int Find(int x)
		{
			while (x != parents_[x]) {
				parents_[x] = parents_[parents_[x]];
				x = parents_[x];
			}

			return x;
		}

		public void ExtendSize(int treeSize)
		{
			if (treeSize <= Count) {
				return;
			}

			parents_.Capacity = treeSize;
			sizes_.Capacity = treeSize;
			while (Count < treeSize) {
				parents_.Add(Count);
				sizes_.Add(1);
				Count++;
				GroupCount++;
			}
		}
	}
}
