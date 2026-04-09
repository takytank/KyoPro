using System.Collections;
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
			i => Run(i + 1 - 1),
			(locker, i, ret) => {
				var (input, info) = ret;
				lock (locker) {
					var status = new List<string> {
						$"{i:d4}:",
						$"N={input.N:d2}",
						$"M={input.M:d3}",
						$"C={input.C:d1}",
						$"loop={info.Loop:d8}",
						$"up={info.Up:d5}",
						$"T={info.T:d6}",
						$"E={info.E:d3}",
						$"K={info.K:d3}",
						$"P={info.P:d2}",
						$"score={info.Score:d7}",
						$"same={info.SameScore}",
						$"elapsed={info.Elapsed:d4}",
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
		var (isDebug, sw, rnd) = HeuristicHelper.Initialize();
		using var io = HeuristicHelper.CreateIO(@$"D:\kyopro\AtCoder\AHC063\tools\in\", caseNumber);

		var input = new Input(io, isDebug);
		var info = new Information();
		Zob.Initialize(rnd, input);

		var best = Plan1(rnd, sw, isDebug, input, info);
		long bestScore = best.Score;
		info.P = 1;

		/*
		{
			for (int l = 0; l < 100; l++) {
				var temp = Plan2(rnd, sw, isDebug, input, info, 1000);
				var tempScore = temp.Score;
				if (tempScore < bestScore) {
					bestScore = tempScore;
					best = temp;
					info.P = 2;
				}
			}

			for (int l = 0; l < 10; l++) {
				var temp = Plan2(rnd, sw, isDebug, input, info, 10000);
				var tempScore = temp.Score;
				if (tempScore < bestScore) {
					bestScore = tempScore;
					best = temp;
					info.P = 2;
				}
			}

			{
				var temp = Plan2(rnd, sw, isDebug, input, info, 100000);
				var tempScore = temp.Score;
				if (tempScore < bestScore) {
					bestScore = tempScore;
					best = temp;
					info.P = 2;
				}
			}
		}*/

		/*
		{
			var temp = Plan3(rnd, sw, isDebug, input, info);
			var tempScore = temp.Score;
			if (tempScore < bestScore) {
				bestScore = tempScore;
				best = temp;
				info.P = 3;
			}
		}*/

		input.Output(best);
		info.T = best.Ops.Count;
		(info.Score, info.E, info.K) = best.Calculate();
		info.K = input.M - info.K;
		info.SameScore = info.Score == best.Score;

		if (isDebug) {
			Validate(input, best);
		}

		info.Elapsed = sw.ElapsedMilliseconds;

		if (isDebug) {
			// Move/Undoの正しさを検証
			ValidateMoveUndo(input, rnd);
		}

		return (input, info);
	}

	static void Validate(Input input, State state)
	{
		int n = input.N;
		var (ci, cj) = (4, 0);
		var lastDir = Direction4.D;

		if (state.Ops.Count > 100000) {
			throw new Exception($"Too long output {state.Ops.Count}");
		}

		for (int i = 0; i < state.Ops.Count; i++) {
			var dir = state.Ops[i];
			if (dir == Direction4.N) {
				throw new Exception($"tunr {i} : Invalid direction {dir.ToSymbol()}");
			}

			if (dir.Reverse() == lastDir) {
				throw new Exception($"turn {i} : Try to U-turn {dir.ToSymbol()}");
			}

			var (ni, nj) = dir.Move(ci, cj);
			if ((uint)ni >= n || (uint)nj >= n) {
				throw new Exception($"turn {i} : Try to move out of the board ({ni}, {nj})");
			}

			lastDir = dir;
			(ci, cj) = (ni, nj);
		}
	}

	static void ValidateMoveUndo(Input input, Random rnd)
	{
		var state = new State(true, input);
		int n = input.N;

		// ランダムに100回Move→Undoを実行して状態が完全に復元されることを確認
		for (int trial = 0; trial < 100; trial++) {
			// 現在の状態を記録
			var originalHash = state.Hash;
			var originalE = state.E;
			var originalK = state.K;
			var originalNoErrorCount = state.NoErrorCount;
			var originalOpsCount = state.Ops.Count;
			var originalBorosCount = state.Boros.Count;
			var originalColorsCount = state.Colors.Count;

			// 状態の詳細なコピー（比較用）
			var originalBoros = new List<(int i, int j)>();
			for (int i = 0; i < state.Boros.Count; i++) {
				originalBoros.Add(state.Boros[i]);
			}

			var originalColors = new List<byte>();
			for (int i = 0; i < state.Colors.Count; i++) {
				originalColors.Add(state.Colors[i]);
			}

			var originalWillBites = state.WillBites.Clone();
			var originalEsas = state.Esas.Clone();

			// 有効な移動方向を収集
			var validDirs = new List<Direction4>();
			var (hi, hj) = state.Head;
			var lastDir = state.HeadDir;
			foreach (var dir in Direction4Extensions.ForEach4()) {
				if (dir.Reverse() == lastDir) {
					continue;
				}

				var (ni, nj) = dir.Move(hi, hj);
				if ((uint)ni < n && (uint)nj < n) {
					validDirs.Add(dir);
				}
			}

			if (validDirs.Count == 0) {
				break;
			}

			var moveDir = validDirs[rnd.Next(validDirs.Count)];

			// Move実行
			var undo = state.Move(moveDir);

			// Undo実行
			state.Undo(undo);

			// 状態が完全に復元されたか検証
			if (state.Hash != originalHash) {
				throw new Exception($"MoveUndo trial {trial}: Hash mismatch (expected={originalHash}, actual={state.Hash})");
			}

			if (state.E != originalE) {
				throw new Exception($"MoveUndo trial {trial}: E mismatch (expected={originalE}, actual={state.E})");
			}

			if (state.K != originalK) {
				throw new Exception($"MoveUndo trial {trial}: K mismatch (expected={originalK}, actual={state.K})");
			}

			if (state.NoErrorCount != originalNoErrorCount) {
				throw new Exception($"MoveUndo trial {trial}: NoErrorCount mismatch (expected={originalNoErrorCount}, actual={state.NoErrorCount})");
			}

			if (state.Ops.Count != originalOpsCount) {
				throw new Exception($"MoveUndo trial {trial}: Ops.Count mismatch (expected={originalOpsCount}, actual={state.Ops.Count})");
			}

			if (state.Boros.Count != originalBorosCount) {
				throw new Exception($"MoveUndo trial {trial}: Boros.Count mismatch (expected={originalBorosCount}, actual={state.Boros.Count})");
			}

			if (state.Colors.Count != originalColorsCount) {
				throw new Exception($"MoveUndo trial {trial}: Colors.Count mismatch (expected={originalColorsCount}, actual={state.Colors.Count})");
			}

			// Borosの内容を検証
			for (int i = 0; i < state.Boros.Count; i++) {
				if (state.Boros[i] != originalBoros[i]) {
					throw new Exception($"MoveUndo trial {trial}: Boros[{i}] mismatch (expected={originalBoros[i]}, actual={state.Boros[i]})");
				}
			}

			// Colorsの内容を検証
			for (int i = 0; i < state.Colors.Count; i++) {
				if (state.Colors[i] != originalColors[i]) {
					throw new Exception($"MoveUndo trial {trial}: Colors[{i}] mismatch (expected={originalColors[i]}, actual={state.Colors[i]})");
				}
			}

			// WillBitesの内容を検証
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					if (state.WillBites[i, j] != originalWillBites[i, j]) {
						throw new Exception($"MoveUndo trial {trial}: WillBites[{i},{j}] mismatch (expected={originalWillBites[i, j]}, actual={state.WillBites[i, j]})");
					}
				}
			}

			// Esasの内容を検証
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					if (state.Esas[i, j] != originalEsas[i, j]) {
						throw new Exception($"MoveUndo trial {trial}: Esas[{i},{j}] mismatch (expected={originalEsas[i, j]}, actual={state.Esas[i, j]})");
					}
				}
			}
		}
	}

	static State Plan1(Random rnd, Stopwatch sw, bool isDebug, Input input, Information info)
	{
		var state = new State(isDebug, input);

		for (int j = 0; j < input.N; j++) {
			if (j % 2 == 0) {
				while (state.Head.i < input.N - 1) {
					state.Move(Direction4.D);
				}
			} else {
				while (state.Head.i > 0) {
					state.Move(Direction4.U);
				}
			}

			if (j < input.N - 1) {
				state.Move(Direction4.R);
			}
		}

		return state;
	}

	static State Plan2(Random rnd, Stopwatch sw, bool isDebug, Input input, Information info, int max)
	{
		var state = new State(isDebug, input);

		State best = state.Clone();
		var (bestScore, _, _) = state.Calculate();

		for (int i = 0; i < max; i++) {
			var dir = FindNextStepGreedy2(rnd, input, info, state);
			var undo = state.Move(dir);
			if (undo.State == 0) {
				info.KamiCount++;
			} else if (info.KamiCount > 10 && undo.State == 1) {
				//info.KamiCount = 0;
			}

			var (tempScore, _, _) = state.Calculate();
			if (tempScore < bestScore) {
				bestScore = tempScore;
				best = state.Clone();
			}

			if (state.Colors.Count == input.M) {
				break;
			}
		}

		return best;
	}

	static State Plan3(Random rnd, Stopwatch sw, bool isDebug, Input input, Information info)
	{
		var state = new State(isDebug, input);
		State best = state.Clone();
		long bestScore = best.Score;

		int depth = 500;
		int width = 500;
		int n = input.N;

		// 使用する時は全部使用するので、評価の悪い方を優先するようにして、悪いtopを抜いていく。
		var que = new HeapQueue<(State s, long e), long>(x => x.e);
		que.Enqueue((state, state.Evaluate()));
		var stack = new Stack<State>();
		var set = new HashSet<long> {
			state.Hash
		};
		for (int i = 0; i < depth; i++) {
			// 良いやつの方を先に処理して、悪いやつはそもそもキューに入れないようにしたい。
			// 一回全部抜く
			while (que.Count > 0) {
				stack.Push(que.Dequeue().s);
			}

			var nextQue = new HeapQueue<(State s, long e), long>(x => x.e);
			while (stack.Count > 0) {
				var tag = stack.Pop();
				var (hi, hj) = tag.Head;
				var lastDir = tag.HeadDir;
				foreach (var dir in Direction4Extensions.ForEach4()) {
					if (dir.Reverse() == lastDir) {
						continue;
					}

					var (ni, nj) = dir.Move(hi, hj);
					if ((uint)ni < n && (uint)nj < n) {
						var nex = tag.Clone();
						nex.Move(dir);
						var hash = nex.Hash;
						if (set.Contains(hash)) {
							continue;
						}

						set.Add(hash);

						long eval = nex.Evaluate();
						long tempScore = nex.Score;
						if (tempScore < bestScore) {
							bestScore = tempScore;
							best = nex;
						}

						if (nextQue.Count == width) {
							if (nextQue.Top.e > eval) {
								nextQue.Dequeue();
								nextQue.Enqueue((nex, eval));
							}
						} else {
							nextQue.Enqueue((nex, eval));
						}
					}
				}
			}

			que = nextQue;
		}

		return best;
	}

	private static Direction4 FindNextStepGreedy2(Random rnd, Input input, Information info, State state)
	{
		var (hi, hj) = state.Head;
		int n = input.N;

		// まずは次の色を探す
		int nextColor = input.D[state.Boros.Count];
		var ret = Found(
			p => state.Esas[p.i, p.j] == nextColor,
			p => state.WillBites[p.i, p.j] == false && (state.Esas[p.i, p.j] == 0 || state.Esas[p.i, p.j] == nextColor));
		if (ret.i >= 0) {
			return ToDirection(ret.i, ret.j);
		}

		if (state.Ops.Count > 5000) {
			// 自分を食べない餌
			ret = Found(
			p => state.Esas[p.i, p.j] > 0,
			p => state.WillBites[p.i, p.j] == false);
			if (ret.i >= 0) {
				return ToDirection(ret.i, ret.j);
			}
		}

		if (info.KamiCount > 10) {
			// 同じ行動をループしてしまうので、ランダムにする。
			var dame = state.HeadDir.Reverse();
			var kouho = new List<Direction4>();
			foreach (var dir in Direction4Extensions.ForEach4()) {
				if (dir == dame) {
					continue;
				}

				var (ni, nj) = dir.Move(hi, hj);
				if ((uint)ni < n && (uint)nj < n) {
					kouho.Add(dir);
				}
			}

			if (kouho.Count > 0) {
				int ri = rnd.Next(0, kouho.Count);
				return kouho[ri];
			}
		}

		// 無い場合、違う色以外で探す
		ret = Found(
			p => state.Esas[p.i, p.j] == 0,
			p => state.Esas[p.i, p.j] == 0);
		if (ret.i >= 0) {
			return ToDirection(ret.i, ret.j);
		}

		// どこでもよい
		ret = Found(
			p => true,
			p => true);
		return ToDirection(ret.i, ret.j);

		Direction4 ToDirection(int ni, int nj)
		{
			if (hi == ni) {
				if (nj < hj) {
					return Direction4.L;
				} else {
					return Direction4.R;
				}
			} else {
				if (ni < hi) {
					return Direction4.U;
				} else {
					return Direction4.D;
				}
			}
		}

		(int i, int j) Found(Func<(int i, int j), bool> predictor, Func<(int i, int j), bool> canMove)
		{
			var que = new Queue<(int i, int j, int d)>();
			que.Enqueue((hi, hj, 0));
			var dists = new int[n, n].Fill(int.MaxValue);
			(int i, int j)[,] prevs = new (int i, int j)[n, n].Fill((-1, -1));
			dists[hi, hj] = 0;
			// Uターン出来ないように距離0にしておく
			var (bi, bj) = state.HeadDir.Reverse().Move(hi, hj);
			dists[bi, bj] = 0;

			var (ti, tj) = (-1, -1);
			while (que.Count > 0) {
				var (i, j, d) = que.Dequeue();
				if (predictor((i, j)) && (i, j) != (hi, hj)) {
					(ti, tj) = (i, j);
					break;
				}

				int nd = d + 1;
				foreach (var (ii, jj) in Helper.Adjacence4(i, j, n, n)) {
					if (canMove((ii, jj)) == false) {
						continue;
					}

					if (dists[ii, jj] > nd) {
						dists[ii, jj] = nd;
						prevs[ii, jj] = (i, j);
						que.Enqueue((ii, jj, d));
					}
				}
			}

			if (ti >= 0) {
				var (pi, pj) = prevs[ti, tj];
				while (pi >= 0 && prevs[pi, pj].i >= 0) {
					(ti, tj) = (pi, pj);
					(pi, pj) = prevs[ti, tj];
				}

				return (ti, tj);
			} else {
				return (-1, -1);
			}
		}
	}
}

