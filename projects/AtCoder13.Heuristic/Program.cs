using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
						$"MC={info.MC:d3}",
						$"PPM={info.PPM:d3}",
						$"W={info.Width:d4}",
						$"D={info.Depth:d7}",
						//$"ML={info.ML:d3}",
						//$"RA={info.RA:d5}",
						//$"A={info.A:d4}",
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

		var best = Plan1(isLocal, sw, rnd, input, info);
		info.P = 1;

		/*
		var state3 = Plan3(isLocal, sw, rnd, input, info);
		var best = state3;
		info.P = 3;
		{
			best = Plan2(isLocal, sw, rnd, input, info, best);
		}*/


		/*
		var state8 = Plan8(isLocal, sw, rnd, input, info);
		if (state8.Ops.Count < best.Ops.Count) {
			best = state8;
			info.P = 8;
		}*/

		var state9 = Plan10(isLocal, sw, rnd, input, info);
		if (state9.Ops.Count < best.Ops.Count) {
			best = state9;
			info.P = 9;
		}



		/*
		var state5 = Plan5(isLocal, sw, rnd, input, info);
		if (state5.Ops.Count < best.Ops.Count) {
			best = state5;
			info.P = 5;
		}*/

		best = NormalizeStateForDelayedMacroRegistration(best, input, isLocal);
		/*
		var state11 = Plan11(isLocal, sw, rnd, input, info, best);
		if (state11.Ops.Count < best.Ops.Count) {
			best = state11;
			info.P = 11;
		}*/

		input.Output(best);

		info.Macro = best.Macro == null ? "" : new string(best.Macro);
		info.ML = best.Macro == null ? 0 : best.Macro.Length;
		info.A = best.Ops.Count;
		info.Score = best.Calculate();
		info.Elapsed = sw.ElapsedMilliseconds;

		return (input, info);
	}

	static State NormalizeStateForDelayedMacroRegistration(State source, Input input, bool isLocal)
	{
		if (source.Macro == null || source.Macro.Length == 0) return source;
		if (source.Ops.Count == 0) return source;

		int firstP = source.Ops.IndexOf('P');
		if (firstP < 0) {
			return source;
		}

		var normalized = new State(input, isLocal);
		for (int i = 0; i < source.Ops.Count; i++) {
			if (i == firstP) {
				normalized.AddOp('M');
				foreach (var c in source.Macro) normalized.AddOp(c);
				normalized.AddOp('M');
				normalized.SetMacro(source.Macro);
				continue;
			}

			normalized.AddOp(source.Ops[i]);
		}

		return normalized;
	}

	static State Plan1(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(input, isLocal);
		for (int k = 0; k < input.M; k++) {
			var (ci, cj) = state.Robo;
			var (si, sj, ti, tj) = input.Balls[k];

			var ret = input.Map.Bfs(ci, cj, si, sj);
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

			ret = input.Map.Bfs(si, sj, ti, tj);
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
			state.SetMacro(macro.ToCharArray());

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
			state.SetMacro(bestMacro.ToCharArray());
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

	static State Plan10(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		int N = input.N;
		int M = input.M;

		var tempMacroList = new List<string>();

		/*
		// 1. 純直進（長さ4〜7）: 4種
		tempMacroList.AddRange(new[] { "FFFF", "FFFFF", "FFFFFF", "FFFFFFF" });

		// 2. 方向保持・両端ターン（長さ6〜9）: 20種程度
		//    LXXXR, RXXXL 形で F が 4〜7個
		foreach (int nF in new[] { 4, 5, 6, 7 }) {
			string f = new string('F', nF);
			tempMacroList.Add("L" + f + "R");
			tempMacroList.Add("R" + f + "L");
			if (nF >= 5) {
				tempMacroList.Add("LL" + f.Substring(0, nF - 1) + "R");
				tempMacroList.Add("RR" + f.Substring(0, nF - 1) + "L");
				tempMacroList.Add("L" + f.Substring(0, nF - 1) + "RR");
				tempMacroList.Add("R" + f.Substring(0, nF - 1) + "LL");
			}
		}

		// 3. 片側ターン（長さ6〜9）: 20種
		foreach (int nF in new[] { 5, 6, 7, 8 }) {
			string f = new string('F', nF);
			tempMacroList.Add("L" + f);
			tempMacroList.Add("R" + f);
			tempMacroList.Add(f + "L");
			tempMacroList.Add(f + "R");
		}

		// 4. 中間ターン（長さ7〜9）: 20種
		foreach (int a in new[] { 2, 3, 4 }) {
			foreach (int b in new[] { 3, 4, 5 }) {
				if (a + b + 1 > 10) continue;
				string fa = new string('F', a);
				string fb = new string('F', b);
				tempMacroList.Add(fa + "L" + fb);
				tempMacroList.Add(fa + "R" + fb);
			}
		}

		// 5. 右手法・左手法の基本形（長さ6〜10）: 30種
		foreach (int a in new[] { 1, 2, 3 }) {
			foreach (int b in new[] { 1, 2, 3 }) {
				int total = 2 + a + b;
				if (total > 10) continue;
				string fa = new string('F', a);
				string fb = new string('F', b);
				tempMacroList.Add("R" + fa + "L" + fb);  // 右手法
				tempMacroList.Add("L" + fa + "R" + fb);  // 左手法
				tempMacroList.Add(fa + "R" + fb);        // コーナー右
				tempMacroList.Add(fa + "L" + fb);        // コーナー左
			}
		}

		// 6. 長いマクロ（10〜12）: 少数精鋭で10種程度
		tempMacroList.AddRange(new[] {
			"FFFFRFFFFL", "FFFFLFFFFR",  // 採用実績あり
			"RFFFFFFFL", "LFFFFFFR",
			"FFFFFFFFL", "FFFFFFFFR",
			"FFFFFRFFFFFL",  // 12文字・採用実績あり
			"LFFRFFLFFRFF",  // 12文字・採用実績あり
			"RFLFFFFLF", "LFRFFFFRF"  // 右手法の長め
		});

		var macroList = tempMacroList.Distinct().ToArray();
		*/

		// 1. 純直進（長さ7〜12）: 長いものを優先
		tempMacroList.AddRange(new[] {
			"FFFFFFFFFF",    // 10
			"FFFFFFFFF",     // 9
			"FFFFFFFF",      // 8
			"FFFFFFF",       // 7
			"FFFFFF",        // 6
		});

		// 2. 方向保持・両端ターン（長さ9〜14）
		foreach (int nF in new[] { 7, 8, 9, 10, 11, 12 }) {
			string f = new string('F', nF);
			tempMacroList.Add("L" + f + "R");
			tempMacroList.Add("R" + f + "L");
			if (nF >= 8 && nF <= 10) {  // 長すぎると効率悪い
				tempMacroList.Add("LL" + f.Substring(0, nF - 1) + "R");
				tempMacroList.Add("RR" + f.Substring(0, nF - 1) + "L");
				tempMacroList.Add("L" + f.Substring(0, nF - 1) + "RR");
				tempMacroList.Add("R" + f.Substring(0, nF - 1) + "LL");
			}
		}

		// 3. 片側ターン（長さ8〜12）
		foreach (int nF in new[] { 7, 8, 9, 10, 11 }) {
			string f = new string('F', nF);
			tempMacroList.Add("L" + f);
			tempMacroList.Add("R" + f);
			tempMacroList.Add(f + "L");
			tempMacroList.Add(f + "R");
		}

		// 4. 中間ターン（長さ9〜12）
		foreach (int a in new[] { 3, 4, 5, 6 }) {
			foreach (int b in new[] { 4, 5, 6, 7 }) {
				int total = a + b + 1;
				if (total < 9 || total > 14) continue;
				string fa = new string('F', a);
				string fb = new string('F', b);
				tempMacroList.Add(fa + "L" + fb);
				tempMacroList.Add(fa + "R" + fb);
			}
		}

		// 5. 右手法・左手法（長さ8〜18）
		foreach (int a in new[] { 2, 3, 4, 5 }) {
			foreach (int b in new[] { 2, 3, 4, 5 }) {
				int total = 2 + a + b;
				if (total < 8 || total > 18) continue;
				string fa = new string('F', a);
				string fb = new string('F', b);
				tempMacroList.Add("R" + fa + "L" + fb);
				tempMacroList.Add("L" + fa + "R" + fb);
				if (total <= 16) {  // コーナー系は16まで
					tempMacroList.Add(fa + "R" + fb);
					tempMacroList.Add(fa + "L" + fb);
				}
			}
		}

		// 6. 右手法2〜5周（長さ12〜20）
		foreach (int n in new[] { 1, 2, 3 }) {
			string unitR = "R" + new string('F', n) + "L" + new string('F', n);
			string unitL = "L" + new string('F', n) + "R" + new string('F', n);
			for (int repeat = 3; repeat <= 5; repeat++) {
				string macroR = string.Concat(Enumerable.Repeat(unitR, repeat));
				string macroL = string.Concat(Enumerable.Repeat(unitL, repeat));
				if (macroR.Length >= 12 && macroR.Length <= 20) {
					tempMacroList.Add(macroR);
					tempMacroList.Add(macroL);
				}
			}
		}

		// 7. 採用実績のある長いマクロを明示的に追加
		tempMacroList.AddRange(new[] {
			"LFFRFFLFFRFF",   // 12・実績多数
			"FFFFFRFFFFFL",   // 12・実績あり
			"LFFFFFFRR",      // 9・実績多数
			"RFFFFFFLL",      // 9
			"FFFFRFFFFL",     // 10・実績あり
			"FFFFLFFFFR",     // 10・実績あり
		});

		// 重複削除・長さ順ソート（長いほうが precomp で先に評価される）
		var macroList = tempMacroList
			.Distinct()
			.Where(m => m.Length >= 6 && m.Length <= 20)
			.OrderByDescending(m => m.Length)
			.ThenBy(m => {
				int lc = m.Count(c => c == 'L');
				int rc = m.Count(c => c == 'R');
				return Math.Abs(lc - rc);  // 方向保持を優先
			})
			//.Take(100)  // 上限100種
			.ToArray();

		// → 合計 100〜120 種を想定
		info.MC = macroList.Length;

		var dirs = new[] { Direction4.D, Direction4.L, Direction4.U, Direction4.R };

		var macroDestAll = new (int i, int j, Direction4 dir, bool ok)[macroList.Length][,,];
		for (int mi = 0; mi < macroList.Length; mi++) {
			var mac = macroList[mi];
			var dest = new (int i, int j, Direction4 dir, bool ok)[N, N, 4];
			for (int i = 0; i < N; i++) {
				for (int j = 0; j < N; j++) {
					foreach (var dir in dirs) {
						int nr = i, nc = j;
						Direction4 nd = dir;
						bool mOk = true;
						foreach (var op in mac) {
							switch (op) {
								case 'F':
									if (input.Map.CanMove(nr, nc, nd)) {
										var mv = nd.Move(nr, nc);
										nr = mv.i;
										nc = mv.j;
									}

									break;
								case 'L': nd = nd.Rotate(Rotation.L); break;
								case 'R': nd = nd.Rotate(Rotation.R); break;
							}
							if (!mOk) break;
						}
						dest[i, j, dir.ToIndex4()] = (nr, nc, nd, mOk);
					}
				}
			}
			macroDestAll[mi] = dest;
		}

		var macrodist = new int[N, N, 4];
		var macroprev = new (int i, int j, Direction4 dir, char op)[N, N, 4];
		var macroQue = new Queue<int>();
		var macroStamp = new int[N, N, 4];
		int macroCurrentStamp = 0;
		var routeBuf = new char[N * N * 4 * 8];

		int K = 1 + M * 2;
		(int i, int j)[] keyPos = new (int, int)[K];
		keyPos[0] = (0, 0);
		for (int k = 0; k < M; k++) {
			keyPos[1 + k] = (input.Balls[k].b, input.Balls[k].c);
			keyPos[1 + M + k] = (input.Balls[k].d, input.Balls[k].e);
		}

		double precompDeadline = Math.Min(sw.Elapsed.TotalMilliseconds + 1200.0, 1600.0);
		var nodeDistAll = new int[macroList.Length][,,];
		var nodeArrDirAll = new int[macroList.Length][,,];
		var macroEnabled = new bool[macroList.Length];

		var fullDist = new int[N, N, 4];
		var fullStamp = new int[N, N, 4];
		int fullStampVal = 0;

		for (int mi = 0; mi < macroList.Length; mi++) {
			if (mi > 0 && sw.Elapsed.TotalMilliseconds > precompDeadline) break;

			info.PPM++;
			var macroDest = macroDestAll[mi];
			var nodeDist = new int[K, 4, K];
			var nodeArrDir = new int[K, 4, K];
			for (int a = 0; a < K; a++)
				for (int d = 0; d < 4; d++)
					for (int b = 0; b < K; b++)
						nodeDist[a, d, b] = (a == b) ? 0 : int.MaxValue / 2;

			for (int from = 0; from < K; from++) {
				var (fi, fj) = keyPos[from];
				for (int startDirIdx = 0; startDirIdx < 4; startDirIdx++) {
					++fullStampVal;
					int fcs = fullStampVal;
					macroQue.Clear();
					fullDist[fi, fj, startDirIdx] = 0;
					fullStamp[fi, fj, startDirIdx] = fcs;
					macroQue.Enqueue((fi << 8) | (fj << 2) | startDirIdx);

					while (macroQue.Count > 0) {
						int cVal = macroQue.Dequeue();
						int ci = cVal >> 8;
						int cj = (cVal >> 2) & 0x3F;
						int cIdx = cVal & 3;
						Direction4 cdir = cIdx.FromIndex4();
						int d2 = fullDist[ci, cj, cIdx];

						void Push2(int ni, int nj, Direction4 ndir)
						{
							int nIdx = ndir.ToIndex4();
							if (fullStamp[ni, nj, nIdx] != fcs || fullDist[ni, nj, nIdx] > d2 + 1) {
								fullDist[ni, nj, nIdx] = d2 + 1;
								fullStamp[ni, nj, nIdx] = fcs;
								macroQue.Enqueue((ni << 8) | (nj << 2) | nIdx);
							}
						}

						if (input.Map.CanMove(ci, cj, cdir)) {
							var nPos = cdir.Move(ci, cj);
							Push2(nPos.i, nPos.j, cdir);
						}
						Push2(ci, cj, cdir.Rotate(Rotation.L));
						Push2(ci, cj, cdir.Rotate(Rotation.R));
						var dest = macroDest[ci, cj, cIdx];
						if (dest.ok) Push2(dest.i, dest.j, dest.dir);
					}

					for (int to = 0; to < K; to++) {
						if (from == to) continue;
						var (ti2, tj2) = keyPos[to];
						int best = int.MaxValue / 2;
						int bestArrDir = 0;
						for (int d = 0; d < 4; d++) {
							if (fullStamp[ti2, tj2, d] == fcs && fullDist[ti2, tj2, d] < best) {
								best = fullDist[ti2, tj2, d];
								bestArrDir = d;
							}
						}
						nodeDist[from, startDirIdx, to] = best;
						nodeArrDir[from, startDirIdx, to] = bestArrDir;
					}
				}
			}
			nodeDistAll[mi] = nodeDist;
			nodeArrDirAll[mi] = nodeArrDir;
			macroEnabled[mi] = true;
		}

		int CalcOrderCost(int[] order, int[,,] nd, int[,,] nad)
		{
			int cost = 0;
			int curNode = 0;
			int curDir = Direction4.D.ToIndex4();
			for (int i = 0; i < M; i++) {
				int kk = order[i];
				int pickNode = 1 + kk;
				int dropNode = 1 + M + kk;
				cost += nd[curNode, curDir, pickNode];
				curDir = nad[curNode, curDir, pickNode];
				cost += nd[pickNode, curDir, dropNode];
				curDir = nad[pickNode, curDir, dropNode];
				curNode = dropNode;
			}
			return cost;
		}

		int[] MakeGreedyOrder(int[,,] nd, int[,,] nad)
		{
			var order = new int[M];
			var used = new bool[M];
			int curNode = 0;
			int curDir = Direction4.D.ToIndex4();
			for (int i = 0; i < M; i++) {
				int best = -1; int bestD = int.MaxValue;
				for (int kk = 0; kk < M; kk++) {
					if (used[kk]) continue;
					int d2 = nd[curNode, curDir, 1 + kk];
					if (d2 < bestD) { bestD = d2; best = kk; }
				}
				order[i] = best;
				used[best] = true;
				int pickDir = nad[curNode, curDir, 1 + best];
				curDir = nad[1 + best, pickDir, 1 + M + best];
				curNode = 1 + M + best;
			}
			return order;
		}

		var bestOrders = new int[macroList.Length][];
		var bestCosts = new int[macroList.Length];
		var currentOrders = new int[macroList.Length][];
		var currentCosts = new int[macroList.Length];

		for (int mi = 0; mi < macroList.Length; mi++) {
			if (!macroEnabled[mi]) continue;
			var ord = MakeGreedyOrder(nodeDistAll[mi], nodeArrDirAll[mi]);
			int c = CalcOrderCost(ord, nodeDistAll[mi], nodeArrDirAll[mi]);
			bestOrders[mi] = (int[])ord.Clone();
			bestCosts[mi] = c;
			currentOrders[mi] = (int[])ord.Clone();
			currentCosts[mi] = c;
		}

		// SA: 2-opt と or-opt を交互に試みる
		double saStartMs = sw.Elapsed.TotalMilliseconds;
		const double saEndMs = 1700.0;
		double initTemp;
		{
			double sum = 0; int cnt = 0;
			for (int i = 0; i < macroList.Length; i++)
				if (macroEnabled[i]) { sum += bestCosts[i]; cnt++; }
			double cap = 30.0 * M / 15.0;
			initTemp = cnt > 0 ? Math.Min(sum / cnt * 0.10, cap) : cap;
		}
		const double endTemp = 0.01;
		info.Width = (int)initTemp;
		int saAccept = 0;

		// or-opt 用の作業バッファ
		var orOptBuf = new int[M];

		int pruneIter = 0;
		int iter = 0;
		while (true) {
			double elapsed = sw.Elapsed.TotalMilliseconds;
			if (elapsed >= saEndMs) break;
			double progress = (elapsed - saStartMs) / (saEndMs - saStartMs);
			double temp = initTemp * Math.Pow(endTemp / initTemp, progress);

			int mi = iter % macroList.Length;
			iter++;
			if (!macroEnabled[mi] || currentOrders[mi] == null) continue;

			if (iter - pruneIter >= 500) {
				pruneIter = iter;
				if (elapsed > 900.0) {
					int globalMin = int.MaxValue;
					for (int i = 0; i < macroList.Length; i++)
						if (macroEnabled[i] && bestCosts[i] < globalMin)
							globalMin = bestCosts[i];
					double threshold = elapsed < 1300.0 ? 1.1 : 1.03;
					for (int i = 0; i < macroList.Length; i++)
						if (macroEnabled[i] && bestCosts[i] > globalMin * threshold)
							macroEnabled[i] = false;
				}
			}

			var order = currentOrders[mi];
			var nd = nodeDistAll[mi];
			var nad = nodeArrDirAll[mi];

			int newCost;
			// iter % 3 == 0: 2-opt、1: or-opt-1（1要素移動）、2: or-opt-2（2要素セグメント移動）
			int moveType = iter % 3;
			if (moveType == 0) {
				// 2-opt
				int p1 = rnd.Next(M);
				int p2 = rnd.Next(M);
				if (p1 == p2) continue;
				if (p1 > p2) (p1, p2) = (p2, p1);

				Array.Reverse(order, p1, p2 - p1 + 1);
				newCost = CalcOrderCost(order, nd, nad);
				int delta = newCost - currentCosts[mi];
				if (delta <= 0 || rnd.NextDouble() < Math.Exp(-delta / temp)) {
					if (delta > 0) saAccept++;
					currentCosts[mi] = newCost;
					if (newCost < bestCosts[mi]) {
						bestCosts[mi] = newCost;
						bestOrders[mi] = (int[])order.Clone();
						info.Up++;
					}
				} else {
					Array.Reverse(order, p1, p2 - p1 + 1);
				}
			} else if (moveType == 1) {
				// or-opt-1: 位置 src の1要素を別位置に挿入
				if (M < 2) continue;
				int src = rnd.Next(M);
				int dst = rnd.Next(M - 1);
				if (dst >= src) dst++;
				if (src == dst) continue;

				int elem = order[src];
				int idx = 0;
				for (int i = 0; i < M; i++) {
					if (i == src) continue;
					orOptBuf[idx++] = order[i];
				}
				int insertPos = dst < src ? dst + 1 : dst;
				for (int i = M - 1; i > insertPos; i--)
					orOptBuf[i] = orOptBuf[i - 1];
				orOptBuf[insertPos] = elem;

				newCost = CalcOrderCost(orOptBuf, nd, nad);
				int delta = newCost - currentCosts[mi];
				if (delta <= 0 || rnd.NextDouble() < Math.Exp(-delta / temp)) {
					if (delta > 0) saAccept++;
					Array.Copy(orOptBuf, order, M);
					currentCosts[mi] = newCost;
					if (newCost < bestCosts[mi]) {
						bestCosts[mi] = newCost;
						bestOrders[mi] = (int[])order.Clone();
						info.Up++;
					}
				}
			} else {
				// or-opt-2: 連続2要素セグメント [src, src+1] を別位置に挿入（正順・逆順の両方を試す）
				if (M < 3) continue;
				int src = rnd.Next(M - 1); // セグメント先頭インデックス
										   // 挿入先: セグメントを除いた M-2 要素の配列上の挿入インデックス (0 ~ M-3)
				int insertPos = rnd.Next(M - 2);

				int e0 = order[src];
				int e1 = order[src + 1];
				// セグメントを除いた残りを orOptBuf に詰める
				int idx = 0;
				for (int i = 0; i < M; i++) {
					if (i == src || i == src + 1) continue;
					orOptBuf[idx++] = order[i];
				}
				// insertPos+1 以降を後ろにシフト (2つ分)
				for (int i = M - 1; i >= insertPos + 3; i--)
					orOptBuf[i] = orOptBuf[i - 2];

				// 正順: [e0, e1]
				orOptBuf[insertPos + 1] = e0;
				orOptBuf[insertPos + 2] = e1;
				int costFwd = CalcOrderCost(orOptBuf, nd, nad);

				// 50% の確率で逆順 [e1, e0] も試して良い方を採用
				if ((rnd.Next(2) == 0)) {
					orOptBuf[insertPos + 1] = e1;
					orOptBuf[insertPos + 2] = e0;
					int costRev = CalcOrderCost(orOptBuf, nd, nad);
					if (costFwd < costRev) {
						orOptBuf[insertPos + 1] = e0;
						orOptBuf[insertPos + 2] = e1;
					} else {
						costFwd = costRev;
					}
				}

				newCost = costFwd;
				int delta = newCost - currentCosts[mi];
				if (delta <= 0 || rnd.NextDouble() < Math.Exp(-delta / temp)) {
					if (delta > 0) saAccept++;
					Array.Copy(orOptBuf, order, M);
					currentCosts[mi] = newCost;
					if (newCost < bestCosts[mi]) {
						bestCosts[mi] = newCost;
						bestOrders[mi] = (int[])order.Clone();
						info.Up++;
					}
				} else {
					// 元に戻す
					Array.Copy(order, orOptBuf, M);
				}
			}
		}
		info.Loop += iter;
		info.Depth = saAccept;

		// ---- Step 3: 各マクロのベスト訪問順で State 構築用パスを計算 ----
		int globalBestScore = int.MaxValue;
		string globalBestMacro = null;
		int[] globalBestOrder = null;
		char[][] globalBestPickPaths = null;
		char[][] globalBestDropPaths = null;

		var pickPaths = new char[M][];
		var dropPaths = new char[M][];

		for (int mi = 0; mi < macroList.Length; mi++) {
			if (!macroEnabled[mi] || bestOrders[mi] == null) continue;
			if (sw.Elapsed.TotalMilliseconds > 1880.0) break;
			var mac = macroList[mi];
			var macroDest = macroDestAll[mi];
			var order = bestOrders[mi];

			var state = new State(input, isLocal);
			state.SetMacro(mac.ToCharArray());

			int currentScore = 0;
			bool ok = true;

			for (int oi = 0; oi < M; oi++) {
				int k = order[oi];
				var (si, sj, ti, tj) = input.Balls[k];

				var pPick = MacroBfs(input, input.Map, state.Robo.i, state.Robo.j, state.Direction,
					si, sj, macroDest, macrodist, macroprev, macroQue, macroStamp,
					ref macroCurrentStamp, globalBestScore - currentScore, routeBuf);
				if (pPick == null) { ok = false; break; }
				currentScore += pPick.Length;
				if (currentScore >= globalBestScore) { ok = false; break; }
				foreach (var op in pPick) state.AddOp(op);
				pickPaths[k] = pPick;
				state.Exchange();

				var pDrop = MacroBfs(input, input.Map, state.Robo.i, state.Robo.j, state.Direction,
					ti, tj, macroDest, macrodist, macroprev, macroQue, macroStamp,
					ref macroCurrentStamp, globalBestScore - currentScore, routeBuf);
				if (pDrop == null) { ok = false; break; }
				currentScore += pDrop.Length;
				if (currentScore >= globalBestScore) { ok = false; break; }
				foreach (var op in pDrop) state.AddOp(op);
				dropPaths[k] = pDrop;
				state.Exchange();
			}

			if (ok && currentScore < globalBestScore) {
				globalBestScore = currentScore;
				globalBestMacro = mac;
				globalBestOrder = (int[])order.Clone();
				globalBestPickPaths = new char[M][];
				globalBestDropPaths = new char[M][];
				for (int k = 0; k < M; k++) {
					globalBestPickPaths[k] = (char[])pickPaths[k].Clone();
					globalBestDropPaths[k] = (char[])dropPaths[k].Clone();
				}
			}
		}

		// ---- Step 4: 最良の (マクロ, 訪問順) で State 構築 ----
		if (globalBestMacro != null) {
			var state = new State(input, isLocal);
			state.SetMacro(globalBestMacro.ToCharArray());
			foreach (int k in globalBestOrder) {
				ApplyPath(state, globalBestPickPaths[k]);
				state.Exchange();
				ApplyPath(state, globalBestDropPaths[k]);
				state.Exchange();
			}
			info.Macro = globalBestMacro;
			info.ML = globalBestMacro.Length;
			return state;
		}

		return Plan3(isLocal, sw, rnd, input, info);
	}

	static State Plan11(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info, State best)
	{
		if (best.Macro == null || best.Macro.Length == 0) {
			return best;
		}

		if (!TryAnalyzePlan11Baseline(isLocal, input, best, out var order, out var snapshots)) {
			return best;
		}

		double deadline = Math.Min(1985.0, input.T - 1);
		if (sw.Elapsed.TotalMilliseconds >= deadline) {
			return best;
		}

		var macroDest = BuildMacroDest(input, best.Macro);
		var bestState = best;
		long bestScore = best.Calculate();

		for (int startIndex = order.Length - 1; startIndex >= 0; startIndex--) {
			if (sw.Elapsed.TotalMilliseconds >= deadline) break;

			for (int qi = order.Length - 1; qi >= startIndex; qi--) {
				if (sw.Elapsed.TotalMilliseconds >= deadline) break;

				int k = order[qi];
				var baseSnapshot = snapshots[startIndex];
				var (si, sj) = baseSnapshot.Balls[k];
				var (ti, tj) = (input.Balls[k].d, input.Balls[k].e);
				if (si < 0 || (si == ti && sj == tj)) continue;

				if (!IsOccupiedByOtherBall(baseSnapshot, input, k, ti, tj)) {
					var snapshot = CloneState(baseSnapshot, input, isLocal);
					if (TryInsertBallMoveToCell(input, snapshot, k, ti, tj, macroDest)) {
						var skipBalls = new bool[input.M];
						skipBalls[k] = true;
						var candidate = BuildPlan11Tail(isLocal, input, snapshot, order, startIndex, skipBalls, macroDest, sw, deadline);
						if (candidate != null) {
							long score = candidate.Calculate();
							if (score < bestScore) {
								bestScore = score;
								bestState = candidate;
							}
						}
					}
				}

				foreach (var relay in EnumeratePlan11RelayTargets(input, baseSnapshot, k, ti, tj, macroDest, 8)) {
					if (sw.Elapsed.TotalMilliseconds >= deadline) break;

					var snapshot = CloneState(baseSnapshot, input, isLocal);
					if (!TryInsertBallMoveToCell(input, snapshot, k, relay.i, relay.j, macroDest)) continue;

					var candidate = BuildPlan11Tail(isLocal, input, snapshot, order, startIndex, new bool[input.M], macroDest, sw, deadline);
					if (candidate == null) continue;

					long score = candidate.Calculate();
					if (score < bestScore) {
						bestScore = score;
						bestState = candidate;
					}
				}
			}
		}

		if (!ReferenceEquals(bestState, best)) {
			info.Up++;
		}

		return bestState;
	}

	static State CloneState(State source, Input input, bool isLocal)
	{
		var clone = new State(input, isLocal) {
			Robo = source.Robo,
			Direction = source.Direction,
			TakeBall = source.TakeBall,
			IsMacro = source.IsMacro,
			Macro = (char[])source.Macro.Clone(),
		};
		for (int k = 0; k < input.M; k++) {
			clone.Balls[k] = source.Balls[k];
		}
		clone.Ops.AddRange(source.Ops);
		clone.NextMacro.AddRange(source.NextMacro);
		return clone;
	}

	static bool TryAnalyzePlan11Baseline(bool isLocal, Input input, State best, out int[] order, out State[] snapshots)
	{
		var replay = new State(input, isLocal);
		var orderList = new List<int>();
		var snapshotList = new List<State>();
		int macroToggle = 0;

		foreach (var op in best.Ops) {
			int prevTake = replay.TakeBall;
			replay.AddOp(op);

			if (op == 'M') {
				macroToggle++;
				if (macroToggle == 2) {
					snapshotList.Add(CloneState(replay, input, isLocal));
				}
				continue;
			}

			if (macroToggle < 2 || op != 'S') continue;

			if (prevTake == -1 && replay.TakeBall >= 0) {
				orderList.Add(replay.TakeBall);
			} else if (prevTake >= 0 && replay.TakeBall == -1) {
				snapshotList.Add(CloneState(replay, input, isLocal));
			} else {
				order = [];
				snapshots = [];
				return false;
			}
		}

		if (orderList.Count == 0 || snapshotList.Count < orderList.Count) {
			order = [];
			snapshots = [];
			return false;
		}

		order = orderList.ToArray();
		if (snapshotList.Count > order.Length) {
			snapshotList.RemoveRange(order.Length, snapshotList.Count - order.Length);
		}
		snapshots = snapshotList.ToArray();
		return snapshots.Length == order.Length;
	}

	static (int i, int j, Direction4 dir, bool ok)[,,] BuildMacroDest(Input input, char[] macro)
	{
		int N = input.N;
		var dirs = new[] { Direction4.D, Direction4.L, Direction4.U, Direction4.R };
		var dest = new (int i, int j, Direction4 dir, bool ok)[N, N, 4];
		for (int i = 0; i < N; i++) {
			for (int j = 0; j < N; j++) {
				foreach (var dir in dirs) {
					int nr = i, nc = j;
					Direction4 nd = dir;
					bool ok = true;
					foreach (var op in macro) {
						switch (op) {
							case 'F':
								if (!input.Map.CanMove(nr, nc, nd)) {
									ok = false;
								} else {
									var mv = nd.Move(nr, nc);
									nr = mv.i;
									nc = mv.j;
								}
								break;
							case 'L':
								nd = nd.Rotate(Rotation.L);
								break;
							case 'R':
								nd = nd.Rotate(Rotation.R);
								break;
						}
						if (!ok) break;
					}
					dest[i, j, dir.ToIndex4()] = (nr, nc, nd, ok);
				}
			}
		}
		return dest;
	}

	static bool IsOccupiedByOtherBall(State state, Input input, int exceptBall, int ti, int tj)
	{
		for (int t = 0; t < input.M; t++) {
			if (t == exceptBall) continue;
			if (state.Balls[t].i == ti && state.Balls[t].j == tj) {
				return true;
			}
		}
		return false;
	}

	static List<(int i, int j)> EnumeratePlan11RelayTargets(Input input, State state, int k, int ti, int tj,
		(int i, int j, Direction4 dir, bool ok)[,,] macroDest, int limit)
	{
		int N = input.N;
		var (si, sj) = state.Balls[k];
		var scores = new Dictionary<int, (int dirCount, int heuristic)>();
		for (int i = 0; i < N; i++) {
			for (int j = 0; j < N; j++) {
				int dirCount = 0;
				for (int d = 0; d < 4; d++) {
					var dest = macroDest[i, j, d];
					if (dest.ok && dest.i == ti && dest.j == tj) {
						dirCount++;
					}
				}
				if (dirCount == 0) continue;
				if (i == ti && j == tj) continue;
				if (i == si && j == sj) continue;
				if (IsOccupiedByOtherBall(state, input, k, i, j)) continue;

				int key = i * N + j;
				int heuristic = Math.Abs(i - si) + Math.Abs(j - sj) + Math.Abs(i - ti) + Math.Abs(j - tj);
				scores[key] = (dirCount, heuristic);
			}
		}

		return scores
			.OrderByDescending(x => x.Value.dirCount)
			.ThenBy(x => x.Value.heuristic)
			.Take(limit)
			.Select(x => (x.Key / N, x.Key % N))
			.ToList();
	}

	static bool TryInsertBallMoveToCell(Input input, State state, int k, int ti, int tj, (int i, int j, Direction4 dir, bool ok)[,,] macroDest)
	{
		int N = input.N;
		var macrodist = new int[N, N, 4];
		var macroprev = new (int i, int j, Direction4 dir, char op)[N, N, 4];
		var macroQue = new Queue<int>();
		var macroStamp = new int[N, N, 4];
		int macroCurrentStamp = 0;
		var routeBuf = new char[N * N * 4 * 8];

		var (si, sj) = state.Balls[k];
		var pPick = MacroBfs(input, input.Map, state.Robo.i, state.Robo.j, state.Direction,
			si, sj, macroDest, macrodist, macroprev, macroQue, macroStamp,
			ref macroCurrentStamp, int.MaxValue / 4, routeBuf);
		if (pPick == null) return false;
		ApplyPath(state, pPick);
		state.Exchange();
		if (state.TakeBall != k) return false;

		var pDrop = MacroBfs(input, input.Map, state.Robo.i, state.Robo.j, state.Direction,
			ti, tj, macroDest, macrodist, macroprev, macroQue, macroStamp,
			ref macroCurrentStamp, int.MaxValue / 4, routeBuf);
		if (pDrop == null) return false;
		ApplyPath(state, pDrop);
		state.Exchange();
		return state.Balls[k].i == ti && state.Balls[k].j == tj && state.TakeBall == -1;
	}

	static State BuildPlan11Tail(bool isLocal, Input input, State prefixState, int[] order, int startIndex, bool[] skipBalls,
		(int i, int j, Direction4 dir, bool ok)[,,] macroDest, Stopwatch sw, double deadline)
	{
		int N = input.N;
		var state = CloneState(prefixState, input, isLocal);
		var macrodist = new int[N, N, 4];
		var macroprev = new (int i, int j, Direction4 dir, char op)[N, N, 4];
		var macroQue = new Queue<int>();
		var macroStamp = new int[N, N, 4];
		int macroCurrentStamp = 0;
		var routeBuf = new char[N * N * 4 * 8];

		for (int oi = startIndex; oi < order.Length; oi++) {
			if (sw.Elapsed.TotalMilliseconds >= deadline) return null;

			int k = order[oi];
			if (skipBalls[k]) continue;

			var (si, sj) = state.Balls[k];
			var (ti, tj) = (input.Balls[k].d, input.Balls[k].e);
			if (si == ti && sj == tj) continue;

			var pPick = MacroBfs(input, input.Map, state.Robo.i, state.Robo.j, state.Direction,
				si, sj, macroDest, macrodist, macroprev, macroQue, macroStamp,
				ref macroCurrentStamp, int.MaxValue / 4, routeBuf);
			if (pPick == null) return null;
			ApplyPath(state, pPick);
			state.Exchange();

			var pDrop = MacroBfs(input, input.Map, state.Robo.i, state.Robo.j, state.Direction,
				ti, tj, macroDest, macrodist, macroprev, macroQue, macroStamp,
				ref macroCurrentStamp, int.MaxValue / 4, routeBuf);
			if (pDrop == null) return null;
			ApplyPath(state, pDrop);
			state.Exchange();
		}

		return state;
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

	public List<char> Ops { get; } = new List<char>();
	public (int i, int j) Robo { get; set; } = (0, 0);
	public Direction4 Direction { get; set; } = Direction4.R;

	public (int i, int j)[] Balls { get; } = [];

	public int TakeBall { get; set; } = -1;

	public bool IsMacro { get; set; } = false;
	public List<char> NextMacro { get; } = new List<char>();
	public char[] Macro { get; set; } = [];

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
	public void SetMacro(char[] macro)
	{
		Macro = (char[])macro.Clone();
		IsMacro = false;
		NextMacro.Clear();
	}

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
	public int Width { get; set; } = 0;
	public int Depth { get; set; } = 0;

	public int MC { get; set; } = 0;
	public int PPM { get; set; } = 0;
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

