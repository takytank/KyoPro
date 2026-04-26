using TakyTank.KyoProLib.CSharp;
using TakyTank.KyoProLib.CSharp.V11;

namespace AtCoder13.Temp;

public class ContestPool
{
	public static void A()
	{
		using var io = new IOManager();
		var (a, b, c) = io.Int3();
		bool ok = a != b && b == c;
		io.YesNo(ok);
	}

	public static void B()
	{
		using var io = new IOManager();
		var (h, w) = io.Int2();
		var s = io.ArrayString(h);

		int ans = 0;
		for (int t = 0; t < h; t++) {
			for (int b = t + 1; b <= h; b++) {
				for (int l = 0; l < w; l++) {
					for (int r = l + 1; r <= w; r++) {
						bool ok = true;
						for (int i = t; i < b; i++) {
							for (int j = l; j < r; j++) {
								int ii = t + b - 1 - i;
								int jj = l + r - 1 - j;
								if (s[i][j] != s[ii][jj]) {
									ok = false;
								}
							}
						}

						if (ok) {
							++ans;
							//io.WriteLine($"{t} {b} {l} {r}");
						}
					}
				}
			}
		}

		io.WriteLine(ans);
	}

	public static void C()
	{
		using var io = new IOManager();
		var (n, k) = io.Int2();
		var a = io.ArrayLong(n);

		long sum = a.Sum();
		var sums = new HashMap<long, long>(x => 0);
		foreach (var aa in a) {
			sums[aa] += aa;
		}

		var vals = sums.Values.ToArray();
		Array.Sort(vals);
		Array.Reverse(vals);
		int kk = Math.Min(k, vals.Length);
		for (int i = 0; i < kk; i++) {
			sum -= vals[i];
		}

		io.WriteLine(sum);
	}

	public static void D()
	{
		using var io = new IOManager();
		var (n, q) = io.Int2();

		var prevs = new int[n];
		for (int i = 0; i < n; i++) {
			prevs[i] = i + n;
		}

		for (int i = 0; i < q; i++) {
			var (c, p) = io.Int2(-1);
			prevs[c] = p;
		}

		var inCounts = new int[n * 2];
		for (int i = 0; i < n; i++) {
			inCounts[prevs[i]] += 1;
		}

		var firsts = new List<int>();
		for (int i = 0; i < n; i++) {
			if (inCounts[i] == 0) {
				firsts.Add(i);
			}
		}

		var ans = new int[n];
		foreach (var f in firsts) {
			int v = f;
			int count = 0;
			while (v < n) {
				++count;
				v = prevs[v];
			}

			ans[v - n] = count;
		}

		io.Join(ans);
	}

	public static void E()
	{
		using var io = new IOManager();

	}

	public static void F()
	{
		using var io = new IOManager();
		var (n, q) = io.Int2();

		var seg = new LazySegmentTree<(ModInt value, ModInt sum), long>(
			n,
			(0, 0),
			0,
			(x, y) => (x.value + y.value, x.sum + y.sum + (x.value * y.value)),
			(x, f, n) => {
				ModInt mul = n;
				mul *= (n - 1);
				mul /= 2;
				var ff = new ModInt(f, true);
				return (x.value + (ff * n), x.sum + ((n - 1) * x.value * ff) + (mul * ff * ff));
			},
			(f, g) => (f + g) % ModInt.P);

		for (int i = 0; i < q; i++) {
			var (l, r, a) = io.Int3();
			--l;
			long aa = a % ModInt.P;
			seg.Update(l, r, aa);
			ModInt ans = seg.Query(l, r).sum;
			io.WriteLine(ans.ToLong());
		}
	}

	public static void G()
	{
		using var io = new IOManager();

	}

	public static void H()
	{
		using var io = new IOManager();

	}

	public static void I()
	{
		using var io = new IOManager();

	}

	public static void J()
	{
		using var io = new IOManager();

	}

	public static void K()
	{
		using var io = new IOManager();

	}

	public static void L()
	{
		using var io = new IOManager();

	}

	public static void M()
	{
		using var io = new IOManager();

	}

	public static void N()
	{
		using var io = new IOManager();

	}

	public static void O()
	{
		using var io = new IOManager();

	}

	public static void P()
	{
		using var io = new IOManager();

	}
}