public static class Zob
{
	public static long[,,] Esa { get; private set; }
	public static long[,] Head { get; private set; }
	public static long[,] Hebi { get; private set; }
	public static long[,] Color { get; private set; }

	public static void Initialize(Random rnd, Input input)
	{
		int n = input.N;
		int m = input.M;
		int c = input.C;

		Esa = new long[n, n, c + 1];
		Head = new long[n, n];
		Hebi = new long[n, n];
		for (int i = 0; i < n; i++) {
			for (int j = 0; j < n; j++) {
				for (int k = 0; k < c + 1; k++) {
					Esa[i, j, k] = rnd.NextInt64();
				}

				Head[i, j] = rnd.NextInt64();
				Hebi[i, j] = rnd.NextInt64();
			}
		}

		Color = new long[input.M + 1, input.C + 1];
		for (int i = 0; i < m + 1; i++) {
			for (int k = 0; k < c + 1; k++) {
				Color[i, k] = rnd.NextInt64();
			}
		}
	}
}

public readonly struct UndoInfo
{
	public readonly Direction4 Dir;
	public readonly int State; // 0: bite, 1: eat, 2: empty
	public readonly (int i, int j) NewHead;
	public readonly (int i, int j) OldHeadPos;
	public readonly int OldE;
	public readonly int OldK;
	public readonly int OldNoErrorCount;
	public readonly long OldHash;

	// state=0 (bite): 削除された尻尾の情報
	public readonly List<((int i, int j) pos, byte color, bool willBite)> RemovedSegments;

	// state=1 (eat): 追加された色
	public readonly byte EatenColor;
	public readonly (int i, int j) FoodPos;

	// state=2 (empty): 削除された尻尾
	public readonly (int i, int j) RemovedTail;
	public readonly bool RemovedTailWillBite;

	// 共通: WillBites の変更（Move実行前の後ろから2番目）
	public readonly (int i, int j) OldSecondLastPos;
	public readonly bool OldSecondLastWillBite;

	// 共通: WillBites の変更（Move実行後の後ろから2番目）
	public readonly (int i, int j) NewSecondLastPos;
	public readonly bool NewSecondLastWillBite;

	public UndoInfo(Direction4 dir, int state, (int i, int j) newHead, (int i, int j) oldHeadPos,
		int oldE, int oldK, int oldNoErrorCount, long oldHash,
		List<((int i, int j), byte, bool)> removedSegments,
		byte eatenColor, (int i, int j) foodPos,
		(int i, int j) removedTail, bool removedTailWillBite,
		(int i, int j) oldSecondLastPos, bool oldSecondLastWillBite,
		(int i, int j) newSecondLastPos, bool newSecondLastWillBite)
	{
		Dir = dir;
		State = state;
		NewHead = newHead;
		OldHeadPos = oldHeadPos;
		OldE = oldE;
		OldK = oldK;
		OldNoErrorCount = oldNoErrorCount;
		OldHash = oldHash;
		RemovedSegments = removedSegments;
		EatenColor = eatenColor;
		FoodPos = foodPos;
		RemovedTail = removedTail;
		RemovedTailWillBite = removedTailWillBite;
		OldSecondLastPos = oldSecondLastPos;
		OldSecondLastWillBite = oldSecondLastWillBite;
		NewSecondLastPos = newSecondLastPos;
		NewSecondLastWillBite = newSecondLastWillBite;
	}
}

