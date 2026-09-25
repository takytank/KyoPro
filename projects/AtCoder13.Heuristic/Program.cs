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
			localTestCaseRunCount: 1,
			judgePreTestCaseCount: 150,
			isParallel: false,
			i => Run(i),
			(locker, i, ret) => {
				var (input, info) = ret;
				lock (locker) {
					var status = new List<string> {
						$"{i:d4}:",
						$"N={input.N:d2}",
						$"K={input.K:d2}",
						$"W={info.WallCount:d3}",
						$"S={info.SlimeCount:d3}",
						$"loop={info.Loop:d8}",
						$"up={info.Up:d4}",
						$"score={info.Score:d5}",
						$"elapsed={info.Elapsed:d4}",
					};

					Console.WriteLine(status.Join(" "));
					Console.Out.Flush();
				}

				return (info.Score, info.Loop, info.Up, info.Elapsed);
			},
			(judgeCaseCount, localCaseCount) => {
				//Console.WriteLine($"enabled count: {count}");
			});
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	static (Input input, Information info)
		Run(int caseNumber)
	{
		var (isLocal, sw, rnd) = HeuristicHelper.Initialize();
		using var io = HeuristicHelper.CreateIO(@$"D:\kyopro\AtCoder\AHC072\tools\in\", caseNumber);

		var input = new Input(io, isLocal);
		var info = new Information();
		for (int i = 0; i < input.N; i++) {
			for (int j = 0; j < input.N; j++) {
				char c = input.C[i][j];
				if (c >= 'a' && c <= 'l') {
					++info.SlimeCount;
				} else if (c == '#') {
					++info.WallCount;
				}
			}
		}

		var best = DoPlan5(isLocal, sw, rnd, input, info);

		input.Output(best);
		if (isLocal) {
			info.Score = best.CalculateScore();

			// 手元での盤面評価時に true にして実行する。
			bool isDebug = false;
			if (isDebug) {
				best.Simulate();
			}
		}

		info.Elapsed = sw.ElapsedMilliseconds;

		return (input, info);
	}

	static State DoPlan0(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);

		return state;
	}

	static State DoPlan1(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);

		int n = input.N;
		// 各色ごとに巣からBFSし、近い方から順に移動させる。
		for (int c = 0; c < input.K; c++) {
			// BFSで巣からの距離を計算
			var dists = new int[n, n].Fill(int.MaxValue);
			var que = new Queue<(int i, int j, int d)>();
			var (si, sj) = input.Dens[c];
			dists[si, sj] = 0;
			que.Enqueue((si, sj, 0));
			while (que.Count > 0) {
				var (i, j, d) = que.Dequeue();
				int nd = d + 1;
				foreach (var (ii, jj) in Helper.Adjacence4(i, j, n, n)) {
					if (input.Map[ii, jj] == -2) {
						continue;
					}

					if (dists[ii, jj] > nd) {
						dists[ii, jj] = nd;
						que.Enqueue((ii, jj, nd));
					}
				}
			}

			var pque = new HeapQueue<(int i, int j, int d), int>(x => x.d, reverses: true);
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					if (state.Slimes[i, j].Count > 0 && state.Slimes[i, j].Peek() == c) {
						pque.Enqueue((i, j, dists[i, j]));
					}
				}
			}

			while (pque.Count > 0) {
				var (i, j, d) = pque.Dequeue();
				// とりあえず一匹ずつ
				foreach (var dir in Direction4Extensions.ForEach4()) {
					var (ni, nj) = dir.Move(i, j);
					if ((uint)ni < n && (uint)nj < n && dists[ni, nj] < d) {
						bool backed = state.Move(i, j, state.Slimes[i, j].Count - 1, dir, 1);
						if (backed == false) {
							pque.Enqueue((ni, nj, dists[ni, nj]));
						}

						break;
					}
				}
			}
		}

		return state;
	}

	static State DoPlan2(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var colorOrder = new int[input.K];
		for (int c = 0; c < input.K; c++) {
			colorOrder[c] = c;
		}

		return DoPlanByColorOrder(isLocal, sw, rnd, input, info, colorOrder);
	}

	static State DoPlan3(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var colorCounts = new int[input.K];
		foreach (string row in input.C) {
			foreach (char cell in row) {
				if (cell >= 'a' && cell < 'a' + input.K) {
					colorCounts[cell - 'a']++;
				}
			}
		}

		var colorOrder = new int[input.K];
		for (int c = 0; c < input.K; c++) {
			colorOrder[c] = c;
		}

		Array.Sort(colorOrder, (a, b) => {
			int compare = colorCounts[a].CompareTo(colorCounts[b]);
			return compare != 0 ? compare : a.CompareTo(b);
		});

		return DoPlanByColorOrder(isLocal, sw, rnd, input, info, colorOrder);
	}

	static State DoPlan4(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		return DoPlan4Core(isLocal, sw, rnd, input, info, requireNonWorsening: false);
	}

	static State DoPlan5(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		return DoPlan4Core(isLocal, sw, rnd, input, info, requireNonWorsening: true);
	}

	static State DoPlan4Core(
		bool isLocal, Stopwatch sw, Random rnd, Input input, Information info,
		bool requireNonWorsening)
	{
		var state = new State(isLocal, input);
		int n = input.N;
		var dists = new int[input.K, n, n];

		// 各色の巣からの距離を個別に計算する。
		for (int c = 0; c < input.K; c++) {
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					dists[c, i, j] = int.MaxValue;
				}
			}

			var que = new Queue<(int i, int j, int d)>();
			var (si, sj) = input.Dens[c];
			dists[c, si, sj] = 0;
			que.Enqueue((si, sj, 0));

			while (que.Count > 0) {
				var (i, j, d) = que.Dequeue();
				int nd = d + 1;
				foreach (var (ii, jj) in Helper.Adjacence4(i, j, n, n)) {
					if (input.Map[ii, jj] == -2 || dists[c, ii, jj] <= nd) {
						continue;
					}

					dists[c, ii, jj] = nd;
					que.Enqueue((ii, jj, nd));
				}
			}
		}

		var pque = new HeapQueue<
			(int i, int j, (int special, int distance) priority, int version),
			(int special, int distance)>(
			x => x.priority, reverses: false);
		var versions = new int[n, n];

		void Enqueue(int i, int j)
		{
			if (state.Slimes[i, j].Count == 0) {
				return;
			}

			int topColor = state.Slimes[i, j].Peek();
			int nestColor = input.Map[i, j];
			bool blocksNest = nestColor >= 0 && topColor != nestColor;
			bool isHeightLimit = state.Slimes[i, j].Count == 8;
			int special = blocksNest || isHeightLimit ? 1 : 0;
			var priority = (special, dists[topColor, i, j]);
			int version = ++versions[i, j];
			pque.Enqueue((i, j, priority, version));
		}

		for (int i = 0; i < n; i++) {
			for (int j = 0; j < n; j++) {
				Enqueue(i, j);
			}
		}

		var deferred = new List<(int i, int j)>();
		while (true) {
			bool changedSinceRetry = false;
			while (pque.Count > 0 && state.Moves.Count < 100000) {
				var (i, j, _, version) = pque.Dequeue();
				if (version != versions[i, j] || state.Slimes[i, j].Count == 0) {
					continue;
				}

				if (!TryFindBestMove(i, j,
					out int k, out Direction4 dir, out int l,
					out int ni, out int nj)) {
					deferred.Add((i, j));
					continue;
				}

				state.Move(i, j, k, dir, l);
				changedSinceRetry = true;
				Enqueue(i, j);
				Enqueue(ni, nj);
			}

			if (state.Moves.Count >= 100000 || deferred.Count == 0 || !changedSinceRetry) {
				break;
			}

			foreach (var (i, j) in deferred) {
				Enqueue(i, j);
			}
			deferred.Clear();
		}

		return state;

		bool TryFindBestMove(
			int i, int j,
			out int bestK, out Direction4 bestDir, out int bestL,
			out int bestNi, out int bestNj)
		{
			bestK = 0;
			bestDir = Direction4.N;
			bestL = 0;
			bestNi = 0;
			bestNj = 0;

			int h = state.Slimes[i, j].Count;
			int[] stack = state.Slimes[i, j].ToArray();
			long bestImprovement = 0;
			bool found = false;

			for (int k = 0; k < h; k++) {
				int moved = h - k;
				long fromDistance = 0;
				for (int index = 0; index < moved; index++) {
					fromDistance += dists[stack[index], i, j];
				}

				foreach (var dir in Direction4Extensions.ForEach4()) {
					for (int l = 1; l <= k + 1; l++) {
						bool pathIsFloor = true;
						for (int step = 1; step <= l; step++) {
							var (ii, jj) = dir.Move(i, j, step);
							if ((uint)ii >= (uint)n
								|| (uint)jj >= (uint)n
								|| input.Map[ii, jj] == -2) {
								pathIsFloor = false;
								break;
							}
						}

						if (!pathIsFloor) {
							break;
						}

						var (destI, destJ) = dir.Move(i, j, l);
						if (state.Slimes[destI, destJ].Count + moved > 8) {
							continue;
						}

						long toDistance = 0;
						bool noSlimeWorsens = true;
						for (int index = 0; index < moved; index++) {
							int color = stack[index];
							int from = dists[color, i, j];
							int to = dists[color, destI, destJ];
							toDistance += to;
							if (to > from) {
								noSlimeWorsens = false;
							}
						}

						if (requireNonWorsening && !noSlimeWorsens) {
							continue;
						}

						long improvement = fromDistance - toDistance;
						if (improvement > bestImprovement) {
							bestImprovement = improvement;
							bestK = k;
							bestDir = dir;
							bestL = l;
							bestNi = destI;
							bestNj = destJ;
							found = true;
						}
					}
				}
			}

			return found;
		}
	}

	static State DoPlanByColorOrder(
		bool isLocal, Stopwatch sw, Random rnd, Input input, Information info,
		int[] colorOrder)
	{
		var state = new State(isLocal, input);
		int n = input.N;

		foreach (int c in colorOrder) {
			var dists = new int[n, n].Fill(int.MaxValue);
			var que = new Queue<(int i, int j, int d)>();
			var (si, sj) = input.Dens[c];
			dists[si, sj] = 0;
			que.Enqueue((si, sj, 0));

			while (que.Count > 0) {
				var (i, j, d) = que.Dequeue();
				int nd = d + 1;
				foreach (var (ii, jj) in Helper.Adjacence4(i, j, n, n)) {
					if (input.Map[ii, jj] == -2 || dists[ii, jj] <= nd) {
						continue;
					}

					dists[ii, jj] = nd;
					que.Enqueue((ii, jj, nd));
				}
			}

			var pque = new HeapQueue<(int i, int j, int d), int>(x => x.d, reverses: false);
			var queued = new HashSet<(int i, int j)>();

			void EnqueueIfTargetExists(int i, int j)
			{
				if (state.Slimes[i, j].Count > 0
					&& state.Slimes[i, j].Peek() == c
					&& queued.Add((i, j))) {
					pque.Enqueue((i, j, dists[i, j]));
				}
			}

			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					EnqueueIfTargetExists(i, j);
				}
			}

			var deferred = new List<(int i, int j)>();
			bool retryDeferred;
			do {
				bool changedSinceRetry = false;

				while (pque.Count > 0 && state.Moves.Count < 100000) {
					var (i, j, _) = pque.Dequeue();
					queued.Remove((i, j));

					if (state.Slimes[i, j].Count == 0
						|| state.Slimes[i, j].Peek() != c) {
						continue;
					}

					if (!TryFindBestMove(i, j,
						out int k, out Direction4 dir, out int l,
						out int ni, out int nj)) {
						deferred.Add((i, j));
						continue;
					}

					state.Move(i, j, k, dir, l);
					changedSinceRetry = true;
					EnqueueIfTargetExists(i, j);
					EnqueueIfTargetExists(ni, nj);
				}

				if (state.Moves.Count >= 100000) {
					return state;
				}

				retryDeferred = deferred.Count > 0 && changedSinceRetry;
				if (retryDeferred) {
					foreach (var (i, j) in deferred) {
						EnqueueIfTargetExists(i, j);
					}
					deferred.Clear();
				}
			} while (retryDeferred);

			bool TryFindBestMove(
				int i, int j,
				out int bestK, out Direction4 bestDir, out int bestL,
				out int bestNi, out int bestNj)
			{
				bestK = 0;
				bestDir = Direction4.N;
				bestL = 0;
				bestNi = 0;
				bestNj = 0;

				int h = state.Slimes[i, j].Count;
				int fromDist = dists[i, j];
				int targetCount = 0;
				int topTargetCount = 0;
				bool stillCountingTop = true;
				foreach (int slime in state.Slimes[i, j]) {
					if (slime == c) {
						targetCount++;
						if (stillCountingTop) {
							topTargetCount++;
						}
					} else {
						stillCountingTop = false;
					}
				}

				long bestTotalDistance = (long)targetCount * fromDist;
				bool found = false;

				for (int k = h - topTargetCount; k < h; k++) {
					int moved = h - k;
					foreach (var dir in Direction4Extensions.ForEach4()) {
						for (int l = 1; l <= k + 1; l++) {
							bool pathIsFloor = true;
							for (int step = 1; step <= l; step++) {
								var (ii, jj) = dir.Move(i, j, step);
								if ((uint)ii >= (uint)n
									|| (uint)jj >= (uint)n
									|| input.Map[ii, jj] == -2) {
									pathIsFloor = false;
									break;
								}
							}

							if (!pathIsFloor) {
								break;
							}

							var (destI, destJ) = dir.Move(i, j, l);
							int toDist = dists[destI, destJ];
							if (toDist >= fromDist
								|| state.Slimes[destI, destJ].Count + moved > 8) {
								continue;
							}

							long totalDistance =
								(long)(targetCount - moved) * fromDist
								+ (long)moved * toDist;
							if (totalDistance < bestTotalDistance) {
								bestTotalDistance = totalDistance;
								bestK = k;
								bestDir = dir;
								bestL = l;
								bestNi = destI;
								bestNj = destJ;
								found = true;
							}
						}
					}
				}

				return found;
			}
		}

		return state;
	}
}

