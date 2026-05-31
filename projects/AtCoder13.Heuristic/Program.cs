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
						$"W={info.Width:d4}",
						$"D={info.Depth:d7}",
						//$"ML={info.ML:d3}",
						$"RA={info.RA:d5}",
						$"A={info.A:d4}",
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

		//var state6 = Plan6(isLocal, sw, rnd, input, info);
		//if (state6.Ops.Count < best.Ops.Count) {
		//best = state6;
		//info.P = 6;
		//}

		/*
		var state8 = Plan8(isLocal, sw, rnd, input, info);
		if (state8.Ops.Count < best.Ops.Count) {
			best = state8;
			info.P = 8;
		}*/

		var state9 = Plan9(isLocal, sw, rnd, input, info);
		if (state9.Ops.Count < best.Ops.Count) {
			best = state9;
			info.P = 9;
		}

		var state11 = Plan11(isLocal, sw, rnd, input, info, state9);
		if (state11.Ops.Count < best.Ops.Count) {
			best = state11;
			info.P = 11;
		}

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

	// Plan7用: 動的マクロ状態を持つビームノード
	class BeamNode7 : IComparable<BeamNode7>
	{
		public (int i, int j) Robo;
		public Direction4 Dir;
		public int TakeBall;         // 手に持っているボールのインデックス (-1なら持っていない)
		public ulong CompletedBalls; // 各ボールがかごに入っているか (ビットフラグ)
		public (int i, int j)[] BallPositions;

		// MacroState: 0=未作成, 1=記録中, 2=完成
		public int MacroState;
		public string MacroContent; // 記録済み/記録中のマクロ文字列

		public int OpsCount;
		public int EvalScore;

		public BeamNode7 Parent;
		public char LastOp;

		public int CompareTo(BeamNode7 other)
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

		// マクロ選定（1回のみ）: 最短マクロを選ぶ
		var allMacros6 = GenerateMacros().OrderBy(x => x.Length).ToList();
		var dirs6 = new[] { Direction4.D, Direction4.L, Direction4.U, Direction4.R };
		string bestMacro6 = null;
		(int i, int j, Direction4 dir, bool ok)[,,] macroDest6 = null;
		foreach (var candidate in allMacros6) {
			var dest = new (int i, int j, Direction4 dir, bool ok)[N, N, 4];
			bool anyOk = false;
			for (int i = 0; i < N; i++) {
				for (int j = 0; j < N; j++) {
					foreach (var dir in dirs6) {
						int nr = i, nc = j;
						Direction4 nd = dir;
						bool mOk = true;
						foreach (var op in candidate) {
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
						dest[i, j, dir.ToIndex4()] = (nr, nc, nd, mOk);
						if (mOk) anyOk = true;
					}
				}
			}
			if (anyOk) {
				bestMacro6 = candidate;
				macroDest6 = dest;
				break;
			}
		}
		int macroOpsCost6 = bestMacro6 != null ? bestMacro6.Length : 0;
		info.Macro = bestMacro6 ?? "";

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
		// ビーム幅: ノード1個あたりの処理コストが M*N に比例するため逆数スケーリング
		int beamWidth = Math.Max(200, 250000 / (M * N));
		if (isLocal) beamWidth = Math.Max(100, beamWidth / 2);
		double timeLimit = 1900.0;
		info.Width = beamWidth;

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

				// P: マクロ実行
				if (bestMacro6 != null && node.LastOp != 'P') {
					var md = macroDest6[node.Robo.i, node.Robo.j, node.Dir.ToIndex4()];
					if (md.ok) {
						var nextP = new BeamNode {
							Robo = (md.i, md.j),
							Dir = md.dir,
							TakeBall = node.TakeBall,
							CompletedBalls = node.CompletedBalls,
							BallPositions = (ValueTuple<int, int>[])node.BallPositions.Clone(),
							OpsCount = node.OpsCount + 1,
							Parent = node,
							LastOp = 'P'
						};
						nextP.EvalScore = ComputeEvalScore(nextP);
						nextBeam.Add(nextP);
					}
				}
			} // foreach node

			if (bestGoal != null) break;

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
		info.Depth = step;

		var finalState = new State(input, isLocal);
		if (bestGoal != null) {
			var opsList = new List<char>();
			var curr = bestGoal;
			while (curr.Parent != null) {
				opsList.Add(curr.LastOp);
				curr = curr.Parent;
			}
			opsList.Reverse();

			// マクロを使用している場合、最初の 'P' を 'M' + bestMacro6 + 'M' に置換する
			if (bestMacro6 != null && opsList.Contains('P')) {
				int firstP = opsList.IndexOf('P');
				var newOps = new List<char>();
				for (int i = 0; i < firstP; i++) newOps.Add(opsList[i]);
				newOps.Add('M');
				foreach (var c in bestMacro6) newOps.Add(c);
				newOps.Add('M');
				for (int i = firstP + 1; i < opsList.Count; i++) newOps.Add(opsList[i]);
				opsList = newOps;
			}

			foreach (var c in opsList) {
				finalState.AddOp(c);
			}
		}

		return finalState;
	}

	static State Plan7(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		int N = input.N;
		int M = input.M;

		// Plan6と同じ逆BFS前計算
		int[,,,] distToGoal = new int[M, N, N, 4];
		for (int k = 0; k < M; k++) {
			for (int i = 0; i < N; i++)
				for (int j = 0; j < N; j++)
					for (int d = 0; d < 4; d++)
						distToGoal[k, i, j, d] = 999999;
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
				var cdirEnum = Direction4Extensions._delta4Dir[cdir];
				var prevDir = Direction4Extensions.Rotate((Direction4)cdirEnum, Rotation.R).Rotate(Rotation.R);
				int pi = ci + (prevDir == Direction4.D ? 1 : prevDir == Direction4.U ? -1 : 0);
				int pj = cj + (prevDir == Direction4.R ? 1 : prevDir == Direction4.L ? -1 : 0);
				if (pi >= 0 && pi < N && pj >= 0 && pj < N) {
					if (input.Map.CanMove(pi, pj, cdirEnum)) {
						if (distToGoal[k, pi, pj, cdir] > cdist + 1) {
							distToGoal[k, pi, pj, cdir] = cdist + 1;
							q.Enqueue((pi, pj, cdir));
						}
					}
				}
				int pdirL = Direction4Extensions.Rotate(cdirEnum, Rotation.L).ToIndex4();
				if (distToGoal[k, ci, cj, pdirL] > cdist + 1) {
					distToGoal[k, ci, cj, pdirL] = cdist + 1;
					q.Enqueue((ci, cj, pdirL));
				}
				int pdirR = Direction4Extensions.Rotate(cdirEnum, Rotation.R).ToIndex4();
				if (distToGoal[k, ci, cj, pdirR] > cdist + 1) {
					distToGoal[k, ci, cj, pdirR] = cdist + 1;
					q.Enqueue((ci, cj, pdirR));
				}
			}
		}

		int[,] rawDist = new int[N * N, N * N];
		for (int i = 0; i < N * N; i++) {
			for (int j = 0; j < N * N; j++) rawDist[i, j] = 999999;
			rawDist[i, i] = 0;
		}
		for (int r = 0; r < N; r++) {
			for (int c = 0; c < N; c++) {
				int start = r * N + c;
				var q = new Queue<int>();
				q.Enqueue(start);
				while (q.Count > 0) {
					int u = q.Dequeue();
					int ui = u / N, uj = u % N;
					foreach (var d in input.Map.Moveables[ui, uj]) {
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

		// マクロを実行したときの遷移先を計算するキャッシュ (macroStr -> dest[N,N,4])
		var macroDestCache = new Dictionary<string, (int i, int j, Direction4 dir, bool ok)[,,]>();
		(int i, int j, Direction4 dir, bool ok)[,,] GetMacroDest(string mac)
		{
			if (macroDestCache.TryGetValue(mac, out var cached)) return cached;
			var dest = new (int i, int j, Direction4 dir, bool ok)[N, N, 4];
			for (int i = 0; i < N; i++) {
				for (int j = 0; j < N; j++) {
					foreach (var dir in new[] { Direction4.D, Direction4.L, Direction4.U, Direction4.R }) {
						int nr = i, nc = j;
						Direction4 nd = dir;
						bool mOk = true;
						foreach (var op in mac) {
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
						dest[i, j, dir.ToIndex4()] = (nr, nc, nd, mOk);
					}
				}
			}
			macroDestCache[mac] = dest;
			return dest;
		}

		const int MACRO_MIN = 3;
		const int MACRO_MAX = 10;

		// 事前計算: 各セルへの「任意の籠から来られる最短距離」(= 籠→ボールコスト下限)
		int[] minDistFromAnyGoal = new int[N * N];
		for (int pos = 0; pos < N * N; pos++) minDistFromAnyGoal[pos] = 999999;
		for (int k = 0; k < M; k++) {
			int gp = input.Balls[k].d * N + input.Balls[k].e;
			for (int pos = 0; pos < N * N; pos++) {
				if (rawDist[gp, pos] < minDistFromAnyGoal[pos])
					minDistFromAnyGoal[pos] = rawDist[gp, pos];
			}
		}

		BeamNode7 initialNode = new BeamNode7 {
			Robo = (0, 0),
			Dir = Direction4.R,
			TakeBall = -1,
			CompletedBalls = 0UL,
			BallPositions = new (int i, int j)[M],
			MacroState = 0,
			MacroContent = "",
			OpsCount = 0,
			Parent = null,
			LastOp = '\0'
		};
		for (int k = 0; k < M; k++) {
			initialNode.BallPositions[k] = (input.Balls[k].b, input.Balls[k].c);
		}

		int ComputeEvalScore7(BeamNode7 node)
		{
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
					ballTargetCost += distToGoal[k, node.Robo.i, node.Robo.j, node.Dir.ToIndex4()];
					minDistToAnyBall = 0;
				} else {
					var bPos = node.BallPositions[k];
					int bp = bPos.i * N + bPos.j;
					int minD = int.MaxValue;
					for (int d = 0; d < 4; d++) {
						if (distToGoal[k, bPos.i, bPos.j, d] < minD) minD = distToGoal[k, bPos.i, bPos.j, d];
					}
					ballTargetCost += minD;
					int dToBall = rawDist[roboPos, bp];
					if (dToBall < minDistToAnyBall) minDistToAnyBall = dToBall;
				}
			}

			if (minDistToAnyBall == int.MaxValue) minDistToAnyBall = 0;
			if (node.TakeBall != -1) minDistToAnyBall = 0;

			int effectiveOps = node.OpsCount;
			if (node.MacroState == 1 || node.MacroState == 2) {
				effectiveOps -= (2 + node.MacroContent.Length);
			}

			return effectiveOps
				- completedCount * 10000
				+ ballTargetCost * 8
				+ minDistToAnyBall * 5;
		}

		initialNode.EvalScore = ComputeEvalScore7(initialNode);

		var beam = new List<BeamNode7> { initialNode };
		// Plan7 は遷移種類が多い (M開始/終了/P) ので Plan6 より少し小さく
		int beamWidth = Math.Max(150, 150000 / (M * N));
		if (isLocal) beamWidth = Math.Max(75, beamWidth / 2);
		double timeLimit = 1900.0;
		info.Width = beamWidth;

		BeamNode7 bestGoal = null;

		int step7 = 0;
		while (beam.Count > 0 && sw.Elapsed.TotalMilliseconds < timeLimit && step7 < input.T) {
			step7++;
			var nextBeam = new List<BeamNode7>();

			foreach (var node in beam) {
				++info.Loop;
				if (node.CompletedBalls == (1UL << M) - 1) {
					if (bestGoal == null || node.OpsCount < bestGoal.OpsCount) {
						++info.Up;
						bestGoal = node;
					}
					continue;
				}

				// ---- F: 前進 ----
				if (input.Map.CanMove(node.Robo.i, node.Robo.j, node.Dir)
					&& !(node.MacroState == 1 && node.MacroContent.Length >= MACRO_MAX)) {
					var nextRobo = node.Dir.Move(node.Robo.i, node.Robo.j);
					var nf = new BeamNode7 {
						Robo = nextRobo,
						Dir = node.Dir,
						TakeBall = node.TakeBall,
						CompletedBalls = node.CompletedBalls,
						BallPositions = (ValueTuple<int, int>[])node.BallPositions.Clone(),
						MacroState = node.MacroState,
						MacroContent = node.MacroState == 1 ? node.MacroContent + "F" : node.MacroContent,
						OpsCount = node.OpsCount + 1,
						Parent = node,
						LastOp = 'F'
					};
					nf.EvalScore = ComputeEvalScore7(nf);
					nextBeam.Add(nf);
				}

				// ---- L: 左回転 ----
				if (node.LastOp != 'R'
					&& !(node.MacroState == 1 && node.MacroContent.Length >= MACRO_MAX)) {
					var nl = new BeamNode7 {
						Robo = node.Robo,
						Dir = node.Dir.Rotate(Rotation.L),
						TakeBall = node.TakeBall,
						CompletedBalls = node.CompletedBalls,
						BallPositions = (ValueTuple<int, int>[])node.BallPositions.Clone(),
						MacroState = node.MacroState,
						MacroContent = node.MacroState == 1 ? node.MacroContent + "L" : node.MacroContent,
						OpsCount = node.OpsCount + 1,
						Parent = node,
						LastOp = 'L'
					};
					nl.EvalScore = ComputeEvalScore7(nl);
					nextBeam.Add(nl);
				}

				// ---- R: 右回転 ----
				if (node.LastOp != 'L'
					&& !(node.MacroState == 1 && node.MacroContent.Length >= MACRO_MAX)) {
					var nr2 = new BeamNode7 {
						Robo = node.Robo,
						Dir = node.Dir.Rotate(Rotation.R),
						TakeBall = node.TakeBall,
						CompletedBalls = node.CompletedBalls,
						BallPositions = (ValueTuple<int, int>[])node.BallPositions.Clone(),
						MacroState = node.MacroState,
						MacroContent = node.MacroState == 1 ? node.MacroContent + "R" : node.MacroContent,
						OpsCount = node.OpsCount + 1,
						Parent = node,
						LastOp = 'R'
					};
					nr2.EvalScore = ComputeEvalScore7(nr2);
					nextBeam.Add(nr2);
				}

				// ---- S: 交換 ----
				if (node.LastOp != 'S' && node.MacroState != 1) { // 記録中はS不可
					var ns = new BeamNode7 {
						Robo = node.Robo,
						Dir = node.Dir,
						TakeBall = -1,
						CompletedBalls = node.CompletedBalls,
						BallPositions = (ValueTuple<int, int>[])node.BallPositions.Clone(),
						MacroState = node.MacroState,
						MacroContent = node.MacroContent,
						OpsCount = node.OpsCount + 1,
						Parent = node,
						LastOp = 'S'
					};
					int take = node.TakeBall;
					int currentTake = -1;
					bool hasBallHere = false;
					for (int k = 0; k < M; k++) {
						if (ns.BallPositions[k].i == node.Robo.i && ns.BallPositions[k].j == node.Robo.j) {
							hasBallHere = true;
							currentTake = k;
							ns.BallPositions[k] = (-1, -1);
							break;
						}
					}
					if (take == -1 && !hasBallHere) goto SkipS7;
					if (currentTake >= 0 && (node.Robo.i == input.Balls[currentTake].d && node.Robo.j == input.Balls[currentTake].e)) goto SkipS7;
					ns.TakeBall = currentTake;
					if (take >= 0) {
						ns.BallPositions[take] = (node.Robo.i, node.Robo.j);
						if (node.Robo.i == input.Balls[take].d && node.Robo.j == input.Balls[take].e) {
							ns.CompletedBalls |= (1UL << take);
						} else {
							ns.CompletedBalls &= ~(1UL << take);
						}
					}
					ns.EvalScore = ComputeEvalScore7(ns);
					nextBeam.Add(ns);
				}
SkipS7:;

				// ---- M: マクロ記録開始 (MacroState==0 のときのみ) ----
				if (node.MacroState == 0) {
					var nm = new BeamNode7 {
						Robo = node.Robo,
						Dir = node.Dir,
						TakeBall = node.TakeBall,
						CompletedBalls = node.CompletedBalls,
						BallPositions = (ValueTuple<int, int>[])node.BallPositions.Clone(),
						MacroState = 1,
						MacroContent = "",
						OpsCount = node.OpsCount + 1,
						Parent = node,
						LastOp = 'M'
					};
					nm.EvalScore = ComputeEvalScore7(nm);
					nextBeam.Add(nm);
				}

				// ---- M: マクロ記録終了 (記録中かつMACRO_MIN以上またはMACRO_MAX到達) ----
				if (node.MacroState == 1 && (node.MacroContent.Length >= MACRO_MIN || node.MacroContent.Length >= MACRO_MAX)) {
					var nm = new BeamNode7 {
						Robo = node.Robo,
						Dir = node.Dir,
						TakeBall = node.TakeBall,
						CompletedBalls = node.CompletedBalls,
						BallPositions = (ValueTuple<int, int>[])node.BallPositions.Clone(),
						MacroState = 2,
						MacroContent = node.MacroContent,
						OpsCount = node.OpsCount + 1,
						Parent = node,
						LastOp = 'M'
					};
					nm.EvalScore = ComputeEvalScore7(nm);
					nextBeam.Add(nm);
				}

				// ---- P: マクロ実行 (MacroState==2 かつ直前がPでない) ----
				if (node.MacroState == 2 && node.LastOp != 'P') {
					var dest = GetMacroDest(node.MacroContent);
					var md = dest[node.Robo.i, node.Robo.j, node.Dir.ToIndex4()];
					if (md.ok) {
						// P実行でボールが一緒に動く（持っているボールを移動）
						var newBalls = (ValueTuple<int, int>[])node.BallPositions.Clone();
						// 持っているボールはロボットと一緒に動く(位置は変わらない: TakeBallで管理)
						var np = new BeamNode7 {
							Robo = (md.i, md.j),
							Dir = md.dir,
							TakeBall = node.TakeBall,
							CompletedBalls = node.CompletedBalls,
							BallPositions = newBalls,
							MacroState = 2,
							MacroContent = node.MacroContent,
							OpsCount = node.OpsCount + 1,
							Parent = node,
							LastOp = 'P'
						};
						// Pの結果として現在地にボールが来たら完了判定
						if (node.TakeBall >= 0) {
							int kb = node.TakeBall;
							if (md.i == input.Balls[kb].d && md.j == input.Balls[kb].e) {
								// SをP後に打つことになるが、評価上は到達分の加点のみ
							}
						}
						np.EvalScore = ComputeEvalScore7(np);
						nextBeam.Add(np);
					}
				}
			} // foreach node

			if (bestGoal != null) break;

			nextBeam.Sort();
			if (nextBeam.Count > beamWidth) {
				nextBeam.RemoveRange(beamWidth, nextBeam.Count - beamWidth);
			}
			beam = nextBeam;
		}

		if (bestGoal == null && beam.Count > 0) {
			bestGoal = beam[0];
		}

		info.Depth = step7;
		// 最終的なマクロを info.Macro に記録
		if (bestGoal != null && bestGoal.MacroState == 2) {
			info.Macro = bestGoal.MacroContent;
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

			// opsListには M(開始) ... M(終了) ... P ... の形で入っているので
			// State.AddOp がそのまま M/P のセマンティクスを処理してくれる
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

	static State Plan8(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		int N = input.N;
		int M = input.M;

		// マクロ候補：直進 + 両端ターン + 先頭/末尾ターン + 中間ターン
		var macroList = new[] {
			// 純直進
			"FF", "FFF", "FFFF", "FFFFF", "FFFFFF", "FFFFFFF",
			// 両端ターン（最多パターン）長さ5〜7
			"LFFFR", "RFFFL",
			"LFFFFR", "RFFFFL",
			"LLFFFR", "RRFFFL",
			"LFFFRR", "RFFFLL",
			"LLFFFFL", "RRFFFFR",
			"LFFFFFL", "RFFFFFL",
			// 先頭/末尾のみターン
			"LFFFFF", "RFFFFF",
			"FFFFFL", "FFFFFR",
			"FFFFFFL", "FFFFFFR",
			"LLFFFFF", "RRFFFFF",
			// 中間ターン
			"FFLFFF", "FFRFFF",
			"FFFLFF", "FFFRFF",
			"FFL", "FFR", "FFFL", "FFFR",
			// 長さ8: 両端ターン
			"LFFFFFR", "RFFFFFL",
			"LLFFFFR", "RRFFFFL",
			"LFFFFRR", "RFFFFLL",
			// 長さ8: 先頭/末尾ターン
			"LFFFFFF", "RFFFFFF",
			"FFFFFFFL", "FFFFFFFR",
			// 長さ8: 中間ターン
			"FFFLFFF", "FFFRFFF",
			// 長さ9: 両端ターン
			"LFFFFFFR", "RFFFFFFL",
			"LLFFFFR", "RRFFFFL",
			"LFFFFFR", "RFFFFFLL",
			// 長さ9: 先頭/末尾ターン
			"LFFFFFFF", "RFFFFFFF",
			"FFFFFFFFL", "FFFFFFFFR",
			// 長さ10: 両端ターン
			"LFFFFFFFR", "RFFFFFFFL",
			// 長さ10: 先頭/末尾ターン
			"LFFFFFFFF", "RFFFFFFFF",
		};
		var dirs = new[] { Direction4.D, Direction4.L, Direction4.U, Direction4.R };

		// 各マクロの macroDest を事前計算（F/L/R すべて対応）
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
									if (!input.Map.CanMove(nr, nc, nd)) { mOk = false; } else { var mv = nd.Move(nr, nc); nr = mv.i; nc = mv.j; }
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

		// BFS スクラッチバッファ（前計算・2-opt・最終パス構築 共用）
		var macrodist = new int[N, N, 4];
		var macroprev = new (int i, int j, Direction4 dir, char op)[N, N, 4];
		var macroQue = new Queue<int>();
		var macroStamp = new int[N, N, 4];
		int macroCurrentStamp = 0;
		var routeBuf = new char[N * N * 4 * 8];

		// キーノード: origin(0), pick(1..M), drop(M+1..2M)
		int K = 1 + M * 2;
		(int i, int j)[] keyPos = new (int, int)[K];
		keyPos[0] = (0, 0);
		for (int k = 0; k < M; k++) {
			keyPos[1 + k] = (input.Balls[k].b, input.Balls[k].c);
			keyPos[1 + M + k] = (input.Balls[k].d, input.Balls[k].e);
		}

		// マクロ毎にキーノード間の最短距離行列を前計算
		// nodeDist[from, fromDirIdx, to] = 最小コスト
		// nodeArrDir[from, fromDirIdx, to] = to 到着時の方向index
		double precompDeadline = Math.Min(sw.Elapsed.TotalMilliseconds + 800.0, 1200.0);
		var nodeDistAll = new int[macroList.Length][,,];
		var nodeArrDirAll = new int[macroList.Length][,,];
		var macroEnabled = new bool[macroList.Length];

		var fullDist = new int[N, N, 4];
		var fullStamp = new int[N, N, 4];
		int fullStampVal = 0;

		for (int mi = 0; mi < macroList.Length; mi++) {
			if (mi > 0 && sw.Elapsed.TotalMilliseconds > precompDeadline) break;
			var macroDest = macroDestAll[mi];
			var nodeDist = new int[K, 4, K];
			var nodeArrDir = new int[K, 4, K];
			for (int a = 0; a < K; a++)
				for (int d = 0; d < 4; d++)
					for (int b = 0; b < K; b++)
						nodeDist[a, d, b] = (a == b) ? 0 : int.MaxValue / 2;

			// from キーノード × 4方向 の個別単一始点 BFS
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

					// 各 to キーノードへの最小コストと到着方向を取り出す
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

		// 前計算テーブルで order の巡回コストを方向追跡しながら O(M) で計算
		int CalcOrderCost(int[] order, int[,,] nd, int[,,] nad)
		{
			int cost = 0;
			int curNode = 0;
			int curDir = Direction4.D.ToIndex4(); // 初期方向はD
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

		// 貪欲初期順列（方向追跡あり）
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

		// 各マクロのベスト訪問順とコストを保持
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

		// ---- 2-opt（前計算テーブルで方向追跡 O(M) 評価、絶対時刻 1700ms まで）----
		// 時間経過とともに有望なマクロに絞り込む
		// 前計算完了時点の有効マクロ数を記録（W=絞り込み前）
		info.Width = 0;
		for (int i = 0; i < macroList.Length; i++)
			if (macroEnabled[i]) info.Width++;

		// 焼き鈍し: 初期温度は有効マクロの初期コスト平均の 10%、上限 30、終了温度 0.01
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
		// W=初期温度(整数), D=SA悪化受理回数
		info.Width = (int)initTemp;
		int saAccept = 0;

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

			// 一定間隔で絞り込み判定
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

			int p1 = rnd.Next(M);
			int p2 = rnd.Next(M);
			if (p1 == p2) continue;
			if (p1 > p2) (p1, p2) = (p2, p1);

			Array.Reverse(order, p1, p2 - p1 + 1);
			int newCost = CalcOrderCost(order, nd, nad);
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
			state.AddOp('M');
			foreach (var c in mac) state.AddOp(c);
			state.AddOp('M');

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
				//++info.Up;
			}
		}

		// ---- Step 4: 最良の (マクロ, 訪問順) で State 構築 ----
		if (globalBestMacro != null) {
			var state = new State(input, isLocal);
			state.AddOp('M');
			foreach (var c in globalBestMacro) state.AddOp(c);
			state.AddOp('M');
			foreach (int k in globalBestOrder) {
				ApplyPath(state, globalBestPickPaths[k]);
				state.Exchange();
				ApplyPath(state, globalBestDropPaths[k]);
				state.Exchange();
			}
			info.RA = state.Ops.Count - (globalBestMacro.Length + 2 + globalBestScore);
			info.Macro = globalBestMacro;
			info.ML = globalBestMacro.Length;
			return state;
		}

		return Plan3(isLocal, sw, rnd, input, info);
	}

	static State Plan9(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		int N = input.N;
		int M = input.M;

		var macroList = new[] {
			// 純直進
			"FF", "FFF", "FFFF", "FFFFF", "FFFFFF", "FFFFFFF",
			// 両端ターン 長さ5〜7
			"LFFFR", "RFFFL",
			"LFFFFR", "RFFFFL",
			"LLFFFR", "RRFFFL",
			"LFFFRR", "RFFFLL",
			"LLFFFFL", "RRFFFFR",
			"LFFFFFL", "RFFFFFL",
			// 先頭/末尾のみターン
			"LFFFFF", "RFFFFF",
			"FFFFFL", "FFFFFR",
			"FFFFFFL", "FFFFFFR",
			"LLFFFFF", "RRFFFFF",
			// 中間ターン
			"FFLFFF", "FFRFFF",
			"FFFLFF", "FFFRFF",
			"FFL", "FFR", "FFFL", "FFFR",
			// 長さ8
			"LFFFFFR", "RFFFFFL",
			"LLFFFFR", "RRFFFFL",
			"LFFFFRR", "RFFFFLL",
			"LFFFFFF", "RFFFFFF",
			"FFFFFFFL", "FFFFFFFR",
			"FFFLFFF", "FFFRFFF",
			// 長さ9
			"LFFFFFFR", "RFFFFFFL",
			"LLFFFFR", "RRFFFFL",
			"LFFFFFR", "RFFFFFLL",
			"LFFFFFFF", "RFFFFFFF",
			"FFFFFFFFL", "FFFFFFFFR",
			// 長さ10
			"LFFFFFFFR", "RFFFFFFFL",
			"LFFFFFFFF", "RFFFFFFFF",
		};
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
									if (!input.Map.CanMove(nr, nc, nd)) { mOk = false; } else { var mv = nd.Move(nr, nc); nr = mv.i; nc = mv.j; }
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

		double precompDeadline = Math.Min(sw.Elapsed.TotalMilliseconds + 800.0, 1200.0);
		var nodeDistAll = new int[macroList.Length][,,];
		var nodeArrDirAll = new int[macroList.Length][,,];
		var macroEnabled = new bool[macroList.Length];

		var fullDist = new int[N, N, 4];
		var fullStamp = new int[N, N, 4];
		int fullStampVal = 0;

		for (int mi = 0; mi < macroList.Length; mi++) {
			if (mi > 0 && sw.Elapsed.TotalMilliseconds > precompDeadline) break;
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
			state.AddOp('M');
			foreach (var c in mac) state.AddOp(c);
			state.AddOp('M');

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
			state.AddOp('M');
			foreach (var c in globalBestMacro) state.AddOp(c);
			state.AddOp('M');
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
	public int Width { get; set; } = 0;
	public int Depth { get; set; } = 0;
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