public class State
{
	private readonly Input _input;
	private readonly bool _isDebug;

	public List<Direction4> Ops { get; } = [];
	public Deque<(int i, int j)> Boros { get; }
	public Deque<byte> Colors { get; }

	// 移動先がここだったら胴体への噛み付きが発生する。
	public Array2D<bool> WillBites { get; }
	public Array2D<byte> Esas { get; }

	public int E { get; private set; }
	public int K { get; private set; }
	public long Score => Calculate(Ops.Count, K, E);

	public long Hash { get; set; } = 0;

	// 先頭からエラーなく色が詰まっている数
	public int NoErrorCount { get; private set; }

	public (int i, int j) Head => Boros.Front;
	public Direction4 HeadDir => Ops.Count > 0 ? Ops[^1] : Direction4.D;

	public State(bool isDebug, Input input)
	{
		_isDebug = isDebug;
		_input = input;

		int n = input.N;
		WillBites = new Array2D<bool>(n, n);
		Esas = new Array2D<byte>(n, n);

		Boros = new();
		Colors = new();
		// BrosもColorsもFrontが頭側でBackが尻尾側。
		// 初期配置は頭側が (4, 0) なので、ループ順に前から詰めていけば結果的にFrontが頭になる。
		for (int i = 0; i < 5; i++) {
			Boros.PushFront((i, 0));
			Colors.PushFront(1);
			// 尻尾に移動しても噛み付かない。
			// またその次の胴体がある場所に移動しても、移動が完了したら尻尾になるので噛み付かない。
			// 頭(i=4)と尻尾(i=0)と尻尾の隣(i=1)以外を胴体とする
			if (i >= 2 && i < 4) {
				WillBites[i, 0] = true;
			}

			if (i == 4) {
				Hash ^= Zob.Head[i, 0];
			} else {
				Hash ^= Zob.Hebi[i, 0];
			}

			Hash ^= Zob.Color[i, 1];
		}

		for (int i = 0; i < n; i++) {
			for (int j = 0; j < n; j++) {
				Esas[i, j] = (byte)input.F[i, j];
				Hash ^= Zob.Esa[i, j, Esas[i, j]];
			}
		}

		K = 5;
		E = 0;
		NoErrorCount = 5;
	}

