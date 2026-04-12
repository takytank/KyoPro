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
		int successCount = 0;
		long successScoreSum = 0;
		HeuristicHelper.RunCases(
			runCaseCount: 100,
			testCaseCount: 50,
			isParallel: false,
			i => Run(i),
			(locker, i, ret) => {
				var (input, info) = ret;
				lock (locker) {
					if (info.E == 0 && info.K == 0) {
						successCount++;
						successScoreSum += info.Score;
					}

					var status = new List<string> {
						$"{i:d4}:",
						$"N={input.N:d2}",
						$"M={input.M:d3}",
						$"C={input.C:d1}",
						$"loop={info.Loop:d5}",
						$"up={info.Up:d3}",
						$"B={info.B:d4}",
						$"D={info.D:d4}",
						$"T={info.T:d6}",
						$"E={info.E:d3}",
						$"K={info.K:d3}",
						$"P={info.P:d2}",
						$"S={info.S:d2}",
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
				Console.WriteLine($"success: {successCount}");
				if (successCount > 0) {
					Console.WriteLine($"success score ave: {(double)successScoreSum / successCount:f2}");
				}
			});
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	static (Input input, Information info)
		Run(int caseNumber)
	{
		var (isLocal, sw, rnd) = HeuristicHelper.Initialize();
		using var io = HeuristicHelper.CreateIO(@$"D:\kyopro\AtCoder\AHC063\tools\in\", caseNumber);

		var input = new Input(io, isLocal);
		var info = new Information();
		var zob = new Zob(rnd, input);

		int limit = 1500;

		// まずはどのケースでもE=0 K=0を達成出来るPlan4を実行して、opの長さの目安を算出
		var best = Plan4(rnd, sw, isLocal, input, info, 10000, zob);
		long bestScore = best.Score;
		info.P = 4;
		// ただし、たまに成功しないケースもあるので、何回かやる
		for (int l = 0; l < 100; l++) {
			if (sw.ElapsedMilliseconds >= limit) {
				break;
			}

			if (best.K == input.M && best.E == 0) {
				info.B = l;
				break;
			}

			++info.Loop;
			var temp = Plan4(rnd, sw, isLocal, input, info, 10000, zob);
			var tempScore = temp.Score;
			if (tempScore < bestScore) {
				bestScore = tempScore;
				best = temp;
				info.P = 4;
				++info.Up;
			}
		}

		int maxOp = best.Ops.Count;

		// plan2 は割と速いので共通で先にやっておく
		// TODO やらなくていいかも
		for (int l = 0; l < 100; l++) {
			if (sw.ElapsedMilliseconds >= limit) {
				break;
			}

			++info.Loop;
			var temp = Plan2(rnd, sw, isLocal, input, info, maxOp, zob);
			var tempScore = temp.Score;
			if (tempScore < bestScore) {
				bestScore = tempScore;
				best = temp;
				info.P = 2;
				++info.Up;
				if (temp.E == 0 && temp.K == input.M) {
					maxOp.UpdateMin(temp.Ops.Count);
				}
			}
		}

		// この味点でopが少ない場合、ビームサーチが効く
		if (maxOp <= 500) {
			// 先にビームサーチを実行。
			{
				var temp = Plan8(rnd, sw, isLocal, input, info, maxOp, 350, zob, limit);
				var tempScore = temp.Score;
				if (tempScore < bestScore) {
					bestScore = tempScore;
					best = temp;
					info.P = 8;
					++info.Up;
					if (temp.E == 0 && temp.K == input.M) {
						maxOp.UpdateMin(temp.Ops.Count);
					}
				}
			}

			// ビームサーチの評価値も完璧でないので、Plan3 が上回ることもある。
			// 時間いっぱいまわす。
			while (sw.ElapsedMilliseconds < limit) {
				++info.Loop;
				int maxStep = rnd.Next(8, 10);
				var temp = Plan3(rnd, sw, isLocal, input, info, maxOp, zob, maxStep);
				var tempScore = temp.Score;
				if (tempScore < bestScore) {
					bestScore = tempScore;
					best = temp;
					info.P = 3;
					++info.Up;
					info.S = maxStep;
					if (temp.E == 0 && temp.K == input.M) {
						maxOp.UpdateMin(temp.Ops.Count);
					}
				}
			}
		} else {
			// plan2 や 3 はおそらく成功しないので、plan4 を回す
			// が、opが比較的少ない場合は回す。
			if (maxOp <= 1000) {
				// 但し、ビムサが動くように少なめ
				for (int l = 0; l < 10; l++) {
					if (sw.ElapsedMilliseconds >= limit) {
						break;
					}

					++info.Loop;
					int maxStep = rnd.Next(8, 10);
					var temp = Plan3(rnd, sw, isLocal, input, info, maxOp, zob, maxStep);
					var tempScore = temp.Score;
					if (tempScore < bestScore) {
						bestScore = tempScore;
						best = temp;
						info.P = 3;
						++info.Up;
						info.S = maxStep;
						if (temp.E == 0 && temp.K == input.M) {
							maxOp.UpdateMin(temp.Ops.Count);
						}
					}
				}
			}

			if (info.P == 4) {
				for (int l = 0; l < 100; l++) {
					if (sw.ElapsedMilliseconds >= limit) {
						break;
					}

					++info.Loop;
					var temp = Plan4(rnd, sw, isLocal, input, info, maxOp, zob);
					var tempScore = temp.Score;
					if (tempScore < bestScore) {
						bestScore = tempScore;
						best = temp;
						info.P = 4;
						++info.Up;
						if (temp.E == 0 && temp.K == input.M) {
							maxOp.UpdateMin(temp.Ops.Count);
						}
					}
				}
			}

			// 最後にビームサーチを時間いっぱい回す
			{
				int width = maxOp >= 700 ? 100 : 100; // BFSからマンハッタン距離への変更により高速化したため幅を拡大
				var temp = Plan8(rnd, sw, isLocal, input, info, maxOp, width, zob, limit);
				var tempScore = temp.Score;
				if (tempScore < bestScore) {
					bestScore = tempScore;
					best = temp;
					info.P = 8;
					++info.Up;
					if (temp.E == 0 && temp.K == input.M) {
						maxOp.UpdateMin(temp.Ops.Count);
					}
				}
			}

			// ビームサーチが思ったより速く終わったときよう
			while (sw.ElapsedMilliseconds < limit) {
				++info.Loop;
				var temp = Plan4(rnd, sw, isLocal, input, info, maxOp, zob);
				var tempScore = temp.Score;
				if (tempScore < bestScore) {
					bestScore = tempScore;
					best = temp;
					info.P = 4;
					++info.Up;
					if (temp.E == 0 && temp.K == input.M) {
						maxOp.UpdateMin(temp.Ops.Count);
					}
				}
			}
		}

		input.Output(best);
		info.T = best.Ops.Count;
		(info.Score, info.E, info.K) = best.Calculate();
		info.K = input.M - info.K;
		info.SameScore = info.Score == best.Score;

		if (isLocal) {
			Validate(input, best);

		}

		info.Elapsed = sw.ElapsedMilliseconds;

		if (isLocal) {
			// Move/Undoの正しさを検証
			string logPath = @"D:\programs\taky\KyoPro\validate_log.txt";
			using (var logWriter = new StreamWriter(logPath)) {
				var originalOut = Console.Out;
				Console.SetOut(logWriter);

				try {
					Console.WriteLine("[Validation Start]");
					Console.WriteLine($"Log file: {logPath}");
					Console.WriteLine();

					Console.WriteLine("[ValidateMoveUndo] Starting random move/undo test...");
					ValidateMoveUndo(input, rnd, zob);
					Console.WriteLine("[ValidateMoveUndo] All tests passed!");
					Console.WriteLine();

					Console.WriteLine("[ValidateMoveUndo2] Starting body-bite move/undo test...");
					ValidateMoveUndo2(input, rnd, zob);
					Console.WriteLine("[ValidateMoveUndo2] All tests passed!");
					Console.WriteLine();

					Console.WriteLine("[Validation Complete]");
				} catch (Exception ex) {
					Console.WriteLine($"[ERROR] Validation failed: {ex.Message}");
					Console.WriteLine(ex.StackTrace);
				} finally {
					logWriter.Flush();
					Console.SetOut(originalOut);
				}
			}

			// ログファイルが作成されたことを標準出力に通知
			// Console.WriteLine($"Validation log written to: {logPath}");

			Validate2(isLocal, input, zob, best);
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

	static void Validate2(bool isLocal, Input input, Zob zob, State state)
	{
		var other = new State(isLocal, input, zob);

		for (int i = 0; i < state.Ops.Count; i++) {
			var dir = state.Ops[i];
			other.Move(dir);
		}

		var (score1, e1, k1) = state.Calculate();
		var (score2, e2, k2) = other.Calculate();

		if (score1 != score2) {
			Console.WriteLine($"[Validate2] Score mismatch: state={score1}, other={score2}");
			Console.WriteLine($"  state: E={state.E}, K={state.K}, T={state.Ops.Count}, Colors.Count={state.Colors.Count}");
			Console.WriteLine($"  other: E={other.E}, K={other.K}, T={other.Ops.Count}, Colors.Count={other.Colors.Count}");
			Console.WriteLine($"  calc:  e1={e1}, k1={k1}, e2={e2}, k2={k2}");

			int n = input.N;
			for (int r = 0; r < n; r++) {
				for (int c = 0; c < n; c++) {
					if (state.WillBites[r, c] != other.WillBites[r, c]) {
						Console.WriteLine($"  WillBites mismatch at ({r},{c}): state={state.WillBites[r, c]}, other={other.WillBites[r, c]}");
					}
				}
			}

			// Colors配列の内容を比較

			int mismatchCount = 0;
			int minCount = Math.Min(state.Colors.Count, other.Colors.Count);
			for (int i = 0; i < minCount && mismatchCount < 10; i++) {
				if (state.Colors[i] != other.Colors[i]) {
					Console.WriteLine($"  Colors[{i}]: state={state.Colors[i]}, other={other.Colors[i]}, expected={input.D[i]}");
					mismatchCount++;
				}
			}

			throw new Exception($"best state internal score {score1} different from real move score {score2}");
		}
	}

	static void ValidateMoveUndo(Input input, Random rnd, Zob zob)
	{
		var state = new State(true, input, zob);
		int n = input.N;

		// ランダムに200回Move→Undoを実行して状態が完全に復元されることを確認
		for (int trial = 0; trial < 200; trial++) {
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

			// Colors配列の値レベルでの完全な検証
			for (int i = 0; i < originalColors.Count; i++) {
				if (state.Colors[i] != originalColors[i]) {
					throw new Exception($"MoveUndo trial {trial}: Colors[{i}] value mismatch after full cycle");
				}
			}
		}
	}

	static void ValidateMoveUndo2(Input input, Random rnd, Zob zob)
	{
		var state = new State(true, input, zob);
		int n = input.N;

		// Phase 1: 蛇行して餌を食べる（胴体を長くする）
		Console.WriteLine("[ValidateMoveUndo2] Phase 1: Eating food in zigzag pattern");
		int targetFoodCount = Math.Min(20, input.M - 5); // 初期5個+20個、または可能な限り
		int eatenCount = 0;

		for (int row = 0; row < n && eatenCount < targetFoodCount; row++) {
			bool isEvenRow = (row % 2 == 0);
			var (hi, hj) = state.Head;

			// 目標行に移動
			while (hi < row) {
				state.Move(Direction4.D);
				hi = state.Head.i;
			}

			// 横に移動しながら餌を食べる
			Direction4 horizontalDir = isEvenRow ? Direction4.R : Direction4.L;
			int targetJ = isEvenRow ? n - 1 : 0;

			while (hj != targetJ && eatenCount < targetFoodCount) {
				var lastDir = state.HeadDir;
				if (lastDir != horizontalDir.Reverse()) {
					var (ni, nj) = horizontalDir.Move(hi, hj);
					if ((uint)ni < n && (uint)nj < n && !state.WillBites[ni, nj]) {
						var undo = state.Move(horizontalDir);
						if (undo.State == 1) {
							eatenCount++;
						}
						(hi, hj) = state.Head;
						continue;
					}
				}

				// 行き詰まったら下に移動
				if (hi < n - 1) {
					state.Move(Direction4.D);
					(hi, hj) = state.Head;
					break;
				} else {
					break;
				}
			}
		}

		Console.WriteLine($"[ValidateMoveUndo2] Ate {eatenCount} food items, Boros.Count={state.Boros.Count}");

		// Phase 2: 胴体の適当な座標に向かって移動し、噛む
		if (state.Boros.Count <= 5) {
			Console.WriteLine("[ValidateMoveUndo2] Snake is too short, skipping body bite test");
			return;
		}

		Console.WriteLine($"[ValidateMoveUndo2] Phase 2: Moving randomly until a body bite occurs");

		// 噛む直前の状態を記録
		var originalHash = state.Hash;
		var originalE = state.E;
		var originalK = state.K;
		var originalNoErrorCount = state.NoErrorCount;
		var originalOpsCount = state.Ops.Count;
		var originalBorosCount = state.Boros.Count;
		var originalColorsCount = state.Colors.Count;

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

		// 胴体を噛むまで移動
		var undoStack = new Stack<UndoInfo>();
		bool bites = false;
		int maxRandomMoves = 10000;
		int randomMoves = 0;

		while (!bites && randomMoves < maxRandomMoves) {
			var (hi, hj) = state.Head;
			var lastDir = state.HeadDir;
			Direction4? biteDir = null;

			foreach (var dir in Direction4Extensions.ForEach4()) {
				if (dir.Reverse() == lastDir) continue;
				var (ni, nj) = dir.Move(hi, hj);
				if ((uint)ni < n && (uint)nj < n && state.WillBites[ni, nj]) {
					biteDir = dir;
					break;
				}
			}

			Direction4 moveDir;
			if (biteDir.HasValue) {
				moveDir = biteDir.Value;
			} else {
				var tempDirs = new List<Direction4>();
				foreach (var dir in Direction4Extensions.ForEach4()) {
					if (dir.Reverse() == lastDir) continue;
					var (ni, nj) = dir.Move(hi, hj);
					if ((uint)ni < n && (uint)nj < n) {
						tempDirs.Add(dir);
					}
				}
				if (tempDirs.Count > 0) {
					moveDir = tempDirs[rnd.Next(tempDirs.Count)];
				} else {
					break; // 行き止まり
				}
			}

			var undo = state.Move(moveDir);
			undoStack.Push(undo);
			if (undo.State == 0) {
				bites = true;
			}
			randomMoves++;
		}

		if (bites == false) {
			throw new Exception("biting not done.");
		}

		Console.WriteLine($"[ValidateMoveUndo2] Moved to body position, bite occurred: {undoStack.Peek().State == 0}");
		Console.WriteLine($"[ValidateMoveUndo2] After bite: Boros.Count={state.Boros.Count}, Colors.Count={state.Colors.Count}");

		// Phase 3: 全てのMoveをUndoして検証
		Console.WriteLine("[ValidateMoveUndo2] Phase 3: Undoing all moves");
		while (undoStack.Count > 0) {
			var undo = undoStack.Pop();
			state.Undo(undo);
		}

		Console.WriteLine($"[ValidateMoveUndo2] After undo: Boros.Count={state.Boros.Count}, Colors.Count={state.Colors.Count}");

		// 状態が完全に復元されたか検証
		if (state.Hash != originalHash) {
			throw new Exception($"[ValidateMoveUndo2] Hash mismatch (expected={originalHash}, actual={state.Hash})");
		}

		if (state.E != originalE) {
			throw new Exception($"[ValidateMoveUndo2] E mismatch (expected={originalE}, actual={state.E})");
		}

		if (state.K != originalK) {
			throw new Exception($"[ValidateMoveUndo2] K mismatch (expected={originalK}, actual={state.K})");
		}

		if (state.NoErrorCount != originalNoErrorCount) {
			throw new Exception($"[ValidateMoveUndo2] NoErrorCount mismatch (expected={originalNoErrorCount}, actual={state.NoErrorCount})");
		}

		if (state.Ops.Count != originalOpsCount) {
			throw new Exception($"[ValidateMoveUndo2] Ops.Count mismatch (expected={originalOpsCount}, actual={state.Ops.Count})");
		}

		if (state.Boros.Count != originalBorosCount) {
			throw new Exception($"[ValidateMoveUndo2] Boros.Count mismatch (expected={originalBorosCount}, actual={state.Boros.Count})");
		}

		if (state.Colors.Count != originalColorsCount) {
			throw new Exception($"[ValidateMoveUndo2] Colors.Count mismatch (expected={originalColorsCount}, actual={state.Colors.Count})");
		}

		// Borosの内容を検証
		for (int i = 0; i < state.Boros.Count; i++) {
			if (state.Boros[i] != originalBoros[i]) {
				throw new Exception($"[ValidateMoveUndo2] Boros[{i}] mismatch (expected={originalBoros[i]}, actual={state.Boros[i]})");
			}
		}

		// Colorsの内容を検証
		for (int i = 0; i < state.Colors.Count; i++) {
			if (state.Colors[i] != originalColors[i]) {
				throw new Exception($"[ValidateMoveUndo2] Colors[{i}] mismatch (expected={originalColors[i]}, actual={state.Colors[i]})");
			}
		}

		// WillBitesの内容を検証
		for (int i = 0; i < n; i++) {
			for (int j = 0; j < n; j++) {
				if (state.WillBites[i, j] != originalWillBites[i, j]) {
					throw new Exception($"[ValidateMoveUndo2] WillBites[{i},{j}] mismatch (expected={originalWillBites[i, j]}, actual={state.WillBites[i, j]})");
				}
			}
		}

		// Esasの内容を検証
		for (int i = 0; i < n; i++) {
			for (int j = 0; j < n; j++) {
				if (state.Esas[i, j] != originalEsas[i, j]) {
					throw new Exception($"[ValidateMoveUndo2] Esas[{i},{j}] mismatch (expected={originalEsas[i, j]}, actual={state.Esas[i, j]})");
				}
			}
		}

		Console.WriteLine("[ValidateMoveUndo2] All validations passed!");
	}

	static State Plan1(Random rnd, Stopwatch sw, bool isLocal, Input input, Information info, Zob zob)
	{
		var state = new State(isLocal, input, zob);

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

	static State Plan2(Random rnd, Stopwatch sw, bool isLocal, Input input, Information info, int max, Zob zob)
	{
		var state = new State(isLocal, input, zob);

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

			long tempScore = state.Score;
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

	static State Plan3(Random rnd, Stopwatch sw, bool isLocal, Input input, Information info, int max, Zob zob, int maxStep)
	{
		var state = new State(isLocal, input, zob);

		State best = state.Clone();
		var (bestScore, _, _) = state.Calculate();

		while (state.Ops.Count <= max) {
			bool isFound = false;
			if (state.NoErrorCount == state.Colors.Count) {
				int moveMax = Math.Min(maxStep, input.M - state.Colors.Count);
				Span<byte> neededColors = stackalloc byte[moveMax];
				for (int i = 0; i < moveMax; i++) {
					neededColors[i] = (byte)input.D[state.Colors.Count + i];
				}

				var path = FindBestPathDFS3(state, input, neededColors, maxStep, -1);
				if (path is not null) {
					isFound = true;
					foreach (var dir in path) {
						state.Move(dir);
					}
				}
			}

			if (isFound == false) {
				var dir = FindNextStepGreedy2(rnd, input, info, state);
				var undo = state.Move(dir);
				if (undo.State == 0) {
					info.KamiCount++;
				} else if (info.KamiCount > 10 && undo.State == 1) {
					//info.KamiCount = 0;
				}
			}

			long tempScore = state.Score;
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

	static State Plan4(Random rnd, Stopwatch sw, bool isLocal, Input input, Information info, int maxOp, Zob zob)
	{
		bool isDebugPlan4 = false; // Plan4のデバッグ出力フラグ（一時的にtrue）
		var state = new State(isLocal, input, zob);
		int n = input.N;
		int m = input.M;

		var bfsQueue = new Queue<(int i, int j)>();
		var bfsVisited = new bool[n, n];
		var bfsPrev = new (int pi, int pj, Direction4 fromDir)[n, n];

		// 何行配置できるか（最大N行、またはM-5個の色を全て配置できる行数）
		// 初期の5個は既に保持しているため、盤面に配置する必要があるのはM-5個
		int maxRows = Math.Min(n, (m - 5 + n - 1) / n);

		if (isDebugPlan4) {
			Console.WriteLine($"Plan4 Start: M={m}, N={n}, maxRows={maxRows}");
			Console.WriteLine();
		}

		// 各行をループで処理
		for (int row = 0; row < maxRows; row++) {
			// この行で配置する色の範囲
			int targetFromBackStart = row * n;
			int targetFromBackEnd = Math.Min((row + 1) * n, m - 5);
			int targetCountForRow = targetFromBackEnd - targetFromBackStart;
			int targetFromBack = 0; // この行で何個処理したか
			int maxRetries = 10;
			int retryCount = 0;

			if (isDebugPlan4) {
				Console.WriteLine($"=== Row {row} Start ===");
				Console.WriteLine($"Target colors: D[{m - targetFromBackEnd}..{m - targetFromBackStart - 1}]");
				Console.Write("Expected colors (from back): ");
				for (int i = 0; i < targetCountForRow; i++) {
					Console.Write($"{input.D[m - 1 - targetFromBackStart - i]} ");
				}
				Console.WriteLine();
				Console.WriteLine();
			}

			while (retryCount < maxRetries && state.Ops.Count < maxOp) {
				// Step 1: この行用のN色を集める（既に配置した行は避ける）
				int consecutiveFailures = 0; // DFSの連続失敗回数
				int maxConsecutiveFailures = 100; // 100回連続失敗したら諦める

				while (targetFromBack < targetCountForRow) {
					if (isDebugPlan4) {
						Console.WriteLine($"[Row {row} Step1 Loop] targetFromBack={targetFromBack}/{targetCountForRow}");
						Console.Write("  Current Colors: [");
						for (int i = 0; i < state.Colors.Count; i++) {
							Console.Write($"{state.Colors[i]}");
							if (i < state.Colors.Count - 1) Console.Write(", ");
						}
						Console.WriteLine("]");
					}

					// 次に必要な色の配列を作成（最大8個先まで）
					int neededCount = Math.Min(8, targetCountForRow - targetFromBack);
					Span<byte> neededColors = stackalloc byte[neededCount];
					bool isFinalRow = (row == maxRows - 1);
					for (int i = 0; i < neededCount; i++) {
						int index;
						if (isFinalRow) {
							// 最終行は前から（配置済みの行を食べるための準備）
							index = m - targetFromBackEnd + targetFromBack + i;
						} else {
							// それ以外は後ろから（配置のための準備）
							index = m - 1 - targetFromBackStart - targetFromBack - i;
						}
						neededColors[i] = (byte)input.D[index];
					}

					if (isDebugPlan4 && neededColors.Length > 0) {
						Console.Write("  Needed colors: [");
						for (int i = 0; i < neededColors.Length; i++) {
							Console.Write($"{neededColors[i]}");
							if (i < neededColors.Length - 1) Console.Write(", ");
						}
						Console.WriteLine("]");
					}

					if (neededColors.Length == 0) break;

					// FindBestPathDFSで経路を探索（既に配置した行は避ける）
					var path = FindBestPathDFS3(state, input, neededColors, 8, row - 1);

					if (isDebugPlan4) {
						if (path != null && path.Length > 0) {
							Console.Write($"  DFS found path (length={path.Length}): ");
							for (int i = 0; i < path.Length; i++) {
								Console.Write($"{path[i].ToSymbol()}");
							}
							Console.WriteLine();
						} else {
							Console.WriteLine("  DFS: No path found");
						}
					}

					if (path != null && path.Length > 0) {
						// 経路に従って移動し、実際に必要な色を集めたかを検証
						int collectedBefore = state.Colors.Count;
						foreach (var dir in path) {
							state.Move(dir);
						}
						int collectedCount = state.Colors.Count - collectedBefore;

						// 侵入チェック：DFS実行後
						if (isDebugPlan4) {
							var (checkI, checkJ) = state.Head;
							if (checkI < row) {
								Console.WriteLine($"  ★★★ INVASION DETECTED after DFS: Head=({checkI},{checkJ}), should be > row {row} ★★★");
							} else {
								Console.WriteLine($"  DFS OK: Head=({checkI},{checkJ}), row={row}");
							}
						}

						// 実際に集めた色が期待通りかを検証（デバッグ用）
						int matchedCount = 0;
						for (int i = 0; i < collectedCount && targetFromBack + matchedCount < targetCountForRow; i++) {
							int colorIndex = collectedBefore + i;
							if (colorIndex < state.Colors.Count) {
								byte collectedColor = state.Colors[colorIndex];
								byte expectedColor = (byte)input.D[m - 1 - targetFromBackStart - targetFromBack - matchedCount];
								if (collectedColor == expectedColor) {
									matchedCount++;
									if (isDebugPlan4) {
										Console.WriteLine($"    DFS Match[{i}]: collected={collectedColor}, expected={expectedColor}");
									}
								} else {
									if (isDebugPlan4) {
										Console.WriteLine($"    DFS Mismatch[{i}]: collected={collectedColor}, expected={expectedColor} - CONTINUE (wrong color will be eaten)");
									}
								}
							}
						}
						if (isDebugPlan4) {
							Console.WriteLine($"  DFS execution: matched={matchedCount}, collected={collectedCount}, targetFromBack: {targetFromBack} -> {targetFromBack + collectedCount}");
						}
						// 重要: matchedCountではなくcollectedCountを加算
						targetFromBack += collectedCount;

						// 色を集められたので失敗カウントをリセット
						consecutiveFailures = 0;
					} else {
						// 経路が見つからない場合
						consecutiveFailures++;

						// 連続失敗が閾値を超えたら諦める
						if (consecutiveFailures >= maxConsecutiveFailures) {
							if (isDebugPlan4) {
								Console.WriteLine($"  Too many consecutive failures ({consecutiveFailures}), giving up on this row");
							}
							break;
						}

						// 胴体を噛まない範囲で空マスにランダム移動
						// 既に配置した行（0からrow-1まで）は避ける
						var (hi, hj) = state.Head;
						var lastDir = state.HeadDir;

						// 連続失敗が少ない場合は空マスへのランダム移動を優先（方向転換のため）
						bool usedRandomMove = false;
						if (consecutiveFailures < 20) {
							var validDirs = new List<Direction4>();
							foreach (var dir in Direction4Extensions.ForEach4()) {
								if (dir.Reverse() == lastDir) continue;
								var (ni, nj) = dir.Move(hi, hj);
								// 空マスかつ胴体を噛まない、かつ既に配置した行を避ける
								if ((uint)ni < n && (uint)nj < n && ni >= row && !state.WillBites[ni, nj] && state.Esas[ni, nj] == 0) {
									validDirs.Add(dir);
								}
							}

							if (validDirs.Count > 0) {
								if (isDebugPlan4) {
									Console.WriteLine("  Trying random move to empty cell...");
								}
								state.Move(validDirs[rnd.Next(validDirs.Count)]);
								if (isDebugPlan4) {
									var (checkI, checkJ) = state.Head;
									if (checkI < row) {
										Console.WriteLine($"  ★★★ INVASION DETECTED after random move: Head=({checkI},{checkJ}), should be > row {row} ★★★");
									} else {
										Console.WriteLine($"  Random move OK: Head=({checkI},{checkJ}), row={row}");
									}
								}
								usedRandomMove = true;
							}
						}

						if (!usedRandomMove) {
							// 空マス移動が不可能またはランダム移動の回数制限超過の場合、
							// BFSで目的の色に向かいリセット
							if (isDebugPlan4) {
								Console.WriteLine("  No valid empty cell or threshold reached - using BFS to reach target color then reset...");
							}

							byte bfsTargetColor = neededColors[0];
							bfsQueue.Clear();
							Array.Clear(bfsVisited, 0, bfsVisited.Length);

							bfsVisited[hi, hj] = true;
							{
								var (bfsBackI, bfsBackJ) = lastDir.Reverse().Move(hi, hj);
								if ((uint)bfsBackI < n && (uint)bfsBackJ < n) bfsVisited[bfsBackI, bfsBackJ] = true;
							}
							bfsQueue.Enqueue((hi, hj));

							// Fisher-Yatesシャッフルで移動順をランダム化
							var bfsDirs = new[] { Direction4.D, Direction4.L, Direction4.U, Direction4.R };
							for (int bfsK = bfsDirs.Length - 1; bfsK > 0; bfsK--) {
								int bfsSwap = rnd.Next(bfsK + 1);
								(bfsDirs[bfsK], bfsDirs[bfsSwap]) = (bfsDirs[bfsSwap], bfsDirs[bfsK]);
							}

							int bfsFoundI = -1, bfsFoundJ = -1;
							while (bfsQueue.Count > 0) {
								var (bfsCi, bfsCj) = bfsQueue.Dequeue();
								if ((bfsCi, bfsCj) != (hi, hj) && state.Esas[bfsCi, bfsCj] == bfsTargetColor) {
									bfsFoundI = bfsCi;
									bfsFoundJ = bfsCj;
									break;
								}
								foreach (var bfsDir in bfsDirs) {
									var (bfsNi, bfsNj) = bfsDir.Move(bfsCi, bfsCj);
									if ((uint)bfsNi >= n || (uint)bfsNj >= n) continue;
									if (bfsNi < row) continue;
									if (state.WillBites[bfsNi, bfsNj]) continue;
									if (bfsVisited[bfsNi, bfsNj]) continue;
									bfsVisited[bfsNi, bfsNj] = true;
									bfsPrev[bfsNi, bfsNj] = (bfsCi, bfsCj, bfsDir);
									bfsQueue.Enqueue((bfsNi, bfsNj));
								}
							}

							bool movedForReset = false;
							if (bfsFoundI >= 0) {
								var bfsPath = new Stack<Direction4>();
								int bfsTi = bfsFoundI, bfsTj = bfsFoundJ;
								while ((bfsTi, bfsTj) != (hi, hj)) {
									var (bfsPi, bfsPj, bfsMoveDir) = bfsPrev[bfsTi, bfsTj];
									bfsPath.Push(bfsMoveDir);
									(bfsTi, bfsTj) = (bfsPi, bfsPj);
								}
								if (isDebugPlan4) {
									Console.WriteLine($"  BFS found path (length={bfsPath.Count}), following...");
								}
								while (bfsPath.Count > 0) {
									state.Move(bfsPath.Pop());
								}
								movedForReset = true;
							} else {
								// BFSも失敗した場合、任意の方向へ移動
								var allDirs = new List<Direction4>();
								foreach (var dir in Direction4Extensions.ForEach4()) {
									if (dir.Reverse() == lastDir) continue;
									var (ni, nj) = dir.Move(hi, hj);
									if ((uint)ni < n && (uint)nj < n && ni >= row) {
										allDirs.Add(dir);
									}
								}
								if (allDirs.Count > 0) {
									state.Move(allDirs[rnd.Next(allDirs.Count)]);
									movedForReset = true;
								}
							}

							if (movedForReset) {
								// Borosの長さが5になるまで胴体を噛み続ける
								while (state.Boros.Count > 5) {
									(hi, hj) = state.Head;
									lastDir = state.HeadDir;

									Direction4? biteDir = null;
									foreach (var dir in Direction4Extensions.ForEach4()) {
										if (dir.Reverse() == lastDir) continue;
										var (ni, nj) = dir.Move(hi, hj);
										if ((uint)ni < n && (uint)nj < n && ni >= row && state.WillBites[ni, nj]) {
											biteDir = dir;
											break;
										}
									}

									if (biteDir.HasValue) {
										state.Move(biteDir.Value);
									} else {
										var tempDirs = new List<Direction4>();
										foreach (var dir in Direction4Extensions.ForEach4()) {
											if (dir.Reverse() == lastDir) continue;
											var (ni, nj) = dir.Move(hi, hj);
											if ((uint)ni < n && (uint)nj < n && ni >= row) {
												tempDirs.Add(dir);
											}
										}
										if (tempDirs.Count > 0) {
											state.Move(tempDirs[rnd.Next(tempDirs.Count)]);
										} else {
											break;
										}
									}
								}
								if (isDebugPlan4) {
									var (checkI, checkJ) = state.Head;
									Console.WriteLine($"  Body bite reset complete: targetFromBack {targetFromBack} -> 0, Head=({checkI},{checkJ})");
									if (checkI < row) {
										Console.WriteLine($"  ★★★ INVASION DETECTED after body bite reset in Step1: Head=({checkI},{checkJ}), should be > row {row} ★★★");
									}
								}
								targetFromBack = 0;
							} else {
								break; // 移動不可能
							}
						}
					}

					// 無限ループ防止
					if (state.Ops.Count > maxOp) break;

					if (isDebugPlan4) {
						Console.WriteLine();
					}
				}

				// Step 1で目標に到達できなかった場合はリトライ
				if (targetFromBack < targetCountForRow) {
					retryCount++;
					continue;
				}

				// 最終行の場合、配置せずに配置済みの行を食べていく
				if (row == maxRows - 1) {
					if (isDebugPlan4) {
						Console.WriteLine($"[Row {row}] Final row completed, now eating placed rows from bottom to top");
						Console.WriteLine();
					}

					bool eatingSucceeded = true;

					// 配置済みの行を下から順番に食べる（蛇行）
					for (int r = maxRows - 2; r >= 0 && eatingSucceeded; r--) {
						bool isEvenRowForEating = (r % 2 == 0);
						int startJForEating = isEvenRowForEating ? 0 : n - 1;
						int endJForEating = isEvenRowForEating ? n - 1 : 0;
						Direction4 horizontalDirForEating = isEvenRowForEating ? Direction4.R : Direction4.L;

						if (isDebugPlan4) {
							Console.WriteLine($"[Eating Row {r}] Moving to ({r + 1},{startJForEating}) then entering row {r}");
						}

						// Step 1: (r+1, startJForEating) の位置に移動
						int maxMoveAttemptsForEating = 10000;
						int moveCountForEating = 0;
						while (state.Head != (r + 1, startJForEating) && moveCountForEating < maxMoveAttemptsForEating) {
							var (hi, hj) = state.Head;
							var lastDir = state.HeadDir;

							// 目標方向を決定
							Direction4 targetDir = Direction4.N;
							if (hi < r + 1) targetDir = Direction4.D;
							else if (hi > r + 1) targetDir = Direction4.U;
							else if (hj < startJForEating) targetDir = Direction4.R;
							else if (hj > startJForEating) targetDir = Direction4.L;

							// 目標方向に移動可能かチェック（未食の配置済み行は避ける）
							bool moved = false;
							if (targetDir != Direction4.N && targetDir.Reverse() != lastDir) {
								var (ni, nj) = targetDir.Move(hi, hj);
								if ((uint)ni < n && (uint)nj < n && ni > r && !state.WillBites[ni, nj]) {
									state.Move(targetDir);
									moved = true;
								}
							}

							// 直接移動できない場合、迂回（未食の配置済み行は避ける）
							if (!moved) {
								var validDirs = new List<Direction4>();
								foreach (var dir in Direction4Extensions.ForEach4()) {
									if (dir.Reverse() == lastDir) continue;
									var (ni, nj) = dir.Move(hi, hj);
									if ((uint)ni < n && (uint)nj < n && ni > r && !state.WillBites[ni, nj]) {
										validDirs.Add(dir);
									}
								}

								if (validDirs.Count > 0) {
									state.Move(validDirs[0]);
								} else {
									if (isDebugPlan4) {
										Console.WriteLine($"[Eating Row {r}] Failed to reach ({r + 1},{startJForEating})");
									}
									eatingSucceeded = false;
									break; // 移動不可能
								}
							}
							moveCountForEating++;
						}

						if (!eatingSucceeded) break;

						// Step 2: 行 r に侵入
						if (state.Head == (r + 1, startJForEating) && state.HeadDir != Direction4.D) {
							state.Move(Direction4.U);
						}

						if (isDebugPlan4) {
							var (checkI, checkJ) = state.Head;
							Console.WriteLine($"[Eating Row {r}] Entered at ({checkI},{checkJ}), now moving to column {endJForEating}");
						}

						// Step 3: 行 r を横断して餌を食べる
						moveCountForEating = 0;
						while (state.Head.j != endJForEating && moveCountForEating < maxMoveAttemptsForEating) {
							var (hi, hj) = state.Head;
							var lastDir = state.HeadDir;

							if (lastDir != horizontalDirForEating.Reverse()) {
								var (ni, nj) = horizontalDirForEating.Move(hi, hj);
								if ((uint)ni < n && (uint)nj < n && !state.WillBites[ni, nj]) {
									state.Move(horizontalDirForEating);
									moveCountForEating++;
									continue;
								}
							}

							// 直接移動できない場合は迂回（行r内のみ）
							var validDirs = new List<Direction4>();
							foreach (var dir in Direction4Extensions.ForEach4()) {
								if (dir.Reverse() == lastDir) continue;
								var (ni, nj) = dir.Move(hi, hj);
								// 行r内のみを許可
								if ((uint)ni < n && (uint)nj < n && ni == r && !state.WillBites[ni, nj]) {
									validDirs.Add(dir);
								}
							}

							if (validDirs.Count > 0) {
								state.Move(validDirs[0]);
							} else {
								if (isDebugPlan4) {
									Console.WriteLine($"[Eating Row {r}] Failed to reach column {endJForEating}");
								}
								eatingSucceeded = false;
								break; // 移動不可能
							}
							moveCountForEating++;
						}

						if (!eatingSucceeded) break;

						if (isDebugPlan4) {
							var (checkI, checkJ) = state.Head;
							Console.WriteLine($"[Eating Row {r}] Completed at ({checkI},{checkJ})");
							Console.WriteLine();
						}
					}

					if (!eatingSucceeded) {
						// 蛇行失敗、胴体を噛んでリトライ
						while (state.Boros.Count > 5) {
							var (hi, hj) = state.Head;
							var lastDir = state.HeadDir;
							Direction4? biteDir = null;
							foreach (var dir in Direction4Extensions.ForEach4()) {
								if (dir.Reverse() == lastDir) continue;
								var (ni, nj) = dir.Move(hi, hj);
								if ((uint)ni < n && (uint)nj < n && ni >= row && state.WillBites[ni, nj]) {
									biteDir = dir;
									break;
								}
							}

							if (biteDir.HasValue) {
								state.Move(biteDir.Value);
							} else {
								var tempDirs = new List<Direction4>();
								foreach (var dir in Direction4Extensions.ForEach4()) {
									if (dir.Reverse() == lastDir) continue;
									var (ni, nj) = dir.Move(hi, hj);
									if ((uint)ni < n && (uint)nj < n && ni >= row) {
										tempDirs.Add(dir);
									}
								}
								if (tempDirs.Count > 0) {
									state.Move(tempDirs[rnd.Next(tempDirs.Count)]);
								} else {
									break;
								}
							}
						}
						if (isDebugPlan4) {
							var (checkI, checkJ) = state.Head;
							Console.WriteLine($"[Row {row}] Eating failed, retry {retryCount + 1}/{maxRetries}: targetFromBack {targetFromBack} -> 0, Head=({checkI},{checkJ})");
						}
						targetFromBack = 0;
						retryCount++;
						continue;
					}

					break;
				}

				// Step 2-5: 偶奇で場合分け
				bool isEvenRow = (row % 2 == 0);
				int targetRow = row;
				int targetJ = isEvenRow ? 0 : n - 1; // 侵入後の目標j座標
				int endJ = isEvenRow ? n - 1 : 0; // 最終目標j座標

				// Step 2: (row+1, targetJ)の位置に移動（既に配置した行は避ける）
				int maxMoveAttempts = 10000;
				int moveCount = 0;
				bool step2Failed = false;
				while (state.Head != (row + 1, targetJ) && moveCount < maxMoveAttempts) {
					var (hi, hj) = state.Head;
					var lastDir = state.HeadDir;

					// 目標方向を決定
					Direction4 targetDir = Direction4.N;
					if (hi < row + 1) targetDir = Direction4.D;
					else if (hi > row + 1) targetDir = Direction4.U;
					else if (hj < targetJ) targetDir = Direction4.R;
					else if (hj > targetJ) targetDir = Direction4.L;

					// 目標方向に移動可能かチェック（既に配置した行は避ける）
					bool moved = false;
					if (targetDir != Direction4.N && targetDir.Reverse() != lastDir) {
						var (ni, nj) = targetDir.Move(hi, hj);
						if ((uint)ni < n && (uint)nj < n && ni > row && !state.WillBites[ni, nj]) {
							state.Move(targetDir);
							moved = true;
						}
					}

					// 直接移動できない場合、迂回（既に配置した行と配置予定の行は避ける）
					if (!moved) {
						var validDirs = new List<Direction4>();
						foreach (var dir in Direction4Extensions.ForEach4()) {
							if (dir.Reverse() == lastDir) continue;
							var (ni, nj) = dir.Move(hi, hj);
							if ((uint)ni < n && (uint)nj < n && ni > row && !state.WillBites[ni, nj]) {
								validDirs.Add(dir);
							}
						}

						if (validDirs.Count > 0) {
							state.Move(validDirs[0]);
						} else {
							step2Failed = true;
							break;
						}
					}
					moveCount++;
				}

				// Step 2完了時の侵入チェック
				if (isDebugPlan4) {
					var (checkI, checkJ) = state.Head;
					Console.WriteLine($"[Row {row} Step2 Complete] Head=({checkI},{checkJ}), expected=({row + 1},{targetJ})");
					if (checkI <= row) {
						Console.WriteLine($"  ★★★ INVASION DETECTED after Step2: Head=({checkI},{checkJ}), should be > row {row} ★★★");
					}
				}

				// Step 2で目標位置に到達できなかった場合、胴体を噛んでリセット。ここは配置予定の行を移動してもよい。
				if (step2Failed || state.Head != (row + 1, targetJ)) {
					while (state.Boros.Count > 5) {
						var (hi, hj) = state.Head;
						var lastDir = state.HeadDir;
						Direction4? biteDir = null;
						foreach (var dir in Direction4Extensions.ForEach4()) {
							if (dir.Reverse() == lastDir) continue;
							var (ni, nj) = dir.Move(hi, hj);
							if ((uint)ni < n && (uint)nj < n && ni >= row && state.WillBites[ni, nj]) {
								biteDir = dir;
								break;
							}
						}

						if (biteDir.HasValue) {
							state.Move(biteDir.Value);
						} else {
							var tempDirs = new List<Direction4>();
							foreach (var dir in Direction4Extensions.ForEach4()) {
								if (dir.Reverse() == lastDir) continue;
								var (ni, nj) = dir.Move(hi, hj);
								if ((uint)ni < n && (uint)nj < n && ni >= row) {
									tempDirs.Add(dir);
								}
							}
							if (tempDirs.Count > 0) {
								state.Move(tempDirs[rnd.Next(tempDirs.Count)]);
							} else {
								break;
							}
						}
					}
					if (isDebugPlan4) {
						var (checkI, checkJ) = state.Head;
						Console.WriteLine($"[Row {row} Step2 Failed] Body bite reset: targetFromBack {targetFromBack} -> 0, retry {retryCount + 1}/{maxRetries}, Head=({checkI},{checkJ})");
						if (checkI <= row) {
							Console.WriteLine($"  ★★★ INVASION DETECTED after Step2 body bite reset: Head=({checkI},{checkJ}), should be > row {row} ★★★");
						}
					}
					targetFromBack = 0;
					retryCount++;
					continue;
				}

				// Step 3: (row, targetJ)に侵入
				if (state.Head == (row + 1, targetJ) && state.HeadDir != Direction4.D) {
					state.Move(Direction4.U);
				}

				// Step 3完了時の侵入チェック
				if (isDebugPlan4) {
					var (checkI, checkJ) = state.Head;
					Console.WriteLine($"[Row {row} Step3 Complete] Head=({checkI},{checkJ}), expected=({row},{targetJ})");
					if (checkI != row) {
						Console.WriteLine($"  ★★★ INVASION DETECTED after Step3: Head=({checkI},{checkJ}), should be at row {row} ★★★");
					}
				}

				// Step 4: (row, endJ)まで移動
				moveCount = 0;
				bool step4Failed = false;
				Direction4 moveDir = isEvenRow ? Direction4.R : Direction4.L;
				Direction4 reverseDir = moveDir.Reverse();

				while (state.Head.j != endJ && moveCount < maxMoveAttempts) {
					var (hi, hj) = state.Head;
					var lastDir = state.HeadDir;

					if (lastDir != reverseDir) {
						var (ni, nj) = moveDir.Move(hi, hj);
						if ((uint)ni < n && (uint)nj < n && !state.WillBites[ni, nj]) {
							state.Move(moveDir);
							moveCount++;
							continue;
						}
					}

					// 直接移動できない場合は迂回（行row内のみ）
					var validDirs = new List<Direction4>();
					foreach (var dir in Direction4Extensions.ForEach4()) {
						if (dir.Reverse() == lastDir) continue;
						var (ni, nj) = dir.Move(hi, hj);
						// 行row内のみを許可
						if ((uint)ni < n && (uint)nj < n && ni == row && !state.WillBites[ni, nj]) {
							validDirs.Add(dir);
						}
					}

					if (validDirs.Count > 0) {
						state.Move(validDirs[0]);
					} else {
						step4Failed = true;
						break;
					}
					moveCount++;
				}

				// Step 4完了時の侵入チェック
				if (isDebugPlan4) {
					var (checkI, checkJ) = state.Head;
					Console.WriteLine($"[Row {row} Step4 Complete] Head=({checkI},{checkJ}), expected=({row},{endJ})");
					if (checkI != row) {
						Console.WriteLine($"  ★★★ INVASION DETECTED after Step4: Head=({checkI},{checkJ}), should be at row {row} ★★★");
					}
				}

				// Step 4で目標位置に到達できなかった場合、胴体を噛んでリセット
				if (step4Failed || state.Head.j != endJ) {
					while (state.Boros.Count > 5) {
						var (hi, hj) = state.Head;
						var lastDir = state.HeadDir;
						Direction4? biteDir = null;
						foreach (var dir in Direction4Extensions.ForEach4()) {
							if (dir.Reverse() == lastDir) continue;
							var (ni, nj) = dir.Move(hi, hj);
							if ((uint)ni < n && (uint)nj < n && ni > row && state.WillBites[ni, nj]) {
								biteDir = dir;
								break;
							}
						}

						if (biteDir.HasValue) {
							state.Move(biteDir.Value);
						} else {
							var tempDirs = new List<Direction4>();
							foreach (var dir in Direction4Extensions.ForEach4()) {
								if (dir.Reverse() == lastDir) continue;
								var (ni, nj) = dir.Move(hi, hj);
								if ((uint)ni < n && (uint)nj < n && ni > row) {
									tempDirs.Add(dir);
								}
							}
							if (tempDirs.Count > 0) {
								state.Move(tempDirs[rnd.Next(tempDirs.Count)]);
							} else {
								break;
							}
						}
					}
					if (isDebugPlan4) {
						var (checkI, checkJ) = state.Head;
						Console.WriteLine($"[Row {row} Step4 Failed] Body bite reset: targetFromBack {targetFromBack} -> 0, retry {retryCount + 1}/{maxRetries}, Head=({checkI},{checkJ})");
						if (checkI <= row) {
							Console.WriteLine($"  ★★★ INVASION DETECTED after Step4 body bite reset: Head=({checkI},{checkJ}), should be > row {row} ★★★");
						}
					}
					targetFromBack = 0;
					retryCount++;
					continue;
				}

				// Step 5: 胴体を噛むシーケンス（偶奇で異なる）
				Direction4[] sequence = isEvenRow
					? new[] { Direction4.D, Direction4.D, Direction4.L, Direction4.U, Direction4.R }
					: new[] { Direction4.D, Direction4.D, Direction4.R, Direction4.U, Direction4.L };

				foreach (var dir in sequence) {
					if (dir.Reverse() != state.HeadDir) {
						var (hi, hj) = state.Head;
						var (ni, nj) = dir.Move(hi, hj);
						if ((uint)ni < n && (uint)nj < n) {
							state.Move(dir);
						}
					}
				}

				// この行の処理が完了したらループを抜ける
				break;
			}

			if (isDebugPlan4) {
				Console.WriteLine($"=== Row {row} Complete ===");
				Console.WriteLine();
			}
		}

		return state;
	}

	static State Plan8(Random rnd, Stopwatch sw, bool isLocal, Input input, Information info, int depth, int beamWidth, Zob zob, int limit)
	{
		var state = new State(isLocal, input, zob);
		State best = state.Clone();
		long bestScore = best.Score;

		int n = input.N;

		// ビーム内の状態を保持（Clone済み）
		var beam = new List<State>(beamWidth) { state.Clone() };
		var nextBeam = new List<State>(beamWidth);
		var visitedHashes = new HashSet<long>(beamWidth * 4) { state.Hash };
		var candidates = new List<(int parentIdx, Direction4 dir, long eval, long hash)>(beamWidth * 4);

		info.B = beamWidth;
		info.D = depth;
		for (int d = 0; d < depth; d++) {
			if (sw.ElapsedMilliseconds >= limit) {
				info.D = d;
				break;
			}

			candidates.Clear();

			// ビーム内の各状態から遷移を試す
			for (int beamIdx = 0; beamIdx < beam.Count; beamIdx++) {
				var currentState = beam[beamIdx];

				var (hi, hj) = currentState.Head;
				var lastDir = currentState.HeadDir;

				int oldCC = currentState.Colors.Count;
				int oldOC = currentState.Ops.Count;

				// 全方向を試す（Move→評価→Undoで効率的に探索）
				for (int dn = 0; dn < 4; dn++) {
					var dir = Direction4Extensions._delta4Dir[dn];
					if (dir.Reverse() == lastDir) {
						continue;
					}

					var (ni, nj) = dir.Move(hi, hj);
					if ((uint)ni < n && (uint)nj < n) {
						// 既にM個集めている場合、これ以上の探索は不要
						if (currentState.Colors.Count >= input.M) {
							continue;
						}

						// Moveして評価
						var undo = currentState.Move(dir);

						// M個を超えた場合は候補に追加しない（本来ここには来ないはず）
						if (currentState.Colors.Count > input.M) {
							currentState.Undo(undo);
							continue;
						}

						long hash = currentState.Hash;

						// 重複チェック
						if (!visitedHashes.Contains(hash)) {
							long eval = currentState.Evaluate();
							long tempScore = currentState.Score;

							if (tempScore < bestScore) {
								bestScore = tempScore;
								best = currentState.Clone();
							}

							candidates.Add((beamIdx, dir, eval, hash));
						}

						// Undoで元の状態に戻す
						currentState.Undo(undo);
					}
				}

				int newCC = currentState.Colors.Count;
				int newOC = currentState.Ops.Count;
				if (oldCC != newCC) {
					throw new Exception($"undo failed src color count = {oldCC} dst color count = {newCC}");
				}
				if (oldOC != newOC) {
					throw new Exception($"undo failed src ops count = {oldOC} dst ops count = {newOC}");
				}
			}

			if (candidates.Count == 0) {
				break;
			}

			// 評価値でソートしてビーム幅分残す
			candidates.Sort((a, b) => a.eval.CompareTo(b.eval));

			nextBeam.Clear();
			visitedHashes.Clear();

			int count = Math.Min(beamWidth, candidates.Count);
			for (int i = 0; i < count; i++) {
				var cand = candidates[i];
				var parentState = beam[cand.parentIdx];

				// 親状態をCloneして、dirで進めた状態を作る
				var nextState = parentState.Clone();
				nextState.Move(cand.dir);

				nextBeam.Add(nextState);
				visitedHashes.Add(cand.hash);
			}

			// ビームの入れ替え
			var temp = beam;
			beam = nextBeam;
			nextBeam = temp;
		}

		return best;
	}

	/*
	static State Plan9(Random rnd, Stopwatch sw, bool isLocal, Input input, Information info, Zob zob)
	{
		var state = new State(isLocal, input, zob);
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
	}*/

	/// <summary>
	/// DFSで指定歩数先まで探索し、必要な色を最も多く（同数なら最短歩数で）拾える経路を返す
	/// </summary>
	/// <param name="state">現在の状態（Move/Undoで変更される）</param>
	/// <param name="input">入力情報</param>
	/// <param name="neededColors">必要な色の配列（順序通りに拾う必要がある）</param>
	/// <param name="maxDepth">探索する最大歩数</param>
	/// <param name="avoidRowsBelow">この行番号以下には侵入しない（既に配置済みの行を避ける）</param>
	/// <returns>最適な移動経路。1つも拾えない場合はnull</returns>
	static Direction4[] FindBestPathDFS(State state, Input input, byte[] neededColors, int maxDepth, int avoidRowsBelow = -1)
	{
		int n = input.N;

		Direction4[] bestPath = null;
		int bestCollected = 0;
		int bestSteps = int.MaxValue;

		void DFS(int depth, List<Direction4> currentPath, int collectedIndex)
		{
			// 現在の経路を評価（より良い経路なら更新）
			if (collectedIndex > bestCollected ||
				(collectedIndex > 0 && collectedIndex == bestCollected && currentPath.Count > 0 && currentPath.Count < bestSteps)) {
				bestCollected = collectedIndex;
				bestSteps = currentPath.Count;
				bestPath = currentPath.ToArray();
			}

			// 深さ上限チェック
			if (depth >= maxDepth) {
				return;
			}

			// 必要な色を全て拾い終えたら終了
			if (collectedIndex >= neededColors.Length) {
				return;
			}

			var (hi, hj) = state.Head;
			var lastDir = state.HeadDir;

			foreach (var dir in Direction4Extensions.ForEach4()) {
				// Uターン禁止
				if (dir.Reverse() == lastDir) {
					continue;
				}

				var (ni, nj) = dir.Move(hi, hj);

				// 範囲チェック
				if ((uint)ni >= n || (uint)nj >= n) {
					continue;
				}

				// 既に配置済みの行には侵入しない
				if (ni <= avoidRowsBelow) {
					continue;
				}

				// 胴体への噛み付き禁止
				if (state.WillBites[ni, nj]) {
					continue;
				}

				// 餌がある場合、必要な色かチェック
				byte esa = state.Esas[ni, nj];
				if (esa > 0) {
					byte neededColor = neededColors[collectedIndex];
					if (esa != neededColor) {
						// 必要な色以外の餌は拾わない
						continue;
					}
				}

				// Move実行
				var undo = state.Move(dir);
				currentPath.Add(dir);

				int newCollectedIndex = collectedIndex;
				if (undo.State == 1) {
					// 餌を食べた
					newCollectedIndex++;
				}

				// 再帰探索
				DFS(depth + 1, currentPath, newCollectedIndex);

				// Undo
				currentPath.RemoveAt(currentPath.Count - 1);
				state.Undo(undo);
			}
		}

		DFS(0, new List<Direction4>(), 0);

		return bestCollected > 0 ? bestPath : null;
	}

	/// <summary>
	/// DFSで指定歩数先まで探索し、必要な色を最も多く（同数なら最短歩数で）拾える経路を返す（高速化版）
	/// </summary>
	/// <remarks>
	/// FindBestPathDFSとの違い：
	/// - 距離pruning: 次の必要な色に残り手数内で届かないノードを枝刈り
	/// - Move ordering: 次の必要な色に近い方向を優先して試す
	/// </remarks>
	/// <param name="state">現在の状態（Move/Undoで変更される）</param>
	/// <param name="input">入力情報</param>
	/// <param name="neededColors">必要な色の配列（順序通りに拾う必要がある）</param>
	/// <param name="maxDepth">探索する最大歩数</param>
	/// <param name="avoidRowsBelow">この行番号以下には侵入しない（既に配置済みの行を避ける）</param>
	/// <returns>最適な移動経路。1つも拾えない場合はnull</returns>
	static Direction4[] FindBestPathDFS2(State state, Input input, byte[] neededColors, int maxDepth, int avoidRowsBelow = -1)
	{
		int n = input.N;

		// (fi, fj) から color の餌までの最小マンハッタン距離を返す
		int CalcMinManhattan(int fi, int fj, byte color)
		{
			int min = int.MaxValue;
			foreach (var (pi, pj) in input.FoodPositionsByColor[color]) {
				if (state.Esas[pi, pj] == color) {
					int dist = Math.Abs(fi - pi) + Math.Abs(fj - pj);
					if (dist < min) {
						min = dist;
						if (dist <= 1) break;
					}
				}
			}
			return min;
		}

		Direction4[] bestPath = null;
		int bestCollected = 0;
		int bestSteps = int.MaxValue;

		void DFS(int depth, List<Direction4> currentPath, int collectedIndex)
		{
			// 現在の経路を評価（より良い経路なら更新）
			if (collectedIndex > bestCollected ||
				(collectedIndex > 0 && collectedIndex == bestCollected && currentPath.Count > 0 && currentPath.Count < bestSteps)) {
				bestCollected = collectedIndex;
				bestSteps = currentPath.Count;
				bestPath = currentPath.ToArray();
			}

			// 深さ上限チェック
			if (depth >= maxDepth) {
				return;
			}

			// 必要な色を全て拾い終えたら終了
			if (collectedIndex >= neededColors.Length) {
				return;
			}

			var (hi, hj) = state.Head;
			var lastDir = state.HeadDir;

			// 距離pruning: 次の必要な色に残り手数内で届かないなら枝刈り
			int remaining = maxDepth - depth;
			if (CalcMinManhattan(hi, hj, neededColors[collectedIndex]) > remaining) {
				return;
			}

			// Move ordering: 有効な方向を収集し、次の必要な色に近い順にソートして試す
			Span<Direction4> orderedDirs = stackalloc Direction4[4];
			Span<int> dirScores = stackalloc int[4];
			int dirCount = 0;

			foreach (var dir in Direction4Extensions._delta4Dir) {
				if (dir.Reverse() == lastDir) {
					continue;
				}

				var (ni, nj) = dir.Move(hi, hj);

				if ((uint)ni >= n || (uint)nj >= n) {
					continue;
				}

				if (ni <= avoidRowsBelow) {
					continue;
				}

				if (state.WillBites[ni, nj]) {
					continue;
				}

				byte esa = state.Esas[ni, nj];
				if (esa > 0 && esa != neededColors[collectedIndex]) {
					continue;
				}

				// 移動先が目的の餌なら0、空マスなら次の必要色への距離をスコアとする
				int score = esa == neededColors[collectedIndex]
					? 0
					: CalcMinManhattan(ni, nj, neededColors[collectedIndex]);

				orderedDirs[dirCount] = dir;
				dirScores[dirCount] = score;
				dirCount++;
			}

			// insertion sort（最大4要素）
			for (int ii = 1; ii < dirCount; ii++) {
				var kd = orderedDirs[ii];
				int ks = dirScores[ii];
				int jj = ii - 1;
				while (jj >= 0 && dirScores[jj] > ks) {
					orderedDirs[jj + 1] = orderedDirs[jj];
					dirScores[jj + 1] = dirScores[jj];
					jj--;
				}
				orderedDirs[jj + 1] = kd;
				dirScores[jj + 1] = ks;
			}

			for (int ii = 0; ii < dirCount; ii++) {
				var dir = orderedDirs[ii];
				var undo = state.Move(dir);
				currentPath.Add(dir);

				int newCollectedIndex = collectedIndex;
				if (undo.State == 1) {
					// 餌を食べた
					newCollectedIndex++;
				}

				// 再帰探索
				DFS(depth + 1, currentPath, newCollectedIndex);

				// Undo
				currentPath.RemoveAt(currentPath.Count - 1);
				state.Undo(undo);
			}
		}

		DFS(0, new List<Direction4>(), 0);

		return bestCollected > 0 ? bestPath : null;
	}

	/// <summary>
	/// DFSで指定歩数先まで探索し、必要な色を最も多く（同数なら最短歩数で）拾える経路を返す（距離pruning版）
	/// </summary>
	/// <remarks>
	/// FindBestPathDFS2との違い：
	/// - Move orderingは行わない
	/// - 距離pruningのみ適用（次の必要な色に残り手数内で届かないノードを枝刈り）
	/// </remarks>
	/// <param name="state">現在の状態（Move/Undoで変更される）</param>
	/// <param name="input">入力情報</param>
	/// <param name="neededColors">必要な色の配列（順序通りに拾う必要がある）</param>
	/// <param name="maxDepth">探索する最大歩数</param>
	/// <param name="avoidRowsBelow">この行番号以下には侵入しない（既に配置済みの行を避ける）</param>
	/// <returns>最適な移動経路。1つも拾えない場合はnull</returns>
	static Direction4[] FindBestPathDFS3(State state, Input input, ReadOnlySpan<byte> neededColors, int maxDepth, int avoidRowsBelow = -1)
	{
		int n = input.N;

		// (fi, fj) から color の餌までの最小マンハッタン距離を返す
		int CalcMinManhattan(int fi, int fj, byte color)
		{
			int min = int.MaxValue;
			foreach (var (pi, pj) in input.FoodPositionsByColor[color]) {
				if (state.Esas[pi, pj] == color) {
					int dist = Math.Abs(fi - pi) + Math.Abs(fj - pj);
					if (dist < min) {
						min = dist;
						if (dist <= 1) break;
					}
				}
			}
			return min;
		}

		Direction4[] bestPath = null;
		int bestCollected = 0;
		int bestSteps = int.MaxValue;

		void DFS(int depth, List<Direction4> currentPath, int collectedIndex, ReadOnlySpan<byte> nc)
		{
			// 現在の経路を評価（より良い経路なら更新）
			if (collectedIndex > bestCollected ||
				(collectedIndex > 0 && collectedIndex == bestCollected && currentPath.Count > 0 && currentPath.Count < bestSteps)) {
				bestCollected = collectedIndex;
				bestSteps = currentPath.Count;
				bestPath = currentPath.ToArray();
			}

			// 深さ上限チェック
			if (depth >= maxDepth) {
				return;
			}

			// 必要な色を全て拾い終えたら終了
			if (collectedIndex >= nc.Length) {
				return;
			}

			var (hi, hj) = state.Head;
			var lastDir = state.HeadDir;

			// 距離pruning: 次の必要な色に残り手数内で届かないなら枝刈り
			int remaining = maxDepth - depth;
			if (CalcMinManhattan(hi, hj, nc[collectedIndex]) > remaining) {
				return;
			}

			foreach (var dir in Direction4Extensions.ForEach4()) {
				// Uターン禁止
				if (dir.Reverse() == lastDir) {
					continue;
				}

				var (ni, nj) = dir.Move(hi, hj);

				// 範囲チェック
				if ((uint)ni >= n || (uint)nj >= n) {
					continue;
				}

				// 既に配置済みの行には侵入しない
				if (ni <= avoidRowsBelow) {
					continue;
				}

				// 胴体への噛み付き禁止
				if (state.WillBites[ni, nj]) {
					continue;
				}

				// 餌がある場合、必要な色かチェック
				byte esa = state.Esas[ni, nj];
				if (esa > 0) {
					byte neededColor = nc[collectedIndex];
					if (esa != neededColor) {
						// 必要な色以外の餌は拾わない
						continue;
					}
				}

				// Move実行
				var undo = state.Move(dir);
				currentPath.Add(dir);

				int newCollectedIndex = collectedIndex;
				if (undo.State == 1) {
					// 餌を食べた
					newCollectedIndex++;
				}

				// 再帰探索
				DFS(depth + 1, currentPath, newCollectedIndex, nc);

				// Undo
				currentPath.RemoveAt(currentPath.Count - 1);
				state.Undo(undo);
			}
		}

		DFS(0, new List<Direction4>(), 0, neededColors);

		return bestCollected > 0 ? bestPath : null;
	}

	/// <summary>
	/// DFSで指定歩数先まで探索し、必要な色を最も多く（同数なら最短歩数で）拾える経路を返す（キャッシュ版）
	/// </summary>
	/// <remarks>
	/// FindBestPathDFS3との違い：
	/// - キャッシュを使用して同じ探索を避ける
	/// - キャッシュキーは (state.Hash, neededColorsHash) のペア
	/// - maxDepth ごとにキャッシュ配列を保持
	/// </remarks>
	/// <param name="state">現在の状態（Move/Undoで変更される）</param>
	/// <param name="input">入力情報</param>
	/// <param name="neededColors">必要な色の配列（順序通りに拾う必要がある）</param>
	/// <param name="maxDepth">探索する最大歩数</param>
	/// <param name="avoidRowsBelow">この行番号以下には侵入しない（既に配置済みの行を避ける）</param>
	/// <returns>最適な移動経路。1つも拾えない場合はnull</returns>
	static Direction4[] FindBestPathDFS4(State state, Input input, byte[] neededColors, int maxDepth, Zob zob, int avoidRowsBelow = -1)
	{
		// neededColors のハッシュを計算
		int neededHash = 0;
		for (int i = 0; i < neededColors.Length; i++) {
			neededHash = neededHash * 31 + neededColors[i];
		}

		// キャッシュキー（Plan3はavoidRowsBelow=-1固定なので不要）
		var cacheKey = (state.Hash, neededHash);

		// キャッシュから結果を取得
		if (zob.TryGetCache(maxDepth, cacheKey, out var cachedResult)) {
			return cachedResult;
		}

		// キャッシュミス：通常の DFS3 を実行
		var result = FindBestPathDFS3(state, input, neededColors, maxDepth, avoidRowsBelow);

		// 結果をキャッシュに保存
		zob.AddCache(maxDepth, cacheKey, result);

		return result;
	}

	/// <summary>
	/// DFSで指定歩数先まで探索し、必要な色との一致数が最も多い経路を返す（制約緩和版）
	/// </summary>
	/// <remarks>
	/// FindBestPathDFSとの違い：
	/// - 他の餌のマスにも移動を許す（必要な色以外の餌も踏める）
	/// - neededColorsとの一致数をカウント（一致した色のみカウント）
	/// </remarks>
	/// <param name="state">現在の状態（Move/Undoで変更される）</param>
	/// <param name="input">入力情報</param>
	/// <param name="neededColors">必要な色の配列（順序通りに一致をチェック）</param>
	/// <param name="maxDepth">探索する最大歩数</param>
	/// <param name="avoidRowsBelow">この行番号以下には侵入しない（既に配置済みの行を避ける）</param>
	/// <returns>最適な移動経路。1つも一致しない場合はnull</returns>
	static Direction4[] FindBestPathDFSRelaxed(State state, Input input, byte[] neededColors, int maxDepth, int avoidRowsBelow = -1)
	{
		int n = input.N;

		Direction4[] bestPath = null;
		int bestMatched = 0;
		int bestSteps = int.MaxValue;

		void DFS(int depth, List<Direction4> currentPath, int matchedIndex)
		{
			// 現在の経路を評価（より良い経路なら更新）
			if (matchedIndex > bestMatched ||
				(matchedIndex == bestMatched && currentPath.Count > 0 && currentPath.Count < bestSteps)) {
				bestMatched = matchedIndex;
				bestSteps = currentPath.Count;
				bestPath = currentPath.ToArray();
			}

			// 深さ上限チェック
			if (depth >= maxDepth) {
				return;
			}

			// 必要な色を全て一致させたら終了
			if (matchedIndex >= neededColors.Length) {
				return;
			}

			var (hi, hj) = state.Head;
			var lastDir = state.HeadDir;

			foreach (var dir in Direction4Extensions.ForEach4()) {
				// Uターン禁止
				if (dir.Reverse() == lastDir) {
					continue;
				}

				var (ni, nj) = dir.Move(hi, hj);

				// 範囲チェック
				if ((uint)ni >= n || (uint)nj >= n) {
					continue;
				}

				// 既に配置済みの行には侵入しない
				if (ni <= avoidRowsBelow) {
					continue;
				}

				// 胴体への噛み付き禁止
				if (state.WillBites[ni, nj]) {
					continue;
				}

				// Move実行（餌があっても必要な色でなくても移動を許す）
				var undo = state.Move(dir);
				currentPath.Add(dir);

				int newMatchedIndex = matchedIndex;
				if (undo.State == 1) {
					// 餌を食べた場合、必要な色と一致するかチェック
					byte eatenColor = undo.EatenColor;
					if (eatenColor == neededColors[matchedIndex]) {
						newMatchedIndex++;
					}
				}

				// 再帰探索
				DFS(depth + 1, currentPath, newMatchedIndex);

				// Undo
				currentPath.RemoveAt(currentPath.Count - 1);
				state.Undo(undo);
			}
		}

		DFS(0, new List<Direction4>(), 0);

		return bestMatched > 0 ? bestPath : null;
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

public class Zob
{
	public long[,,] Esa { get; private set; }
	public long[,] Head { get; private set; }
	public long[,] Hebi { get; private set; }
	public long[,] Color { get; private set; }
	public long[] Dir { get; private set; }

	// DFS キャッシュ（maxDepth ごとに配列を保持、最大16まで）
	private Dictionary<(long stateHash, int neededHash), Direction4[]>[] _dfsCache =
		new Dictionary<(long, int), Direction4[]>[16];

	// キャッシュ統計
	private long _dfsCacheHits = 0;
	private long _dfsCacheMisses = 0;

	public bool[,] BfsVisited { get; }
	public (int i, int j, int d)[] BfsQueue { get; }

	public Zob(Random rnd, Input input)
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

		// HeadDir用のハッシュ（N, D, L, U, R の5つ）
		Dir = new long[5];
		for (int i = 0; i < 5; i++) {
			Dir[i] = rnd.NextInt64();
		}

		BfsVisited = new bool[n, n];
		BfsQueue = new (int i, int j, int d)[n * n * 4];
	}

	public bool TryGetCache(int maxDepth, (long stateHash, int neededHash) key, out Direction4[] result)
	{
		if (maxDepth < _dfsCache.Length && _dfsCache[maxDepth] != null) {
			if (_dfsCache[maxDepth].TryGetValue(key, out result)) {
				_dfsCacheHits++;
				return true;
			}
		}
		_dfsCacheMisses++;
		result = null;
		return false;
	}

	public void AddCache(int maxDepth, (long stateHash, int neededHash) key, Direction4[] value)
	{
		const int MAX_CACHE_SIZE = 100000;
		if (maxDepth < _dfsCache.Length) {
			if (_dfsCache[maxDepth] == null) {
				_dfsCache[maxDepth] = new Dictionary<(long, int), Direction4[]>();
			}

			if (_dfsCache[maxDepth].Count < MAX_CACHE_SIZE) {
				_dfsCache[maxDepth][key] = value;
			}
		}
	}

	public void PrintCacheStats()
	{
		long total = _dfsCacheHits + _dfsCacheMisses;
		if (total > 0) {
			double hitRate = (double)_dfsCacheHits / total * 100.0;
			Console.WriteLine($"DFS Cache Stats: Hits={_dfsCacheHits}, Misses={_dfsCacheMisses}, HitRate={hitRate:F2}%");
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
	private readonly bool _isLocal;
	private readonly Zob _zob;

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

	public State(bool isLocal, Input input, Zob zob)
	{
		_isLocal = isLocal;
		_input = input;
		_zob = zob;

		int n = input.N;
		WillBites = new Array2D<bool>(n, n);
		Esas = new Array2D<byte>(n, n);

		Boros = new();
		Colors = new();
		// BrosはFrontが頭側でBackが尻尾側。
		// ColorsはColors[0]が最古（D[0]）、Colors[Count-1]が最新（D[Count-1]）。
		// 初期配置は頭側が (4, 0) なので、ループ順に前から詰めていけば結果的にFrontが頭になる。
		for (int i = 0; i < 5; i++) {
			Boros.PushFront((i, 0));
			Colors.PushBack(1);  // PushBackで順番通りに追加
								 // 尻尾に移動しても噛み付かない。
								 // またその次の胴体がある場所に移動しても、移動が完了したら尻尾になるので噛み付かない。
								 // 頭(i=4)と尻尾(i=0)と尻尾の隣(i=1)以外を胴体とする
			if (i >= 2 && i < 4) {
				WillBites[i, 0] = true;
			}

			if (i == 4) {
				Hash ^= _zob.Head[i, 0];
			} else {
				Hash ^= _zob.Hebi[i, 0];
			}

			Hash ^= _zob.Color[i, 1];
		}

		// 初期状態の HeadDir (Direction4.D) を Hash に含める
		Hash ^= _zob.Dir[Direction4.D.ToIndex4()];

		for (int i = 0; i < n; i++) {
			for (int j = 0; j < n; j++) {
				Esas[i, j] = (byte)input.F[i, j];
				Hash ^= _zob.Esa[i, j, Esas[i, j]];
			}
		}

		K = 5;
		E = 0;
		NoErrorCount = 5;
	}

	private State(State src)
	{
		_input = src._input;
		_isLocal = src._isLocal;
		_zob = src._zob;
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

		// move前の頭位置のWillBitesは保存する必要は無い。何故なら頭の位置はfalseでよいので。
		// また、move後の頭の位置のwillBitesも保存する必要は無い。
		// かみつきが発生しない場合はfalseだし、発生した場合はtrueに戻せばよいので。

		// Move前の後ろから2番目の位置とWillBitesを保存（空マスの場合に復元するため）
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
				var color = (byte)PopTailColor(ti, tj);
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

			// Move後の後ろから2番目の位置を保存（Undo時の検証用）
			var (bbi, bbj) = Boros[^2];
			newSecondLastPos = (bbi, bbj);
			newSecondLastWillBite = WillBites[bbi, bbj];

			// この時点で後ろから2番目、すなわち尻尾の次の胴体のマスは、次の移動の後には尻尾になる。 
			// 尻尾は噛み付き対象ではないので、すなわち噛み付き対象ではなくなる(hashの変更は無し)
			WillBites[bbi, bbj] = false;
		}

		// 古い HeadDir を除外して新しい HeadDir を追加
		var oldDir = HeadDir;
		Ops.Add(dir);
		Hash ^= _zob.Dir[oldDir.ToIndex4()];
		Hash ^= _zob.Dir[dir.ToIndex4()];

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

		// 状態を復元（Hashは最後に復元）
		E = undo.OldE;
		K = undo.OldK;
		NoErrorCount = undo.OldNoErrorCount;

		if (undo.State == 0) {
			// 噛み付きの場合（エッジケース）：一括で WillBites を再構築
			if (Boros.Count > 3) {
				for (int i = 1; i < Boros.Count - 2; i++) {
					var (r, c) = Boros[i];
					WillBites[r, c] = false;
				}
			}

			Boros.PopFront();
			for (int i = undo.RemovedSegments.Count - 1; i >= 0; i--) {
				var (pos, color, _) = undo.RemovedSegments[i];
				Boros.PushBack(pos);
				if (i > 0) {
					Colors.PushBack(color);
					Esas[pos.i, pos.j] = 0;
				}
			}

			if (Boros.Count > 3) {
				for (int i = 1; i < Boros.Count - 2; i++) {
					var (r, c) = Boros[i];
					WillBites[r, c] = true;
				}
			}
		} else if (undo.State == 1) {
			// 餌を食べた
			WillBites[undo.NewSecondLastPos.i, undo.NewSecondLastPos.j] = undo.NewSecondLastWillBite;
			Boros.PopFront();
			WillBites[undo.OldHeadPos.i, undo.OldHeadPos.j] = false;
			Colors.PopBack();
			Esas[undo.FoodPos.i, undo.FoodPos.j] = undo.EatenColor;
			WillBites[undo.OldSecondLastPos.i, undo.OldSecondLastPos.j] = undo.OldSecondLastWillBite;
		} else if (undo.State == 2) {
			// 空マスを踏んだ
			WillBites[undo.NewSecondLastPos.i, undo.NewSecondLastPos.j] = undo.NewSecondLastWillBite;
			Boros.PopFront();
			WillBites[undo.OldHeadPos.i, undo.OldHeadPos.j] = false;
			WillBites[undo.OldSecondLastPos.i, undo.OldSecondLastPos.j] = undo.OldSecondLastWillBite;
			Boros.PushBack(undo.RemovedTail);
			WillBites[undo.RemovedTail.i, undo.RemovedTail.j] = undo.RemovedTailWillBite;
		}

		Hash = undo.OldHash;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void PushHead(int ni, int nj, int hi, int hj)
	{
		// 頭が次の位置へ移動
		Boros.PushFront((ni, nj));
		Hash ^= _zob.Head[ni, nj];
		// 元々頭があった位置が胴体になる
		WillBites[hi, hj] = true;
		Hash ^= _zob.Head[hi, hj];
		Hash ^= _zob.Hebi[hi, hj];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private (int ti, int tj) PopTail()
	{
		// 尻尾を抜く
		var (ti, tj) = Boros.PopBack();
		Hash ^= _zob.Hebi[ti, tj];
		return (ti, tj);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int PushTailColor(int ni, int nj)
	{
		byte color = Esas[ni, nj];
		Colors.PushBack(color);
		Esas[ni, nj] = 0;
		Hash ^= _zob.Esa[ni, nj, 0];
		Hash ^= _zob.Esa[ni, nj, color];
		Hash ^= _zob.Color[Colors.Count - 1, color];
		return color;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int PopTailColor(int ti, int tj)
	{
		var color = Colors.PopBack();
		Hash ^= _zob.Color[Colors.Count, color];
		Esas[ti, tj] = color;
		Hash ^= _zob.Esa[ti, tj, 0];
		Hash ^= _zob.Esa[ti, tj, color];

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

		val += Ops.Count;

		// 短い手数で無駄なく進めるよう、エラー状態へのペナルティ
		// Score ベースだとエラーした方が K が増えて Score が下がるバグがあるため、独自計算にする。
		long errorCountVal = (K - NoErrorCount) + E;
		if (errorCountVal > 0) {
			val += errorCountVal * 1000000000L;
		}

		val += (_input.M - NoErrorCount) * 1000000L;

		// 次に必要な色への最短マンハッタン距離を評価に組み込む
		if (NoErrorCount == Colors.Count && Colors.Count < _input.M) {
			byte nextColor = (byte)_input.D[Colors.Count];
			int dist = CalcMinManhattan(nextColor);
			if (dist != 100000) {
				// 短手数を優先したいため距離の重みを小さすぎず大きすぎず
				val += dist * 10;
			} else {
				// 次の目標が存在しない場合は落とす
				val += 500000L;
			}
		}

		return val;
	}

	private int CalcMinManhattan(byte targetEsa)
	{
		var (hi, hj) = Head;
		int minDist = 100000;
		// 該当色の餌の位置のリストから最小マンハッタン距離を計算
		foreach (var (r, c) in _input.FoodPositionsByColor[targetEsa]) {
			if (Esas[r, c] == targetEsa) {
				int d = Math.Abs(hi - r) + Math.Abs(hj - c);
				if (d < minDist) {
					minDist = d;
					if (d == 1) break; // 距離1なられ以上短くならないので打ち切り
				}
			}
		}

		return minDist;
	}

	private int CalcBFS(byte targetEsa)
	{
		int n = _input.N;
		var bfsVisited = _zob.BfsVisited;
		var bfsQueue = _zob.BfsQueue;

		var (hi, hj) = Head;

		int head = 0, tail = 0;
		bfsQueue[tail++] = (hi, hj, 0);
		bfsVisited[hi, hj] = true;

		// Uターン禁止の考慮を最初の1手だけ入れる
		var lastDir = HeadDir;
		var (bi, bj) = lastDir.Reverse().Move(hi, hj);
		if ((uint)bi < n && (uint)bj < n) {
			bfsVisited[bi, bj] = true; // 逆戻りは最初から訪問済み扱いにする
		}

		int minDist = 100000;
		while (head < tail) {
			var (ci, cj, d) = bfsQueue[head++];
			if (ci != hi || cj != hj) { // 現在位置以外で
				if (Esas[ci, cj] == targetEsa) {
					minDist = d;
					break;
				}
			}

			if (d > 30) {
				break; // 深追いしすぎない
			}

			int ni, nj;

			ni = ci + 1; nj = cj;
			if ((uint)ni < (uint)n && (uint)nj < (uint)n && !WillBites[ni, nj] && !bfsVisited[ni, nj]) {
				bfsVisited[ni, nj] = true;
				bfsQueue[tail++] = (ni, nj, d + 1);
			}

			ni = ci; nj = cj - 1;
			if ((uint)ni < (uint)n && (uint)nj < (uint)n && !WillBites[ni, nj] && !bfsVisited[ni, nj]) {
				bfsVisited[ni, nj] = true;
				bfsQueue[tail++] = (ni, nj, d + 1);
			}

			ni = ci - 1; nj = cj;
			if ((uint)ni < (uint)n && (uint)nj < (uint)n && !WillBites[ni, nj] && !bfsVisited[ni, nj]) {
				bfsVisited[ni, nj] = true;
				bfsQueue[tail++] = (ni, nj, d + 1);
			}

			ni = ci; nj = cj + 1;
			if ((uint)ni < (uint)n && (uint)nj < (uint)n && !WillBites[ni, nj] && !bfsVisited[ni, nj]) {
				bfsVisited[ni, nj] = true;
				bfsQueue[tail++] = (ni, nj, d + 1);
			}
		}

		// 片付け (再利用するため visited を false に戻す)
		for (int i = 0; i < tail; i++) {
			var (ci, cj, _) = bfsQueue[i];
			bfsVisited[ci, cj] = false;
		}
		if ((uint)bi < n && (uint)bj < n) {
			bfsVisited[bi, bj] = false;
		}

		return minDist;
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

	public int B { get; set; } = -1;
	public int D { get; set; } = -1;
	public int KamiCount { get; set; } = 0;
	public int S { get; set; } = 8;
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
	public List<(int r, int c)>[] FoodPositionsByColor { get; }

	public Input(IOManager io, bool isLocal)
	{
		_io = io;
		_isDebug = isLocal;

		(N, M, C) = io.Int3();
		D = io.ArrayInt(M);
		F = io.Repeat(N, N, io => io.Int());

		FoodPositionsByColor = new List<(int r, int c)>[C + 1];
		for (int i = 0; i <= C; i++) {
			FoodPositionsByColor[i] = new List<(int r, int c)>();
		}
		for (int r = 0; r < N; r++) {
			for (int c = 0; c < N; c++) {
				if (F[r, c] > 0) {
					FoodPositionsByColor[F[r, c]].Add((r, c));
				}
			}
		}
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
					Console.WriteLine($"{i:d4}: {ex.ToString()}");
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

	public static (bool isLocal, Stopwatch sw, Random rnd) Initialize()
	{
		var sw = new Stopwatch();
		sw.Start();

		bool isLocal = false;
#if DEBUG
		isLocal = true;
#endif
		var rnd = new Random(123456);

		return (isLocal, sw, rnd);
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