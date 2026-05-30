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

		var best = Plan1(isLocal, sw, rnd, input, info);
		info.P = 1;

		/*
		var state3 = Plan3(isLocal, sw, rnd, input, info);
		var best = state3;
		info.P = 3;
		{
			best = Plan2(isLocal, sw, rnd, input, info, best);
		}*/

		var state6 = Plan6(isLocal, sw, rnd, input, info);
		//if (state6.Ops.Count < best.Ops.Count) {
		best = state6;
		info.P = 6;
		//}

		/*
		var state5 = Plan5(isLocal, sw, rnd, input, info);
		if (state5.Ops.Count < best.Ops.Count) {
			best = state5;
			info.P = 5;
		}*/

		input.Output(best);

		info.A = best.Ops.Count;
		info.Score = best.Calculate();
		info.Elapsed = sw.ElapsedMilliseconds;

		return (input, info);
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

	class BeamNode : IComparable<BeamNode>
	{
		public (int i, int j) Robo;
		public Direction4 Dir;
		public int TakeBall; // 手に持っているボールのインデックス (-1なら持っていない)
		public ulong CompletedBalls; // 各ボールがかごに入っているか (ビットフラグ)
		public (int i, int j)[] BallPositions;

		public int OpsCount;
		public int EvalScore;

		public BeamNode Parent;
		public char LastOp;

		public int CompareTo(BeamNode other)
		{
			return EvalScore.CompareTo(other.EvalScore);
		}
	}

	static State Plan6(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		int N = input.N;
		int M = input.M;

		// かごの位置を始点として、向きも考慮した逆BFSでボールごとの距離を前計算する
		// distToGoal[k, i, j, dir] : ボールkのかごに向けた、現在地(i,j) 向きdir からの最短操作数
		int[,,,] distToGoal = new int[M, N, N, 4];
		for (int k = 0; k < M; k++) {
			for (int i = 0; i < N; i++) {
				for (int j = 0; j < N; j++) {
					for (int d = 0; d < 4; d++) {
						distToGoal[k, i, j, d] = 999999;
					}
				}
			}
		}

		for (int k = 0; k < M; k++) {
			int goal_i = input.Balls[k].d;
			int goal_j = input.Balls[k].e;

			var q = new Queue<(int i, int j, int dir)>();
			for (int d = 0; d < 4; d++) {
				distToGoal[k, goal_i, goal_j, d] = 0;
				q.Enqueue((goal_i, goal_j, Direction4Extensions._delta4Dir[d].ToIndex4()));
			}

			while (q.Count > 0) {
				var (ci, cj, cdir) = q.Dequeue();
				int cdist = distToGoal[k, ci, cj, cdir];

				// Fの逆：自分が前のマスにいて、cdirの方向へ進んだら(ci,cj)に来る場合
				// -> 前のマスは(ci, cj)からcdirの逆方向へ1マス移動した位置
				var cdirEnum = Direction4Extensions._delta4Dir[cdir];
				var prevDir = Direction4Extensions.Rotate((Direction4)cdirEnum, Rotation.R).Rotate(Rotation.R); // cdirの180度反転
				int pi = ci + (prevDir == Direction4.D ? 1 : prevDir == Direction4.U ? -1 : 0);
				int pj = cj + (prevDir == Direction4.R ? 1 : prevDir == Direction4.L ? -1 : 0);

				if (pi >= 0 && pi < N && pj >= 0 && pj < N) {
					// 移動可能かチェック: pi,pj から cdir方向へ移動して ci,cj へ行けるか
					if (input.Map.CanMove(pi, pj, cdirEnum)) {
						if (distToGoal[k, pi, pj, cdir] > cdist + 1) {
							distToGoal[k, pi, pj, cdir] = cdist + 1;
							q.Enqueue((pi, pj, cdir));
						}
					}
				}

				// Rの逆：最後にRしたら cdirになった -> ひとつ前は cdir をL回転した向き
				int pdirL = Direction4Extensions.Rotate(cdirEnum, Rotation.L).ToIndex4();
				if (distToGoal[k, ci, cj, pdirL] > cdist + 1) {
					distToGoal[k, ci, cj, pdirL] = cdist + 1;
					q.Enqueue((ci, cj, pdirL));
				}

				// Lの逆：最後にLしたら cdirになった -> ひとつ前は cdir をR回転した向き
				int pdirR = Direction4Extensions.Rotate(cdirEnum, Rotation.R).ToIndex4();
				if (distToGoal[k, ci, cj, pdirR] > cdist + 1) {
					distToGoal[k, ci, cj, pdirR] = cdist + 1;
					q.Enqueue((ci, cj, pdirR));
				}
			}
		}

		// ボール同士の距離などは適宜簡易計算とするために、単純なBFS距離も残しておく(ロボット->ボールなどのため)
		int[,] rawDist = new int[N * N, N * N];
		for (int i = 0; i < N * N; i++) {
			for (int j = 0; j < N * N; j++) {
				rawDist[i, j] = 999999;
			}
			rawDist[i, i] = 0;
		}

		for (int r = 0; r < N; r++) {
			for (int c = 0; c < N; c++) {
				int start = r * N + c;
				var q = new Queue<int>();
				q.Enqueue(start);
				while (q.Count > 0) {
					int u = q.Dequeue();
					int ui = u / N;
					int uj = u % N;
					var moves = input.Map.Moveables[ui, uj];
					foreach (var d in moves) {
						var (nr, nc) = d.Move(ui, uj);
						int v = nr * N + nc;
						if (rawDist[start, v] > rawDist[start, u] + 1) {
							rawDist[start, v] = rawDist[start, u] + 1;
							q.Enqueue(v);
						}
					}
				}
			}
		}

		BeamNode initialNode = new BeamNode {
			Robo = (0, 0),
			Dir = Direction4.R,
			TakeBall = -1,
			CompletedBalls = 0UL,
			BallPositions = new (int i, int j)[M],
			OpsCount = 0,
			Parent = null,
			LastOp = '\0'
		};

		for (int k = 0; k < M; k++) {
			initialNode.BallPositions[k] = (input.Balls[k].b, input.Balls[k].c);
		}

		int ComputeEvalScore(BeamNode node)
		{
			int score = node.OpsCount;
			int ballTargetCost = 0;
			int minDistToAnyBall = int.MaxValue;
			int roboPos = node.Robo.i * N + node.Robo.j;
			int completedCount = 0;

			for (int k = 0; k < M; k++) {
				if ((node.CompletedBalls & (1UL << k)) != 0) {
					completedCount++;
					continue;
				}

				if (node.TakeBall == k) {
					// 持っているなら現在のかごへの最短距離を直接足す（向き考慮）
					ballTargetCost += distToGoal[k, node.Robo.i, node.Robo.j, node.Dir.ToIndex4()];
					minDistToAnyBall = 0; // 手にあるので拾いに行く距離は0
					score -= 50; // 持っている状態を少しだけ優遇
				} else {
					var bPos = node.BallPositions[k];
					int bp = bPos.i * N + bPos.j;

					// 置いてあるボールの場合は、向きが不定なのでとりあえず最小コストを採用するか、適当な方向の平均等を考える
					// 実際にはロボットが拾い上げる時の向きになるが、ここでは4方向の最小値をベースの持ち運びコストとする
					int minD = int.MaxValue;
					for (int d = 0; d < 4; d++) {
						if (distToGoal[k, bPos.i, bPos.j, d] < minD) {
							minD = distToGoal[k, bPos.i, bPos.j, d];
						}
					}
					ballTargetCost += minD;

					int dToBall = rawDist[roboPos, bp];
					if (dToBall < minDistToAnyBall) {
						minDistToAnyBall = dToBall;
					}
				}
			}

			if (minDistToAnyBall == int.MaxValue) minDistToAnyBall = 0;
			if (node.TakeBall != -1) minDistToAnyBall = 0;

			// すべての各ボールからかごへの距離の和(ballTargetCost) を最優先し、
			// 補助的に直近の操作対象への距離(minDistToAnyBall) を足す
			return score - completedCount * 10000 + ballTargetCost * 10 + minDistToAnyBall * 2;
		}

		initialNode.EvalScore = ComputeEvalScore(initialNode);

		var beam = new List<BeamNode> { initialNode };
		int beamWidth = 1000;
		double timeLimit = 1900.0;
		if (isLocal) {
			beamWidth = 500;
			timeLimit = 1900.0;
		}

		BeamNode bestGoal = null;

		int step = 0;
		while (beam.Count > 0 && sw.Elapsed.TotalMilliseconds < timeLimit && step < input.T) {
			step++;
			var nextBeam = new List<BeamNode>();
			var seen = new HashSet<ulong>(); // Zobrist Hashなどを使うのが理想だが、ここでは簡易化

			foreach (var node in beam) {
				++info.Loop;
				// すでに全部かごに入っているか判定
				if (node.CompletedBalls == (1UL << M) - 1) {
					if (bestGoal == null || node.OpsCount < bestGoal.OpsCount) {
						++info.Up;
						bestGoal = node;
					}
					continue;
				}

				// F: 前進
				if (input.Map.CanMove(node.Robo.i, node.Robo.j, node.Dir)) {
					var nextRobo = node.Dir.Move(node.Robo.i, node.Robo.j);
					var nextNode = new BeamNode {
						Robo = nextRobo,
						Dir = node.Dir,
						TakeBall = node.TakeBall,
						CompletedBalls = node.CompletedBalls,
						BallPositions = (ValueTuple<int, int>[])node.BallPositions.Clone(),
						OpsCount = node.OpsCount + 1,
						Parent = node,
						LastOp = 'F'
					};
					// 持っているボールがあれば座標を現在地に更新（持っている判定はTakeBallでしているため必須ではない）
					nextNode.EvalScore = ComputeEvalScore(nextNode);
					nextBeam.Add(nextNode);
				}

				// L, R: 回転 (直前の逆回転=元に戻るだけの操作を枝刈り)
				if (node.LastOp != 'R') { // 直前がRならLは逆回転で無駄
					var nextL = new BeamNode {
						Robo = node.Robo,
						Dir = node.Dir.Rotate(Rotation.L),
						TakeBall = node.TakeBall,
						CompletedBalls = node.CompletedBalls,
						BallPositions = (ValueTuple<int, int>[])node.BallPositions.Clone(),
						OpsCount = node.OpsCount + 1,
						Parent = node,
						LastOp = 'L'
					};
					nextL.EvalScore = ComputeEvalScore(nextL);
					nextBeam.Add(nextL);
				}

				if (node.LastOp != 'L') { // 直前がLならRは逆回転で無駄
					var nextR = new BeamNode {
						Robo = node.Robo,
						Dir = node.Dir.Rotate(Rotation.R),
						TakeBall = node.TakeBall,
						CompletedBalls = node.CompletedBalls,
						BallPositions = (ValueTuple<int, int>[])node.BallPositions.Clone(),
						OpsCount = node.OpsCount + 1,
						Parent = node,
						LastOp = 'R'
					};
					nextR.EvalScore = ComputeEvalScore(nextR);
					nextBeam.Add(nextR);
				}

				// S: 交換
				if (node.LastOp != 'S') {
					var nextS = new BeamNode {
						Robo = node.Robo,
						Dir = node.Dir,
						TakeBall = -1,
						CompletedBalls = node.CompletedBalls,
						BallPositions = (ValueTuple<int, int>[])node.BallPositions.Clone(),
						OpsCount = node.OpsCount + 1,
						Parent = node,
						LastOp = 'S'
					};

					int take = node.TakeBall;
					int currentTake = -1;
					bool hasBallHere = false;
					for (int k = 0; k < M; k++) {
						if (nextS.BallPositions[k].i == node.Robo.i && nextS.BallPositions[k].j == node.Robo.j) {
							hasBallHere = true;
							// すでに完了しているボールを拾い上げるだけのSは枝刈りしたいが、
							// 盤面によっては除ける必要があるので緩くする。ただし、かごの位置なら拾わない方がよい。
							currentTake = k;
							nextS.BallPositions[k] = (-1, -1);
							break;
						}
					}

					// 枝刈り: 手ぶらで、かつ現在地にボールが無い場合はSの無駄
					if (take == -1 && !hasBallHere) {
						goto SkipS;
					}

					nextS.TakeBall = currentTake;

					if (take >= 0) {
						nextS.BallPositions[take] = (node.Robo.i, node.Robo.j);
						if (node.Robo.i == input.Balls[take].d && node.Robo.j == input.Balls[take].e) {
							nextS.CompletedBalls |= (1UL << take);
						} else {
							nextS.CompletedBalls &= ~(1UL << take);
						}
					}

					// 枝刈り: 完了したボールを再度拾うようなSはペナルティまたは除外
					if (currentTake >= 0 && (node.Robo.i == input.Balls[currentTake].d && node.Robo.j == input.Balls[currentTake].e)) {
						goto SkipS; // 既に完了している場所から拾うのは基本NG
					}

					nextS.EvalScore = ComputeEvalScore(nextS);
					nextBeam.Add(nextS);
				}
SkipS:;
			}

			// 枝刈りとソート
			nextBeam.Sort();
			if (nextBeam.Count > beamWidth) {
				nextBeam.RemoveRange(beamWidth, nextBeam.Count - beamWidth);
			}

			beam = nextBeam;
		}

		if (bestGoal == null && beam.Count > 0) {
			bestGoal = beam[0];
		}

		var finalState = new State(input, isLocal);
		if (bestGoal != null) {
			var opsList = new List<char>();
			var curr = bestGoal;
			while (curr.Parent != null) {
				opsList.Add(curr.LastOp);
				curr = curr.Parent;
			}
			opsList.Reverse();
			foreach (var c in opsList) {
				finalState.AddOp(c);
			}
		}

		return finalState;
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