	private State(State src)
	{
		_input = src._input;
		_isDebug = src._isDebug;
		Ops.AddRange(src.Ops);
		Boros = src.Boros.Clone();
		Colors = src.Colors.Clone();
		WillBites = src.WillBites.Clone();
		Esas = src.Esas.Clone();
		K = src.K;
		E = src.E;
		Hash = src.Hash;
		NoErrorCount = src.NoErrorCount;
	}

	public State Clone() => new(this);

	public UndoInfo Move(Direction4 dir)
	{
		var (hi, hj) = Boros[0];
		var (ni, nj) = dir.Move(hi, hj);

		int oldE = E;
		int oldK = K;
		int oldNoErrorCount = NoErrorCount;
		long oldHash = Hash;

		// Move前の後ろから2番目の位置とWillBitesを保存（Undo時に復元する）
		var oldSecondLastPos = Boros[^2];
		bool oldSecondLastWillBite = WillBites[oldSecondLastPos.i, oldSecondLastPos.j];

		int state;
		List<((int i, int j), byte, bool)> removedSegments = null;
		byte eatenColor = 0;
		(int i, int j) foodPos = (-1, -1);
		(int i, int j) removedTail = (-1, -1);
		bool removedTailWillBite = false;
		(int i, int j) newSecondLastPos = (-1, -1);
		bool newSecondLastWillBite = false;

		if (WillBites[ni, nj]) {
			// 噛み付き
			state = 0;
			removedSegments = new List<((int i, int j), byte, bool)>();

			// 処理を簡単にするため先に移動
			PushHead(ni, nj, hi, hj);
			var firstTail = PopTail();
			removedSegments.Add((firstTail, 0, false)); // 色は後で設定

			// 後ろから噛んだところまでを削除
			while (Boros.Back != (ni, nj)) {
				var (ti, tj) = PopTail();
				byte color = (byte)PopTailColor(ti, tj);
				bool willBite = WillBites[ti, tj];
				removedSegments.Add(((ti, tj), color, willBite));
				WillBites[ti, tj] = false;
				if (_input.D[Colors.Count] == color) {
					// Kが減ってEは減らない
					--K;
				} else {
					// Kが減ってEは減る
					--K;
					--E;
				}
			}

			// あらたに頭と尻尾が居る位置も胴体ではない
			WillBites[ni, nj] = false;

			// Move後の後ろから2番目の位置と変更前のWillBitesを保存（Undo時に復元する）
			var (tti, ttj) = Boros[^2];
			newSecondLastPos = (tti, ttj);
			newSecondLastWillBite = WillBites[tti, ttj];

			// さらに後ろから2番目も噛み付き対象ではない
			WillBites[tti, ttj] = false;

			// 切ったとこまでエラーカウントをリセット。長さとのminを取れば良い
			NoErrorCount = Math.Min(NoErrorCount, Colors.Count);
		} else {
			if (Esas[ni, nj] > 0) {
				// 餌を食べた
				state = 1;

				// 現在の尻尾の位置がそのまま尻尾となる。
				// つまり、末尾は削除せず頭だけ追加すれば良い。
				// 色を末尾に追加する。
				eatenColor = Esas[ni, nj];
				foodPos = (ni, nj);
				PushTailColor(ni, nj);

				if (_input.D[Colors.Count - 1] == Colors[^1]) {
					// Kが増えてEが減らない
					++K;

					// 1個前までエラー無く入っているなら、カウントアップ
					if (NoErrorCount == Colors.Count - 1) {
						++NoErrorCount;
					}
				} else {
					// Kが増えてEが増える
					++K;
					++E;
				}
			} else {
				// 空マスを踏んだ
				state = 2;

				// 末尾が無くなる
				removedTail = Boros.PopBack();
				// その上で、次の末尾が尻尾になるので噛み付き対象ではなくなる。
				// 末尾2を対象外にしているのでこれ必要無いかも。
				removedTailWillBite = WillBites[removedTail.i, removedTail.j];

				var (ti, tj) = Boros.Back;
				WillBites[ti, tj] = false;
			}

			PushHead(ni, nj, hi, hj);

			// Move後の後ろから2番目の位置と変更前のWillBitesを保存（Undo時に復元する）
			var (bbi, bbj) = Boros[^2];
			newSecondLastPos = (bbi, bbj);
			newSecondLastWillBite = WillBites[bbi, bbj];

			// 後ろから2番目も噛み付き対象ではなくなる(hashの変更は無し)
			WillBites[bbi, bbj] = false;
		}

		Ops.Add(dir);

		return new UndoInfo(dir, state, (ni, nj), (hi, hj),
			oldE, oldK, oldNoErrorCount, oldHash,
			removedSegments, eatenColor, foodPos,
			removedTail, removedTailWillBite,
			oldSecondLastPos, oldSecondLastWillBite,
			newSecondLastPos, newSecondLastWillBite);
	}

