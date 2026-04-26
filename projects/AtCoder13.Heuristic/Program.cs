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
			testCaseCount: 150,
			isParallel: false,
			i => Run(i),
			(locker, i, ret) => {
				var (input, info) = ret;
				lock (locker) {
					var status = new List<string> {
						$"{i:d4}:",
						//$"N={input.N:d2}",
						$"loop={info.Loop:d8}",
						$"ack={info.Ack:d4}",
						$"up={info.Up:d4}",
						$"M={info.Move:d5}",
						$"T={info.Turn:d4}",
						$"W={info.W:d4}",
						$"score={info.Score:d5}",
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
		var (isLocal, sw, rnd) = HeuristicHelper.Initialize();
		using var io = HeuristicHelper.CreateIO(@$"D:\kyopro\AtCoder\AHC064\tools\in\", caseNumber);

		var input = new Input(io, isLocal);
		var info = new Information();

		var best = Plan4(isLocal, rnd, sw, input);
		info.W = 4;
		long bestScore = best.Calculate().score;

		/*
		{
			var state = Plan2(isLocal, rnd, sw, input);
			Update(state, 2);
		}*/

		int limit = 1900;
		while (sw.ElapsedMilliseconds < limit) {
			++info.Loop;
			var temp = Plan5(isLocal, rnd, sw, input, best);
			if (temp is null) {
				continue;
			}

			Update(temp, 5);
		}


		input.Output(best);
		if (isLocal) {
			info.Move = best.Moves.Count;
			(info.Score, info.Turn) = best.Calculate();
		}

		info.Elapsed = sw.ElapsedMilliseconds;

		return (input, info);

		void Update(State state, int w)
		{
			var tempScore = state.Calculate().score;
			if (tempScore > bestScore) {
				++info.Ack;
				++info.Up;
				best = state;
				bestScore = tempScore;
				info.W = w;
			} else if (tempScore == bestScore) {
				++info.Ack;
				best = state;
				info.W = w;
			}
		}
	}

	static State Plan2(bool isLocal, Random rnd, Stopwatch sw, Input input)
	{
		var state = new State(isLocal, input);
		int stuckCount = 0;
		Move? lastMove = null;

		for (int turn = 0; turn < 4000; turn++) {
			if (IsFinished(state, input)) {
				break;
			}

			// DP for non-crossing matching
			double[,] dp = new double[input.R + 1, input.R + 1];
			int[,] prev = new int[input.R + 1, input.R + 1];
			int[,] prevK = new int[input.R + 1, input.R + 1];

			for (int i = 1; i <= input.R; i++) {
				for (int j = 1; j <= input.R; j++) {
					dp[i, j] = dp[i - 1, j];
					prev[i, j] = 0;
					prevK[i, j] = 0;

					if (dp[i, j - 1] > dp[i, j]) {
						dp[i, j] = dp[i, j - 1];
						prev[i, j] = 1;
						prevK[i, j] = 0;
					}

					var (score0, k0) = ScoreStartToAvoid(state, input, i - 1, j - 1, rnd, stuckCount);
					if (score0 > 0 && dp[i - 1, j - 1] + score0 > dp[i, j]) {
						dp[i, j] = dp[i - 1, j - 1] + score0;
						prev[i, j] = 2;
						prevK[i, j] = k0;
					}

					var (score1, k1) = ScoreAvoidToStart(state, input, i - 1, j - 1, rnd, stuckCount);
					if (score1 > 0 && dp[i - 1, j - 1] + score1 > dp[i, j]) {
						dp[i, j] = dp[i - 1, j - 1] + score1;
						prev[i, j] = 3;
						prevK[i, j] = k1;
					}
				}
			}

			int ci = input.R;
			int cj = input.R;
			var turnMoves = new List<Move>();

			while (ci > 0 && cj > 0) {
				int p = prev[ci, cj];
				int k = prevK[ci, cj];
				if (p == 0) {
					ci--;
				} else if (p == 1) {
					cj--;
				} else {
					ci--;
					cj--;
					if (p == 2) {
						turnMoves.Add(new Move(0, ci, cj, k));
					} else if (p == 3) {
						turnMoves.Add(new Move(1, ci, cj, k));
					}
				}
			}

			turnMoves.Reverse();

			if (turnMoves.Count == 0) {
				stuckCount++;
				if (stuckCount > 30) break; // Deadlock escape exhausted or no possible moves
			} else {
				if (turnMoves.Count == 1 && lastMove != null && IsReverse(turnMoves[0], lastMove.Value)) {
					stuckCount++;
					turnMoves.Clear();
				} else {
					stuckCount = 0;
				}
			}

			if (turnMoves.Count > 0) {
				if (turnMoves.Count == 1) lastMove = turnMoves[0];
				else lastMove = null;
			}

			foreach (var move in turnMoves) {
				state.Add(move);
			}
		}

		return state;
	}

	static bool IsFinished(State state, Input input)
	{
		for (int i = 0; i < input.R; i++) {
			if (state.Starts[i].Count != input.N) return false;
			for (int j = 0; j < input.N; j++) {
				if (state.Starts[i][j] != i * input.N + j) {
					return false;
				}
			}
		}
		return true;
	}

	static int GetCorrectCount(State state, Input input, int i)
	{
		int count = 0;
		for (int j = 0; j < state.Starts[i].Count; j++) {
			if (state.Starts[i][j] == i * input.N + j) {
				count++;
			} else {
				break;
			}
		}
		return count;
	}

	static bool IsReverse(Move a, Move b)
	{
		if (a.Type == 0 && b.Type == 1 && a.I == b.I && a.J == b.J && a.K == b.K) return true;
		if (a.Type == 1 && b.Type == 0 && a.I == b.I && a.J == b.J && a.K == b.K) return true;
		return false;
	}

	static (double score, int k) ScoreStartToAvoid(State state, Input input, int i, int j, Random rnd, int stuckCount)
	{
		int correctCount = GetCorrectCount(state, input, i);
		int maxK = Math.Min(state.Starts[i].Count - correctCount, input.MaxAvoid - state.Avoids[j].Count);
		if (maxK <= 0) return (-1, 0);

		double bestScore = -100000;
		int bestK = 0;
		double noise = rnd.NextDouble();

		for (int k = 1; k <= maxK; k++) {
			int x = state.Starts[i][state.Starts[i].Count - k];
			int destX = x / input.N;
			double delta = 10.0 * k; // 出発線から邪魔なものをどける基本報酬

			if (state.Avoids[j].Count > 0) {
				int y = state.Avoids[j].Front;
				int destY = y / input.N;

				if (destX != destY) delta -= 50.0 * k; // 違う行のものが混ざるペナルティ

				if (x < y) {
					delta += 50.0 * k; // 正しく降順（上が小さい）なら報酬
					if (y - x == 1) delta += 100.0 * k; // 連番なら大ボーナス
				} else {
					delta -= 200.0 * k; // 逆転（上が大きい）は大きなペナルティ
				}
			} else {
				if (destX == j) delta += 30.0 * k; // 自分の行用の待避線ならボーナス
			}

			if (k > 1) {
				int prevX = state.Starts[i][state.Starts[i].Count - k + 1];
				if (x < prevX) delta += 50.0 * k;
				else delta -= 100.0 * k;
			}

			delta += noise; // 完全なループを防ぐためのノイズ

			if (stuckCount > 0) {
				delta += stuckCount * 200.0 * rnd.NextDouble(); // スタック解消時は強引に移動させる
			}

			if (delta > bestScore) {
				bestScore = delta;
				bestK = k;
			}
		}

		return (bestScore, bestK);
	}

	static (double score, int k) ScoreAvoidToStart(State state, Input input, int i, int j, Random rnd, int stuckCount)
	{
		int maxK = Math.Min(state.Avoids[j].Count, input.MaxStart - state.Starts[i].Count);
		if (maxK <= 0) return (-1, 0);

		int correctCount = GetCorrectCount(state, input, i);

		double bestScore = -100000;
		int bestK = 0;
		double noise = rnd.NextDouble();

		for (int k = 1; k <= maxK; k++) {
			int topMoved = state.Avoids[j].Front;
			int bottomMoved = state.Avoids[j][k - 1];
			int destX = bottomMoved / input.N;

			bool perfect = false;
			if (i == destX && state.Starts[i].Count == correctCount) {
				perfect = true;
				for (int m = 0; m < k; m++) {
					int train = state.Avoids[j][k - 1 - m];
					if (train != i * input.N + correctCount + m) {
						perfect = false;
						break;
					}
				}
			}

			// 完全に次に必要な車両の場合は超大ボーナス（確定）
			if (perfect) {
				double score = 10000.0 * k + noise;
				if (score > bestScore) {
					bestScore = score;
					bestK = k;
				}
				continue;
			}

			double delta = -10.0 * k; // 基本的に目的以外の出発線に戻すのはペナルティ

			if (state.Starts[i].Count == correctCount) {
				delta -= 100.0 * k; // 綺麗な状態の出発線を汚すのは強く避ける
			} else {
				int startTop = state.Starts[i].Back;
				if (startTop > bottomMoved) delta += 10.0 * k; // 少なくとも上に小さいものを置けるなら少しマシ
				else delta -= 10.0 * k;
			}

			if (state.Avoids[j].Count > k) {
				int y = state.Avoids[j][k]; // どけたことによって次に見えるようになる車両
				int destY = y / input.N;

				if ((topMoved / input.N) != destY) delta += 50.0 * k;
				if (topMoved < y) delta -= 50.0 * k; // 本来良い並びだったものを崩すのはペナルティ
				else delta += 150.0 * k; // 待避線内の逆転現象（悪い状態）を解消するための発掘移動なら報酬
			}

			delta += noise;

			if (stuckCount > 0) {
				delta += stuckCount * 200.0 * rnd.NextDouble(); // スタック解消
			}

			if (delta > bestScore) {
				bestScore = delta;
				bestK = k;
			}
		}

		if (bestScore <= 0 && stuckCount == 0) return (-1, 0);

		return (bestScore, bestK);
	}

	static State Plan4(bool isLocal, Random rnd, Stopwatch sw, Input input)
	{
		var state = new State(isLocal, input);
		return Plan4Core(isLocal, rnd, sw, input, state);
	}

	static State Plan5(bool isLocal, Random rnd, Stopwatch sw, Input input, State best)
	{
		var state = new State(isLocal, input);
		// 挿入位置を決定
		int tag = rnd.Next(0, best.Moves.Count);
		// そこまで進める
		for (int t = 0; t < tag; t++) {
			state.Add(best.Moves[t]);
		}

		// ここで良い手を探す

		// どっちから動かすか
		if (rnd.Next(0, 2) == 0) {
			// 八者側
			// 動かす対象
			int i = rnd.Next(0, input.R);
			if (state.Starts[i].Count == 0) {
				// 何もない
				return null;
			}

			int u = state.Starts[i][^1];
			int ui = u / input.N;
			int uj = u % input.N;
			if (ui == i && uj == state.Starts[i].Count - 1) {
				// 既に正しく格納されているやつは動かさない
				return null;
			}

			// 上の車両と連番のとき、まとめて動かす。
			int moveCount = 1;
			while (true) {
				if (state.Starts[i].Count > moveCount) {
					int v = state.Starts[i][^(moveCount + 1)];
					int vi = v / input.N;
					if (v == u - moveCount && ui == vi) {
						++moveCount;
					} else {
						break;
					}
				} else {
					break;
				}
			}

			// 移動先
			// とりあえずランダムにする。真下の方が採用率は高そうだが、効果があるかは微妙。
			// くっつく車両があれば探すべき？
			int j = rnd.Next(0, input.R);
			if (state.Avoids[j].Count + moveCount > input.MaxAvoid) {
				return null;
			}

			state.Add(new Move(0, i, j, moveCount));
		} else {
			// 退避側

			// 動かす対象
			int j = rnd.Next(0, input.R);
			if (state.Avoids[j].Count == 0) {
				// 何もない
				return null;
			}

			int u = state.Avoids[j][0];
			int ui = u / input.N;
			int uj = u % input.N;
			// 下の車両と連番のとき、まとめて動かす
			int moveCount = 1;
			while (true) {
				if (state.Avoids[j].Count > moveCount) {
					int v = state.Avoids[j][moveCount];
					int vi = v / input.N;
					if (v == u + moveCount && ui == vi) {
						++moveCount;
					} else {
						break;
					}
				} else {
					break;
				}
			}

			// 移動先
			// とりあえずランダムにする。
			int i = rnd.Next(0, input.R);
			if (state.Starts[i].Count + moveCount > input.MaxStart) {
				return null;
			}

			state.Add(new Move(1, i, j, moveCount));
		}


		// 以降は貪欲で
		return Plan4Core(isLocal, rnd, sw, input, state);
	}

	static State Plan4Core(bool isLocal, Random rnd, Stopwatch sw, Input input, State state)
	{
		int railCount = input.R * input.N;
		var moves = new List<Move>();
		for (int k = 0; k < railCount; k++) {
			int ti = k / input.N;
			int tj = k % input.N;
			// 番号kの車両を探す
			var ret = state.FindTrain(k);
			if (ret.isStart && ret.i == ti && ret.j == tj) {
				// もう適切な位置にいるので何もしない。
				continue;
			}

			if (ret.isStart) {
				// kが待避線の先頭に来るようにどける
				int moveCount = state.Starts[ret.i].Count - ret.j;
				int tagJ = state.FindAvoidLine(moveCount, ret.i);
				state.Add(new Move(0, ret.i, tagJ, moveCount));

				// kの行き先をあける
				if (state.Starts[ti].Count > tj) {
					moveCount = state.Starts[ti].Count - tj;
					int tagJ2 = state.FindAvoidLine2(moveCount, tagJ, ti);
					state.Add(new Move(0, ti, tagJ2, moveCount));
				}

				// kを移動。次がすぐに移動出来るようにkだけ。
				// ただし、次に動かすべきものがすぐ下にいる場合には一緒に動かす。
				moveCount = 1;
				while (true) {
					if (state.Avoids[tagJ].Count > moveCount) {
						int v = state.Avoids[tagJ][moveCount];
						int vi = v / input.N;
						if (v == k + moveCount && ti == vi) {
							++moveCount;
						} else {
							break;
						}
					} else {
						break;
					}
				}
				// 待避線が足りないときは後で考える。
				state.Add(new Move(1, ti, tagJ, moveCount));
			} else {
				// kが先頭で無い場合、上をどける
				int moveCount = 0;
				if (ret.j > 0) {
					moveCount = ret.j;
					int tagI2 = state.FindStartLine2(moveCount, ti, ret.i);
					if (tagI2 >= 0) {
						state.Add(new Move(1, tagI2, ret.i, moveCount));
					} else {
						while (moveCount > 0) {
							var moveRet = state.TryFindStartLine(ti, ret.i);
							int moveCount2 = Math.Min(moveCount, moveRet.count);
							state.Add(new Move(1, moveRet.i, ret.i, moveCount2));
							moveCount -= moveCount2;
						}
					}
				}

				// kの行き先をあける
				if (state.Starts[ti].Count > tj) {
					moveCount = state.Starts[ti].Count - tj;
					int tagJ2 = state.FindAvoidLine2(moveCount, ret.i, ti);
					state.Add(new Move(0, ti, tagJ2, moveCount));
				}

				// kを移動。次がすぐに移動出来るようにkだけ。
				// ただし、次に動かすべきものがすぐ下にいる場合には一緒に動かす。
				moveCount = 1;
				while (true) {
					if (state.Avoids[ret.i].Count > moveCount) {
						int v = state.Avoids[ret.i][moveCount];
						int vi = v / input.N;
						if (v == k + moveCount && ti == vi) {
							++moveCount;
						} else {
							break;
						}
					} else {
						break;
					}
				}
				// 待避線が足りないときは後で考える。
				state.Add(new Move(1, ti, ret.i, moveCount));
			}
		}

		return state;
	}
}

public class State
{
	private readonly bool _isLocal;
	private readonly Input _input;

	public List<Move> Moves { get; private set; } = [];

	public Deque<int>[] Starts { get; private set; }
	public Deque<int>[] Avoids { get; private set; }

	public State(bool isLocal, Input input)
	{
		_isLocal = isLocal;
		_input = input;

		Starts = Helper.Array1(input.R, x => new Deque<int>());
		for (int i = 0; i < input.R; i++) {
			for (int j = 0; j < input.N; j++) {
				Starts[i].PushBack(input.Y[i, j]);
			}
		}

		Avoids = Helper.Array1(input.R, x => new Deque<int>());
	}

	public void Add(Move move)
	{
		if (move.Type == 0) {
			for (int k = 0; k < move.K; k++) {
				Avoids[move.J].PushFront(Starts[move.I].PopBack());
			}
		} else {
			for (int k = 0; k < move.K; k++) {
				Starts[move.I].PushBack(Avoids[move.J].PopFront());
			}
		}

		Moves.Add(move);
	}

	public (long score, int opCount) Calculate()
	{
		var operations = ZipMoves2();

		bool ok = true;
		long temp = 0;
		for (int i = 0; i < _input.R; i++) {
			for (int j = 0; j < Starts[i].Count; j++) {
				int v = Starts[i][j];
				if (v / _input.N == i) {
					++temp;
					if (v % _input.N == j) {
						temp += 9;
					} else {
						ok = false;
					}
				} else {
					ok = false;
				}
			}

			if (Avoids[i].Count > 0) {
				ok = false;
			}
		}

		long score = ok
			? (100 * _input.R) + 4000 - operations.Count
			: temp;
		return (score, operations.Count);
	}

	public List<Operation> ZipMoves()
	{
		var operations = new List<Operation>();
		var op = new Operation();
		foreach (var move in Moves) {
			if (op.CanAdd(move)) {
				op.Moves.Add(move);
			} else {
				operations.Add(op);
				op = new Operation();
				op.Moves.Add(move);
			}
		}

		if (op.Moves.Count > 0) {
			operations.Add(op);
		}

		return operations;
	}

	public List<Operation> ZipMoves2()
	{
		var operations = new List<Operation>();

		var moveQueuesForI = Helper.Array1(_input.R, x => new Queue<int>());
		var moveQueuesForJ = Helper.Array1(_input.R, x => new Queue<int>());

		for (int idx = 0; idx < Moves.Count; idx++) {
			moveQueuesForI[Moves[idx].I].Enqueue(idx);
			moveQueuesForJ[Moves[idx].J].Enqueue(idx);
		}

		int processedCount = 0;
		while (processedCount < Moves.Count) {
			var op = new Operation();
			var readyMoves = new List<int>();

			for (int i = 0; i < _input.R; i++) {
				if (moveQueuesForI[i].Count > 0) {
					int idx = moveQueuesForI[i].Peek();
					if (moveQueuesForJ[Moves[idx].J].Peek() == idx) {
						readyMoves.Add(idx);
					}
				}
			}

			var addedInThisOp = new List<int>();
			foreach (int idx in readyMoves) {
				var move = Moves[idx];
				if (op.CanAdd(move)) {
					op.Moves.Add(move);
					addedInThisOp.Add(idx);
				}
			}

			if (addedInThisOp.Count == 0) {
				break; // 安全策
			}

			operations.Add(op);
			foreach (int idx in addedInThisOp) {
				moveQueuesForI[Moves[idx].I].Dequeue();
				moveQueuesForJ[Moves[idx].J].Dequeue();
				processedCount++;
			}
		}

		return operations;
	}

	public (bool isStart, int i, int j) FindTrain(int k)
	{
		for (int i = 0; i < _input.R; i++) {
			for (int j = 0; j < Starts[i].Count; j++) {
				if (Starts[i][j] == k) {
					return (true, i, j);
				}
			}

			for (int j = 0; j < Avoids[i].Count; j++) {
				if (Avoids[i][j] == k) {
					return (false, i, j);
				}
			}
		}

		// ここには来ない
		return (false, 0, 0);
	}

	public int FindAvoidLine(int count, int startIndex = 0)
	{
		for (int i = 0; i < _input.R; i++) {
			int ii = (i + startIndex) % _input.R;
			if (Avoids[ii].Count + count <= _input.MaxAvoid) {
				return ii;
			}
		}

		return -1;
	}

	public int FindAvoidLine2(int count, int dame, int startIndex = 0)
	{
		for (int i = 0; i < _input.R; i++) {
			int ii = (i + startIndex) % _input.R;
			if (ii == dame) {
				continue;
			}

			if (Avoids[ii].Count + count <= _input.MaxAvoid) {
				return ii;
			}
		}

		return -1;
	}

	public int FindStartLine(int count, int startIndex = 0)
	{
		for (int i = 0; i < _input.R; i++) {
			int ii = (i + startIndex) % _input.R;
			if (Starts[ii].Count + count <= _input.MaxStart) {
				return ii;
			}
		}

		return -1;
	}

	public int FindStartLine2(int count, int dame, int startIndex = 0)
	{
		for (int i = 0; i < _input.R; i++) {
			int ii = (i + startIndex) % _input.R;
			if (ii == dame) {
				continue;
			}

			if (Starts[ii].Count + count <= _input.MaxStart) {
				return ii;
			}
		}

		return -1;
	}

	public (int i, int count) TryFindStartLine(int dame, int startIndex = 0)
	{
		for (int i = 0; i < _input.R; i++) {
			int ii = (i + startIndex) % _input.R;
			if (ii == dame) {
				continue;
			}

			int can = _input.MaxStart - Starts[ii].Count;
			if (can > 0) {
				return (ii, can);
			}
		}

		return (0, -1);
	}
}

public class Operation
{
	public List<Move> Moves { get; set; } = [];

	public bool CanAdd(Move move)
	{
		foreach (var src in Moves) {
			if (src.IsCross(move)) {
				return false;
			}
		}

		return true;
	}
}

public readonly struct Move
{
	public readonly int Type;
	public readonly int I;
	public readonly int J;
	public readonly int K;
	public Move(int type, int i, int j, int k)
	{
		Type = type;
		I = i;
		J = j;
		K = k;
	}

	public override string ToString()
	{
		return $"{Type} {I} {J} {K}";
	}

	public bool IsCross(Move other)
	{
		return ((I < other.I && J < other.J) == false) && ((I > other.I && J > other.J) == false);
	}
}

public class Information
{
	public long Time { get; set; } = 0;
	public long Score { get; set; } = 0;
	public int Loop { get; set; } = 0;
	public int Ack { get; set; } = 0;
	public int Up { get; set; } = 0;
	public long Elapsed { get; set; } = 0;

	public int W { get; set; } = 0;
	public int Move { get; set; } = 0;
	public int Turn { get; set; } = 0;
}

public class Input
{
	private readonly IOManager _io;
	private readonly bool _isLocal;

	public int R { get; private set; }
	public int[,] Y { get; private set; }

	public int N { get; private set; } = 10;
	public int MaxStart { get; private set; } = 15;
	public int MaxAvoid { get; private set; } = 20;

	public Input(IOManager io, bool isLocal)
	{
		_io = io;
		_isLocal = isLocal;

		R = io.Int();
		Y = io.Repeat(R, N, io => io.Int());
	}

	public void Output(State state)
	{
		var operations = state.ZipMoves2();
		_io.WriteLine(operations.Count);
		foreach (var op in operations) {
			int k = op.Moves.Count;
			_io.WriteLine(k);
			foreach (var move in op.Moves) {
				_io.WriteLine(move.ToString());
			}
		}
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