public class State
{
	private readonly bool _isLocal;
	private readonly Input _input;

	public int SlimeCount { get; private set; }
	public Stack<int>[,] Slimes { get; private set; }
	public List<(int i, int j, int k, Direction4 d, int l)> Moves { get; private set; } = [];

	public State(bool isLocal, Input input)
	{
		_isLocal = isLocal;
		_input = input;

		int n = _input.N;
		Slimes = Helper.Array2(n, n, (i, j) => new Stack<int>());
		for (int i = 0; i < n; i++) {
			for (int j = 0; j < n; j++) {
				char c = _input.C[i][j];
				if (c >= 'a' && c <= 'l') {
					Slimes[i, j].Push(c - 'a');
					++SlimeCount;
				}
			}
		}
	}

	public bool Move(int i, int j, int k, Direction4 d, int l)
	{
		var (ni, nj) = d.Move(i, j, l);
		int moveCount = Slimes[i, j].Count - k;
		for (int count = 0; count < moveCount; count++) {
			Slimes[ni, nj].Push(Slimes[i, j].Pop());
		}

		Moves.Add((i, j, k, d, l));
		bool backed = ReturnHome(i, j);
		backed |= ReturnHome(ni, nj);
		return backed;
	}

	private bool ReturnHome(int i, int j)
	{
		int denColor = _input.Map[i, j];
		if (denColor < 0) {
			return false;
		}

		bool backed = false;
		while (Slimes[i, j].Count > 0 && Slimes[i, j].Peek() == denColor) {
			Slimes[i, j].Pop();
			--SlimeCount;
			backed = true;
		}

		return backed;
	}