	public void Undo(UndoInfo undo)
	{
		// Ops から削除
		Ops.RemoveAt(Ops.Count - 1);

		// 状態を復元
		E = undo.OldE;
		K = undo.OldK;
		NoErrorCount = undo.OldNoErrorCount;
		Hash = undo.OldHash;

		switch (undo.State) {
			case 0:
				// 噛み付き
				// 頭を戻す
				Boros.PopFront();

				// 元の頭位置の胴体化を戻す
				WillBites[undo.OldHeadPos.i, undo.OldHeadPos.j] = false;

				// 削除されたセグメントを逆順で復元
				for (int i = undo.RemovedSegments.Count - 1; i >= 0; i--) {
					var (pos, color, willBite) = undo.RemovedSegments[i];
					if (i == undo.RemovedSegments.Count - 1) {
						// 最後（噛んだ位置）
						WillBites[pos.i, pos.j] = false;
					}

					if (i > 0) {
						// 尻尾と色を復元
						Boros.PushBack(pos);
						Colors.PushBack(color);
						Esas[pos.i, pos.j] = color;
						WillBites[pos.i, pos.j] = willBite;
					} else {
						// 最初の尻尾（色情報なし）
						Boros.PushBack(pos);
					}
				}

				// 後ろから2番目の WillBites を復元
				if (Boros.Count >= 2) {
					var (tti, ttj) = Boros[^2];
					WillBites[tti, ttj] = true;
				}

				// Move前後の後ろから2番目のWillBitesを復元
				WillBites[undo.OldSecondLastPos.i, undo.OldSecondLastPos.j] = undo.OldSecondLastWillBite;
				WillBites[undo.NewSecondLastPos.i, undo.NewSecondLastPos.j] = undo.NewSecondLastWillBite;
				break;

			case 1:
				// 餌を食べた
				// 頭を戻す
				Boros.PopFront();

				// 元の頭位置は頭に戻るのでfalse
				WillBites[undo.OldHeadPos.i, undo.OldHeadPos.j] = false;

				// 末尾の色を削除
				Colors.PopBack();

				// 餌を戻す
				Esas[undo.FoodPos.i, undo.FoodPos.j] = undo.EatenColor;

				// Move前の後ろから2番目の WillBites を復元
				WillBites[undo.OldSecondLastPos.i, undo.OldSecondLastPos.j] = undo.OldSecondLastWillBite;
				// Move後の後ろから2番目の WillBites を復元
				WillBites[undo.NewSecondLastPos.i, undo.NewSecondLastPos.j] = undo.NewSecondLastWillBite;
				break;

			case 2:
				// 空マスを踏んだ
				// 頭を戻す
				Boros.PopFront();

				// 元の頭位置は頭に戻るのでfalse
				WillBites[undo.OldHeadPos.i, undo.OldHeadPos.j] = false;

				// 尻尾を復元
				Boros.PushBack(undo.RemovedTail);
				WillBites[undo.RemovedTail.i, undo.RemovedTail.j] = undo.RemovedTailWillBite;

				// Move前の後ろから2番目の WillBites を復元
				WillBites[undo.OldSecondLastPos.i, undo.OldSecondLastPos.j] = undo.OldSecondLastWillBite;
				// Move後の後ろから2番目の WillBites を復元
				WillBites[undo.NewSecondLastPos.i, undo.NewSecondLastPos.j] = undo.NewSecondLastWillBite;
				break;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void PushHead(int ni, int nj, int hi, int hj)
	{
		// 頭が次の位置へ移動
		Boros.PushFront((ni, nj));
		Hash ^= Zob.Head[ni, nj];
		// 元々頭があった位置が胴体になる
		WillBites[hi, hj] = true;
		Hash ^= Zob.Head[hi, hj];
		Hash ^= Zob.Hebi[ni, nj];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private (int ti, int tj) PopTail()
	{
		// 尻尾を抜く
		var (ti, tj) = Boros.PopBack();
		Hash ^= Zob.Hebi[ti, tj];
		return (ti, tj);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int PushTailColor(int ni, int nj)
	{
		byte color = Esas[ni, nj];
		Colors.PushBack(color);
		Esas[ni, nj] = 0;
		Hash ^= Zob.Esa[ni, nj, 0];
		Hash ^= Zob.Esa[ni, nj, color];

		return color;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int PopTailColor(int ti, int tj)
	{
		var color = Colors.PopBack();
		Hash ^= Zob.Color[Colors.Count, color];
		Esas[ti, tj] = color;
		Hash ^= Zob.Esa[ti, tj, 0];
		Hash ^= Zob.Esa[ti, tj, color];

		return color;
	}

	public (long score, int e, int k) Calculate()
	{
		int k = Math.Min(_input.M, Colors.Count);
		int e = 0;
		for (int i = 0; i < k; i++) {
			if (_input.D[i] != Colors[i]) {
				++e;
			}
		}

		long score = Calculate(Ops.Count, k, e);
		return (score, e, k);
	}

	public long Calculate(int t, int k, int e)
	{
		long score = t;
		score += 10000 * (e + (2 * (_input.M - k)));
		return score;
	}

	// ビームサーチ用の評価値。低いほど良い。
	public long Evaluate()
	{
		long val = 0;

		// 生スコア
		val += Score;

		// エラーなく色が沢山入っている方がよい。
		// 生スコアだと、違う色を食べるのと何も食べないのでは前者の方が良くなるが、
		// 後を考えると後者を優先したい。
		// ただ、何も食べないのを優先した結果、何もないところにずっと居られても困る。
		// 数歩あるけば餌に着くと思うので、盤面ハッシュで重複を弾いておけば良いか？
		val += E * 1;
		val += (_input.M - K) * 100;
		val -= NoErrorCount * 10000;

		// エラーの上に積み上げよりも噛みを優先したい気持ち
		val += (K - NoErrorCount) * 10000;

		return val;
	}
}

public class Information
{
	public long Time { get; set; } = 0;
	public long Score { get; set; } = 0;
	public int Loop { get; set; } = 0;
	public int Up { get; set; } = 0;
	public long Elapsed { get; set; } = 0;

	public int T { get; set; } = 0;
	public int E { get; set; } = 0;
	public int K { get; set; } = 0;

	public int P { get; set; } = 0;
	public bool SameScore { get; set; } = false;

	public int KamiCount { get; set; } = 0;
}

public class Input
{
	private readonly IOManager _io;
	private readonly bool _isDebug;
	public int N { get; }
	public int M { get; }
	public int C { get; }
	public int[] D { get; }
	public int[,] F { get; }

	public Input(IOManager io, bool isDebug)
	{
		_io = io;
		_isDebug = isDebug;

		(N, M, C) = io.Int3();
		D = io.ArrayInt(M);
		F = io.Repeat(N, N, io => io.Int());
	}

	public void Output(State state)
	{
		foreach (var dir in state.Ops) {
			_io.WriteLine($"{dir.ToSymbol()}");
		}
	}
}

public class HeapQueue<TObject, TPriority> where TPriority : IComparable<TPriority>
{
	private readonly Func<TObject, TPriority> selector_;
	private readonly int reverseFactory_;
	private TObject[] heap_;

	public TObject Top => heap_[0];
	public int Count { get; private set; }

	public HeapQueue(
		Func<TObject, TPriority> selector,
		bool reverses = false)
		: this(1024, selector, reverses) { }

	public HeapQueue(
		int capacity,
		Func<TObject, TPriority> selector,
		bool reverses = false)
	{
		if (capacity == 0) {
			capacity = 1024;
		}

		heap_ = new TObject[capacity];
		selector_ = selector;
		reverseFactory_ = reverses ? -1 : 1;
	}

	public void Enqueue(TObject item)
	{
		if (heap_.Length == Count) {
			Extend(heap_.Length * 2);
		}

		var priority = selector_(item);
		heap_[Count] = item;
		++Count;

		int c = Count - 1;
		while (c > 0) {
			int p = (c - 1) >> 1;
			if (Compare(Priority(p), priority) < 0) {
				heap_[c] = heap_[p];
				c = p;
			} else {
				break;
			}
		}

		heap_[c] = item;
	}

	public TObject Dequeue()
	{
		TObject ret = heap_[0];
		int n = Count - 1;

		var item = heap_[n];
		var priority = Priority(n);
		int p = 0;
		int c = (p << 1) + 1;
		while (c < n) {
			if (c != n - 1 && Compare(Priority(c + 1), Priority(c)) > 0) {
				++c;
			}

			if (Compare(priority, Priority(c)) < 0) {
				heap_[p] = heap_[c];
				p = c;
				c = (p << 1) + 1;
			} else {
				break;
			}
		}

		heap_[p] = item;
		Count--;

		return ret;
	}

	public void Clear() => Count = 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private TPriority Priority(int index)
		=> selector_(heap_[index]);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int Compare(TPriority x, TPriority y)
		=> x.CompareTo(y) * reverseFactory_;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Extend(int newSize)
	{
		var newHeap = new TObject[newSize];
		heap_.CopyTo(newHeap, 0);
		heap_ = newHeap;
	}
}

public class Deque<T> : IEnumerable<T>
{
	/// <summary>リングバッファのサイズ</summary>
	private int _capacity;
	/// <summary>両端キューの実体であるリングバッファ</summary>
	private T[] _ringBuffer;
	/// <summary>キューの先頭のインデックス</summary>
	/// <remarks>
	/// キューに格納されている要素を閉区間[front, back]の範囲で持つ。
	/// 閉区間のため、中身が空のときはbackがfront-1の位置にある。
	/// </remarks>
	private int _front;
	/// <summary>キューの末尾のインデックス</summary>
	private int _back;

	/// <summary>格納されている要素数</summary>
	public int Count { get; private set; } = 0;
	/// <summary>キューの先頭の要素</summary>
	public T Front => _ringBuffer[_front];
	/// <summary>キューの末尾の要素</summary>
	public T Back => _ringBuffer[_back];

	/// <summary>ランダムアクセス</summary>
	/// <remarks>
	/// 範囲チェックは省いてあるので注意。
	/// </remarks>
	public T this[int index]
	{
		get
		{
			index += _front;
			// バッファの終端を超えた場合は先頭に戻る
			return _ringBuffer[index < _capacity ? index : index - _capacity];
		}
	}

	/// <summary>既定のサイズでインスタンスを生成</summary>
	public Deque() : this(1024) { }
	/// <summary>
	/// 指定したサイズでインスタンスを生成
	/// </summary>
	/// <param name="capacity">リングバッファのサイズ</param>
	public Deque(int capacity)
	{
		if (capacity <= 0) {
			capacity = 1024;
		}

		_capacity = capacity;
		_ringBuffer = new T[capacity];
		Clear();
	}

	private Deque(Deque<T> src)
	{
		_capacity = src._capacity;
		_ringBuffer = new T[_capacity];
		Array.Copy(src._ringBuffer, _ringBuffer, _capacity);
		_front = src._front;
		_back = src._back;
		Count = src.Count;
	}

	public Deque<T> Clone() => new(this);

	/// <summary>
	/// キューの中身を空にする O(1)
	/// </summary>
	public void Clear()
	{
		_front = 0;
		_back = _capacity - 1;
		Count = 0;
		// バッファのクリアはしない。
		// 再度バッファにアクセスされる際には新しい値に上書きされるので問題無い。
	}

	/// <summary>
	/// キューの先頭に要素を追加する amortized O(1)
	/// </summary>
	/// <param name="item">追加する要素</param>
	public void PushFront(T item)
	{
		// リングバッファが一杯の場合、バッファサイズを倍に拡張する。
		// この部分がならしO(1)。
		if (Count == _capacity) {
			Extend(_capacity * 2);
		}

		// 要素が格納されている区間を前方向に拡張。
		// バッファの先頭を超えた場合は末尾に追加する。
		--_front;
		if (_front < 0) {
			_front += _capacity;
		}

		++Count;
		_ringBuffer[_front] = item;
	}

	/// <summary>
	/// キューの末尾に要素を追加する amortized O(1)
	/// </summary>
	/// <param name="item">追加する要素</param>
	public void PushBack(T item)
	{
		// リングバッファが一杯の場合、バッファサイズを倍に拡張する。
		// この部分がならしO(1)。
		if (Count == _capacity) {
			Extend(_capacity * 2);
		}

		// 要素が格納されている区間を後ろ方向に拡張。
		// バッファの末尾を超えた場合は先頭に追加する。
		++_back;
		if (_back >= _capacity) {
			_back -= _capacity;
		}

		++Count;
		_ringBuffer[_back] = item;
	}

	/// <summary>
	/// キューの先頭から要素を取り出す O(1)
	/// </summary>
	/// <returns>取り出した要素</returns>
	public T PopFront()
	{
		if (Count == 0) {
			// 例外を投げるべき？
			return default(T);
		}

		T ret = _ringBuffer[_front];
		--Count;
		// 要素が格納されている区間の先頭を縮小。
		// バッファの末尾を超えた場合は先頭を指すようにする。
		++_front;
		if (_front >= _capacity) {
			_front -= _capacity;
		}

		return ret;
	}

	/// <summary>
	/// キューの末尾から要素を取り出す O(1)
	/// </summary>
	/// <returns>取り出した要素</returns>
	public T PopBack()
	{
		if (Count == 0) {
			// 例外を投げるべき？
			return default(T);
		}

		T ret = _ringBuffer[_back];
		--Count;
		// 要素が格納されている区間の末尾を縮小。
		// バッファの先頭を超えた場合は末尾を指すようにする。
		--_back;
		if (_back < 0) {
			_back += _capacity;
		}

		return ret;
	}

	/// <summary>
	/// バッファサイズを倍に拡張
	/// </summary>
	/// <remarks>
	/// この処理自体はO(N)かかるのであるが、
	/// 呼ばれるタイイングがサイズNのバッファーが埋まったときなので、
	/// 実際に使われるケースでの計算量はならしO(1)となる。
	/// </remarks>
	/// <param name="newSize">拡張後のサイズ</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Extend(int newSize)
	{
		var newBuffer = new T[newSize];
		if (Count > 0) {
			if (_front <= _back) {
				// ここにくるのは実質frontが0でbackが^1のときだけ。
				Array.Copy(_ringBuffer, _front, newBuffer, 0, Count);
			} else {
				// [front, 末尾] と [先頭, back] の2回に分けてコピーする必要がある。
				int prevLength = _ringBuffer.Length - _front;
				Array.Copy(_ringBuffer, _front, newBuffer, 0, prevLength);
				Array.Copy(_ringBuffer, 0, newBuffer, prevLength, _back + 1);
			}
		}

		_ringBuffer = newBuffer;
		_capacity = newSize;
		_front = 0;
		_back = Count - 1;
	}

	public IEnumerator<T> GetEnumerator()
	{
		for (int i = 0; i < Count; i++) {
			yield return this[i];
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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

	public static Direction4 Rorate(this Direction4 dir, Rotation rot)
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

public readonly struct BitFlag(int flags)
{
	public static BitFlag Begin() => 0;
	public static BitFlag End(int bitCount) => 1 << bitCount;
	public static BitFlag FromBit(int bitNumber) => 1 << bitNumber;
	public static BitFlag Fill(int count) => (1 << count) - 1;

	public static IEnumerable<BitFlag> All(int n)
	{
		for (var f = Begin(); f < End(n); ++f) {
			yield return f;
		}
	}

	private readonly int flags_ = flags;
	public int Flag => flags_;
	public bool this[int bitNumber] => (flags_ & (1 << bitNumber)) != 0;

	public bool Has(BitFlag target) => (flags_ & target.flags_) == target.flags_;
	public bool Has(int target) => (flags_ & target) == target;
	public bool HasBit(int bitNumber) => (flags_ & (1 << bitNumber)) != 0;
	public BitFlag OrBit(int bitNumber) => flags_ | (1 << bitNumber);
	public BitFlag AndBit(int bitNumber) => flags_ & (1 << bitNumber);
	public BitFlag XorBit(int bitNumber) => flags_ ^ (1 << bitNumber);
	public BitFlag ComplementOf(BitFlag sub) => flags_ ^ sub.flags_;
	public int PopCount() => BitOperations.PopCount((uint)flags_);

	public static BitFlag operator ++(BitFlag src) => new(src.flags_ + 1);
	public static BitFlag operator --(BitFlag src) => new(src.flags_ - 1);
	public static BitFlag operator |(BitFlag lhs, BitFlag rhs)
		=> new(lhs.flags_ | rhs.flags_);
	public static BitFlag operator |(BitFlag lhs, int rhs)
		=> new(lhs.flags_ | rhs);
	public static BitFlag operator |(int lhs, BitFlag rhs)
		=> new(lhs | rhs.flags_);
	public static BitFlag operator &(BitFlag lhs, BitFlag rhs)
		=> new(lhs.flags_ & rhs.flags_);
	public static BitFlag operator &(BitFlag lhs, int rhs)
		=> new(lhs.flags_ & rhs);
	public static BitFlag operator &(int lhs, BitFlag rhs)
		=> new(lhs & rhs.flags_);
	public static BitFlag operator ^(BitFlag lhs, BitFlag rhs)
		=> new(lhs.flags_ ^ rhs.flags_);
	public static BitFlag operator ^(BitFlag lhs, int rhs)
		=> new(lhs.flags_ ^ rhs);
	public static BitFlag operator ^(int lhs, BitFlag rhs)
		=> new(lhs ^ rhs.flags_);
	public static BitFlag operator <<(BitFlag bit, int shift) => bit.flags_ << shift;
	public static BitFlag operator >>(BitFlag bit, int shift) => bit.flags_ >> shift;

	public static bool operator <(BitFlag lhs, BitFlag rhs) => lhs.flags_ < rhs.flags_;
	public static bool operator <(BitFlag lhs, int rhs) => lhs.flags_ < rhs;
	public static bool operator <(int lhs, BitFlag rhs) => lhs < rhs.flags_;
	public static bool operator >(BitFlag lhs, BitFlag rhs) => lhs.flags_ > rhs.flags_;
	public static bool operator >(BitFlag lhs, int rhs) => lhs.flags_ > rhs;
	public static bool operator >(int lhs, BitFlag rhs) => lhs > rhs.flags_;
	public static bool operator <=(BitFlag lhs, BitFlag rhs) => lhs.flags_ <= rhs.flags_;
	public static bool operator <=(BitFlag lhs, int rhs) => lhs.flags_ <= rhs;
	public static bool operator <=(int lhs, BitFlag rhs) => lhs <= rhs.flags_;
	public static bool operator >=(BitFlag lhs, BitFlag rhs) => lhs.flags_ >= rhs.flags_;
	public static bool operator >=(BitFlag lhs, int rhs) => lhs.flags_ >= rhs;
	public static bool operator >=(int lhs, BitFlag rhs) => lhs >= rhs.flags_;

	public static implicit operator BitFlag(int t) => new(t);
	public static implicit operator int(BitFlag t) => t.flags_;

	public override string ToString() => $"{Convert.ToString(flags_, 2).PadLeft(32, '0')} ({flags_})";

	public SubBitsEnumerator SubBits => new(flags_);
	public readonly struct SubBitsEnumerator(int flags) : IEnumerable<BitFlag>
	{
		private readonly int flags_ = flags;

		IEnumerator<BitFlag> IEnumerable<BitFlag>.GetEnumerator() => new Enumerator(flags_);
		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(flags_);
		public Enumerator GetEnumerator() => new(flags_);
		public struct Enumerator(int flags) : IEnumerator<BitFlag>
		{
			private readonly int src_ = flags;
			public BitFlag Current { get; private set; } = flags + 1;
			readonly object IEnumerator.Current => Current;

			public readonly void Dispose() { }
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext() => (Current = --Current & src_) > 0;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Reset() => Current = src_;
		}
	}
}

public class Array2D<T>(int h, int w)
{
	private readonly T[] _arr = new T[h * w];
	public int H { get; } = h;
	public int W { get; } = w;

	public T this[int i]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _arr[i];
		//get => Unsafe.Add(ref _arr[0], i * W + j);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set => _arr[i] = value;
	}

	public T this[int i, int j]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _arr[i * W + j];
		//get => Unsafe.Add(ref _arr[0], i * W + j);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set => _arr[i * W + j] = value;
	}

	public Array2D<T> Fill(T value)
	{
		_arr.AsSpan().Fill(value);
		return this;
	}

	public Span<T> Row(int i)
	{
		return _arr.AsSpan().Slice(i * W, W);
	}

	public Array2D<T> Clone()
	{
		var newArr = new Array2D<T>(H, W);
		Array.Copy(_arr, newArr._arr, _arr.Length);
		// Unsafe.CopyBlock(ref newArr._arr[0], ref _arr[0], (uint)_arr.Length);

		return newArr;
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
		var rnd = new Random(123456);

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

public class DijkstraQ
{
	private int count_ = 0;
	private long[] distanceHeap_;
	private int[] vertexHeap_;

	public int Count => count_;
	public DijkstraQ()
	{
		distanceHeap_ = new long[8];
		vertexHeap_ = new int[8];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Enqueue(long distance, int v)
	{
		if (distanceHeap_.Length == count_) {
			var newDistanceHeap = new long[distanceHeap_.Length << 1];
			var newVertexHeap = new int[vertexHeap_.Length << 1];
			Unsafe.CopyBlock(
				ref Unsafe.As<long, byte>(ref newDistanceHeap[0]),
				ref Unsafe.As<long, byte>(ref distanceHeap_[0]),
				(uint)(8 * count_));

			Unsafe.CopyBlock(
				ref Unsafe.As<int, byte>(ref newVertexHeap[0]),
				ref Unsafe.As<int, byte>(ref vertexHeap_[0]),
				(uint)(4 * count_));
			distanceHeap_ = newDistanceHeap;
			vertexHeap_ = newVertexHeap;
		}

		ref var dRef = ref distanceHeap_[0];
		ref var vRef = ref vertexHeap_[0];
		Unsafe.Add(ref dRef, count_) = distance;
		Unsafe.Add(ref vRef, count_) = v;
		++count_;

		int c = count_ - 1;
		while (c > 0) {
			int p = (c - 1) >> 1;
			var tempD = Unsafe.Add(ref dRef, p);
			if (tempD <= distance) {
				break;
			} else {
				Unsafe.Add(ref dRef, c) = tempD;
				Unsafe.Add(ref vRef, c) = Unsafe.Add(ref vRef, p);
				c = p;
			}
		}

		Unsafe.Add(ref dRef, c) = distance;
		Unsafe.Add(ref vRef, c) = v;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public (long distance, int v) Dequeue()
	{
		ref var dRef = ref distanceHeap_[0];
		ref var vRef = ref vertexHeap_[0];
		(long distance, int v) ret = (dRef, vRef);
		int n = count_ - 1;

		var distance = Unsafe.Add(ref dRef, n);
		var vertex = Unsafe.Add(ref vRef, n);
		int p = 0;
		int c = (p << 1) + 1;
		while (c < n) {
			if (c != n - 1 && Unsafe.Add(ref dRef, c + 1) < Unsafe.Add(ref dRef, c)) {
				++c;
			}

			var tempD = Unsafe.Add(ref dRef, c);
			if (distance > tempD) {
				Unsafe.Add(ref dRef, p) = tempD;
				Unsafe.Add(ref vRef, p) = Unsafe.Add(ref vRef, c);
				p = c;
				c = (p << 1) + 1;
			} else {
				break;
			}
		}

		Unsafe.Add(ref dRef, p) = distance;
		Unsafe.Add(ref vRef, p) = vertex;
		--count_;

		return ret;
	}
}

public class HashMap<TKey, TValue> : Dictionary<TKey, TValue>
{
	private readonly Func<TKey, TValue> initialzier_;
	public HashMap(Func<TKey, TValue> initialzier)
		: base()
	{
		initialzier_ = initialzier;
	}

	public HashMap(Func<TKey, TValue> initialzier, int capacity)
		: base(capacity)
	{
		initialzier_ = initialzier;
	}

	new public TValue this[TKey key]
	{
		get
		{
			if (TryGetValue(key, out TValue value)) {
				return value;
			} else {
				var init = initialzier_(key);
				base[key] = init;
				return init;
			}
		}

		set { base[key] = value; }
	}

	public HashMap<TKey, TValue> Merge(
		HashMap<TKey, TValue> src,
		Func<TValue, TValue, TValue> mergeValues)
	{
		foreach (var key in src.Keys) {
			this[key] = mergeValues(this[key], src[key]);
		}

		return this;
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

public static class Extensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> AsSpan<T>(this List<T> list)
	{
		return Unsafe.As<FakeList<T>>(list).Array.AsSpan(0, list.Count);
	}

	private class FakeList<T>
	{
		public T[] Array = null;
	}
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
		} while (b < ASCII_CHAR_BEGIN || b > ASCII_CHAR_END);

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