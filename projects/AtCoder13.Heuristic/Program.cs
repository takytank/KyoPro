using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace AtCoder13.Heuristic;

public class Program
{
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	static void Main()
	{
		HeuristicHelper.RunCases(
			runCaseCount: 100,
			testCaseCount: 50,
			isParallel: false,
			i => Run(i),
			(locker, i, ret) => {
				var (input, info) = ret;
				lock (locker) {
					var status = new List<string> {
						$"{i:d4}:",
						$"N={input.N:d2}",
						$"M={input.M:d2}",
						$"T={input.T:d5}",
						$"loop={info.Loop:d8}",
						$"up={info.Up:d4}",
						$"P={info.P:d2}",
						$"ML={info.ML:d3}",
						$"RA={info.RA:d5}",
						$"A={info.A:d5}",
						$"score={info.Score:d5}",
						$"elapsed={info.Elapsed:d4}",
						$"M={info.Macro}",
					};

					Console.WriteLine(status.Join(" "));
					Console.Out.Flush();
				}

				return (info.Score, info.Loop, info.Up);
			},
			(tc, rc) => {
				//Console.WriteLine($"enabled count: {count}");
			});
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	static (Input input, Information info)
		Run(int caseNumber)
	{
		var (isLocal, sw, rnd) = HeuristicHelper.Initialize();
		using var io = HeuristicHelper.CreateIO(@$"D:\kyopro\AtCoder\AHC066\tools\in\", caseNumber);

		var input = new Input(io, isLocal);
		var info = new Information();

		var state3 = Plan3(isLocal, sw, rnd, input, info);
		var best = state3;
		info.P = 3;

		if (info.P != 5) {
			best = Plan2(isLocal, sw, rnd, input, info, best);
		}

		var state5 = Plan5(isLocal, sw, rnd, input, info);
		if (state5.Ops.Count < best.Ops.Count) {
			best = state5;
			info.P = 5;
		}

		input.Output(best);

		info.A = best.Ops.Count;
		info.Score = best.Calculate();
		info.Elapsed = sw.ElapsedMilliseconds;

		return (input, info);
	}

	static State Plan2(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info, State temp)
	{
		var ops = temp.Ops.ToArray();
		var bestOp = ops.ToArray();

		info.RA = ops.Length;
		int maxLen = Math.Min(10, ops.Length);
		for (int l = 2; l <= maxLen; l++) {
			for (int i = 0; i < ops.Length - l; i++) {
				var template = ops.AsSpan().Slice(i, l);
				var list = new List<int>();
				for (int j = 0; j < ops.Length - l; j++) {
					bool ok = true;
					for (int k = 0; k < template.Length; k++) {
						if (ops[j + k] != template[k]) {
							ok = false;
							break;
						}
					}

					if (ok) {
						list.Add(j);
						j += l - 1;
					}
				}

				int len = ops.Length - ((list.Count - 1) * (l - 1)) + 2;
				if (len < bestOp.Length) {
					var tempOp = new List<char>();
					int fj = list[0];
					int tj = -1;
					int tl = 0;

					for (int j = 0; j < ops.Length; j++) {
						if (j == fj) {
							tempOp.Add('M');
							tempOp.Add(ops[j]);
							if (tl < list.Count - 1) {
								++tl;
								tj = list[tl];
							}
						} else if (j == tj) {
							tempOp.Add('P');
							j += l - 1;
							if (tl < list.Count - 1) {
								++tl;
								tj = list[tl];
							}
						} else {
							tempOp.Add(ops[j]);
						}

						if (j == fj + l - 1) {
							tempOp.Add('M');
						}
					}

					info.ML = l;
					bestOp = tempOp.ToArray();
				}
			}
		}

		var state = new State(input, isLocal);
		foreach (var op in bestOp) {
			state.AddOp(op);
		}

		return state;
	}

	static State Plan3(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(input, isLocal);
		var order = OptimizeBallOrder(input, sw, rnd);
		foreach (int k in order) {
			var (ci, cj) = state.Robo;
			var (si, sj, ti, tj) = input.Balls[k];

			var ret = input.Map.Bfs(ci, cj, si, sj, state.Direction);
			foreach (var dir in ret) {
				if (state.Direction != dir) {
					var rot = state.Direction.Rotate(dir);
					foreach (var rr in rot) {
						state.Rotate(rr);
					}
				}

				state.Foward();
			}

			state.Exchange();

			ret = input.Map.Bfs(si, sj, ti, tj, state.Direction);
			foreach (var dir in ret) {
				if (state.Direction != dir) {
					var rot = state.Direction.Rotate(dir);
					foreach (var rr in rot) {
						state.Rotate(rr);
					}
				}

				state.Foward();
			}

			state.Exchange();
		}

		return state;
	}

	static State Plan5(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var order = OptimizeBallOrder(input, sw, rnd);
		// bestScore は純粋な経路ステップ数の和（overhead 除く）
		int bestScore = int.MaxValue;

		var macros = GenerateMacros();
		// 短いマクロ優先でソート
		macros = macros.OrderBy(x => x.Length).ThenBy(x => rnd.Next()).ToList();

		double timeLimit = 1900;

		// macroDest を遅延計算でキャッシュ
		var macroDestCache = new Dictionary<string, (int i, int j, Direction4 dir, bool ok)[,,]>();
		var dirs = new[] { Direction4.D, Direction4.L, Direction4.U, Direction4.R };

		var dist = new int[input.N, input.N, 4];
		var prev = new (int i, int j, Direction4 dir, char op)[input.N, input.N, 4];
		var que = new Queue<int>();
		// stamp 方式で visited クリアを O(1) に
		var stamp = new int[input.N, input.N, 4];
		int currentStamp = 0;
		// routeBuffer: 最大経路長として盤面サイズ×4方向×7(マクロ長)で十分な大きさ
		var routeBuffer = new char[input.N * input.N * 4 * 8];
		// dist を初期化
		MemoryMarshal.CreateSpan(ref dist[0, 0, 0], dist.Length).Fill(int.MaxValue);

		// 経路バッファ: ボールごとに pick/drop の経路を保存
		var pickPaths = new char[input.M][];
		var dropPaths = new char[input.M][];
		// 確定した最良経路を保存
		var bestPickPaths = new char[input.M][];
		var bestDropPaths = new char[input.M][];
		string bestMacro = null;

		// 全マクロを1周だけ試す
		var allMacros = macros.ToList();

		foreach (var macro in allMacros) {
			if (sw.Elapsed.TotalMilliseconds > timeLimit) break;
			++info.Loop;

			// macroDest を遅延計算
			if (!macroDestCache.TryGetValue(macro, out var macroDest)) {
				macroDest = new (int i, int j, Direction4 dir, bool ok)[input.N, input.N, 4];
				for (int i = 0; i < input.N; i++) {
					for (int j = 0; j < input.N; j++) {
						foreach (var dir in dirs) {
							int nr = i, nc = j;
							Direction4 nd = dir;
							bool mOk = true;
							foreach (var op in macro) {
								if (op == 'F') {
									if (!input.Map.CanMove(nr, nc, nd)) { mOk = false; break; }
									var moved = nd.Move(nr, nc);
									nr = moved.i; nc = moved.j;
								} else if (op == 'L') {
									nd = nd.Rotate(Rotation.L);
								} else if (op == 'R') {
									nd = nd.Rotate(Rotation.R);
								}
							}
							macroDest[i, j, dir.ToIndex4()] = (nr, nc, nd, mOk);
						}
					}
				}
				macroDestCache[macro] = macroDest;
			}

			// State を使って正確に評価
			var state = new State(input, isLocal);
			state.AddOp('M');
			foreach (var c in macro) state.AddOp(c);
			state.AddOp('M');

			int currentScore = 0;
			bool ok = true;

			foreach (int k in order) {
				var (si, sj, ti, tj) = input.Balls[k];

				var pPick = MacroBfs(input, input.Map, state.Robo.i, state.Robo.j, state.Direction, si, sj, macroDest, dist, prev, que, stamp, ref currentStamp, bestScore - currentScore, routeBuffer);
				if (pPick == null) { ok = false; break; }
				currentScore += pPick.Length;
				if (currentScore >= bestScore) { ok = false; break; }
				foreach (var op in pPick) state.AddOp(op);
				pickPaths[k] = pPick;
				state.Exchange();

				var pDrop = MacroBfs(input, input.Map, state.Robo.i, state.Robo.j, state.Direction, ti, tj, macroDest, dist, prev, que, stamp, ref currentStamp, bestScore - currentScore, routeBuffer);
				if (pDrop == null) { ok = false; break; }
				currentScore += pDrop.Length;
				if (currentScore >= bestScore) { ok = false; break; }
				foreach (var op in pDrop) state.AddOp(op);
				dropPaths[k] = pDrop;
				state.Exchange();
			}

			if (ok && currentScore < bestScore) {
				bestScore = currentScore;
				for (int k = 0; k < input.M; k++) {
					bestPickPaths[k] = (char[])pickPaths[k].Clone();
					bestDropPaths[k] = (char[])dropPaths[k].Clone();
				}
				bestMacro = macro;
				++info.Up;
			}
		}

		// 最良マクロで State を1回だけ構築
		if (bestMacro != null) {
			var state = new State(input, isLocal);
			state.AddOp('M');
			foreach (var c in bestMacro) state.AddOp(c);
			state.AddOp('M');
			foreach (int k in order) {
				ApplyPath(state, bestPickPaths[k]);
				state.Exchange();
				ApplyPath(state, bestDropPaths[k]);
				state.Exchange();
			}
			info.ML = bestMacro.Length;
			info.Macro = bestMacro;
			return state;
		}

		return Plan3(isLocal, sw, rnd, input, info);
	}

	// 軽量シミュレート
	// 位置・向きを軽量シミュレート（State を作らない版）
	static void SimulateDir(char op, ref Direction4 roboDir, (int i, int j, Direction4 dir, bool ok)[,,] macroDest, ref int roboI, ref int roboJ, Input input)
	{
		switch (op) {
			case 'F':
				if (input.Map.CanMove(roboI, roboJ, roboDir)) {
					var moved = roboDir.Move(roboI, roboJ);
					roboI = moved.i; roboJ = moved.j;
				}
				break;
			case 'L':
				roboDir = roboDir.Rotate(Rotation.L);
				break;
			case 'R':
				roboDir = roboDir.Rotate(Rotation.R);
				break;
			case 'P':
				var dest = macroDest[roboI, roboJ, roboDir.ToIndex4()];
				if (dest.ok) { roboI = dest.i; roboJ = dest.j; roboDir = dest.dir; }
				break;
		}
	}

	static void ApplyPath(State state, char[] path)
	{
		foreach (var op in path) {
			state.AddOp(op);
		}
	}

	static List<string> GenerateMacros()
	{
		var list = new List<string>();
		var ops = new[] { 'F', 'L', 'R' };
		for (int len = 2; len <= 7; len++) {
			GenerateMacrosDfs(list, "", len, ops);
		}
		return list;
	}

	static void GenerateMacrosDfs(List<string> list, string current, int targetLen, char[] ops)
	{
		if (current.Length == targetLen) {
			// 一切進まないマクロ(Fが含まれない)は採用しない
			if (current.Contains('F')) {
				list.Add(current);
			}
			return;
		}
		foreach (var op in ops) {
			GenerateMacrosDfs(list, current + op, targetLen, ops);
		}
	}

	static char[] MacroBfs(Input input, Maze map, int si, int sj, Direction4 sdir, int ti, int tj, (int i, int j, Direction4 dir, bool ok)[,,] macroDest, int[,,] dist, (int i, int j, Direction4 dir, char op)[,,] prev, Queue<int> que, int[,,] stamp, ref int currentStamp, int bestScoreSoFar, char[] routeBuffer)
	{
		if (si == ti && sj == tj) return [];

		int N = input.N;
		// stamp をインクリメントすることで O(1) で dist をリセット
		++currentStamp;
		int cs = currentStamp;

		que.Clear();

		int sIdx = sdir.ToIndex4();
		if (sIdx >= 4) {
			sIdx = 0;
			sdir = sIdx.FromIndex4();
		}
		dist[si, sj, sIdx] = 0;
		stamp[si, sj, sIdx] = cs;
		que.Enqueue((si << 8) | (sj << 2) | sIdx);

		int goalDist = int.MaxValue;
		Direction4 goalDir = Direction4.N;

		// P(マクロ実行) および F, L, R のコストは全て 1 とみなして BFS を行う
		while (que.Count > 0) {
			int cVal = que.Dequeue();
			int ci = cVal >> 8;
			int cj = (cVal >> 2) & 0x3F;
			int cIdx = cVal & 3;
			Direction4 cdir = cIdx.FromIndex4();
			int d = dist[ci, cj, cIdx];

			// 枝刈り: すでに現在のベストを超えている場合は中断
			if (d > bestScoreSoFar) break;

			if (ci == ti && cj == tj) {
				goalDist = d;
				goalDir = cdir;
				break;
			}

			void Push(int ni, int nj, Direction4 ndir, char op)
			{
				int nIdx = ndir.ToIndex4();
				if (nIdx >= 4) return;
				if (stamp[ni, nj, nIdx] != cs || dist[ni, nj, nIdx] > d + 1) {
					dist[ni, nj, nIdx] = d + 1;
					stamp[ni, nj, nIdx] = cs;
					prev[ni, nj, nIdx] = (ci, cj, cdir, op);
					que.Enqueue((ni << 8) | (nj << 2) | nIdx);
				}
			}

			// F (前進)
			if (map.CanMove(ci, cj, cdir)) {
				var nPos = cdir.Move(ci, cj);
				Push(nPos.i, nPos.j, cdir, 'F');
			}
			// L (左回転)
			Push(ci, cj, cdir.Rotate(Rotation.L), 'L');
			// R (右回転)
			Push(ci, cj, cdir.Rotate(Rotation.R), 'R');

			// P (マクロ実行)
			var dest = macroDest[ci, cj, cIdx];
			if (dest.ok) {
				Push(dest.i, dest.j, dest.dir, 'P');
			}
		}

		if (goalDist == int.MaxValue) return null;

		int routeLen = 0;
		int currI = ti, currJ = tj;
		Direction4 currDir = goalDir;
		while (currI != si || currJ != sj || currDir != sdir) {
			int cIdx = currDir.ToIndex4();
			if (cIdx >= 4) break;
			var p = prev[currI, currJ, cIdx];
			routeBuffer[routeLen++] = p.op;
			currI = p.i;
			currJ = p.j;
			currDir = p.dir;
		}
		Array.Reverse(routeBuffer, 0, routeLen);
		return routeBuffer[..routeLen];
	}

	static int[] OptimizeBallOrder(Input input, Stopwatch sw, Random rnd)
	{
		int N = input.N;
		int M = input.M;
		int[,] dist = new int[N * N, N * N];

		// 距離行列の初期化
		for (int i = 0; i < N * N; i++) {
			for (int j = 0; j < N * N; j++) {
				dist[i, j] = 999999;
			}
		}

		// 事前にすべてのマス間の最短距離を BFS で計算しておく (最大で 400 マスなので一瞬で終わる)
		for (int i = 0; i < N; i++) {
			for (int j = 0; j < N; j++) {
				int u = i * N + j;
				dist[u, u] = 0;
				var (pos, _) = input.Map.Bfs(i, j, int.MaxValue);
				foreach (var p in pos) {
					dist[u, p.i * N + p.j] = p.d;
				}
			}
		}

		// 貪欲法（一番近いボールを取りに行く）による初期順列の作成
		int[] bestOrder = new int[M];
		bool[] used = new bool[M];
		int currLoc = 0; // 開始位置 (0, 0) は 0
		for (int i = 0; i < M; i++) {
			int bestK = -1;
			int minD = int.MaxValue;
			for (int k = 0; k < M; k++) {
				if (!used[k]) {
					int start = input.Balls[k].b * N + input.Balls[k].c;
					int d = dist[currLoc, start];
					if (d < minD) {
						minD = d;
						bestK = k;
					}
				}
			}
			bestOrder[i] = bestK;
			used[bestK] = true;
			currLoc = input.Balls[bestK].d * N + input.Balls[bestK].e; // 次の現在地は今拾う操作のゴール
		}

		// コスト計算用関数（TSP的な距離）
		long CalcCost(int[] order)
		{
			long cost = 0;
			int loc = 0;
			for (int i = 0; i < M; i++) {
				int k = order[i];
				int start = input.Balls[k].b * N + input.Balls[k].c;
				int goal = input.Balls[k].d * N + input.Balls[k].e;
				cost += dist[loc, start] + dist[start, goal];
				loc = goal;
			}
			return cost;
		}

		int[] currentOrder = (int[])bestOrder.Clone();
		long bestScore = CalcCost(bestOrder);
		long currentScore = bestScore;

		// 2-opt近傍を用いた山登り法
		double startMs = sw.Elapsed.TotalMilliseconds;
		double timeLimit = 200.0;
		int iter = 0;

		while (true) {
			if ((iter & 0xFF) == 0 && sw.Elapsed.TotalMilliseconds - startMs > timeLimit) break;
			iter++;

			int p1 = rnd.Next(M);
			int p2 = rnd.Next(M);
			if (p1 == p2) continue;
			if (p1 > p2) (p1, p2) = (p2, p1);

			// 区間を反転させる (2-opt)
			Array.Reverse(currentOrder, p1, p2 - p1 + 1);
			long newScore = CalcCost(currentOrder);

			// 改善した、または同じスコアなら採用
			if (newScore <= currentScore) {
				currentScore = newScore;
				if (newScore < bestScore) {
					bestScore = newScore;
					Array.Copy(currentOrder, bestOrder, M);
				}
			} else {
				// 改悪した場合は区間を反転して元に戻す
				Array.Reverse(currentOrder, p1, p2 - p1 + 1);
			}
		}

		return bestOrder;
	}
}

public class State
{
	private readonly Input _input;
	private readonly bool _isDebug;

	public List<char> Ops { get; } = [];

	public (int i, int j) Robo { get; private set; }
	public Direction4 Direction { get; private set; }
	public (int i, int j)[] Balls { get; } = [];
	public int TakeBall { get; private set; } = -1;

	public bool IsMacro { get; private set; } = false;
	public char[] Macro { get; private set; } = [];
	public List<char> NextMacro { get; } = [];

	public State(Input input, bool isDebug)
	{
		_input = input;
		_isDebug = isDebug;

		Robo = (0, 0);
		Direction = Direction4.R;
		Balls = new (int i, int j)[_input.M];
		for (int k = 0; k < _input.M; k++) {
			Balls[k] = (_input.Balls[k].b, _input.Balls[k].c);
		}
	}

	public void Foward() => AddOp('F');
	public void Exchange() => AddOp('S');

	public void Rotate(Rotation rot)
	{
		switch (rot) {
			case Rotation.N:
				break;

			case Rotation.L:
				AddOp('L');
				break;

			case Rotation.R:
				AddOp('R');
				break;

			default:
				break;
		}
	}

	public void AddOp(char op, bool macroNow = false)
	{
		switch (op) {
			case 'F':
				if (_input.Map.CanMove(Robo.i, Robo.j, Direction)) {
					Robo = Direction.Move(Robo.i, Robo.j);
				}
				break;

			case 'R':
				Direction = Direction.Rotate(Rotation.R);
				break;

			case 'L':
				Direction = Direction.Rotate(Rotation.L);
				break;

			case 'S':
				int take = TakeBall;
				TakeBall = -1;
				for (int k = 0; k < _input.M; k++) {
					var (i, j) = Balls[k];
					if (i == Robo.i && j == Robo.j) {
						TakeBall = k;
						Balls[k] = (-1, -1);
						break;
					}
				}

				if (take >= 0) {
					Balls[take] = (Robo.i, Robo.j);
				}

				break;

			case 'M':
				if (IsMacro) {
					Macro = NextMacro.ToArray();
				} else {
					NextMacro.Clear();
				}

				IsMacro = !IsMacro;
				break;

			case 'P':
				foreach (var mac in Macro) {
					AddOp(mac, macroNow: true);
				}
				break;

			default:
				break;
		}

		if (IsMacro && op != 'M' && op != 'P') {
			NextMacro.Add(op);
		}

		if (macroNow == false) {
			Ops.Add(op);
		}
	}

	public long Calculate()
	{
		int a = Ops.Count;
		int v = 0;
		for (int k = 0; k < _input.M; k++) {
			var (bi, bj) = Balls[k];
			if (bi == _input.Balls[k].d && bj == _input.Balls[k].e) {
				++v;
			}
		}

		long score;
		if (v == _input.M) {
			score = a;
		} else {
			score = (long)_input.T * (_input.M - v);
		}

		return score;
	}
}

public class Information
{
	public long Time { get; set; } = 0;
	public long Score { get; set; } = 0;
	public int Loop { get; set; } = 0;
	public int Up { get; set; } = 0;
	public long Elapsed { get; set; } = 0;

	public int P { get; set; } = 0;

	public string Macro { get; set; } = "";
	public int ML { get; set; } = 0;
	public int RA { get; set; } = 0;
	public int A { get; set; } = 0;
}

public class Input
{
	private readonly IOManager _io;
	private readonly bool _isDebug;

	public int N { get; }
	public int M { get; }
	public int T { get; }
	public string[] V { get; }
	public string[] H { get; }
	public (int b, int c, int d, int e)[] Balls { get; }

	public Maze Map { get; }
	public Input(IOManager io, bool isDebug)
	{
		_io = io;
		_isDebug = isDebug;

		(N, M, T) = io.Int3();
		V = io.ArrayString(N);
		H = io.ArrayString(N - 1);
		Balls = io.Repeat<(int b, int c, int d, int e)>(M, io => io.Int4());

		Map = new Maze(N, N, V, H);
	}

	public void Output(State state)
	{
		int count = 0;
		foreach (var op in state.Ops) {
			_io.WriteLine(op);

			++count;
			if (count == T) {
				break;
			}
		}
	}
}

public class Maze
{
	/// <summary>BFSなどをする時に使う配列</summary>
	/// <remarks>
	/// 領域を使い回すためにフィールドにする。
	/// </remarks>
	private int[,] _distances = null;

	/// <summary>迷路の水平方向のマス数</summary>
	public int Width { get; private set; }
	/// <summary>迷路の垂直方向のマス数</summary>
	public int Height { get; private set; }
	/// <summary>各マスから動ける方向の一覧</summary>
	public Direction4[,][] Moveables { get; private set; }
	/// <summary>各マスから指定方向に動けるか</summary>
	public bool[,][] CanMoves { get; private set; }
	public bool CanMove(int i, int j, Direction4 dir) => CanMoves[i, j][dir.ToIndex4()];
	/// <summary>各マスから移動可能な隣接4マスの座標情報</summary>
	public (int i, int j)[,][] Adjacence4 { get; private set; }

	/// <summary>
	/// 指定した壁の情報で迷路を作成
	/// </summary>
	/// <param name="width">迷路の水平方向のマス数</param>
	/// <param name="height">迷路の垂直方向のマス数</param>
	/// <param name="verticalWalls">
	/// 垂直な壁情報。
	/// つまり、水平方向に移動可能かどうかを表す[height, width-1]個の情報。
	/// 対応するサイズの文字列配列として与えられ、
	/// 0の場合に壁が無いことを、1の場合に壁があることを表す。
	/// </param>
	/// <param name="horizontalWalls">
	/// 水平な壁情報。
	/// つまり、垂直方向に移動可能かどうかを表す[height-1, width]個の情報。
	/// あとは垂直と同じ。
	/// </param>
	/// <param name="canStay">
	/// true: 動かないということが可能。CanMoveに Dir.N が追加される。
	/// </param>
	public Maze(int width, int height, string[] verticalWalls, string[] horizontalWalls, bool canStay = false)
	{
		var tempVerticals = new bool[height, width - 1];
		for (int i = 0; i < height; i++) {
			for (int j = 0; j < width - 1; j++) {
				tempVerticals[i, j] = verticalWalls[i][j] == '1';
			}
		}

		var tempHorizontals = new bool[height - 1, width];
		for (int i = 0; i < height - 1; i++) {
			for (int j = 0; j < width; j++) {
				tempHorizontals[i, j] = horizontalWalls[i][j] == '1';
			}
		}

		InitializeCore(width, height, tempVerticals, tempHorizontals, canStay);
	}

	/// <summary>
	/// 指定した壁の情報で迷路を作成
	/// </summary>
	/// <param name="width">迷路の水平方向のマス数</param>
	/// <param name="height">迷路の垂直方向のマス数</param>
	/// <param name="verticalWalls">
	/// 垂直な壁情報。
	/// つまり、水平方向に移動可能かどうかを表す[height, width-1]個の情報。
	/// trueの場合に壁がある。
	/// </param>
	/// <param name="horizontalWalls">
	/// 水平な壁情報。
	/// つまり、垂直方向に移動可能かどうかを表す[height-1, width]個の情報。
	/// trueの場合に壁がある。
	/// </param>
	/// <param name="canStay">
	/// true: 動かないということが可能。CanMoveに Dir.N が追加される。
	/// </param>
	public Maze(int width, int height, bool[,] verticalWalls, bool[,] horizontalWalls, bool canStay = false)
		=> InitializeCore(width, height, verticalWalls, horizontalWalls, canStay);

	private void InitializeCore(int width, int height, bool[,] verticalWalls, bool[,] horizontalWalls, bool canStay = false)
	{
		Width = width;
		Height = height;
		_distances = new int[Height, Width];

		Moveables = new Direction4[Height, Width][];
		CanMoves = new bool[Height, Width][];
		for (int i = 0; i < Height; i++) {
			for (int j = 0; j < Width; j++) {
				var moves = new List<Direction4>();
				if (canStay) {
					moves.Add(Direction4.N);
				}

				if (i > 0 && horizontalWalls[i - 1, j] == false) {
					moves.Add(Direction4.U);
				}

				if (i < Height - 1 && horizontalWalls[i, j] == false) {
					moves.Add(Direction4.D);
				}

				if (j > 0 && verticalWalls[i, j - 1] == false) {
					moves.Add(Direction4.L);
				}

				if (j < Width - 1 && verticalWalls[i, j] == false) {
					moves.Add(Direction4.R);
				}

				Moveables[i, j] = moves.ToArray();

				CanMoves[i, j] = new bool[5];
				foreach (var d in moves) {
					CanMoves[i, j][d.ToIndex4()] = true;
				}
			}
		}

		Adjacence4 = new (int i, int j)[Height, Width][];
		for (int i = 0; i < Height; i++) {
			for (int j = 0; j < Width; j++) {
				var temp = new List<(int i, int j)>();
				foreach (var dir in Moveables[i, j]) {
					if (dir == Direction4.N) {
						continue;
					}

					temp.Add(dir.Move(i, j));
				}

				Adjacence4[i, j] = temp.ToArray();
			}
		}
	}

	/// <summary>
	/// 指定した位置から距離length以内のマスをBFSで列挙
	/// </summary>
	/// <param name="si">探索開始位置Y座標</param>
	/// <param name="sj">探索開始位置X座標</param>
	/// <param name="length">探索距離</param>
	/// <returns>
	/// positions -> 距離length以内のマスの集合
	/// trace -> 経路復元のためのオブジェクト
	/// </returns>
	public ((int i, int j, int d, int id)[] positions, Trace<Direction4> trace) Bfs(int si, int sj, int length)
	{
		MemoryMarshal.CreateSpan(ref _distances[0, 0], _distances.Length).Fill(int.MaxValue);

		var que = new Queue<(int i, int j, int id)>();
		var ret = new List<(int i, int j, int d, int id)>();
		var trace = new Trace<Direction4>();
		que.Enqueue((si, sj, 0));
		_distances[si, sj] = 0;

		while (que.Count > 0) {
			var (i, j, id) = que.Dequeue();
			int nd = _distances[i, j] + 1;
			if (nd > length) {
				continue;
			}

			ret.Add((i, j, _distances[i, j], id));

			foreach (var dir in Moveables[i, j]) {
				var (ni, nj) = dir.Move(i, j);
				if (_distances[ni, nj] != int.MaxValue) {
					continue;
				}

				_distances[ni, nj] = nd;
				int nextID = trace.Add(dir, id);
				que.Enqueue((ni, nj, nextID));
			}
		}

		return (ret.ToArray(), trace);
	}

	/// <summary>
	/// 指定した位置から条件に合うマスを1つ見つけるまでBFS
	/// </summary>
	/// <param name="si">探索開始位置Y座標</param>
	/// <param name="sj">探索開始位置X座標</param>
	/// <param name="moveables">移動可能な方向</param>
	/// <param name="needs">条件に合うマスのときにtrueを返すデリゲート</param>
	/// <returns>
	/// positions -> 探索したマスの集合
	/// trace -> 経路復元のためのオブジェクト
	/// found -> true:条件に合うマスを見つけた
	/// </returns>
	public ((int i, int j, int d, int id)[] positions, Trace<Direction4> trace, bool found)
		Bfs(
		int si, int sj, Direction4[] moveables, Predicate<(int i, int j)> needs)
	{
		MemoryMarshal.CreateSpan(ref _distances[0, 0], _distances.Length).Fill(int.MaxValue);

		var que = new Queue<(int i, int j, int id)>();
		var ret = new List<(int i, int j, int d, int id)>();
		var trace = new Trace<Direction4>();
		que.Enqueue((si, sj, 0));
		_distances[si, sj] = 0;

		while (que.Count > 0) {
			var (i, j, id) = que.Dequeue();
			int nd = _distances[i, j] + 1;

			if (needs.Invoke((i, j))) {
				ret.Add((i, j, _distances[i, j], id));
				return (ret.ToArray(), trace, true);
			}

			foreach (var dir in moveables) {
				if (CanMove(i, j, dir) == false) {
					continue;
				}

				var (ni, nj) = dir.Move(i, j);
				if (_distances[ni, nj] != int.MaxValue) {
					continue;
				}

				_distances[ni, nj] = nd;
				int nextID = trace.Add(dir, id);
				que.Enqueue((ni, nj, nextID));
			}
		}

		return (ret.ToArray(), trace, false);
	}

	/// <summary>
	/// 指定したスタート位置から目標位置までの最短経路をBFSで探索し、移動方向を返す
	/// </summary>
	/// <param name="si">探索開始位置Y座標</param>
	/// <param name="sj">探索開始位置X座標</param>
	/// <param name="ti">目標位置Y座標</param>
	/// <param name="tj">目標位置X座標</param>
	/// <returns>最短経路の移動方向の配列。到達不可能な場合は空配列を返す。</returns>
	public Direction4[] Bfs(int si, int sj, int ti, int tj)
	{
		if (si == ti && sj == tj) {
			return [];
		}

		MemoryMarshal.CreateSpan(ref _distances[0, 0], _distances.Length).Fill(int.MaxValue);

		var que = new Queue<(int i, int j, int id)>();
		var trace = new Trace<Direction4>();
		que.Enqueue((si, sj, 0));
		_distances[si, sj] = 0;

		int goalId = -1;

		while (que.Count > 0) {
			var (i, j, id) = que.Dequeue();
			int nd = _distances[i, j] + 1;

			foreach (var dir in Moveables[i, j]) {
				var (ni, nj) = dir.Move(i, j);
				if (_distances[ni, nj] != int.MaxValue) {
					continue;
				}

				_distances[ni, nj] = nd;
				int nextID = trace.Add(dir, id);

				if (ni == ti && nj == tj) {
					goalId = nextID;
					break;
				}

				que.Enqueue((ni, nj, nextID));
			}

			if (goalId != -1) {
				break;
			}
		}

		if (goalId == -1) {
			return [];
		}

		return trace.GetRouteTo(goalId).ToArray();
	}

	/// <summary>
	/// 指定したスタート位置から目標位置までの最短経路を、向きの変更を考慮してBFSで探索し、移動方向を返す
	/// </summary>
	/// <param name="si">探索開始位置Y座標</param>
	/// <param name="sj">探索開始位置X座標</param>
	/// <param name="ti">目標位置Y座標</param>
	/// <param name="tj">目標位置X座標</param>
	/// <param name="startDir">開始時の向き</param>
	/// <returns>最短経路の移動方向の配列。到達不可能な場合は空配列を返す。</returns>
	public Direction4[] Bfs(int si, int sj, int ti, int tj, Direction4 startDir)
	{
		if (si == ti && sj == tj) {
			return [];
		}

		var distances = new int[Height, Width, 5];
		if (distances.Length > 0) {
			MemoryMarshal.CreateSpan(ref distances[0, 0, 0], distances.Length).Fill(int.MaxValue);
		}

		var que = new Queue<(int i, int j, Direction4 dir, int id)>();
		var trace = new Trace<Direction4>();

		int sIdx = startDir.ToIndex4();
		que.Enqueue((si, sj, startDir, 0));
		distances[si, sj, sIdx] = 0;

		int goalId = -1;

		while (que.Count > 0) {
			var (i, j, dir, id) = que.Dequeue();
			int nd = distances[i, j, dir.ToIndex4()] + 1;

			if (CanMove(i, j, dir)) {
				var (ni, nj) = dir.Move(i, j);
				if (distances[ni, nj, dir.ToIndex4()] == int.MaxValue) {
					distances[ni, nj, dir.ToIndex4()] = nd;
					int nextID = trace.Add(dir, id);
					if (ni == ti && nj == tj) {
						goalId = nextID;
						break;
					}
					que.Enqueue((ni, nj, dir, nextID));
				}
			}

			var rotL = dir.Rotate(Rotation.L);
			if (distances[i, j, rotL.ToIndex4()] == int.MaxValue) {
				distances[i, j, rotL.ToIndex4()] = nd;
				int nextID = trace.Add(Direction4.N, id);
				que.Enqueue((i, j, rotL, nextID));
			}

			var rotR = dir.Rotate(Rotation.R);
			if (distances[i, j, rotR.ToIndex4()] == int.MaxValue) {
				distances[i, j, rotR.ToIndex4()] = nd;
				int nextID = trace.Add(Direction4.N, id);
				que.Enqueue((i, j, rotR, nextID));
			}
		}

		if (goalId == -1) {
			return [];
		}

		var route = trace.GetRouteTo(goalId);
		var result = new List<Direction4>();
		while (route.Count > 0) {
			var r = route.Pop();
			if (r != Direction4.N) {
				result.Add(r);
			}
		}

		return [.. result];
	}
}

[Flags]
public enum Direction4
{
	N = 0,
	D = 0x01,
	L = 0x02,
	U = 0x04,
	R = 0x08,
}

public static class Direction4Extensions
{
	private static readonly int[] _delta4 = [1, 0, -1, 0, 1];
	public static readonly Direction4[] _delta4Dir = [Direction4.D, Direction4.L, Direction4.U, Direction4.R];

	public static char ToSymbol(this Direction4 dir)
	{
		return dir switch {
			Direction4.N => 'S',
			Direction4.D => 'D',
			Direction4.L => 'L',
			Direction4.U => 'U',
			Direction4.R => 'R',
			_ => '.',
		};
	}

	public static int ToIndex4(this Direction4 dir)
	{
		return dir switch {
			Direction4.N => 4,
			Direction4.D => 0,
			Direction4.L => 1,
			Direction4.U => 2,
			Direction4.R => 3,
			_ => 0,
		};
	}

	public static Direction4 FromIndex4(this int index)
	{
		return index switch {
			0 => Direction4.D,
			1 => Direction4.L,
			2 => Direction4.U,
			3 => Direction4.R,
			_ => Direction4.N,
		};
	}

	public static (int i, int j) Move(this Direction4 dir, int i, int j)
	{
		return dir switch {
			Direction4.N => (i, j),
			Direction4.D => (i + 1, j),
			Direction4.L => (i, j - 1),
			Direction4.U => (i - 1, j),
			Direction4.R => (i, j + 1),
			_ => (i, j),
		};
	}

	public static (int i, int j) Move(this Direction4 dir, int i, int j, int d)
	{
		return dir switch {
			Direction4.N => (i, j),
			Direction4.D => (i + d, j),
			Direction4.L => (i, j - d),
			Direction4.U => (i - d, j),
			Direction4.R => (i, j + d),
			_ => (i, j),
		};
	}

	public static Direction4 Reverse(this Direction4 dir)
	{
		return dir switch {
			Direction4.N => Direction4.N,
			Direction4.D => Direction4.U,
			Direction4.L => Direction4.R,
			Direction4.U => Direction4.D,
			Direction4.R => Direction4.L,
			_ => Direction4.N,
		};
	}

	public static Direction4 Rotate(this Direction4 dir, Rotation rot)
	{
		if (rot == Rotation.N) {
			return dir;
		}

		return dir switch {
			Direction4.N => Direction4.N,
			Direction4.D => rot == Rotation.L ? Direction4.R : Direction4.L,
			Direction4.L => rot == Rotation.L ? Direction4.D : Direction4.U,
			Direction4.U => rot == Rotation.L ? Direction4.L : Direction4.R,
			Direction4.R => rot == Rotation.L ? Direction4.U : Direction4.D,
			_ => Direction4.N,
		};
	}

	public static Rotation[] Rotate(this Direction4 src, Direction4 dst)
	{
		if (src == dst) {
			return [];
		}

		if (src.Reverse() == dst) {
			return [Rotation.R, Rotation.R];
		}

		if (src.Rotate(Rotation.R) == dst) {
			return [Rotation.R];
		} else {
			return [Rotation.L];
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static IEnumerable<Direction4> ForEach()
	{
		yield return Direction4.N;
		yield return Direction4.U;
		yield return Direction4.R;
		yield return Direction4.D;
		yield return Direction4.L;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static IEnumerable<Direction4> ForEach4()
	{
		for (int dn = 0; dn < 4; ++dn) {
			yield return _delta4Dir[dn];
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static IEnumerable<(int i, int j, Direction4 dir)> Adjacence4(int i, int j, int imax, int jmax)
	{
		for (int dn = 0; dn < 4; ++dn) {
			int d4i = i + _delta4[dn];
			int d4j = j + _delta4[dn + 1];
			var dir = _delta4Dir[dn];
			if ((uint)d4i < (uint)imax && (uint)d4j < (uint)jmax) {
				yield return (d4i, d4j, dir);
			}
		}
	}
}

[Flags]
public enum Rotation
{
	N = 0,
	L = 0x01,
	R = 0x02,
}

public static class RotationExtensions
{
	public static char ToSymbol(this Rotation rot)
	{
		return rot switch {
			Rotation.N => '.',
			Rotation.L => 'L',
			Rotation.R => 'R',
			_ => '.',
		};
	}

	public static (int i, int j) Rotate(this Rotation rot, int i, int j)
	{
		return rot switch {
			Rotation.N => (i, j),
			Rotation.L => (-j, i),
			Rotation.R => (j, -i),
			_ => (i, j),
		};
	}

	public static (int i, int j) Rotate(this Rotation rot, int i, int j, int cy, int cx)
	{
		int dx = j - cx;
		int dy = i - cy;
		switch (rot) {
			case Rotation.N:
				return (i, j);

			case Rotation.L: {
					int jj = dy;
					int ii = -1 * dx;
					return (ii + cy, jj + cx);
				}

			case Rotation.R: {
					int jj = -1 * dy;
					int ii = dx;
					return (ii + cy, jj + cx);
				}

			default:
				return (i, j);
		}
	}

	public static Rotation GetRotation(Direction4 cur, Direction4 tag)
	{
		if (cur == tag || cur == Direction4.N || tag == Direction4.N) {
			return Rotation.N;
		}

		int ci = cur.ToIndex4();
		int ti = tag.ToIndex4();
		// 真反対の場合はとりあえずどっちでもいい
		if (Math.Abs(ci - ti) == 2) {
			return Rotation.L;
		}

		int cci = ci - 1;
		if (cci < 0) {
			cci += 4;
		}

		return cci == ti ? Rotation.L : Rotation.R;
	}
}

/// <summary>
/// 移動経路復元用のクラス
/// </summary>
/// <typeparam name="T">移動情報の型</typeparam>
public class Trace<T>
{
	/// <summary>移動情報ログ</summary>
	/// <remarks>
	/// IDはこのフィールドのインデックスに対応している。
	/// 移動元のIDを保持することによって、任意の位置から開始位置に辿ることが可能。
	/// </remarks>
	private readonly List<(T move, int prevID)> _log = [(default, -1)];

	/// <summary>
	/// 移動情報の追加
	/// </summary>
	/// <param name="move">どういう移動を行ったかの情報</param>
	/// <param name="prevID">移動元のID</param>
	/// <returns>移動先のID</returns>
	public int Add(T move, int prevID)
	{
		_log.Add((move, prevID));
		return _log.Count - 1;
	}

	/// <summary>
	/// 移動開始位置から指定したIDに対応する位置までの移動情報を復元する
	/// </summary>
	/// <param name="id">移動先の位置に対応するID</param>
	/// <returns>
	/// 復元した移動情報。
	/// stackの上から順番に使用する事でidで指定した位置に辿り着ける。</returns>
	public Stack<T> GetRouteTo(int id)
	{
		var route = new Stack<T>();
		while (id != 0) {
			route.Push(_log[id].move);
			id = _log[id].prevID;
		}

		return route;
	}
}

public class HeuristicHelper
{
	public static void RunCases<T>(
		int runCaseCount,
		double testCaseCount,
		bool isParallel,
		Func<int, T> run,
		Func<object, int, T, (long score, int loop, int up)> outputCaseInformation,
		Action<double, int> addOutput = null)
	{
#if DEBUG
		object locker = new();
		long scoreSum = 0;
		double scoreLogSum = 0;
		long loopSum = 0;
		long upSum = 0;
		long scoreMin = long.MaxValue;
		long scoreMax = long.MinValue;
		int errorCount = 0;
		if (isParallel) {
			Parallel.For(0, runCaseCount, i => {
				RunCases(i);
			});
		} else {
			for (int i = 0; i < runCaseCount; i++) {
				RunCases(i);
			}
		}

		void RunCases(int i)
		{
			try {
				var ret = run(i);
				var (score, loop, up) = outputCaseInformation(locker, i, ret);
				Console.Out.Flush();
				lock (locker) {
					scoreSum += score;
					scoreLogSum += Math.Log10(score);
					loopSum += loop;
					upSum += up;
					scoreMin = Math.Min(score, scoreMin);
					scoreMax = Math.Max(score, scoreMax);
				}
			} catch (Exception ex) {
				lock (locker) {
					++errorCount;
					Console.WriteLine($"{i:d4}: {ex.Message}");
					Console.Out.Flush();
				}
			}
		}

		scoreSum = (long)(scoreSum / (runCaseCount / testCaseCount));
		scoreLogSum /= runCaseCount / testCaseCount;
		Console.WriteLine("");

		Console.WriteLine("");
		Console.WriteLine($"sum: {scoreSum}");
		Console.WriteLine($"ave: {scoreSum / testCaseCount}");
		Console.WriteLine($"min: {scoreMin}");
		Console.WriteLine($"max: {scoreMax}");
		Console.WriteLine($"log: {scoreLogSum / testCaseCount}");
		Console.WriteLine($"loop ave.: {loopSum / (double)runCaseCount:f3}");
		Console.WriteLine($"up ave.: {upSum / (double)runCaseCount:f3}");

		addOutput?.Invoke(testCaseCount, runCaseCount);

		Console.WriteLine($"error : {errorCount}");

		Console.Out.Flush();
#else
		run(-1);
#endif
	}

	public static (bool isDebug, Stopwatch sw, Random rnd) Initialize()
	{
		var sw = new Stopwatch();
		sw.Start();

		bool isDebug = false;
#if DEBUG
		isDebug = true;
#endif
		var rnd = new Random();

		return (isDebug, sw, rnd);
	}

	public static IOManager CreateIO(
		string caseDirectory, int caseNumber)
	{
#if DEBUG
		var cin = caseNumber >= 0
			? new IOManager($"{caseDirectory}{caseNumber:d4}.txt")
			: new IOManager();
#else
		var cin = new IOManager();
#endif
		return cin;
	}
}

public static class Helper
{
	public static long INF => 1L << 50;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Clamp<T>(this T value, T min, T max) where T : struct, IComparable<T>
	{
		if (value.CompareTo(min) <= 0) {
			return min;
		}

		if (value.CompareTo(max) >= 0) {
			return max;
		}

		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateMin<T>(this ref T target, T value) where T : struct, IComparable<T>
		=> target = target.CompareTo(value) > 0 ? value : target;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateMin<T>(this ref T target, T value, Action<T> onUpdated)
		where T : struct, IComparable<T>
	{
		if (target.CompareTo(value) > 0) {
			target = value;
			onUpdated(value);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateMax<T>(this ref T target, T value) where T : struct, IComparable<T>
		=> target = target.CompareTo(value) < 0 ? value : target;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateMax<T>(this ref T target, T value, Action<T> onUpdated)
		where T : struct, IComparable<T>
	{
		if (target.CompareTo(value) < 0) {
			target = value;
			onUpdated(value);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long BinarySearchOKNG(long ok, long ng, Func<long, bool> satisfies)
	{
		while (ng - ok > 1) {
			long mid = (ok + ng) / 2;
			if (satisfies(mid)) {
				ok = mid;
			} else {
				ng = mid;
			}
		}

		return ok;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long BinarySearchNGOK(long ng, long ok, Func<long, bool> satisfies)
	{
		while (ok - ng > 1) {
			long mid = (ok + ng) / 2;
			if (satisfies(mid)) {
				ok = mid;
			} else {
				ng = mid;
			}
		}

		return ok;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int LowerBound<T>(Span<T> array, T value) where T : IComparable<T>
	=> LowerBound(array, -1, array.Length, value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int LowerBound<T>(Span<T> array, int ng, int ok, T value)
		where T : IComparable<T>
	{
		while (ok - ng > 1) {
			int mid = (ok + ng) / 2;
			if (array[mid].CompareTo(value) >= 0) {
				ok = mid;
			} else {
				ng = mid;
			}
		}

		return ok;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int UpperBound<T>(Span<T> array, T value) where T : IComparable<T>
		=> UpperBound(array, -1, array.Length, value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int UpperBound<T>(Span<T> array, int ng, int ok, T value)
		where T : IComparable<T>
	{
		while (ok - ng > 1) {
			int mid = (ok + ng) / 2;
			if (array[mid].CompareTo(value) > 0) {
				ok = mid;
			} else {
				ng = mid;
			}
		}

		return ok;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T[] Array1<T>(int n, T initialValue) where T : struct
		=> new T[n].Fill(initialValue);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T[] Array1<T>(int n, Func<int, T> initializer)
		=> [.. Enumerable.Range(0, n).Select(x => initializer(x))];
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T[] Fill<T>(this T[] array, T value)
		where T : struct
	{
		array.AsSpan().Fill(value);
		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T[,] Array2<T>(int n, int m, T initialValule) where T : struct
		=> new T[n, m].Fill(initialValule);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T[,] Array2<T>(int n, int m, Func<int, int, T> initializer)
	{
		var array = new T[n, m];
		for (int i = 0; i < n; ++i) {
			for (int j = 0; j < m; ++j) {
				array[i, j] = initializer(i, j);
			}
		}

		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T[,] Fill<T>(this T[,] array, T initialValue)
		where T : struct
	{
		MemoryMarshal.CreateSpan<T>(ref array[0, 0], array.Length).Fill(initialValue);
		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> AsSpan<T>(this T[,] array, int i)
		=> MemoryMarshal.CreateSpan<T>(ref array[i, 0], array.GetLength(1));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T[,,] Array3<T>(int n1, int n2, int n3, T initialValue)
		where T : struct
		=> new T[n1, n2, n3].Fill(initialValue);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T[,,] Fill<T>(this T[,,] array, T initialValue)
		where T : struct
	{
		MemoryMarshal.CreateSpan<T>(ref array[0, 0, 0], array.Length).Fill(initialValue);
		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> AsSpan<T>(this T[,,] array, int i, int j)
		=> MemoryMarshal.CreateSpan<T>(ref array[i, j, 0], array.GetLength(2));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T[,,,] Array4<T>(int n1, int n2, int n3, int n4, T initialValue)
		where T : struct
		=> new T[n1, n2, n3, n4].Fill(initialValue);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T[,,,] Fill<T>(this T[,,,] array, T initialValue)
		where T : struct
	{
		MemoryMarshal.CreateSpan<T>(ref array[0, 0, 0, 0], array.Length).Fill(initialValue);
		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> AsSpan<T>(this T[,,,] array, int i, int j, int k)
		=> MemoryMarshal.CreateSpan<T>(ref array[i, j, k, 0], array.GetLength(3));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T[] Merge<T>(ReadOnlySpan<T> first, ReadOnlySpan<T> second) where T : IComparable<T>
	{
		var ret = new T[first.Length + second.Length];
		int p = 0;
		int q = 0;
		while (p < first.Length || q < second.Length) {
			if (p == first.Length) {
				ret[p + q] = second[q];
				q++;
				continue;
			}

			if (q == second.Length) {
				ret[p + q] = first[p];
				p++;
				continue;
			}

			if (first[p].CompareTo(second[q]) < 0) {
				ret[p + q] = first[p];
				p++;
			} else {
				ret[p + q] = second[q];
				q++;
			}
		}

		return ret;
	}

	private static readonly int[] delta4_ = [1, 0, -1, 0, 1];
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ReadOnlySpan<(int i, int j)> Adjacence4(int i, int j, int imax, int jmax)
	{
		int p = 0;
		Span<(int i, int j)> adjacences = new (int i, int j)[4];
		for (int dn = 0; dn < 4; ++dn) {
			int d4i = i + delta4_[dn];
			int d4j = j + delta4_[dn + 1];
			if ((uint)d4i < (uint)imax && (uint)d4j < (uint)jmax) {
				adjacences[p] = (d4i, d4j);
				++p;
			}
		}

		return adjacences[..p];
	}

	private static readonly int[] delta8_ = [1, 0, -1, 0, 1, 1, -1, -1, 1];
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ReadOnlySpan<(int i, int j)> Adjacence8(int i, int j, int imax, int jmax)
	{
		int p = 0;
		Span<(int i, int j)> adjacences = new (int i, int j)[8];
		for (int dn = 0; dn < 8; ++dn) {
			int d8i = i + delta8_[dn];
			int d8j = j + delta8_[dn + 1];
			if ((uint)d8i < (uint)imax && (uint)d8j < (uint)jmax) {
				adjacences[p] = (d8i, d8j);
				++p;
			}
		}

		return adjacences[..p];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static IEnumerable<int> SubBitsOf(int bit)
	{
		for (int sub = bit; sub > 0; sub = --sub & bit) {
			yield return sub;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Reverse(string src)
	{
		var chars = src.ToCharArray();
		for (int i = 0, j = chars.Length - 1; i < j; ++i, --j) {
			(chars[j], chars[i]) = (chars[i], chars[j]);
		}

		return new string(chars);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Exchange(string src, char a, char b)
	{
		var chars = src.ToCharArray();
		for (int i = 0; i < chars.Length; i++) {
			if (chars[i] == a) {
				chars[i] = b;
			} else if (chars[i] == b) {
				chars[i] = a;
			}
		}

		return new string(chars);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Swap(this string str, int i, int j)
	{
		var span = str.AsWriteableSpan();
		(span[i], span[j]) = (span[j], span[i]);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char Replace(this string str, int index, char c)
	{
		var span = str.AsWriteableSpan();
		char old = span[index];
		span[index] = c;
		return old;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<char> ReverseInPlace(this string str)
		=> str.ReverseInPlace(0, str.Length);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<char> ReverseInPlace(this string str, int l, int r)
	{
		var span = str.AsWriteableSpan(l, r);
		for (int i = 0, j = span.Length - 1; i < j; ++i, --j) {
			(span[j], span[i]) = (span[i], span[j]);
		}

		return MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), span.Length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<char> AsWriteableSpan(this string str)
		=> str.AsWriteableSpan(0, str.Length);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<char> AsWriteableSpan(this string str, int l, int r)
	{
		var span = str.AsSpan(l, r - l);
		return MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), span.Length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Join<T>(this IEnumerable<T> values, string separator = "")
		=> string.Join(separator, values);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string JoinNL<T>(this IEnumerable<T> values)
		=> string.Join(Environment.NewLine, values);
}

public class IOManager : IDisposable
{
	private const int BUFFER_SIZE = 1024;
	private const int ASCII_SPACE = 32;
	private const int ASCII_CHAR_BEGIN = 33;
	private const int ASCII_CHAR_END = 126;
	private readonly byte[] _buf = new byte[BUFFER_SIZE];
	private readonly bool _isStdIn;
	private readonly Stream _reader;
	private readonly bool _isStdOut;
	private readonly TextWriter _writer;
	private int _length = 0;
	private int _index = 0;
	private bool _isEof = false;

	public IOManager(string inFilePath = "", string outFilePath = "")
	{
		// Console.Readline をすると UTF-16 の string 型で読み込まれるが、
		// 競プロの入力は基本的に ASCII の範囲なので、メモリも時間も倍食ってしまう。
		// byte[] で入力して手動で変換出来るように、TextReader ではなく Stream を使う。
		if (string.IsNullOrWhiteSpace(inFilePath)) {
			_isStdIn = true;
			_reader = Console.OpenStandardInput();
		} else {
			_reader = new FileStream(inFilePath, FileMode.Open);
		}

		// Console.WriteLine も同様であるのだが、今のところ出力速度が問題になったことがなく、
		// ファイル出力時のエンコード処理の方が面倒そうなので TextWriter を使う。
		string outPath = GetOutFilePath(inFilePath, outFilePath);
		if (string.IsNullOrWhiteSpace(outPath)) {
			// 毎回 flush すると、10^6 回出力したときに TLE してしまう。
			// 自動 flush は無効にしておき、Dispose 時に flush するようにする。
			Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) {
				AutoFlush = false
			});

			_isStdOut = true;
			_writer = Console.Out;
		} else {
			_writer = new StreamWriter(outPath);
		}
	}

	public void Dispose()
	{
		_writer.Flush();
		// Console.In や Console.Out は Dispose してはいけない。
		if (_isStdIn == false) {
			_reader.Dispose();
		}

		if (_isStdOut == false) {
			_writer.Dispose();
		}

		// Dispose パターンは面倒なのでサボり。
		GC.SuppressFinalize(this);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string NextLine()
	{
		var sb = new StringBuilder();
		for (var b = Char(); b >= ASCII_SPACE && b <= ASCII_CHAR_END; b = (char)Read()) {
			sb.Append(b);
		}

		return sb.ToString();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T[] Repeat<T>(int count, Func<IOManager, T> read)
	{
		var array = new T[count];
		for (int i = 0; i < count; ++i) {
			// キャプチャーを避けるために自身を引数として渡す。
			array[i] = read(this);
		}

		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T[,] Repeat<T>(int height, int width, Func<IOManager, T> read)
	{
		var array = new T[height, width];
		for (int i = 0; i < height; ++i) {
			for (int j = 0; j < width; ++j) {
				// キャプチャーを避けるために自身を引数として渡す。
				array[i, j] = read(this);
			}
		}

		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public char Char()
	{
		byte b;
		do {
			b = Read();
		} while (b < ASCII_CHAR_BEGIN || ASCII_CHAR_END < b);

		return (char)b;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string String()
	{
		var sb = new StringBuilder();
		for (var b = Char(); b >= ASCII_CHAR_BEGIN && b <= ASCII_CHAR_END; b = (char)Read()) {
			sb.Append(b);
		}

		return sb.ToString();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string[] ArrayString(int length)
	{
		var array = new string[length];
		for (int i = 0; i < length; ++i) {
			array[i] = String();
		}

		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int Int() => (int)Long();
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int Int(int offset) => Int() + offset;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (int, int) Int2(int offset = 0)
		=> (Int(offset), Int(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (int, int, int) Int3(int offset = 0)
		=> (Int(offset), Int(offset), Int(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (int, int, int, int) Int4(int offset = 0)
		=> (Int(offset), Int(offset), Int(offset), Int(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (int, int, int, int, int) Int5(int offset = 0)
		=> (Int(offset), Int(offset), Int(offset), Int(offset), Int(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int[] ArrayInt(int length, int offset = 0)
	{
		var array = new int[length];
		for (int i = 0; i < length; ++i) {
			array[i] = Int(offset);
		}

		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long Long()
	{
		long ret = 0;
		byte b;
		bool negative = false;
		// 空白その他の読み飛ばし。+も読み飛ばしてしまって問題無い。
		do {
			b = Read();
		} while (b != '-' && (b < '0' || '9' < b));

		if (b == '-') {
			negative = true;
			b = Read();
		}

		// 下に一桁ずつ加えていく。
		for (; true; b = Read()) {
			if (b < '0' || '9' < b) {
				return negative ? -ret : ret;
			} else {
				ret = ret * 10 + (b - '0');
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long Long(long offset) => Long() + offset;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (long, long) Long2(long offset = 0)
		=> (Long(offset), Long(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (long, long, long) Long3(long offset = 0)
		=> (Long(offset), Long(offset), Long(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (long, long, long, long) Long4(long offset = 0)
		=> (Long(offset), Long(offset), Long(offset), Long(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (long, long, long, long, long) Long5(long offset = 0)
		=> (Long(offset), Long(offset), Long(offset), Long(offset), Long(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long[] ArrayLong(int length, long offset = 0)
	{
		var array = new long[length];
		for (int i = 0; i < length; ++i) {
			array[i] = Long(offset);
		}

		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public BigInteger Big() => BigInteger.Parse(String());
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public BigInteger Big(long offset) => Big() + offset;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (BigInteger, BigInteger) Big2(long offset = 0)
		=> (Big(offset), Big(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (BigInteger, BigInteger, BigInteger) Big3(long offset = 0)
		=> (Big(offset), Big(offset), Big(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (BigInteger, BigInteger, BigInteger, BigInteger) Big4(long offset = 0)
		=> (Big(offset), Big(offset), Big(offset), Big(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (BigInteger, BigInteger, BigInteger, BigInteger, BigInteger) Big5(long offset = 0)
		=> (Big(offset), Big(offset), Big(offset), Big(offset), Big(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public BigInteger[] ArrayBig(int length, long offset = 0)
	{
		var array = new BigInteger[length];
		for (int i = 0; i < length; ++i) {
			array[i] = Big(offset);
		}

		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public double Double() => double.Parse(String(), CultureInfo.InvariantCulture);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public double Double(double offset) => Double() + offset;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (double, double) Double2(double offset = 0)
		=> (Double(offset), Double(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (double, double, double) Double3(double offset = 0)
		=> (Double(offset), Double(offset), Double(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (double, double, double, double) Double4(double offset = 0)
		=> (Double(offset), Double(offset), Double(offset), Double(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (double, double, double, double, double) Double5(double offset = 0)
		=> (Double(offset), Double(offset), Double(offset), Double(offset), Double(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public double[] ArrayDouble(int length, double offset = 0)
	{
		var array = new double[length];
		for (int i = 0; i < length; ++i) {
			array[i] = Double(offset);
		}

		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public decimal Decimal() => decimal.Parse(String(), CultureInfo.InvariantCulture);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public decimal Decimal(decimal offset) => Decimal() + offset;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (decimal, decimal) Decimal2(decimal offset = 0)
		=> (Decimal(offset), Decimal(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (decimal, decimal, decimal) Decimal3(decimal offset = 0)
		=> (Decimal(offset), Decimal(offset), Decimal(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (decimal, decimal, decimal, decimal) Decimal4(decimal offset = 0)
		=> (Decimal(offset), Decimal(offset), Decimal(offset), Decimal(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (decimal, decimal, decimal, decimal, decimal) Decimal5(decimal offset = 0)
		=> (Decimal(offset), Decimal(offset), Decimal(offset), Decimal(offset), Decimal(offset));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public decimal[] ArrayDecimal(int length, decimal offset = 0)
	{
		var array = new decimal[length];
		for (int i = 0; i < length; ++i) {
			array[i] = Decimal(offset);
		}

		return array;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLine() => _writer.WriteLine();
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLine(bool value) => _writer.WriteLine(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLine(char value) => _writer.WriteLine(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLine(decimal value) => _writer.WriteLine(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLine(double value) => _writer.WriteLine(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLine(float value) => _writer.WriteLine(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLine(int value) => _writer.WriteLine(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLine(uint value) => _writer.WriteLine(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLine(long value) => _writer.WriteLine(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLine(ulong value) => _writer.WriteLine(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLine(BigInteger value) => _writer.WriteLine(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteLine(string value) => _writer.WriteLine(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write(bool value) => _writer.Write(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write(char value) => _writer.Write(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write(decimal value) => _writer.Write(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write(double value) => _writer.Write(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write(float value) => _writer.Write(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write(int value) => _writer.Write(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write(uint value) => _writer.Write(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write(long value) => _writer.Write(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write(ulong value) => _writer.Write(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write(BigInteger value) => _writer.Write(value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write(string value) => _writer.Write(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void YesNo(bool ok, bool isUpper = false)
	{
		_writer.WriteLine(
			isUpper == false
				? ok ? "Yes" : "No"
				: ok ? "YES" : "NO");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void JoinNL<T>(IEnumerable<T> values) => Join(values, Environment.NewLine);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Join<T>(IEnumerable<T> values, string separator = " ")
		=> _writer.WriteLine(string.Join(separator, values));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void JoinNL<T>(T[,] valuess) => Join(valuess, Environment.NewLine);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Join<T>(T[,] valuess, string separator = " ")
	{
		int height = valuess.GetLength(0);
		int width = valuess.GetLength(1);
		for (int i = 0; i < height; i++) {
			_writer.WriteLine(
				string.Join(separator, MemoryMarshal.CreateSpan<T>(ref valuess[i, 0], width).ToArray()));
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Flush() => _writer.Flush();

	private byte Read()
	{
		if (_isEof) {
			throw new EndOfStreamException();
		}

		if (_index >= _length) {
			_index = 0;
			if ((_length = _reader.Read(_buf, 0, BUFFER_SIZE)) <= 0) {
				_isEof = true;
				return 0;
			}
		}

		return _buf[_index++];
	}

	private static string GetOutFilePath(string inFilePath, string outFilePath)
	{
		if (string.IsNullOrWhiteSpace(outFilePath) == false) {
			return outFilePath;
		}

		if (string.IsNullOrWhiteSpace(inFilePath)) {
			return inFilePath;
		}


		string directory = Path.GetDirectoryName(inFilePath);
		string title = Path.GetFileNameWithoutExtension(inFilePath);
		string ext = Path.GetExtension(inFilePath);
		string directoryName = Path.GetFileName(directory);
		string parentDirectory = Path.GetDirectoryName(directory);
		if (directoryName.Contains(@"in")) {
			// AHCで配布される tools の in フォルダーの横に out フォルダーを作る。
			// mastersのinAにも対応出来るようにReplaceする。
			directoryName = directoryName.Replace("in", "out");
			directory = Path.Combine(parentDirectory, directoryName);
		} else {
			// ビジュアライザーの一括読み込みは、1234.txt 又は xxx_1234.txt のような形式に対応している。
			// 入力と同じフォルダーの場合は、出力ファイルであることが分かるようにしておく。
			title = "out_" + title;
		}

		if (Directory.Exists(directory) == false) {
			Directory.CreateDirectory(directory);
		}

		return Path.Combine(directory, title + ext);
	}
}