	public long CalculateScore()
	{
		long score = Moves.Count;
		score += SlimeCount * 100000L;
		return score;
	}

	public void Simulate()
	{
		if (Moves.Count > 100000) {
			throw new InvalidOperationException(
				$"Invalid move sequence: {Moves.Count} operations exceeds the 100000-operation limit.");
		}

		int n = _input.N;
		var board = Helper.Array2(n, n, (i, j) => new Stack<int>());
		for (int i = 0; i < n; i++) {
			for (int j = 0; j < n; j++) {
				char cell = _input.C[i][j];
				if (cell >= 'a' && cell <= 'l') {
					board[i, j].Push(cell - 'a');
				}
			}
		}

		var dists = new int[_input.K, n, n];
		for (int c = 0; c < _input.K; c++) {
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					dists[c, i, j] = int.MaxValue;
				}
			}

			var que = new Queue<(int i, int j, int d)>();
			var (si, sj) = _input.Dens[c];
			dists[c, si, sj] = 0;
			que.Enqueue((si, sj, 0));
			while (que.Count > 0) {
				var (i, j, d) = que.Dequeue();
				int nd = d + 1;
				foreach (var (ii, jj) in Helper.Adjacence4(i, j, n, n)) {
					if (_input.Map[ii, jj] == -2 || dists[c, ii, jj] <= nd) {
						continue;
					}

					dists[c, ii, jj] = nd;
					que.Enqueue((ii, jj, nd));
				}
			}
		}

		void ReturnHome(int i, int j)
		{
			int denColor = _input.Map[i, j];
			while (denColor >= 0 && board[i, j].Count > 0
				&& board[i, j].Peek() == denColor) {
				board[i, j].Pop();
			}
		}

		InvalidOperationException InvalidMove(
			int turn, (int i, int j, int k, Direction4 d, int l) move, string reason)
		{
			return new InvalidOperationException(
				$"Invalid move at turn {turn}: ({move.i}, {move.j}) k={move.k} "
				+ $"d={move.d} l={move.l}. {reason}");
		}

		void WriteStateLog(int turn, string moveJson)
		{
			var colorCounts = new int[_input.K];
			var distanceByColor = new long[_input.K];
			var heightCounts = new int[9];
			int remaining = 0;
			int maxHeight = 0;
			int blockedNests = 0;
			long totalDistance = 0;

			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					var tower = board[i, j];
					int height = tower.Count;
					if (_input.Map[i, j] != -2) {
						heightCounts[height]++;
					}
					maxHeight = Math.Max(maxHeight, height);

					if (_input.Map[i, j] >= 0 && height > 0
						&& tower.Peek() != _input.Map[i, j]) {
						blockedNests++;
					}

					foreach (int color in tower) {
						colorCounts[color]++;
						distanceByColor[color] += dists[color, i, j];
						remaining++;
					}
				}
			}

			for (int c = 0; c < _input.K; c++) {
				totalDistance += distanceByColor[c];
			}

			var log = new StringBuilder();
			log.Append("{\"turn\":").Append(turn)
				.Append(",\"move\":").Append(moveJson ?? "null")
				.Append(",\"remaining\":").Append(remaining)
				.Append(",\"maxHeight\":").Append(maxHeight)
				.Append(",\"heightCounts\":");
			AppendArray(log, heightCounts);
			log.Append(",\"colorCounts\":");
			AppendArray(log, colorCounts);
			log.Append(",\"distanceByColor\":");
			AppendArray(log, distanceByColor);
			log.Append(",\"totalDistance\":").Append(totalDistance)
				.Append(",\"blockedNests\":").Append(blockedNests)
				.Append(",\"board\":[");

			for (int i = 0; i < n; i++) {
				if (i > 0) {
					log.Append(',');
				}
				log.Append('[');
				for (int j = 0; j < n; j++) {
					if (j > 0) {
						log.Append(',');
					}

					string cell;
					if (_input.Map[i, j] == -2) {
						cell = "#";
					} else {
						var tower = board[i, j].ToArray();
						var towerText = new StringBuilder();
						for (int index = tower.Length - 1; index >= 0; index--) {
							towerText.Append((char)('a' + tower[index]));
						}

						if (_input.Map[i, j] >= 0) {
							cell = $"{(char)('A' + _input.Map[i, j])}:{(towerText.Length == 0 ? "-" : towerText.ToString())}";
						} else {
							cell = towerText.Length == 0 ? "." : towerText.ToString();
						}
					}

					log.Append('"').Append(cell).Append('"');
				}
				log.Append(']');
			}

			log.Append("]}");
			Console.Error.WriteLine(log);
		}

		void AppendArray<T>(StringBuilder builder, T[] values)
		{
			builder.Append('[');
			for (int index = 0; index < values.Length; index++) {
				if (index > 0) {
					builder.Append(',');
				}
				builder.Append(values[index]);
			}
			builder.Append(']');
		}

		WriteStateLog(0, null);
		for (int turn = 0; turn < Moves.Count; turn++) {
			var move = Moves[turn];
			if ((uint)move.i >= (uint)n || (uint)move.j >= (uint)n) {
				throw InvalidMove(turn, move, "The source is outside the board.");
			}
			if (move.d != Direction4.U && move.d != Direction4.D
				&& move.d != Direction4.L && move.d != Direction4.R) {
				throw InvalidMove(turn, move, "The direction is invalid.");
			}
			if (_input.Map[move.i, move.j] == -2) {
				throw InvalidMove(turn, move, "The source is a wall.");
			}

			int height = board[move.i, move.j].Count;
			if (height == 0) {
				throw InvalidMove(turn, move, "The source has no slime tower.");
			}
			if (move.k < 0 || move.k >= height) {
				throw InvalidMove(turn, move,
					$"k must satisfy 0 <= k < {height}.");
			}
			if (move.l < 1 || move.l > move.k + 1) {
				throw InvalidMove(turn, move,
					$"l must satisfy 1 <= l <= k + 1 ({move.k + 1}).");
			}

			for (int step = 1; step <= move.l; step++) {
				var (ii, jj) = move.d.Move(move.i, move.j, step);
				if ((uint)ii >= (uint)n || (uint)jj >= (uint)n) {
					throw InvalidMove(turn, move,
						$"The path leaves the board at ({ii}, {jj}).");
				}
				if (_input.Map[ii, jj] == -2) {
					throw InvalidMove(turn, move,
						$"The path hits a wall at ({ii}, {jj}).");
				}
			}

			var (destI, destJ) = move.d.Move(move.i, move.j, move.l);
			int moved = height - move.k;
			int destinationHeight = board[destI, destJ].Count;
			if (destinationHeight + moved > 8) {
				throw InvalidMove(turn, move,
					$"The destination tower height would be {destinationHeight + moved}, exceeding 8.");
			}

			for (int count = 0; count < moved; count++) {
				board[destI, destJ].Push(board[move.i, move.j].Pop());
			}
			ReturnHome(move.i, move.j);
			ReturnHome(destI, destJ);

			string moveJson =
				$"[{move.i},{move.j},{move.k},\"{move.d.ToSymbol()}\",{move.l}]";
			WriteStateLog(turn + 1, moveJson);
		}
	}
}

public class Information
{
	public long Time { get; set; } = 0;
	public long Score { get; set; } = 0;
	public int Loop { get; set; } = 0;
	public int Up { get; set; } = 0;
	public long Elapsed { get; set; } = 0;

	public int WallCount { get; set; } = 0;
	public int SlimeCount { get; set; } = 0;
}

public class Input
{
	private readonly IOManager _io;
	private readonly bool _isLocal;

	public int N { get; }
	public int K { get; }
	public string[] C { get; }

	public int[,] Map { get; }
	public (int i, int j)[] Dens { get; }

	public Input(IOManager io, bool isLocal)
	{
		_io = io;
		_isLocal = isLocal;

		(N, K) = _io.Int2();
		C = io.ArrayString(N);

		Map = new int[N, N];
		Dens = new (int i, int j)[K];
		for (int i = 0; i < N; i++) {
			for (int j = 0; j < N; j++) {
				char c = C[i][j];
				if (c >= 'A' && c <= 'L') {
					int cc = c - 'A';
					Map[i, j] = c - 'A';
					Dens[cc] = (i, j);
				} else if (c == '#') {
					Map[i, j] = -2;
				} else {
					Map[i, j] = -1;
				}
			}
		}
	}

	public void Output(State state)
	{
		foreach (var (i, j, k, d, l) in state.Moves) {
			_io.WriteLine($"{i} {j} {k} {d.ToSymbol()} {l}");
		}
	}
}

// 以下は共通ライブラリー。 https://github.com/takytank/KyoProLib
// 明示的な指示があるまで変更してはいけない。

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
		int localTestCaseRunCount,
		double judgePreTestCaseCount,
		bool isParallel,
		Func<int, T> run,
		Func<object, int, T, (long score, int loop, int up, long elapsed)> outputCaseInformation,
		Action<double, int> addOutput = null)
	{
#if DEBUG
		object locker = new();
		long scoreSum = 0;
		double scoreLogSum = 0;
		long loopSum = 0;
		long upSum = 0;
		long elapsedSum = 0;
		long scoreMin = long.MaxValue;
		long scoreMax = long.MinValue;
		int errorCount = 0;
		if (isParallel) {
			Parallel.For(0, localTestCaseRunCount, i => {
				RunCases(i);
			});
		} else {
			for (int i = 0; i < localTestCaseRunCount; i++) {
				RunCases(i);
			}
		}

		void RunCases(int i)
		{
			try {
				var ret = run(i);
				var (score, loop, up, elapsed) = outputCaseInformation(locker, i, ret);
				Console.Out.Flush();
				lock (locker) {
					scoreSum += score;
					scoreLogSum += Math.Log10(score);
					loopSum += loop;
					upSum += up;
					elapsedSum += elapsed;
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

		// 本番環境への提出時とスコアのオーダーが揃うように、プレテストのケース数換算のスコアにする。
		scoreSum = (long)(scoreSum / (localTestCaseRunCount / judgePreTestCaseCount));
		scoreLogSum /= localTestCaseRunCount / judgePreTestCaseCount;
		Console.WriteLine("");

		Console.WriteLine("");
		Console.WriteLine($"sum: {scoreSum}");
		Console.WriteLine($"ave: {scoreSum / judgePreTestCaseCount}");
		Console.WriteLine($"min: {scoreMin}");
		Console.WriteLine($"max: {scoreMax}");
		Console.WriteLine($"log: {scoreLogSum / judgePreTestCaseCount}");
		Console.WriteLine($"loop ave.: {loopSum / (double)localTestCaseRunCount:f3}");
		Console.WriteLine($"up ave.: {upSum / (double)localTestCaseRunCount:f3}");
		Console.WriteLine($"elpased ave.: {elapsedSum / (double)localTestCaseRunCount:f3}");

		addOutput?.Invoke(judgePreTestCaseCount, localTestCaseRunCount);

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