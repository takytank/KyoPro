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
			localTestCaseRunCount: 100,
			judgePreTestCaseCount: 50,
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

		var best = DoPlan12(isLocal, sw, rnd, input, info);

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

	// 他色の塔を踏み台にした移動手数で木を作り、合流の各移動は最新盤面で探索する。
	static State DoPlan15(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
		=> DoTreeBase(isLocal, sw, rnd, input, info);

	// 開始時の先行移動と、初期位置の通過回数による全色横断のグループ選択を組み合わせる。
	static State DoPlan19(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
		=> DoAdvanceBase(isLocal, sw, rnd, input, info);

	static State DoAdvanceBase(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var prediction = DoPlan15(isLocal, sw, rnd, input, new Information());
		int n = input.N, cells = n * n;
		var identities = CreateTreeIdentities(input);
		var visitors = new HashSet<int>[cells];
		var prefixes = new List<(int src, int dst, Direction4 dir)>[cells];
		var closed = new bool[cells];
		foreach (var (i, j, keep, dir, len) in prediction.Moves) {
			var source = identities[i, j];
			var cargo = source.Take(source.Count - keep).ToArray();
			for (int step = 1; step <= len; step++) {
				var (pi, pj) = dir.Move(i, j, step);
				var users = visitors[pi * n + pj] ??= [];
				foreach (int id in cargo) users.Add(id);
			}
			var (li, lj) = dir.Move(i, j, len);
			if (source.Count == 1 && cargo.Length == 1 && !closed[cargo[0]]
				&& len == 1 && identities[li, lj].Count == 0 && input.Map[li, lj] < 0) {
				(prefixes[cargo[0]] ??= []).Add((i * n + j, li * n + lj, dir));
			} else {
				foreach (int id in cargo) closed[id] = true;
			}
			foreach (int id in identities[li, lj]) closed[id] = true;
			MoveTreeIdentities(input, identities, i, j, keep, dir, len);
		}
		var state = new State(isLocal, input);
		identities = CreateTreeIdentities(input);
		var positions = Enumerable.Range(0, cells).ToArray();
		var progress = new int[cells];
		var excluded = new bool[cells];
		int replayed = 0;
		Advance(state);
		return DoTreeBase(isLocal, sw, rnd, input, info,
			usage: CountInitialPassages(input, prediction), initialState: state);

		void Advance(State board)
		{
			// 前回からの操作を再生し、単独のまま残っている個体だけを追跡する。
			while (replayed < board.Moves.Count) {
				var (i, j, keep, dir, len) = board.Moves[replayed++];
				var (li, lj) = dir.Move(i, j, len);
				var cargo = identities[i, j].Take(identities[i, j].Count - keep).ToArray();
				if (identities[i, j].Count != 1 || identities[li, lj].Count > 0) {
					foreach (int id in cargo) excluded[id] = true;
					foreach (int id in identities[li, lj]) excluded[id] = true;
				}
				MoveTreeIdentities(input, identities, i, j, keep, dir, len);
				foreach (int id in cargo) {
					positions[id] = li * n + lj;
					if (identities[li, lj].Count == 0) excluded[id] = true;
				}
			}
			for (int id = 0; id < cells; id++) {
				if (excluded[id] || prefixes[id] == null || UsedByOthers(id, id)) continue;
				int target = prefixes[id].FindIndex(move => UsedByOthers(move.dst, id));
				if (target < progress[id]) continue;
				int first = progress[id];
				int pos = positions[id];
				if (prefixes[id][first].src != pos || identities[pos / n, pos % n].Count != 1
					|| identities[pos / n, pos % n].Peek() != id) continue;
				// 途中がまだ占有されていれば、次の帰巣後まで待つ。
				bool clear = true;
				for (int k = first; k <= target; k++) {
					int dst = prefixes[id][k].dst;
					if (board.Slimes[dst / n, dst % n].Count != 0) { clear = false; break; }
				}
				if (!clear || board.Moves.Count + target - first + 1 > 100000) continue;
				for (int k = first; k <= target; k++) {
					var (src, dst, dir) = prefixes[id][k];
					board.Move(src / n, src % n, 0, dir, 1);
					MoveTreeIdentities(input, identities, src / n, src % n, 0, dir, 1);
					positions[id] = dst;
					replayed++;
				}
				progress[id] = target + 1;
			}
		}

		bool UsedByOthers(int cell, int id) => visitors[cell] != null && visitors[cell].Any(other => other != id);
	}

	static int[] CountInitialPassages(Input input, State prediction)
	{
		var identities = CreateTreeIdentities(input);
		var usage = new int[input.N * input.N];
		foreach (var (i, j, keep, dir, len) in prediction.Moves) {
			var cargo = identities[i, j].Take(identities[i, j].Count - keep).ToArray();
			// 現在の占有状態ではなく、初期位置に対する通過を数える。自分だけの移動は除外する。
			for (int step = 1; step <= len; step++) {
				var (pi, pj) = dir.Move(i, j, step);
				int cell = pi * input.N + pj;
				if (input.C[pi][pj] >= 'a' && input.C[pi][pj] <= 'l' && cargo.Any(id => id != cell)) usage[cell]++;
			}
			MoveTreeIdentities(input, identities, i, j, keep, dir, len);
		}
		return usage;
	}

	static Stack<int>[,] CreateTreeIdentities(Input input)
		=> Helper.Array2(input.N, input.N, (i, j) => {
			var ids = new Stack<int>();
			if (input.C[i][j] >= 'a' && input.C[i][j] <= 'l') ids.Push(i * input.N + j);
			return ids;
		});

	static void MoveTreeIdentities(Input input, Stack<int>[,] ids, int i, int j, int keep, Direction4 dir, int len)
	{
		var (li, lj) = dir.Move(i, j, len);
		int count = ids[i, j].Count - keep;
		for (int k = 0; k < count; k++) ids[li, lj].Push(ids[i, j].Pop());
		Return(i, j);
		Return(li, lj);

		void Return(int ri, int rj)
		{
			int color = input.Map[ri, rj];
			while (color >= 0 && ids[ri, rj].Count > 0) {
				int id = ids[ri, rj].Peek();
				if (input.C[id / input.N][id % input.N] - 'a' != color) break;
				ids[ri, rj].Pop();
			}
		}
	}

	static State DoTreeBase(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info, int[] usage = null, State initialState = null)
	{
		var state = initialState ?? new State(isLocal, input);
		int n = input.N;
		int cells = n * n;
		var identities = usage == null ? null : CreateTreeIdentities(input);
		// 先行移動後も、通過評価は個体の初期位置に結び付けて扱う。
		if (identities != null && initialState != null) {
			foreach (var (i, j, keep, dir, len) in initialState.Moves)
				MoveTreeIdentities(input, identities, i, j, keep, dir, len);
		}

		// 色は少ない順に処理する (Plan8 と同じ)。
		var colorCounts = new int[input.K];
		foreach (string row in input.C) {
			foreach (char ch in row) {
				if (ch >= 'a' && ch < 'a' + input.K) colorCounts[ch - 'a']++;
			}
		}
		var order = Enumerable.Range(0, input.K).ToArray();
		Array.Sort(order, (a, b) => colorCounts[a] != colorCounts[b]
			? colorCounts[a].CompareTo(colorCounts[b]) : a.CompareTo(b));

		var denCells = new int[input.K];
		for (int c = 0; c < input.K; c++) denCells[c] = input.Dens[c].i * n + input.Dens[c].j;

		// 床だけを歩く全点対距離 (木を作るときの最短経路と、木に乗れない場合の迂回にだけ使う)。
		var floor = new int[cells, cells];
		for (int s = 0; s < cells; s++) {
			for (int t = 0; t < cells; t++) floor[s, t] = int.MaxValue;
			if (input.Map[s / n, s % n] == -2) continue;
			var queue = new Queue<int>();
			floor[s, s] = 0;
			queue.Enqueue(s);
			while (queue.Count > 0) {
				int u = queue.Dequeue();
				foreach (var (ni, nj) in Helper.Adjacence4(u / n, u % n, n, n)) {
					int v = ni * n + nj;
					if (input.Map[ni, nj] == -2 || floor[s, v] != int.MaxValue) continue;
					floor[s, v] = floor[s, u] + 1;
					queue.Enqueue(v);
				}
			}
		}

		// 現在の色の木。treeParent は根 (巣) 方向の親マス、treeDepth は根からの深さ。
		var treeParent = new int[cells];
		var treeDepth = new int[cells];
		var inTree = new bool[cells];

		// 確定したグループ。edges は (運ぶ元, 運ぶ先) を実行順 (深い方から) に並べたもので、root はグループ最上位の一団のいるマス。
		var groups = new List<(List<(int src, int dst)> edges, int root)>();
		while (state.SlimeCount > 0 && state.Moves.Count < 100000) {
			int roundStart = state.Moves.Count;
			if (usage != null) {
				int selectedColor = -1, selectedScore = int.MaxValue;
				(List<(int src, int dst)> edges, int root) selected = default;
				foreach (int color in order) {
					if (!HasTop(color) || !BuildGroups(color)) continue;
					// 各色で Plan15 が最初に処理する候補だけを比較する。
					var group = groups[0];
					var sources = new HashSet<int> { group.root };
					foreach (var (src, dst) in group.edges) sources.Add(src);
					int score = 0;
					foreach (int src in sources) {
						foreach (int id in identities[src / n, src % n]) {
							if (input.C[id / n][id % n] - 'a' != color) break;
							score += usage[id];
						}
					}
					if (score >= selectedScore) continue;
					selectedScore = score;
					selectedColor = color;
					selected = group;
				}
				if (selectedColor < 0) break;
				foreach (var (src, dst) in selected.edges) Carry(selectedColor, src, dst);
				CarryHome(selectedColor, selected.root);
				if (state.Moves.Count == roundStart) break;
				continue;
			}
			foreach (int color in order) {
				while (state.Moves.Count < 100000 && HasTop(color)) {
					int before = state.Moves.Count;
					if (!BuildGroups(color)) break;
					// 最も深いグループだけを実行し、置いていかれたスライムを含めてグルーピングをやり直す。
					var group = groups[0];
					foreach (var (src, dst) in group.edges) Carry(color, src, dst);
					CarryHome(color, group.root);
					if (state.Moves.Count == before) break;
				}
			}
			if (state.Moves.Count == roundStart) break;
		}

		return state;

		void Move(int i, int j, int keep, Direction4 dir, int len)
		{
			if (identities != null) MoveTreeIdentities(input, identities, i, j, keep, dir, len);
			state.Move(i, j, keep, dir, len);
		}

		int TopRun(Stack<int> tower, int c)
		{
			int count = 0;
			foreach (int x in tower) {
				if (x != c) break;
				count++;
			}

			return count;
		}

		bool HasTop(int c)
		{
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					if (state.Slimes[i, j].Count > 0 && state.Slimes[i, j].Peek() == c) return true;
				}
			}

			return false;
		}

		// 最小全域木を作り、深い方から根へ 1 マスずつ合流させてグループを確定する。
		// 合流後の塔の高さは 8 以下とし、高さ 8 または巣の手前でグループを確定する。
		bool BuildGroups(int c)
		{
			int denCell = denCells[c];
			Array.Fill(inTree, false);
			Array.Fill(treeParent, -1);
			var treeCells = new List<int> { denCell };
			inTree[denCell] = true;
			treeDepth[denCell] = 0;

			var terminals = new List<int>();
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					int cell = i * n + j;
					if (cell == denCell || floor[denCell, cell] == int.MaxValue) continue;
					if (state.Slimes[i, j].Count == 0 || state.Slimes[i, j].Peek() != c) continue;
					terminals.Add(cell);
				}
			}
			if (terminals.Count == 0) return false;

			// Prim 法: 木にまだつながっていない塔のうち、木のマスに最も近いものを、その最短経路ごと木につなぐ。
			var pending = new List<int>(terminals);
			var path = new List<int>();
			while (pending.Count > 0) {
				int bestTerminal = -1, bestTreeCell = -1, bestDistance = int.MaxValue;
				{
					foreach (int t in pending) {
						var connection = FindJumpConnection(c, t);
						if (connection.Count == 0 || connection.Count - 1 >= bestDistance) continue;
						bestDistance = connection.Count - 1;
						bestTerminal = t;
						bestTreeCell = connection[^1];
						path.Clear();
						path.AddRange(connection.Take(connection.Count - 1));
					}
					if (bestTerminal < 0) return false;
					for (int k = path.Count - 1; k >= 0; k--) {
						int parentCell = k + 1 < path.Count ? path[k + 1] : bestTreeCell;
						treeParent[path[k]] = parentCell;
						treeDepth[path[k]] = treeDepth[parentCell] + 1;
						inTree[path[k]] = true;
						treeCells.Add(path[k]);
					}
					pending.RemoveAll(t => inTree[t]);
					continue;
				}

			}

			var isTerminal = new bool[cells];
			foreach (int t in terminals) isTerminal[t] = true;
			var kids = new List<int>[cells];
			foreach (int x in treeCells) {
				if (x != denCell) (kids[treeParent[x]] ??= []).Add(x);
			}
			var byDepth = treeCells.Where(x => x != denCell).OrderByDescending(x => treeDepth[x]).ToList();

			// 各マスに「そこまで上がってきた一団」を持つ (合流できなかった一団は確定させず、そのまま根へ向かって進める)。
			// position は一団が実際にいるマス (合流するまで動かさない)。maxDepth は一団に含まれる最も深い塔の深さ。
			var bundlesAt = new List<(int load, int baseHeight, int position, int maxDepth, List<(int src, int dst)> edges)>[cells];
			groups.Clear();
			foreach (int x in byDepth) {
				var incoming = new List<(int load, int baseHeight, int position, int maxDepth, List<(int src, int dst)> edges)>();
				if (kids[x] != null) {
					foreach (int k in kids[x]) {
						if (bundlesAt[k] != null) incoming.AddRange(bundlesAt[k]);
					}
				}

				// 深い葉を持つ一団から先に合流させる。
				incoming.Sort((b1, b2) => b2.maxDepth != b1.maxDepth
					? b2.maxDepth.CompareTo(b1.maxDepth) : b2.load.CompareTo(b1.load));

				var current = new List<(int load, int baseHeight, int position, int maxDepth, List<(int src, int dst)> edges)>();
				if (isTerminal[x]) {
					var tower = state.Slimes[x / n, x % n];
					int run = TopRun(tower, c);
					current.Add((run, tower.Count - run, x, treeDepth[x], []));
				}

				// 合流後の高さが 8 以下になる最初の一団へ合流させる。合流先が無ければ別の一団として残す。
				foreach (var bundle in incoming) {
					// 準備時は一団内の既存の塔を集合先にし、別グループと分岐マスを共有しない。
					int meeting = x;
					int target = -1;
					for (int m = 0; m < current.Count; m++) {
						int targetBase = current[m].position == meeting ? current[m].baseHeight
							: state.Slimes[meeting / n, meeting % n].Count;
						int height = targetBase + current[m].load + bundle.load;
						if (height <= 8) {
							target = m;
							break;
						}
					}
					if (target < 0) {
						current.Add(bundle);
						continue;
					}

					var (load, baseHeight, position, maxDepth, edges) = current[target];
					if (position != meeting) {
						edges.Add((position, meeting));
						position = meeting;
						baseHeight = state.Slimes[meeting / n, meeting % n].Count;
					}
					edges.AddRange(bundle.edges);
					edges.Add((bundle.position, meeting));
					current[target] = (load + bundle.load, baseHeight, position, Math.Max(maxDepth, bundle.maxDepth), edges);
				}

				// 高さが 8 に達した一団、または巣に着いた一団はここで確定する。それ以外は根へ向かって進む。
				var rest = new List<(int load, int baseHeight, int position, int maxDepth, List<(int src, int dst)> edges)>();
				foreach (var bundle in current) {
					int height = (bundle.position == x ? bundle.baseHeight : 0) + bundle.load;
					if (height >= 8
						|| treeParent[x] == denCell) {
						groups.Add((bundle.edges, bundle.position));
					} else {
						rest.Add(bundle);
					}
				}
				if (rest.Count > 0) bundlesAt[x] = rest;
			}

			return groups.Count > 0;
		}

		// 運ぶ匹数を固定し、他色の土台は動かさず、既存の木までの最短手数を求める。
		List<int> FindJumpConnection(int c, int src)
		{
			int load = TopRun(state.Slimes[src / n, src % n], c);
			var previous = new int[cells];
			Array.Fill(previous, -1);
			var queue = new Queue<int>();
			previous[src] = src;
			queue.Enqueue(src);
			while (queue.Count > 0) {
				int pos = queue.Dequeue();
				var tower = state.Slimes[pos / n, pos % n];
				int pad = pos == src ? tower.Count - load : tower.Count;
				foreach (var dir in Direction4Extensions.ForEach4()) {
					for (int len = 1; len <= pad + 1; len++) {
						var (li, lj) = dir.Move(pos / n, pos % n, len);
						if ((uint)li >= (uint)n || (uint)lj >= (uint)n || input.Map[li, lj] == -2) break;
						int land = li * n + lj;
						if (previous[land] >= 0) continue;
						var landing = state.Slimes[li, lj];
						if (landing.Count + load > 8) continue;
						if (inTree[land]) {
							var result = new List<int> { land };
							for (int x = pos; ; x = previous[x]) {
								result.Add(x);
								if (x == src) break;
							}
							result.Reverse();
							return result;
						}
						if (input.Map[li, lj] == c || TopRun(landing, c) > 0) continue;
						previous[land] = pos;
						queue.Enqueue(land);
					}
				}
			}
			return [];
		}

		void CarryHome(int c, int src)
		{
			CarryRoute(c, src, denCells[c], true);
		}

		// 合流後は木から離れ、位置と同色の匹数を状態にした BFS で帰巣経路を探す。
		bool CarryRoute(int c, int src, int den, bool home)
		{
			var source = state.Slimes[src / n, src % n];
			int run = TopRun(source, c);
			if (run == 0) return true;
			if (src == den) return true;
			int start = src * 9 + run;
			var previous = new int[cells * 9];
			Array.Fill(previous, -1);
			var moves = new (int keep, Direction4 dir, int len)[cells * 9];
			var queue = new Queue<int>();
			previous[start] = start;
			queue.Enqueue(start);
			int goal = -1;
			while (queue.Count > 0 && goal < 0) {
				int node = queue.Dequeue();
				int pos = node / 9, count = node % 9;
				var tower = state.Slimes[pos / n, pos % n];
				int baseHeight = tower.Count - TopRun(tower, c);
				int height = baseHeight + count;
				for (int carry = count; carry >= (height == 8 && count == 8 ? 7 : count); carry--) {
					int keep = height - carry;
					foreach (var dir in Direction4Extensions.ForEach4()) {
						for (int len = 1; len <= keep + 1; len++) {
							var (li, lj) = dir.Move(pos / n, pos % n, len);
							if ((uint)li >= (uint)n || (uint)lj >= (uint)n || input.Map[li, lj] == -2) break;
							int land = li * n + lj;
							var landing = state.Slimes[li, lj];
							if (landing.Count + carry > 8) continue;
							if (!home && land != den && (TopRun(landing, c) > 0 || input.Map[li, lj] == c)) continue;
							if (!home && carry != count) continue;
							if (carry < count && landing.Count != 1) continue;
							// 通過済みのマスには戻らない。これにより残した土台と元盤面の相違を経路に持ち込まない。
							bool visited = false;
							for (int ancestor = node; ; ancestor = previous[ancestor]) {
								if (ancestor / 9 == land) { visited = true; break; }
								if (ancestor == start) break;
							}
							if (visited) continue;
							int nextCount = carry + TopRun(landing, c);
							int next = land * 9 + nextCount;
							if (previous[next] >= 0) continue;
							previous[next] = node;
							moves[next] = (keep, dir, len);
							if (land == den) { goal = next; break; }
							queue.Enqueue(next);
						}
						if (goal >= 0) break;
					}
					if (goal >= 0) break;
				}
			}
			if (goal < 0) {
				int pos = src;
				Walk(c, ref pos, den, false);
				return false;
			}
			var route = new List<int>();
			for (int node = goal; node != start; node = previous[node]) route.Add(node);
			route.Reverse();
			foreach (int node in route) {
				if (state.Moves.Count >= 100000) break;
				int pos = previous[node] / 9;
				var (keep, dir, len) = moves[node];
				Move(pos / n, pos % n, keep, dir, len);
			}
			return state.Moves.Count < 100000;
		}

		// src の最上段の同色一団を tgt
		void Carry(int c, int src, int tgt) => CarryRoute(c, src, tgt, false);

		bool Walk(int c, ref int pos, int tgt, bool force)
		{
			var dist = new int[cells];
			var queue = new Queue<int>();
			while (pos != tgt && state.Moves.Count < 100000) {
				int si = pos / n, sj = pos % n;
				var tower = state.Slimes[si, sj];
				if (tower.Count == 0 || tower.Peek() != c) return false;
				int run = TopRun(tower, c);
				int keep = tower.Count - run;

				bool useTowers = force;
				while (true) {
					Array.Fill(dist, int.MaxValue);
					dist[tgt] = 0;
					queue.Clear();
					queue.Enqueue(tgt);
					while (queue.Count > 0) {
						int u = queue.Dequeue();
						foreach (var (ni2, nj2) in Helper.Adjacence4(u / n, u % n, n, n)) {
							int v = ni2 * n + nj2;
							if (input.Map[ni2, nj2] == -2 || dist[v] != int.MaxValue) continue;
							if (v != pos && !useTowers) {
								if (state.Slimes[ni2, nj2].Count > 0) continue;
								if (input.Map[ni2, nj2] >= 0 && input.Map[ni2, nj2] != c) continue;
							}
							dist[v] = dist[u] + 1;
							queue.Enqueue(v);
						}
					}
					if (dist[pos] != int.MaxValue || useTowers) break;
					useTowers = true;
				}
				if (dist[pos] == int.MaxValue) return false;

				// まず一団を丸ごと運ぶ。動けない場合だけ、上から運ぶ匹数を減らして (残す匹数を増やして) 探す。
				Direction4 bestDir = Direction4.N;
				int bestLen = 0;
				for (int carry = run; carry >= 1 && bestLen == 0; carry--) {
					int keepCount = tower.Count - carry;
					long best = long.MaxValue;
					foreach (var dir in Direction4Extensions.ForEach4()) {
						for (int len = 1; len <= keepCount + 1; len++) {
							var (li, lj) = dir.Move(si, sj, len);
							if ((uint)li >= (uint)n || (uint)lj >= (uint)n || input.Map[li, lj] == -2) break;
							int land = li * n + lj;
							if (state.Slimes[li, lj].Count + carry > 8) continue;
							if (land != tgt && !useTowers) {
								if (state.Slimes[li, lj].Count > 0) continue;
								if (input.Map[li, lj] >= 0 && input.Map[li, lj] != c) continue;
							}
							if (dist[land] >= dist[pos]) continue;
							long score = dist[land] * 100L + len;
							if (score < best) {
								best = score;
								bestDir = dir;
								bestLen = len;
							}
						}
					}
					keep = keepCount;
				}
				if (bestLen == 0) return false;
				Move(si, sj, keep, bestDir, bestLen);
				var (ni, nj) = bestDir.Move(si, sj, bestLen);
				pos = ni * n + nj;
			}

			return pos == tgt;
		}
	}

	static State DoPlan9(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var baseline = DoPlan19(isLocal, sw, rnd, input, info);
		var optimizer = new Plan9Optimizer(isLocal, sw, input, info);
		baseline = optimizer.OptimizeSameColorMerges(baseline);
		return optimizer.Optimize(baseline);
	}

	static State DoPlan10(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var baseline = DoPlan9(isLocal, sw, rnd, input, info);
		return new Plan9Optimizer(isLocal, sw, input, info).OptimizeJumpSupports(baseline);
	}

	static State DoPlan11(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var baseline = DoPlan10(isLocal, sw, rnd, input, info);
		return new Plan9Optimizer(isLocal, sw, input, info).OptimizeRebuiltRides(baseline);
	}

	static State DoPlan12(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var baseline = DoPlan19(isLocal, sw, rnd, input, info);
		var optimizer = new Plan9Optimizer(isLocal, sw, input, info, 1200);
		baseline = optimizer.OptimizeSameColorMerges(baseline);
		baseline = optimizer.Optimize(baseline);
		baseline = optimizer.OptimizeJumpSupports(baseline);
		baseline = optimizer.OptimizeRebuiltRides(baseline);
		return optimizer.OptimizeBeamIntervals(baseline);
	}

	static State DoPlan20(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var baseline = DoPlan19(isLocal, sw, rnd, input, info);
		return new Plan9Optimizer(isLocal, sw, input, info).SearchWholeBoard(baseline);
	}

	private sealed class Plan9Optimizer
	{
		private readonly int searchDeadline;
		private const int SearchMilliseconds = 1200;
		private const int BeamDeadlineMilliseconds = 1900;
		private const int WholeBeamWidth = 48;
		private const double WholeDistanceWeight = 0.25;
		private const double WholeRemainingWeight = 6;
		private const double WholeLateRemainingWeight = 12;
		private const double WholeTransportWeight = 2;
		private const double WholeRoutePadWeight = 1;
		private const double WholeOverlapWeight = 0.15;
		private const double WholeSameColorWeight = 0.25;
		private const int BranchStageMilliseconds = 1900;
		private const int CheckpointInterval = 32;
		private const int MaxTrialsPerRound = 1200;
		private readonly bool isLocal;
		private readonly Input input;
		private readonly Information info;
		private readonly Stopwatch stopwatch;
		private readonly int n;
		private readonly int[] colors;
		private readonly int[] origins;
		private readonly Direction4[] directions = Direction4Extensions.ForEach4().ToArray();
		private readonly record struct Candidate(int First, int Second, int Length);
		private readonly record struct Leg(int Index, int Start, int End);
		private readonly record struct RouteHop(int Cell, Direction4 Direction, int Length);
		private sealed record Journey(int Color, int[] Cells, Leg[] Legs);
		private sealed record RideCandidate(Journey First, Journey Second, int[] Ends, int Saving);

		// Cargoは下から上の個体ID。修復で同色内の順序が変わった場合は、実際のIDを記録し直す。
		private sealed class Transport
		{
			public int From { get; }
			public int To { get; }
			public Direction4 Direction { get; }
			public int Length { get; }
			public int[] Cargo { get; }
			public int[] SortedCargo { get; }
			public int[] Returned { get; }
			public int Color { get; }

			public Transport(int from, int to, Direction4 direction, int length, int[] cargo, int[] returned, int[] colors)
			{
				From = from;
				To = to;
				Direction = direction;
				Length = length;
				Cargo = cargo;
				Returned = returned;
				SortedCargo = (int[])cargo.Clone();
				Array.Sort(SortedCargo);
				Color = colors[cargo[0]];
				foreach (int id in cargo) if (colors[id] != Color) { Color = -1; break; }
			}
		}

		public Plan9Optimizer(bool isLocal, Stopwatch sw, Input input, Information info, int searchDeadline = int.MaxValue)
		{
			this.searchDeadline = searchDeadline;
			this.isLocal = isLocal;
			this.input = input;
			this.info = info;
			stopwatch = sw;
			n = input.N;
			var cells = new List<int>();
			var slimeColors = new List<int>();
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					char c = input.C[i][j];
					if (c < 'a' || c >= 'a' + input.K) continue;
					cells.Add(i * n + j);
					slimeColors.Add(c - 'a');
				}
			}
			origins = cells.ToArray();
			colors = slimeColors.ToArray();
		}

		private bool CanSearch => stopwatch.ElapsedMilliseconds < Math.Min(SearchMilliseconds, searchDeadline);
		private bool WithinStage(Stopwatch timer, int limit)
			=> timer.ElapsedMilliseconds < limit && stopwatch.ElapsedMilliseconds < searchDeadline;
		private static bool SameGroup(Transport a, Transport b)
			=> a.SortedCargo.AsSpan().SequenceEqual(b.SortedCargo);

		public State OptimizeJumpSupports(State baseline)
		{
			var board = new ReplayBoard(this);
			var best = new List<Transport>(baseline.Moves.Count);
			foreach (var (i, j, k, direction, length) in baseline.Moves) {
				int from = i * n + j;
				if (!board.Capture(from, direction, length, board.Heights[from] - k, out var step))
					throw new InvalidOperationException("Plan10: invalid Plan9 operation.");
				best.Add(step!);
			}
			var finalBoard = board;
			// 将来の便を足場として前倒しし、同じ集団の直線輸送を一度のジャンプへ置き換える。
			for (int round = 0; round < 16 && CanSearch; round++) {
				BuildIndex(best, out var dependencies, out _, out var checkpoints);
				bool improved = false;
				int trials = 0;
				for (int arrival = 0; arrival + 2 < best.Count && CanSearch && !improved; arrival++) {
					var carrier = best[arrival + 1];
					if (carrier.Cargo.Length != best[arrival].Cargo.Length
						|| carrier.From != best[arrival].To || !SameGroup(carrier, best[arrival])) continue;
					int last = arrival + 1;
					while (last + 1 < best.Count && last - arrival < 7
						&& best[last].Returned.Length == 0 && best[last + 1].From == best[last].To
						&& best[last + 1].Direction == carrier.Direction
						&& SameGroup(best[last + 1], carrier)) last++;
					for (int end = last; end >= arrival + 2 && !improved && CanSearch; end--) {
						int distance = 0;
						for (int index = arrival + 1; index <= end; index++) distance += best[index].Length;
						if (distance > 8 || carrier.Direction.Move(carrier.From / n, carrier.From % n, distance)
							!= (best[end].To / n, best[end].To % n)) continue;
						for (int support = end + 1; support < Math.Min(best.Count, end + 97) && trials < 240 && CanSearch; support++) {
							var future = best[support];
							if (future.To != carrier.From || future.From == carrier.From
								|| future.Returned.Length != 0 || future.Cargo.Intersect(carrier.Cargo).Any()) continue;
							trials++;
							info.Loop++;
							var replacement = TryAdvanceJumpSupport(best, arrival, end, support, distance,
								dependencies, checkpoints, finalBoard);
							if (replacement == null) continue;
							best = replacement;
							info.Up++;
							improved = true;
							break;
						}
					}
				}
				if (!improved) break;
			}
			if (best.Count >= baseline.Moves.Count) return baseline;
			var result = new State(isLocal, input);
			foreach (var step in best) {
				int i = step.From / n;
				int j = step.From % n;
				result.Move(i, j, result.Slimes[i, j].Count - step.Cargo.Length, step.Direction, step.Length);
			}
			if (result.SlimeCount != baseline.SlimeCount) throw new InvalidOperationException("Plan10: incomplete result.");
			return result;
		}

		private List<Transport>? TryAdvanceJumpSupport(List<Transport> steps, int arrival, int end,
			int support, int distance, int[][] dependencies, List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var board = checkpoints[arrival / CheckpointInterval].Copy();
			for (int index = arrival / CheckpointInterval * CheckpointInterval; index < arrival; index++)
				if (!board.Apply(steps[index])) return null;
			var result = new List<Transport>(steps.Count);
			for (int index = 0; index < arrival; index++) result.Add(steps[index]);
			var selected = new HashSet<int>();
			var pending = new Stack<int>(dependencies[support]);
			while (pending.TryPop(out int index)) {
				if (index < arrival || !selected.Add(index)) continue;
				if (index <= end || index >= support || steps[index].Cargo.Intersect(steps[arrival].Cargo).Any()) return null;
				foreach (int dependency in dependencies[index]) pending.Push(dependency);
			}
			foreach (int index in selected.Order()) {
				if (!board.Apply(steps[index])) return null;
				result.Add(steps[index]);
			}
			// 足場が先に到着すれば、元の便はその上に着地して同じ個体を輸送できる。
			if (!board.Apply(steps[support]) || !board.Apply(steps[arrival])) return null;
			result.Add(steps[support]);
			result.Add(steps[arrival]);
			var first = steps[arrival + 1];
			if (board.Heights[first.From] < first.Cargo.Length + distance - 1
				|| !board.Tower(first.From)[^first.Cargo.Length..].SequenceEqual(first.Cargo)
				|| !board.Capture(first.From, first.Direction, distance, first.Cargo.Length, out var jump)) return null;
			if (!jump!.Returned.AsSpan().SequenceEqual(steps[end].Returned)) return null;
			result.Add(jump);
			int budget = end - arrival - 2;
			for (int index = arrival + 1; index < steps.Count; index++) {
				if ((index & 63) == 0 && !CanSearch) return null;
				if (index <= end || index == support || selected.Contains(index)) continue;
				if (!AppendWithRepair(ref board, steps[index], result, ref budget)) return null;
			}
			return result.Count < steps.Count && board.SameState(finalBoard) ? result : null;
		}

		public State OptimizeSameColorMerges(State baseline)
		{
			var board = new ReplayBoard(this);
			var best = new List<Transport>(baseline.Moves.Count);
			foreach (var (i, j, k, direction, length) in baseline.Moves) {
				int from = i * n + j;
				if (!board.Capture(from, direction, length, board.Heights[from] - k, out var step))
					throw new InvalidOperationException("Plan9: invalid Plan19 operation.");
				best.Add(step!);
			}
			var finalBoard = board;
			for (int round = 0; round < 24 && CanSearch; round++) {
				BuildIndex(best, out _, out _, out var checkpoints);
				bool improved = false;
				for (int index = 0; index + 1 < best.Count && CanSearch && !improved; index++) {
					var replacement = TryJoinSplitTransfer(best, index, checkpoints, finalBoard);
					if (replacement == null) continue;
					best = replacement;
					info.Up++;
					improved = true;
				}
				if (improved) continue;
				for (int second = 0; second < best.Count && CanSearch && !improved; second++) {
					info.Loop++;
					var replacement = TryHomewardSameColorMerge(best, second, checkpoints, finalBoard)
						?? TryRedirectSameColorMerge(best, second, checkpoints, finalBoard)
						?? TryShiftSameColorMerge(best, second, checkpoints, finalBoard);
					if (replacement == null) continue;
					best = replacement;
					info.Up++;
					improved = true;
				}
				if (improved) continue;
				for (int merge = 0; merge + 1 < best.Count && CanSearch && !improved; merge++) {
					var incoming = best[merge];
					if (incoming.Color < 0 || incoming.Returned.Length != 0) continue;
					var before = checkpoints[merge / CheckpointInterval].Copy();
					for (int index = merge / CheckpointInterval * CheckpointInterval; index < merge; index++) before.Apply(best[index]);
					var receiver = before.Tower(incoming.To);
					if (receiver.Length == 0 || receiver.Length + incoming.Cargo.Length > 8
						|| receiver.ToArray().Any(id => colors[id] != incoming.Color)) continue;
					int next = -1;
					for (int index = merge + 1; index < Math.Min(best.Count, merge + 17); index++) {
						if (best[index].From != incoming.To) continue;
						if (best[index].Color == incoming.Color && best[index].Cargo.Length == receiver.Length + incoming.Cargo.Length
							&& best[index].Returned.Length == 0) next = index;
						break;
					}
					if (next < 0) continue;
					info.Loop++;
					var detourReplacement = TryRemoveSameColorMergeDetour(best, merge, receiver.Length, checkpoints, finalBoard);
					if (detourReplacement != null) {
						best = detourReplacement;
						info.Up++;
						improved = true;
						break;
					}
					for (int cut = merge - 1; cut >= Math.Max(0, merge - 12) && CanSearch; cut--) {
						if (best[cut].To != incoming.From || !SameGroup(best[cut], incoming)
							|| best[cut].Returned.Length != 0) continue;
						info.Loop++;
						var replacement = TryMoveSameColorMerge(best, cut, merge, next, receiver.Length, checkpoints, finalBoard);
						if (replacement == null) replacement = TryWaitSameColorMerge(best, cut, merge, receiver.Length, checkpoints, finalBoard);
						if (replacement != null) {
							best = replacement;
							info.Up++;
							improved = true;
							break;
						}
					}
				}
				if (!improved) break;
			}
			if (best.Count >= baseline.Moves.Count) return baseline;
			var result = new State(isLocal, input);
			foreach (var step in best) {
				int i = step.From / n;
				int j = step.From % n;
				result.Move(i, j, result.Slimes[i, j].Count - step.Cargo.Length, step.Direction, step.Length);
			}
			if (result.SlimeCount != baseline.SlimeCount) throw new InvalidOperationException("Plan9: incomplete merge optimization.");
			return result;
		}

		private List<Transport>? TryJoinSplitTransfer(List<Transport> steps, int first,
			List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var a = steps[first];
			var b = steps[first + 1];
			if (a.Color < 0 || a.Color != b.Color || a.From != b.From || a.To != b.To
				|| a.Direction != b.Direction || a.Length != b.Length
				|| a.Returned.Length != 0 || b.Returned.Length != 0) return null;
			int count = a.Cargo.Length + b.Cargo.Length;
			var board = checkpoints[first / CheckpointInterval].Copy();
			for (int index = first / CheckpointInterval * CheckpointInterval; index < first; index++)
				if (!board.Apply(steps[index])) return null;
			if (board.Heights[a.From] < count || !board.Tower(a.From)[^count..].ToArray().All(id => colors[id] == a.Color)) return null;
			if (!board.Capture(a.From, a.Direction, a.Length, count, out var combined)
				|| combined!.Returned.Length != 0) return null;
			var result = new List<Transport>(steps.Count - 1);
			for (int index = 0; index < first; index++) result.Add(steps[index]);
			result.Add(combined);
			int budget = 0;
			for (int index = first + 2; index < steps.Count; index++) {
				if ((index & 63) == 0 && !CanSearch) return null;
				if (!AppendWithRepair(ref board, steps[index], result, ref budget)) return null;
			}
			return result.Count < steps.Count && board.SameColorState(finalBoard) ? result : null;
		}

		private List<Transport>? TryHomewardSameColorMerge(List<Transport> steps, int merge,
			List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var joining = steps[merge];
			if (joining.Color < 0 || joining.Returned.Length != 0) return null;
			var before = checkpoints[merge / CheckpointInterval].Copy();
			for (int index = merge / CheckpointInterval * CheckpointInterval; index < merge; index++)
				if (!before.Apply(steps[index])) return null;
			var receiving = before.Tower(joining.To).ToArray();
			int count = receiving.Length + joining.Cargo.Length;
			if (receiving.Length == 0 || count > 8 || receiving.Any(id => colors[id] != joining.Color)) return null;
			var group = receiving.Concat(joining.Cargo).Order().ToArray();
			var continuation = new List<int>();
			int position = joining.To;
			for (int index = merge + 1; index < Math.Min(steps.Count, merge + 40); index++) {
				var step = steps[index];
				if (step.From != position) continue;
				if (!step.SortedCargo.AsSpan().SequenceEqual(group)) break;
				continuation.Add(index);
				position = step.To;
				if (step.Returned.Length > 0) break;
			}
			if (continuation.Count < 3 || !steps[continuation[^1]].Returned.Order().SequenceEqual(group)) return null;
			var incoming = new List<int>();
			position = joining.To;
			for (int index = merge - 1; index >= Math.Max(0, merge - 28); index--) {
				var step = steps[index];
				if (step.To != position) continue;
				if (step.Color != joining.Color || step.Returned.Length != 0
					|| !receiving.All(id => step.Cargo.Contains(id))) break;
				incoming.Add(index);
				position = step.From;
				if (incoming.Count == 12) break;
			}
			// 最初から合流先にいる個体も、動いて来た受け側と同様に再配置する。
			if (incoming.Count == 0 && receiving.Any(id => origins[id] != joining.To)) return null;
			var joiningLegs = new List<int>();
			position = joining.From;
			for (int index = merge - 1; index >= Math.Max(0, merge - 28); index--) {
				var step = steps[index];
				if (step.To != position) continue;
				if (step.Color != joining.Color || step.Returned.Length != 0
					|| !joining.Cargo.All(id => step.Cargo.Contains(id))) break;
				joiningLegs.Add(index);
				position = step.From;
				if (joiningLegs.Count == 12) break;
			}
			var (denI, denJ) = input.Dens[joining.Color];
			int den = denI * n + denJ;
			var candidateCells = new List<(int cell, int estimate)>();
			for (int cell = 0; cell < n * n; cell++) {
				if (input.Map[cell / n, cell % n] == -2 || cell == den || cell == joining.To
					|| before.Heights[cell] + count > 8 || before.Tower(cell).Contains(receiving[0])) continue;
				int nestDistance = Math.Abs(cell / n - denI) + Math.Abs(cell % n - denJ);
				int originalDistance = Math.Abs(joining.To / n - denI) + Math.Abs(joining.To % n - denJ);
				if (nestDistance >= originalDistance) continue;
				int joiningDistance = Math.Abs(cell / n - joining.From / n) + Math.Abs(cell % n - joining.From % n);
				if (joiningDistance > 10) continue;
				candidateCells.Add((cell, nestDistance * 2 + joiningDistance));
			}
			var starts = (incoming.Count == 0 ? new[] { merge } : incoming.ToArray())
				.Concat(joiningLegs.Take(8)).Distinct().ToArray();
			foreach (int start in starts) {
				if (!CanSearch) break;
				int receiverSource = incoming.Where(index => index >= start).DefaultIfEmpty(-1).Min();
				int source = receiverSource < 0 ? joining.To : steps[receiverSource].From;
				int joiningSource = joiningLegs.Where(index => index >= start).DefaultIfEmpty(-1).Min();
				int joiningFrom = joiningSource < 0 ? joining.From : steps[joiningSource].From;
				var skipped = new HashSet<int>(continuation) { merge };
				foreach (int index in incoming) if (index >= start) skipped.Add(index);
				foreach (int index in joiningLegs) if (index >= start) skipped.Add(index);
				int budget = skipped.Count - 1;
				var boardAtStart = checkpoints[start / CheckpointInterval].Copy();
				for (int index = start / CheckpointInterval * CheckpointInterval; index < start; index++)
					if (!boardAtStart.Apply(steps[index])) return null;
				var prefix = new List<Transport>(steps.Count);
				for (int index = 0; index < start; index++) prefix.Add(steps[index]);
				int prefixBudget = budget;
				bool prefixValid = true;
				for (int index = start; index < merge; index++) {
					if (skipped.Contains(index)) continue;
					if (!AppendWithRepair(ref boardAtStart, steps[index], prefix, ref prefixBudget)) {
						prefixValid = false;
						break;
					}
				}
				if (!prefixValid) continue;
				var routes = new List<(int moves, ReplayBoard board, List<Transport> operations)>();
				foreach (var (meeting, _) in candidateCells
					.OrderBy(item => item.estimate + Math.Abs(item.cell / n - source / n)
						+ Math.Abs(item.cell % n - source % n)
						+ Math.Abs(item.cell / n - joiningFrom / n)
						+ Math.Abs(item.cell % n - joiningFrom % n)).Take(32)) {
					if (!CanSearch) return null;
					var trial = boardAtStart.Copy();
					var operations = new List<Transport>();
					if (!RouteSameColor(ref trial, source, meeting, receiving.Length, operations, false, joining.Color)
						|| !RouteSameColor(ref trial, joiningFrom, meeting, joining.Cargo.Length, operations, false, joining.Color)
						|| !RouteSameColor(ref trial, meeting, den, count, operations, true, joining.Color)
						|| operations.Count > prefixBudget) continue;
					routes.Add((operations.Count, trial, operations));
				}
				// 両便の集荷と合流後の帰巣を合わせた実際の手数で評価する。
				foreach (var route in routes.OrderBy(item => item.moves).Take(6)) {
					var board = route.board;
					var result = new List<Transport>(prefix);
					result.AddRange(route.operations);
					int remainingBudget = prefixBudget - route.moves;
					bool valid = true;
					for (int index = merge + 1; index < steps.Count; index++) {
						if ((index & 63) == 0 && !CanSearch) { valid = false; break; }
						if (skipped.Contains(index)) continue;
						if (!AppendWithRepair(ref board, steps[index], result, ref remainingBudget)) { valid = false; break; }
					}
					if (valid && result.Count < steps.Count && board.SameColorState(finalBoard)) return result;
				}
			}
			return null;
		}

		private List<Transport>? TryRedirectSameColorMerge(List<Transport> steps, int merge,
			List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var joining = steps[merge];
			if (joining.Color < 0 || joining.Returned.Length != 0) return null;
			var before = checkpoints[merge / CheckpointInterval].Copy();
			for (int index = merge / CheckpointInterval * CheckpointInterval; index < merge; index++)
				if (!before.Apply(steps[index])) return null;
			var receiving = before.Tower(joining.To).ToArray();
			if (receiving.Length == 0 || receiving.Length + joining.Cargo.Length > 8
				|| receiving.Any(id => colors[id] != joining.Color)) return null;
			int receiverStep = -1;
			for (int index = merge - 1; index >= Math.Max(0, merge - 24); index--) {
				var step = steps[index];
				if (step.To != joining.To || step.Cargo.Length != receiving.Length
					|| step.Returned.Length != 0 || !step.SortedCargo.AsSpan().SequenceEqual(receiving.Order().ToArray())) continue;
				receiverStep = index;
				break;
			}
			if (receiverStep < 0) return null;
			var group = joining.Cargo.Concat(receiving).Order().ToArray();
			var forward = new List<int>();
			int position = joining.To;
			for (int index = merge + 1; index < Math.Min(steps.Count, merge + 24); index++) {
				var step = steps[index];
				if (step.From != position) continue;
				if (step.Returned.Length != 0 || !step.SortedCargo.AsSpan().SequenceEqual(group)) break;
				forward.Add(index);
				position = step.To;
				if (forward.Count == 12) break;
			}
			if (forward.Count < 2) return null;
			int start = steps[receiverStep].From;
			int origin = joining.From;
			var (denI, denJ) = input.Dens[joining.Color];
			for (int anchorIndex = forward.Count - 1; anchorIndex >= 1 && CanSearch; anchorIndex--) {
				int anchor = steps[forward[anchorIndex]].To;
				var candidates = new List<(int cell, int estimate)>();
				for (int cell = 0; cell < n * n; cell++) {
					if (cell == joining.To || cell == start || cell == origin || cell == anchor
						|| input.Map[cell / n, cell % n] == -2 || before.Heights[cell] != 0) continue;
					int first = Math.Abs(cell / n - start / n) + Math.Abs(cell % n - start % n);
					int second = Math.Abs(cell / n - origin / n) + Math.Abs(cell % n - origin % n);
					int onward = Math.Abs(cell / n - anchor / n) + Math.Abs(cell % n - anchor % n);
					int toDen = Math.Abs(cell / n - denI) + Math.Abs(cell % n - denJ);
					int oldToDen = Math.Abs(joining.To / n - denI) + Math.Abs(joining.To % n - denJ);
					if (first > 8 || second > 8 || onward > 10 || toDen > oldToDen + 2) continue;
					candidates.Add((cell, first + second + onward + toDen));
				}
				foreach (var (meeting, _) in candidates.OrderBy(item => item.estimate).Take(32)) {
					if (!CanSearch) return null;
					var board = checkpoints[receiverStep / CheckpointInterval].Copy();
					for (int index = receiverStep / CheckpointInterval * CheckpointInterval; index < receiverStep; index++)
						if (!board.Apply(steps[index])) return null;
					var result = new List<Transport>(steps.Count);
					for (int index = 0; index < receiverStep; index++) result.Add(steps[index]);
					int budget = anchorIndex + 2;
					bool valid = true;
					for (int index = receiverStep; index < steps.Count; index++) {
						if ((index & 63) == 0 && !CanSearch) { valid = false; break; }
						if (index == receiverStep || (index > merge && forward.Take(anchorIndex + 1).Contains(index))) continue;
						if (index == merge) {
							int count = result.Count;
							if (!RouteSameColor(ref board, start, meeting, receiving.Length, result, false, joining.Color)
								|| !RouteSameColor(ref board, origin, meeting, joining.Cargo.Length, result, false, joining.Color)
								|| !RouteSameColor(ref board, meeting, anchor, group.Length, result, false, joining.Color)) {
								valid = false;
								break;
							}
							budget -= result.Count - count;
							if (budget < 0) { valid = false; break; }
							continue;
						}
						if (!AppendWithRepair(ref board, steps[index], result, ref budget)) { valid = false; break; }
					}
					if (valid && result.Count < steps.Count && board.SameColorState(finalBoard)) return result;
				}
			}
			return null;
		}

		private List<Transport>? TryShiftSameColorMerge(List<Transport> steps, int merge,
			List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var joining = steps[merge];
			if (joining.Color < 0 || joining.Returned.Length != 0) return null;
			var before = checkpoints[merge / CheckpointInterval].Copy();
			for (int index = merge / CheckpointInterval * CheckpointInterval; index < merge; index++)
				if (!before.Apply(steps[index])) return null;
			var receiving = before.Tower(joining.To).ToArray();
			if (receiving.Length == 0 || receiving.Length + joining.Cargo.Length > 8
				|| receiving.Any(id => colors[id] != joining.Color)) return null;
			// 受け側の個体が来た経路を遡り、元の合流後に再び通る地点を探す。
			var inbound = new List<int>();
			int position = joining.To;
			for (int index = merge - 1; index >= Math.Max(0, merge - 24); index--) {
				var step = steps[index];
				if (step.To != position) continue;
				if (step.Color != joining.Color || step.Returned.Length != 0
					|| !receiving.All(id => step.Cargo.Contains(id))) break;
				inbound.Add(index);
				position = step.From;
				if (inbound.Count >= 10) break;
			}
			if (inbound.Count == 0) return null;
			var outbound = new List<int>();
			position = joining.To;
			int groupCount = receiving.Length + joining.Cargo.Length;
			for (int index = merge + 1; index < Math.Min(steps.Count, merge + 24); index++) {
				var step = steps[index];
				if (step.From != position) continue;
				if (step.Color != joining.Color || step.Cargo.Length != groupCount || step.Returned.Length != 0) break;
				outbound.Add(index);
				position = step.To;
				if (outbound.Count >= 10) break;
			}
			if (outbound.Count == 0) return null;
			foreach (int start in inbound) {
				if (!CanSearch) break;
				int meeting = steps[start].From;
				if (outbound.All(index => steps[index].To != meeting)) continue;
				var removed = new HashSet<int> { merge };
				foreach (int index in inbound) if (index >= start) removed.Add(index);
				int end = outbound.First(index => steps[index].To == meeting);
				foreach (int index in outbound) {
					removed.Add(index);
					if (index == end) break;
				}
				var board = checkpoints[start / CheckpointInterval].Copy();
				for (int index = start / CheckpointInterval * CheckpointInterval; index < start; index++)
					if (!board.Apply(steps[index])) return null;
				var result = new List<Transport>(steps.Count);
				for (int index = 0; index < start; index++) result.Add(steps[index]);
				int budget = removed.Count - 2;
				bool valid = true;
				for (int index = start; index < steps.Count; index++) {
					if ((index & 63) == 0 && !CanSearch) { valid = false; break; }
					if (index == merge) {
						int beforeRoute = result.Count;
						if (board.Heights[meeting] < receiving.Length
							|| !SameColors(board.Tower(meeting)[^receiving.Length..], receiving)
							|| !RouteSameColor(ref board, joining.From, meeting, joining.Cargo.Length, result, false, joining.Color)) {
							valid = false;
							break;
						}
						budget -= result.Count - beforeRoute;
						if (budget < 0) { valid = false; break; }
						continue;
					}
					if (removed.Contains(index)) continue;
					if (!AppendWithRepair(ref board, steps[index], result, ref budget)) { valid = false; break; }
				}
				if (valid && result.Count < steps.Count && board.SameColorState(finalBoard)) return result;
			}
			return null;
		}

		private bool RouteSameColor(ref ReplayBoard board, int from, int to, int count,
			List<Transport> result, bool home, int color)
		{
			if (from == to || board.Heights[from] < count || board.Heights[to] + count > 8
				|| !board.Tower(from)[^count..].ToArray().All(id => colors[id] == color)) return false;
			var previous = Enumerable.Repeat(-1, n * n).ToArray();
			var incoming = new RouteHop[n * n];
			var queue = new Queue<int>();
			previous[from] = from;
			queue.Enqueue(from);
			while (queue.TryDequeue(out int cell) && previous[to] < 0 && CanSearch) {
				int foot = board.Heights[cell] - (cell == from ? count : 0);
				for (int d = 0; d < directions.Length; d++) {
					var direction = directions[d];
					for (int length = 1; length <= foot + 1; length++) {
						var (i, j) = direction.Move(cell / n, cell % n, length);
						if ((uint)i >= (uint)n || (uint)j >= (uint)n || input.Map[i, j] == -2) break;
						int next = i * n + j;
						if (previous[next] >= 0 || board.Heights[next] + count > 8
							|| (next != to && input.Map[i, j] == color)) continue;
						previous[next] = cell;
						incoming[next] = new RouteHop(cell, direction, length);
						queue.Enqueue(next);
						if (next == to) break;
					}
					if (previous[to] >= 0) break;
				}
			}
			if (previous[to] < 0) return false;
			var route = new List<RouteHop>();
			for (int cell = to; cell != from; cell = previous[cell]) route.Add(incoming[cell]);
			route.Reverse();
			var trial = board.Copy();
			var local = new List<Transport>();
			foreach (var hop in route) {
				if (!trial.Capture(hop.Cell, hop.Direction, hop.Length, count, out var moved)
					|| (home ? hop != route[^1] && moved!.Returned.Length != 0 : moved!.Returned.Length != 0)) return false;
				local.Add(moved!);
			}
			if (home && local[^1].Returned.Length != count) return false;
			board = trial;
			result.AddRange(local);
			return true;
		}

		private List<Transport>? TryRemoveSameColorMergeDetour(List<Transport> steps, int merge,
			int receiverCount, List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var incoming = steps[merge];
			int groupCount = incoming.Cargo.Length + receiverCount;
			for (int outbound = merge - 1; outbound >= Math.Max(0, merge - 12) && CanSearch; outbound--) {
				var departure = steps[outbound];
				if (departure.To != incoming.To || departure.Color != incoming.Color
					|| departure.Cargo.Length != receiverCount || departure.Returned.Length != 0) continue;
				int rendezvous = departure.From;
				var returnLegs = new List<int>();
				int position = incoming.To;
				for (int index = merge + 1; index < Math.Min(steps.Count, merge + 9); index++) {
					var step = steps[index];
					if (step.From != position) continue;
					if (step.Color != incoming.Color || step.Cargo.Length != groupCount || step.Returned.Length != 0) break;
					returnLegs.Add(index);
					position = step.To;
					if (position == rendezvous) break;
				}
				if (position != rendezvous || returnLegs.Count == 0) continue;
				var board = checkpoints[outbound / CheckpointInterval].Copy();
				for (int index = outbound / CheckpointInterval * CheckpointInterval; index < outbound; index++)
					if (!board.Apply(steps[index])) return null;
				var result = new List<Transport>(steps.Count);
				for (int index = 0; index < outbound; index++) result.Add(steps[index]);
				int budget = returnLegs.Count;
				int returnIndex = 0;
				bool valid = true;
				for (int index = outbound; index < steps.Count; index++) {
					if ((index & 63) == 0 && !CanSearch) { valid = false; break; }
					if (index == outbound || (returnIndex < returnLegs.Count && index == returnLegs[returnIndex])) {
						if (index != outbound) returnIndex++;
						continue;
					}
					if (index == merge) {
						if (board.Heights[incoming.From] < incoming.Cargo.Length
							|| !SameColors(board.Tower(incoming.From)[^incoming.Cargo.Length..], incoming.Cargo)
							|| board.Heights[rendezvous] + incoming.Cargo.Length > 8) { valid = false; break; }
						var target = new Transport(incoming.From, rendezvous, incoming.Direction, 1,
							board.Tower(incoming.From)[^incoming.Cargo.Length..].ToArray(), [], colors);
						if (!TryRepairRoute(ref board, target, result, ref budget)) { valid = false; break; }
						continue;
					}
					if (!AppendWithRepair(ref board, steps[index], result, ref budget)) { valid = false; break; }
				}
				if (valid && result.Count < steps.Count && board.SameColorState(finalBoard)) return result;
			}
			return null;
		}

		private List<Transport>? TryWaitSameColorMerge(List<Transport> steps, int cut, int merge,
			int receiverCount, List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var incoming = steps[merge];
			int rendezvous = steps[cut].From;
			int groupCount = incoming.Cargo.Length + receiverCount;
			var route = new List<int>();
			int position = incoming.To;
			for (int index = merge + 1; index < Math.Min(steps.Count, merge + 13); index++) {
				var step = steps[index];
				if (step.From != position) continue;
				if (step.Color != incoming.Color || step.Cargo.Length != groupCount || step.Returned.Length != 0) break;
				route.Add(index);
				position = step.To;
				if (position == rendezvous) break;
			}
			if (position != rendezvous || route.Count == 0) return null;
			var board = checkpoints[cut / CheckpointInterval].Copy();
			for (int index = cut / CheckpointInterval * CheckpointInterval; index < cut; index++) board.Apply(steps[index]);
			var result = new List<Transport>(steps.Count);
			for (int index = 0; index < cut; index++) result.Add(steps[index]);
			int budget = 1;
			int routeIndex = 0;
			for (int index = cut; index < steps.Count; index++) {
				if ((index & 63) == 0 && !CanSearch) return null;
				if (index == cut || index == merge) continue;
				if (routeIndex < route.Count && index == route[routeIndex]) {
					var leg = steps[index];
					if (!board.Capture(leg.From, leg.Direction, leg.Length, receiverCount, out var moved)
						|| moved!.Returned.Length != 0) return null;
					result.Add(moved);
					routeIndex++;
					continue;
				}
				if (!AppendWithRepair(ref board, steps[index], result, ref budget)) return null;
			}
			if (result.Count >= steps.Count || !board.SameColorState(finalBoard)) return null;
			return result;
		}

		private List<Transport>? TryMoveSameColorMerge(List<Transport> steps, int cut, int merge, int next,
			int receiverCount, List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var incoming = steps[merge];
			var successor = steps[next];
			var board = checkpoints[cut / CheckpointInterval].Copy();
			for (int index = cut / CheckpointInterval * CheckpointInterval; index < cut; index++) board.Apply(steps[index]);
			var result = new List<Transport>(steps.Count);
			for (int index = 0; index < cut; index++) result.Add(steps[index]);
			if (board.Heights[successor.To] + incoming.Cargo.Length + receiverCount > 8) return null;
			int budget = 1;
			for (int index = cut; index <= next; index++) {
				if (index == cut) {
					if (!TryStraight(board, steps[cut].From, successor.To, incoming.Cargo.Length, out var dir, out int len)
						|| !board.Capture(steps[cut].From, dir, len, incoming.Cargo.Length, out var moved)
						|| moved!.Returned.Length != 0) return null;
					result.Add(moved);
					continue;
				}
				if (index == merge) continue;
				if (index == next) {
					if (!TryStraight(board, successor.From, successor.To, receiverCount, out var dir, out int len)
						|| !board.Capture(successor.From, dir, len, receiverCount, out var moved)
						|| moved!.Returned.Length != 0) return null;
					result.Add(moved);
					continue;
				}
				if (!AppendWithRepair(ref board, steps[index], result, ref budget)) return null;
			}
			for (int index = next + 1; index < steps.Count; index++) {
				if ((index & 63) == 0 && !CanSearch) return null;
				if (!AppendWithRepair(ref board, steps[index], result, ref budget)) return null;
			}
			if (result.Count >= steps.Count || !board.SameColorState(finalBoard)) return null;
			return result;
		}

		private bool TryStraight(ReplayBoard board, int from, int to, int count, out Direction4 direction, out int length)
		{
			direction = Direction4.N;
			length = 0;
			if (board.Heights[from] < count || board.Heights[to] + count > 8) return false;
			foreach (var dir in directions) {
				for (int distance = 1; distance <= board.Heights[from] - count + 1; distance++) {
					var (i, j) = dir.Move(from / n, from % n, distance);
					if ((uint)i >= (uint)n || (uint)j >= (uint)n || input.Map[i, j] == -2) break;
					if (i * n + j != to) continue;
					direction = dir;
					length = distance;
					return true;
				}
			}
			return false;
		}

		public State Optimize(State baseline)
		{
			var board = new ReplayBoard(this);
			var best = new List<Transport>(baseline.Moves.Count);
			foreach (var (i, j, k, direction, length) in baseline.Moves) {
				int from = i * n + j;
				if (!board.Capture(from, direction, length, board.Heights[from] - k, out var step)) {
					throw new InvalidOperationException("Plan9: invalid Plan19 operation.");
				}
				best.Add(step!);
			}
			var finalBoard = board;
			for (int round = 0; round < 32 && CanSearch; round++) {
				var candidates = FindCandidates(best);
				if (candidates.Count == 0 || !CanSearch) break;
				BuildIndex(best, out var dependencies, out var supportDependencies, out var checkpoints);
				bool improved = false;
				int trials = 0;
				foreach (var candidate in candidates) {
					if (!CanSearch || trials >= MaxTrialsPerRound) break;
					for (int length = candidate.Length; length >= 2 && CanSearch && !improved; length--) {
						for (int mode = 0; mode < 2 && CanSearch && trials < MaxTrialsPerRound; mode++) {
							trials++;
							info.Loop++;
							var replacement = TryCombine(best, candidate with { Length = length },
								mode == 0 ? dependencies : supportDependencies, checkpoints, finalBoard);
							if (replacement == null) continue;
							best = replacement;
							info.Up++;
							improved = true;
							break;
						}
					}
					if (improved) break;
				}
				if (!improved) break;
			}
			// 従来の完全一致による短縮を確保した後、途中合流・飛距離の違い・後続修復を許す。
			for (int round = 0; round < 32 && CanSearch; round++) {
				var candidates = FindRideCandidates(best);
				if (candidates.Count == 0 || !CanSearch) break;
				BuildIndex(best, out var dependencies, out var supportDependencies, out var checkpoints);
				bool improved = false;
				int trials = 0;
				foreach (var candidate in candidates) {
					if (!CanSearch || trials >= MaxTrialsPerRound) break;
					foreach (int end in candidate.Ends) {
						if (!CanSearch || trials >= MaxTrialsPerRound || improved) break;
						for (int mode = 0; mode < 2 && CanSearch && trials < MaxTrialsPerRound; mode++) {
							trials++;
							info.Loop++;
							var replacement = TryRide(best, candidate, end,
								mode == 0 ? dependencies : supportDependencies, checkpoints, finalBoard);
							if (replacement == null) continue;
							best = replacement;
							info.Up++;
							improved = true;
							break;
						}
					}
					if (improved) break;
				}
				if (!improved) break;
			}
			if (best.Count == baseline.Moves.Count) return baseline;
			var result = new State(isLocal, input);
			foreach (var step in best) {
				int i = step.From / n;
				int j = step.From % n;
				result.Move(i, j, result.Slimes[i, j].Count - step.Cargo.Length, step.Direction, step.Length);
			}
			if (result.SlimeCount != baseline.SlimeCount || result.Moves.Count >= baseline.Moves.Count) {
				throw new InvalidOperationException("Plan9: inconsistent optimized result.");
			}
			return result;
		}

		public State OptimizeRebuiltRides(State baseline)
		{
			var board = new ReplayBoard(this);
			var best = new List<Transport>(baseline.Moves.Count);
			foreach (var (i, j, k, direction, length) in baseline.Moves) {
				if (!board.Capture(i * n + j, direction, length, board.Heights[i * n + j] - k, out var step))
					throw new InvalidOperationException("Plan11: invalid baseline operation.");
				best.Add(step!);
			}
			var finalBoard = board;
			var timer = Stopwatch.StartNew();
			for (int round = 0; round < 64 && WithinStage(timer, 1200); round++) {
				BuildIndex(best, out var dependencies, out var supportDependencies, out var checkpoints);
				var routes = new Dictionary<(int From, int To), List<int>>();
				var candidates = new List<(int First, int Second)>();
				for (int index = 0; index < best.Count; index++) {
					var step = best[index];
					if (step.Color < 0 || step.Returned.Length != 0 || step.Cargo.Length >= 8) continue;
					var key = (step.From, step.To);
					if (!routes.TryGetValue(key, out var earlier)) routes[key] = earlier = [];
					for (int j = earlier.Count - 1; j >= Math.Max(0, earlier.Count - 48); j--) {
						var first = best[earlier[j]];
						if (first.Color != step.Color && first.Cargo.Length + step.Cargo.Length <= 8)
							candidates.Add((earlier[j], index));
					}
					earlier.Add(index);
				}
				// 操作順の近い便を優先し、前倒しが必要な準備操作を抑える。
				candidates.Sort((a, b) => (a.Second - a.First).CompareTo(b.Second - b.First));
				bool improved = false;
				int trials = 0;
				foreach (var (first, second) in candidates) {
					if (!WithinStage(timer, 1200) || trials++ >= 500) break;
					info.Loop++;
					var replacement = TryRebuildRide(best, first, second, dependencies, checkpoints, finalBoard);
					if (replacement == null) continue;
					best = replacement;
					info.Up++;
					improved = true;
					break;
				}
				if (!improved) break;
			}
			for (int round = 0; round < 32 && WithinStage(timer, 1600); round++) {
				BuildIndex(best, out var dependencies, out _, out var checkpoints);
				var byRoute = new Dictionary<(int From, int To), List<int>>();
				for (int index = 0; index < best.Count; index++) {
					var step = best[index];
					if (step.Color < 0 || step.Returned.Length != 0) continue;
					if (!byRoute.TryGetValue((step.From, step.To), out var list))
						byRoute[(step.From, step.To)] = list = [];
					list.Add(index);
				}
				bool improved = false;
				int trials = 0;
				for (int first = 0; first < best.Count && !improved && WithinStage(timer, 1600); first++) {
					var a = best[first];
					if (a.Color < 0 || a.Returned.Length != 0 || a.Cargo.Length >= 8) continue;
					// 中継点を越える直行便を、集荷済みの別色と中継点で合流する経路に変更する。
					for (int distance = a.Length + 1; distance <= 7 && !improved; distance++) {
						var (i, j) = a.Direction.Move(a.From / n, a.From % n, distance);
						if ((uint)i >= (uint)n || (uint)j >= (uint)n || input.Map[i, j] == -2) break;
						int end = i * n + j;
						if (!byRoute.TryGetValue((a.From, end), out var direct)
							|| !byRoute.TryGetValue((a.To, end), out var continuation)) continue;
						foreach (int next in continuation) {
							if (next <= first || !SameGroup(a, best[next]) || best[next].Cargo.Length != a.Cargo.Length
								|| best[next].Direction != a.Direction) continue;
							foreach (int other in direct) {
								if (other <= first || other == next || best[other].Color == a.Color
									|| best[other].Cargo.Length + a.Cargo.Length > 8) continue;
								if (++trials > 400 || !WithinStage(timer, 1600)) break;
								info.Loop++;
								var replacement = TryRebuildDetour(best, first, next, other, dependencies, checkpoints, finalBoard);
								if (replacement == null) continue;
								best = replacement;
								info.Up++;
								improved = true;
								break;
							}
							if (trials > 400 || improved) break;
						}
					}
				}
				if (!improved) break;
			}
			best = OptimizeBranches(best, finalBoard, timer);
			best = OptimizeNewRelays(best, finalBoard, timer);
			best = OptimizeRelayNetworks(best, finalBoard, timer);
			if (best.Count == baseline.Moves.Count) return baseline;
			var result = new State(isLocal, input);
			foreach (var step in best) {
				int i = step.From / n;
				int j = step.From % n;
				result.Move(i, j, result.Slimes[i, j].Count - step.Cargo.Length, step.Direction, step.Length);
			}
			if (result.SlimeCount != baseline.SlimeCount || result.Moves.Count >= baseline.Moves.Count)
				throw new InvalidOperationException("Plan11: inconsistent rebuilt rides.");
			return result;
		}

		private List<Transport> OptimizeBranches(List<Transport> best, ReplayBoard finalBoard, Stopwatch timer)
		{
			for (int round = 0; round < 24 && WithinStage(timer, BranchStageMilliseconds); round++) {
				BuildIndex(best, out var dependencies, out _, out var checkpoints);
				var byRoute = new Dictionary<(int From, int To), List<int>>();
				var bySource = new Dictionary<int, List<int>>();
				for (int index = 0; index < best.Count; index++) {
					var step = best[index];
					if (step.Color < 0 || step.Returned.Length != 0) continue;
					if (!byRoute.TryGetValue((step.From, step.To), out var route))
						byRoute[(step.From, step.To)] = route = [];
					route.Add(index);
					if (!bySource.TryGetValue(step.From, out var source)) bySource[step.From] = source = [];
					source.Add(index);
				}
				bool improved = false;
				int trials = 0;
				for (int first = 0; first < best.Count && !improved && WithinStage(timer, BranchStageMilliseconds); first++) {
					var a = best[first];
					if (a.Color < 0 || a.Returned.Length != 0 || a.Cargo.Length >= 8
						|| !byRoute.TryGetValue((a.From, a.To), out var same)
						|| !bySource.TryGetValue(a.To, out var continuations)) continue;
					foreach (int second in same) {
						var b = best[second];
						if (second <= first || b.Color == a.Color || a.Cargo.Length + b.Cargo.Length > 8) continue;
						foreach (int left in continuations) {
							var x = best[left];
							if (left <= first || !SameGroup(a, x) || a.Cargo.Length != x.Cargo.Length) continue;
							foreach (int right in continuations) {
								var y = best[right];
								if (right <= second || right == left || !SameGroup(b, y)
									|| b.Cargo.Length != y.Cargo.Length || x.To == y.To) continue;
								if (++trials > 350 || !WithinStage(timer, BranchStageMilliseconds)) break;
								info.Loop++;
								var replacement = TryRebuildBranch(best, first, second, left, right,
									dependencies, checkpoints, finalBoard);
								if (replacement == null) continue;
								best = replacement;
								info.Up++;
								improved = true;
								break;
							}
							if (improved || trials > 350) break;
						}
						if (improved || trials > 350) break;
					}
				}
				if (!improved) break;
			}
			return best;
		}

		private List<Transport> OptimizeNewRelays(List<Transport> best, ReplayBoard finalBoard, Stopwatch timer)
		{
			for (int round = 0; round < 24 && WithinStage(timer, 2200); round++) {
				BuildIndex(best, out var dependencies, out _, out var checkpoints);
				var bySource = new Dictionary<int, List<int>>();
				var nextUse = Enumerable.Repeat(-1, colors.Length).ToArray();
				var next = Enumerable.Repeat(-1, best.Count).ToArray();
				for (int index = best.Count - 1; index >= 0; index--) {
					foreach (int id in best[index].Cargo) {
						if (nextUse[id] >= 0 && (next[index] < 0 || nextUse[id] < next[index])) next[index] = nextUse[id];
					}
					foreach (int id in best[index].Cargo) nextUse[id] = index;
					foreach (int id in best[index].Returned) nextUse[id] = index;
				}
				for (int index = 0; index < best.Count; index++) {
					var step = best[index];
					if (step.Color < 0 || step.Returned.Length != 0 || step.Cargo.Length >= 8) continue;
					if (!bySource.TryGetValue(step.From, out var list)) bySource[step.From] = list = [];
					list.Add(index);
				}
				bool improved = false;
				int trials = 0;
				for (int first = 0; first < best.Count && !improved && WithinStage(timer, 2200); first++) {
					var a = best[first];
					int left = next[first];
					if (a.Color < 0 || a.Returned.Length != 0 || left <= first || a.Cargo.Length >= 8
						|| !bySource.TryGetValue(a.From, out var same)) continue;
					var x = best[left];
					if (x.From != a.To || x.Color != a.Color || x.Returned.Length != 0 || !SameGroup(a, x)) continue;
					foreach (int second in same) {
						if (second <= first) continue;
						var b = best[second];
						int right = next[second];
						if (b.Color == a.Color || a.Cargo.Length + b.Cargo.Length > 8 || right <= second || right == left) continue;
						var y = best[right];
						if (y.From != b.To || y.Color != b.Color || y.Returned.Length != 0 || !SameGroup(b, y)) continue;
						for (int cell = 0; cell < n * n && !improved; cell++) {
							if (cell == a.From || cell == a.To || cell == b.To || cell == x.To || cell == y.To
								|| input.Map[cell / n, cell % n] == -2) continue;
							if ((cell / n != a.From / n && cell % n != a.From % n)
								|| (cell / n != x.To / n && cell % n != x.To % n)
								|| (cell / n != y.To / n && cell % n != y.To % n)) continue;
							if (++trials > 350 || !WithinStage(timer, 2200)) break;
							info.Loop++;
							var replacement = TryNewRelay(best, first, second, left, right, cell,
								dependencies, checkpoints, finalBoard);
							if (replacement == null) continue;
							best = replacement;
							info.Up++;
							improved = true;
						}
						if (improved || trials > 350) break;
					}
				}
				if (!improved) break;
			}
			return best;
		}

		private List<Transport> OptimizeRelayNetworks(List<Transport> best, ReplayBoard finalBoard, Stopwatch timer)
		{
			for (int round = 0; round < 24 && WithinStage(timer, 2600); round++) {
				BuildIndex(best, out var dependencies, out _, out var checkpoints);
				var bySource = new Dictionary<int, List<int>>();
				var nextUse = Enumerable.Repeat(-1, colors.Length).ToArray();
				var next = Enumerable.Repeat(-1, best.Count).ToArray();
				for (int index = best.Count - 1; index >= 0; index--) {
					foreach (int id in best[index].Cargo) {
						int use = nextUse[id];
						if (use >= 0 && (next[index] < 0 || use < next[index])) next[index] = use;
					}
					foreach (int id in best[index].Cargo) nextUse[id] = index;
					foreach (int id in best[index].Returned) nextUse[id] = index;
				}
				for (int index = 0; index < best.Count; index++) {
					var step = best[index];
					if (step.Color < 0 || step.Returned.Length != 0 || step.Cargo.Length >= 8) continue;
					if (!bySource.TryGetValue(step.From, out var list)) bySource[step.From] = list = [];
					list.Add(index);
				}
				bool improved = false;
				int trials = 0;
				for (int first = 0; first < best.Count && !improved && WithinStage(timer, 2600); first++) {
					var a = best[first];
					int a1 = next[first];
					if (a.Color < 0 || a.Returned.Length != 0 || a.Cargo.Length >= 8 || a1 < 0
						|| !bySource.TryGetValue(a.From, out var same)) continue;
					int a2 = next[a1];
					if (a2 < 0 || !IsContinuous(a, best[a1]) || !IsContinuous(best[a1], best[a2])) continue;
					foreach (int second in same) {
						if (second <= first) continue;
						var b = best[second];
						int b1 = next[second];
						if (b.Color == a.Color || a.Cargo.Length + b.Cargo.Length > 8 || b1 < 0) continue;
						int b2 = next[b1];
						if (b2 < 0 || !IsContinuous(b, best[b1]) || !IsContinuous(best[b1], best[b2])
							|| new[] { first, a1, a2, second, b1, b2 }.Distinct().Count() != 6) continue;
						int endA = best[a2].To;
						int endB = best[b2].To;
						for (int r = 0; r < 2 && !improved; r++) {
							int relay2 = r == 0 ? endA / n * n + endB % n : endB / n * n + endA % n;
							if (relay2 == a.From || relay2 == endA || relay2 == endB
								|| input.Map[relay2 / n, relay2 % n] == -2) continue;
							for (int s = 0; s < 2 && !improved; s++) {
								int relay1 = s == 0 ? a.From / n * n + relay2 % n : relay2 / n * n + a.From % n;
								if (relay1 == a.From || relay1 == relay2 || relay1 == endA || relay1 == endB
									|| input.Map[relay1 / n, relay1 % n] == -2) continue;
								if (++trials > 400 || !WithinStage(timer, 2600)) break;
								info.Loop++;
								var replacement = TryRelayNetwork(best, first, second, a1, a2, b1, b2,
									relay1, relay2, dependencies, checkpoints, finalBoard);
								if (replacement == null) continue;
								best = replacement;
								info.Up++;
								improved = true;
							}
							if (trials > 400 || improved) break;
						}
					}
				}
				if (!improved) break;
			}
			return best;

			static bool IsContinuous(Transport previous, Transport current)
				=> previous.To == current.From && previous.Cargo.Length == current.Cargo.Length
					&& previous.Color == current.Color && current.Returned.Length == 0
					&& SameGroup(previous, current);
		}

		private List<Transport>? TryRelayNetwork(List<Transport> steps, int first, int second,
			int a1, int a2, int b1, int b2, int relay1, int relay2, int[][] dependencies,
			List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var a = steps[first];
			var b = steps[second];
			var selected = new bool[steps.Count];
			var pending = new Stack<int>(dependencies[second]);
			var preparation = new List<int>();
			while (pending.TryPop(out int index)) {
				if (index < first || selected[index]) continue;
				if (index == first || index == a1 || index == a2 || index >= second) return null;
				if (steps[index].Cargo.Any(id => Array.IndexOf(a.Cargo, id) >= 0)
					|| steps[index].Returned.Any(id => Array.IndexOf(a.Cargo, id) >= 0)) return null;
				selected[index] = true;
				preparation.Add(index);
				foreach (int required in dependencies[index]) pending.Push(required);
			}
			preparation.Sort();
			var board = checkpoints[first / CheckpointInterval].Copy();
			for (int index = first / CheckpointInterval * CheckpointInterval; index < first; index++)
				if (!board.Apply(steps[index])) return null;
			var result = new List<Transport>(steps.Count - 2);
			for (int index = 0; index < first; index++) result.Add(steps[index]);
			foreach (int index in preparation) {
				if (!board.Apply(steps[index])) return null;
				result.Add(steps[index]);
			}
			int count = a.Cargo.Length + b.Cargo.Length;
			if (board.Heights[a.From] < count) return null;
			var actual = board.Tower(a.From)[^count..].ToArray();
			Array.Sort(actual);
			var expected = a.Cargo.Concat(b.Cargo).Order().ToArray();
			if (!actual.AsSpan().SequenceEqual(expected)) return null;
			foreach (int target in new[] { relay1, relay2 }) {
				int source = target == relay1 ? a.From : relay1;
				if (!TryStraight(board, source, target, count, out var direction, out int length)
					|| !board.Capture(source, direction, length, count, out var moved)
					|| moved!.Returned.Length != 0) return null;
				result.Add(moved);
			}
			var x = steps[a2];
			var y = steps[b2];
			var tower = board.Tower(relay2);
			var firstSplit = SameColors(tower[^x.Cargo.Length..], x.Cargo) ? x
					: SameColors(tower[^y.Cargo.Length..], y.Cargo) ? y : null;
			if (firstSplit == null) return null;
			var secondSplit = ReferenceEquals(firstSplit, x) ? y : x;
			foreach (var split in new[] { firstSplit, secondSplit }) {
				if (!TryStraight(board, relay2, split.To, split.Cargo.Length, out var direction, out int length)
					|| !board.Capture(relay2, direction, length, split.Cargo.Length, out var moved)) return null;
				result.Add(moved!);
			}
			for (int index = first + 1; index < steps.Count; index++) {
				if (selected[index] || index == second || index == a1 || index == a2 || index == b1 || index == b2) continue;
				if (board.Apply(steps[index])) result.Add(steps[index]);
				else if (board.ApplyCompatible(steps[index], out var compatible)) result.Add(compatible!);
				else return null;
			}
			return board.SameState(finalBoard) ? result : null;
		}

		private List<Transport>? TryNewRelay(List<Transport> steps, int first, int second, int left, int right,
			int relay, int[][] dependencies, List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var a = steps[first];
			var b = steps[second];
			var x = steps[left];
			var y = steps[right];
			var selected = new bool[steps.Count];
			var pending = new Stack<int>(dependencies[second]);
			var preparation = new List<int>();
			while (pending.TryPop(out int index)) {
				if (index < first || selected[index]) continue;
				if (index == first || index == left || index >= second) return null;
				if (steps[index].Cargo.Any(id => Array.IndexOf(a.Cargo, id) >= 0)
					|| steps[index].Returned.Any(id => Array.IndexOf(a.Cargo, id) >= 0)) return null;
				selected[index] = true;
				preparation.Add(index);
				foreach (int required in dependencies[index]) pending.Push(required);
			}
			preparation.Sort();
			var board = checkpoints[first / CheckpointInterval].Copy();
			for (int index = first / CheckpointInterval * CheckpointInterval; index < first; index++)
				if (!board.Apply(steps[index])) return null;
			var result = new List<Transport>(steps.Count - 1);
			for (int index = 0; index < first; index++) result.Add(steps[index]);
			foreach (int index in preparation) {
				if (!board.Apply(steps[index])) return null;
				result.Add(steps[index]);
			}
			int count = a.Cargo.Length + b.Cargo.Length;
			if (board.Heights[a.From] < count) return null;
			var actual = board.Tower(a.From)[^count..].ToArray();
			Array.Sort(actual);
			var expected = a.Cargo.Concat(b.Cargo).Order().ToArray();
			if (!actual.AsSpan().SequenceEqual(expected)
				|| !TryStraight(board, a.From, relay, count, out var direction, out int length)
				|| !board.Capture(a.From, direction, length, count, out var joint)
				|| joint!.Returned.Length != 0) return null;
			result.Add(joint);
			var tower = board.Tower(relay);
			var firstSplit = SameColors(tower[^x.Cargo.Length..], x.Cargo) ? x
				: SameColors(tower[^y.Cargo.Length..], y.Cargo) ? y : null;
			if (firstSplit == null) return null;
			var secondSplit = ReferenceEquals(firstSplit, x) ? y : x;
			foreach (var split in new[] { firstSplit, secondSplit }) {
				if (!TryStraight(board, relay, split.To, split.Cargo.Length, out direction, out length)
					|| !board.Capture(relay, direction, length, split.Cargo.Length, out var moved)) return null;
				result.Add(moved!);
			}
			for (int index = first + 1; index < steps.Count; index++) {
				if (selected[index] || index == second || index == left || index == right) continue;
				if (board.Apply(steps[index])) result.Add(steps[index]);
				else if (board.ApplyCompatible(steps[index], out var compatible)) result.Add(compatible!);
				else return null;
			}
			return board.SameState(finalBoard) ? result : null;
		}

		private List<Transport>? TryRebuildRide(List<Transport> steps, int first, int second,
			int[][] dependencies, List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var a = steps[first];
			var b = steps[second];
			var selected = new bool[steps.Count];
			var pending = new Stack<int>(dependencies[second]);
			var preparation = new List<int>();
			while (pending.TryPop(out int index)) {
				if (index < first || selected[index]) continue;
				if (index == first || index >= second) return null;
				if (steps[index].Cargo.Any(id => Array.IndexOf(a.Cargo, id) >= 0)
					|| steps[index].Returned.Any(id => Array.IndexOf(a.Cargo, id) >= 0)) return null;
				selected[index] = true;
				preparation.Add(index);
				foreach (int required in dependencies[index]) pending.Push(required);
			}
			preparation.Sort();
			var board = checkpoints[first / CheckpointInterval].Copy();
			for (int index = first / CheckpointInterval * CheckpointInterval; index < first; index++)
				if (!board.Apply(steps[index])) return null;
			var result = new List<Transport>(steps.Count - 1);
			for (int index = 0; index < first; index++) result.Add(steps[index]);
			foreach (int index in preparation) {
				if (!board.Apply(steps[index])) return null;
				result.Add(steps[index]);
			}
			int count = a.Cargo.Length + b.Cargo.Length;
			if (board.Heights[a.From] < count) return null;
			var actual = board.Tower(a.From)[^count..].ToArray();
			Array.Sort(actual);
			var expected = a.Cargo.Concat(b.Cargo).Order().ToArray();
			if (!actual.AsSpan().SequenceEqual(expected)) return null;
			if (!board.Capture(a.From, a.Direction, a.Length, count, out var joint)
				|| joint!.To != b.To || joint.Returned.Length != 0) return null;
			result.Add(joint);
			for (int index = first + 1; index < steps.Count; index++) {
				if (selected[index] || index == second) continue;
				if (board.Apply(steps[index])) result.Add(steps[index]);
				else if (board.ApplyCompatible(steps[index], out var compatible)) result.Add(compatible!);
				else return null;
			}
			if (!board.SameState(finalBoard)) return null;
			return result;
		}

		private List<Transport>? TryRebuildDetour(List<Transport> steps, int first, int next, int other,
			int[][] dependencies, List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var a = steps[first];
			var b = steps[other];
			var c = steps[next];
			var selected = new bool[steps.Count];
			var pending = new Stack<int>(dependencies[other]);
			var preparation = new List<int>();
			while (pending.TryPop(out int index)) {
				if (index < first || selected[index]) continue;
				if (index == first || index == next || index >= other) return null;
				if (steps[index].Cargo.Any(id => Array.IndexOf(a.Cargo, id) >= 0)
					|| steps[index].Returned.Any(id => Array.IndexOf(a.Cargo, id) >= 0)) return null;
				selected[index] = true;
				preparation.Add(index);
				foreach (int required in dependencies[index]) pending.Push(required);
			}
			preparation.Sort();
			var board = checkpoints[first / CheckpointInterval].Copy();
			for (int index = first / CheckpointInterval * CheckpointInterval; index < first; index++)
				if (!board.Apply(steps[index])) return null;
			var result = new List<Transport>(steps.Count - 1);
			for (int index = 0; index < first; index++) result.Add(steps[index]);
			foreach (int index in preparation) {
				if (!board.Apply(steps[index])) return null;
				result.Add(steps[index]);
			}
			int count = a.Cargo.Length + b.Cargo.Length;
			if (board.Heights[a.From] < count) return null;
			var actual = board.Tower(a.From)[^count..].ToArray();
			Array.Sort(actual);
			var expected = a.Cargo.Concat(b.Cargo).Order().ToArray();
			if (!actual.AsSpan().SequenceEqual(expected)) return null;
			if (!board.Capture(a.From, a.Direction, a.Length, count, out var joint)
				|| joint!.Returned.Length != 0) return null;
			result.Add(joint);
			if (!board.Capture(c.From, c.Direction, c.Length, count, out joint)) return null;
			result.Add(joint!);
			for (int index = first + 1; index < steps.Count; index++) {
				if (selected[index] || index == other || index == next) continue;
				if (board.Apply(steps[index])) result.Add(steps[index]);
				else if (board.ApplyCompatible(steps[index], out var compatible)) result.Add(compatible!);
				else return null;
			}
			return board.SameState(finalBoard) ? result : null;
		}

		private List<Transport>? TryRebuildBranch(List<Transport> steps, int first, int second, int left, int right,
			int[][] dependencies, List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var a = steps[first];
			var b = steps[second];
			var x = steps[left];
			var y = steps[right];
			var selected = new bool[steps.Count];
			var pending = new Stack<int>(dependencies[second]);
			var preparation = new List<int>();
			while (pending.TryPop(out int index)) {
				if (index < first || selected[index]) continue;
				if (index == first || index == left || index >= second) return null;
				if (steps[index].Cargo.Any(id => Array.IndexOf(a.Cargo, id) >= 0)
					|| steps[index].Returned.Any(id => Array.IndexOf(a.Cargo, id) >= 0)) return null;
				selected[index] = true;
				preparation.Add(index);
				foreach (int required in dependencies[index]) pending.Push(required);
			}
			preparation.Sort();
			var board = checkpoints[first / CheckpointInterval].Copy();
			for (int index = first / CheckpointInterval * CheckpointInterval; index < first; index++)
				if (!board.Apply(steps[index])) return null;
			var result = new List<Transport>(steps.Count - 1);
			for (int index = 0; index < first; index++) result.Add(steps[index]);
			foreach (int index in preparation) {
				if (!board.Apply(steps[index])) return null;
				result.Add(steps[index]);
			}
			int count = a.Cargo.Length + b.Cargo.Length;
			if (board.Heights[a.From] < count) return null;
			var actual = board.Tower(a.From)[^count..].ToArray();
			Array.Sort(actual);
			var expected = a.Cargo.Concat(b.Cargo).Order().ToArray();
			if (!actual.AsSpan().SequenceEqual(expected)) return null;
			if (!board.Capture(a.From, a.Direction, a.Length, count, out var joint)
				|| joint!.Returned.Length != 0) return null;
			result.Add(joint);
			var top = board.Tower(a.To);
			var firstSplit = SameColors(top[^x.Cargo.Length..], x.Cargo) ? x
				: SameColors(top[^y.Cargo.Length..], y.Cargo) ? y : null;
			if (firstSplit == null) return null;
			var secondSplit = ReferenceEquals(firstSplit, x) ? y : x;
			if (!board.Capture(firstSplit.From, firstSplit.Direction, firstSplit.Length,
				firstSplit.Cargo.Length, out var split)) return null;
			result.Add(split!);
			if (!board.Capture(secondSplit.From, secondSplit.Direction, secondSplit.Length,
				secondSplit.Cargo.Length, out split)) return null;
			result.Add(split!);
			for (int index = first + 1; index < steps.Count; index++) {
				if (selected[index] || index == second || index == left || index == right) continue;
				if (board.Apply(steps[index])) result.Add(steps[index]);
				else if (board.ApplyCompatible(steps[index], out var compatible)) result.Add(compatible!);
				else return null;
			}
			return board.SameState(finalBoard) ? result : null;
		}

		private List<Candidate> FindCandidates(List<Transport> steps)
		{
			var byEdge = new Dictionary<(int, int), List<int>>();
			var result = new List<Candidate>();
			for (int second = 0; second < steps.Count && CanSearch; second++) {
				var b = steps[second];
				if (b.Color < 0 || b.Cargo.Length >= 8 || b.Returned.Length != 0) continue;
				var key = (b.From, b.To);
				if (!byEdge.TryGetValue(key, out var earlier)) byEdge[key] = earlier = [];
				for (int index = earlier.Count - 1; index >= Math.Max(0, earlier.Count - 64); index--) {
					int first = earlier[index];
					var a = steps[first];
					if (a.Color == b.Color || a.Cargo.Length + b.Cargo.Length > 8) continue;
					int length = 0;
					while (length < 32 && first + length < second && second + length < steps.Count) {
						var x = steps[first + length];
						var y = steps[second + length];
						if (x.From != y.From || x.To != y.To || x.Returned.Length != 0 || y.Returned.Length != 0
							|| !SameGroup(a, x) || !SameGroup(b, y)) break;
						if (length > 0 && steps[first + length - 1].To != x.From) break;
						length++;
					}
					if (length >= 2) result.Add(new Candidate(first, second, length));
				}
				earlier.Add(second);
			}
			result.Sort((a, b) => a.Length != b.Length ? b.Length.CompareTo(a.Length)
				: a.First != b.First ? a.First.CompareTo(b.First) : a.Second.CompareTo(b.Second));
			return result;
		}

		private void BuildIndex(List<Transport> steps, out int[][] dependencies,
			out int[][] supportDependencies, out List<ReplayBoard> checkpoints)
		{
			dependencies = new int[steps.Count][];
			supportDependencies = new int[steps.Count][];
			checkpoints = [];
			var board = new ReplayBoard(this);
			var lastMove = Enumerable.Repeat(-1, colors.Length).ToArray();
			for (int index = 0; index < steps.Count; index++) {
				if (index % CheckpointInterval == 0) checkpoints.Add(board.Copy());
				var step = steps[index];
				var required = new HashSet<int>();
				foreach (int id in step.Cargo) if (lastMove[id] >= 0) required.Add(lastMove[id]);
				foreach (int id in step.Returned) if (lastMove[id] >= 0) required.Add(lastMove[id]);
				dependencies[index] = required.ToArray();
				// 足場を用意した操作も必要になる場合は、こちらの依存関係で再試行する。
				foreach (int id in board.Tower(step.From)) if (lastMove[id] >= 0) required.Add(lastMove[id]);
				foreach (int id in board.Tower(step.To)) if (lastMove[id] >= 0) required.Add(lastMove[id]);
				supportDependencies[index] = required.ToArray();
				if (!board.Apply(step)) throw new InvalidOperationException("Plan9: trace replay mismatch.");
				foreach (int id in step.Cargo) lastMove[id] = index;
				foreach (int id in step.Returned) lastMove[id] = index;
			}
		}

		private List<Transport>? TryCombine(List<Transport> steps, Candidate candidate,
			int[][] dependencies, List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			int first = candidate.First;
			int second = candidate.Second;
			int length = candidate.Length;
			var a = steps[first];
			var b = steps[second];
			var selected = new bool[steps.Count];
			var pending = new Stack<int>(dependencies[second]);
			var preparation = new List<int>();
			while (pending.TryPop(out int index)) {
				if (index < first || selected[index]) continue;
				if (index < first + length || index >= second) return null;
				// 先発便の個体を動かさないまま、後発便の集荷だけを前倒しする。
				foreach (int id in steps[index].Cargo) if (Array.IndexOf(a.Cargo, id) >= 0) return null;
				foreach (int id in steps[index].Returned) if (Array.IndexOf(a.Cargo, id) >= 0) return null;
				selected[index] = true;
				preparation.Add(index);
				foreach (int dependency in dependencies[index]) pending.Push(dependency);
			}
			preparation.Sort();
			var board = checkpoints[first / CheckpointInterval].Copy();
			for (int index = first / CheckpointInterval * CheckpointInterval; index < first; index++) {
				if (!board.Apply(steps[index])) return null;
			}
			var result = new List<Transport>(steps.Count - length);
			for (int index = 0; index < first; index++) result.Add(steps[index]);
			foreach (int index in preparation) {
				if (!board.Apply(steps[index])) return null;
				result.Add(steps[index]);
			}
			int count = a.Cargo.Length + b.Cargo.Length;
			if (board.Heights[a.From] < count) return null;
			var jointIds = board.Tower(a.From)[^count..].ToArray();
			Array.Sort(jointIds);
			var expected = a.Cargo.Concat(b.Cargo).Order().ToArray();
			if (!jointIds.AsSpan().SequenceEqual(expected)) return null;
			for (int offset = 0; offset < length; offset++) {
				var step = steps[first + offset];
				if (!board.Capture(step.From, step.Direction, step.Length, count, out var joint)
					|| joint!.Returned.Length != 0) return null;
				result.Add(joint);
			}
			// 混色を分離する後続操作も含めて全て検証する。元の集約を妨げる積み順・容量なら棄却する。
			for (int index = first + length; index < steps.Count; index++) {
				if ((index & 63) == 0 && !CanSearch) return null;
				if (selected[index] || (index >= second && index < second + length)) continue;
				if (!board.Apply(steps[index])) return null;
				result.Add(steps[index]);
			}
			if (result.Count >= steps.Count || !board.SameState(finalBoard)) return null;
			return result;
		}

		private List<RideCandidate> FindRideCandidates(List<Transport> steps)
		{
			var nextUse = Enumerable.Repeat(-1, colors.Length).ToArray();
			var nextMove = Enumerable.Repeat(-1, steps.Count).ToArray();
			for (int index = steps.Count - 1; index >= 0; index--) {
				foreach (int id in steps[index].Cargo) {
					int next = nextUse[id];
					if (next >= 0 && (nextMove[index] < 0 || next < nextMove[index])) nextMove[index] = next;
				}
				foreach (int id in steps[index].Cargo) nextUse[id] = index;
				foreach (int id in steps[index].Returned) nextUse[id] = index;
			}
			var cache = new Journey?[steps.Count];
			var bySource = new Dictionary<int, List<int>>();
			var result = new List<RideCandidate>();
			for (int second = 0; second < steps.Count && CanSearch; second++) {
				var b = steps[second];
				if (b.Color < 0 || b.Cargo.Length >= 8 || b.Returned.Length != 0) continue;
				if (!bySource.TryGetValue(b.From, out var earlier)) bySource[b.From] = earlier = [];
				var right = GetJourney(second);
				for (int index = earlier.Count - 1; index >= Math.Max(0, earlier.Count - 64) && CanSearch; index--) {
					int first = earlier[index];
					var a = steps[first];
					if (a.Color == b.Color || a.Cargo.Length + b.Cargo.Length > 8) continue;
					var left = GetJourney(first);
					int shared = 0;
					while (shared + 1 < left.Cells.Length && shared + 1 < right.Cells.Length
						&& left.Cells[shared + 1] == right.Cells[shared + 1]) shared++;
					if (shared < 2) continue;
					var ends = new List<int>();
					int saving = 0;
					for (int x = 0; x < left.Legs.Length; x++) {
						var leg = left.Legs[x];
						if (leg.End > shared || leg.Index >= second) break;
						int y = Array.FindIndex(right.Legs, item => item.End == leg.End);
						if (y < 0 || x + y + 2 < 3) continue;
						ends.Add(leg.End);
						saving = Math.Max(saving, Math.Min(x + 1, y + 1));
					}
					if (ends.Count == 0) continue;
					ends.Reverse();
					result.Add(new RideCandidate(left, right, ends.ToArray(), saving));
				}
				earlier.Add(second);
			}
			result.Sort((a, b) => a.Saving != b.Saving ? b.Saving.CompareTo(a.Saving)
				: a.First.Legs[0].Index != b.First.Legs[0].Index
					? a.First.Legs[0].Index.CompareTo(b.First.Legs[0].Index)
					: a.Second.Legs[0].Index.CompareTo(b.Second.Legs[0].Index));
			return result;

			Journey GetJourney(int start)
			{
				if (cache[start] != null) return cache[start]!;
				var cells = new List<int> { steps[start].From };
				var legs = new List<Leg>();
				int color = steps[start].Color;
				int previous = -1;
				for (int index = start; index >= 0; index = nextMove[index]) {
					var step = steps[index];
					if (step.Color != color || step.Returned.Length != 0 || step.From != cells[^1]
						|| cells.Count + step.Length > 33) break;
					if (previous >= 0 && steps[previous].Cargo.Any(id => Array.IndexOf(step.Cargo, id) < 0)) break;
					int offset = cells.Count - 1;
					for (int distance = 1; distance <= step.Length; distance++) {
						var (i, j) = step.Direction.Move(step.From / n, step.From % n, distance);
						cells.Add(i * n + j);
					}
					legs.Add(new Leg(index, offset, cells.Count - 1));
					previous = index;
				}
				return cache[start] = new Journey(color, cells.ToArray(), legs.ToArray());
			}
		}

		private List<Transport>? TryRide(List<Transport> steps, RideCandidate candidate, int end,
			int[][] dependencies, List<ReplayBoard> checkpoints, ReplayBoard finalBoard)
		{
			var left = candidate.First.Legs.Where(leg => leg.End <= end).ToArray();
			var right = candidate.Second.Legs.Where(leg => leg.End <= end).ToArray();
			int first = left[0].Index;
			var removed = new Dictionary<int, int>();
			foreach (var leg in left) removed[leg.Index] = leg.End;
			foreach (var leg in right) removed[leg.Index] = leg.End;
			int budget = removed.Count - 2;
			var selected = new bool[steps.Count];
			var board = checkpoints[first / CheckpointInterval].Copy();
			for (int index = first / CheckpointInterval * CheckpointInterval; index < first; index++) {
				if (!board.Apply(steps[index])) return null;
			}
			var result = new List<Transport>(steps.Count);
			for (int index = 0; index < first; index++) result.Add(steps[index]);
			int position = candidate.First.Cells[0];
			int[] active = board.Tower(position)[^steps[first].Cargo.Length..].ToArray();
			int offset = 0;
			int jointMoves = 0;
			int a = 0;
			int b = 0;
			while (offset < end && CanSearch) {
				while (left[a].End <= offset) a++;
				while (right[b].End <= offset) b++;
				if (!Prepare(left[a].Index) || !Prepare(right[b].Index)) return null;
				int countA = steps[left[a].Index].Cargo.Length;
				int countB = steps[right[b].Index].Cargo.Length;
				int count = countA + countB;
				if (count > 8 || board.Heights[position] < count) return null;
				var cargo = board.Tower(position)[^count..].ToArray();
				if (cargo.Count(id => colors[id] == candidate.First.Color) != countA
					|| cargo.Count(id => colors[id] == candidate.Second.Color) != countB) return null;
				int stop = end;
				// 次の同色合流地点は飛び越さず、そこで人数と準備操作を更新する。
				foreach (var leg in left) if (leg.Start > offset && steps[leg.Index].Cargo.Length > countA) stop = Math.Min(stop, leg.Start);
				foreach (var leg in right) if (leg.Start > offset && steps[leg.Index].Cargo.Length > countB) stop = Math.Min(stop, leg.Start);
				int next = candidate.First.Cells[offset + 1];
				Direction4 direction = directions.First(d => d.Move(position / n, position % n) == (next / n, next % n));
				int maxLength = Math.Min(stop - offset, board.Heights[position] - count + 1);
				for (int length = 2; length <= maxLength; length++) {
					var (i, j) = direction.Move(position / n, position % n, length);
					if (i * n + j == candidate.First.Cells[offset + length]) continue;
					maxLength = length - 1;
					break;
				}
				bool moved = false;
				for (int length = maxLength; length >= 1; length--) {
					int to = candidate.First.Cells[offset + length];
					var joint = new Transport(position, to, direction, length, cargo, [], colors);
					if (!board.Apply(joint)) continue;
					result.Add(joint);
					active = cargo;
					position = to;
					offset += length;
					jointMoves++;
					if (jointMoves > 1 && --budget < 0) return null;
					moved = true;
					break;
				}
				if (!moved) return null;
			}
			if (offset != end) return null;
			for (int index = first; index < steps.Count; index++) {
				if ((index & 63) == 0 && !CanSearch) return null;
				if (selected[index] || removed.ContainsKey(index)) continue;
				if (!AppendWithRepair(ref board, steps[index], result, ref budget)) return null;
			}
			if (result.Count >= steps.Count || !board.SameState(finalBoard)) return null;
			return result;

			bool Prepare(int target)
			{
				var pending = new Stack<int>(dependencies[target]);
				var preparation = new HashSet<int>();
				while (pending.TryPop(out int index)) {
					if (index < first || selected[index] || preparation.Contains(index)) continue;
					if (removed.TryGetValue(index, out int completedAt)) {
						if (completedAt > offset) return false;
						continue;
					}
					preparation.Add(index);
					foreach (int dependency in dependencies[index]) pending.Push(dependency);
				}
				foreach (int index in preparation.Order()) {
					var step = steps[index];
					if (step.From == position && board.Tower(position).ToArray().TakeLast(step.Cargo.Length).Any(id => Array.IndexOf(active, id) >= 0)) return false;
					if (!AppendWithRepair(ref board, step, result, ref budget)) return false;
					if (active.Any(id => !board.Tower(position).Contains(id))) return false;
					selected[index] = true;
				}
				return true;
			}
		}

		private bool AppendWithRepair(ref ReplayBoard board, Transport step, List<Transport> result, ref int budget, bool uncover = true)
		{
			if (board.Apply(step)) {
				result.Add(step);
				return true;
			}
			// 同色内の個体順だけが反転した場合は、実際のIDを記録し直して後続へ引き継ぐ。
			if (board.ApplyCompatible(step, out var compatible)) {
				result.Add(compatible!);
				return true;
			}
			if (budget <= 0 || !CanSearch) return false;
			if (TryRepairRoute(ref board, step, result, ref budget)) return true;
			if (!uncover || budget < 2) return false;
			var source = board.Tower(step.From).ToArray();
			for (int start = source.Length - step.Cargo.Length - 1; start >= 0; start--) {
				if (!SameColors(source.AsSpan(start, step.Cargo.Length), step.Cargo)) continue;
				int blockers = source.Length - start - step.Cargo.Length;
				foreach (Direction4 direction in directions) {
					var (i, j) = direction.Move(step.From / n, step.From % n);
					if ((uint)i >= (uint)n || (uint)j >= (uint)n || input.Map[i, j] == -2) continue;
					int temporary = i * n + j;
					if (temporary == step.To || board.Heights[temporary] + blockers > 8) continue;
					var trial = board.Copy();
					var local = new List<Transport>();
					int remainingBudget = budget - 2;
					var peel = new Transport(step.From, temporary, direction, 1, source[^blockers..], [], colors);
					if (!trial.Apply(peel)) continue;
					local.Add(peel);
					if (!AppendWithRepair(ref trial, step, local, ref remainingBudget, uncover: false)) continue;
					var back = directions.First(d => d.Move(i, j) == (step.From / n, step.From % n));
					var restore = new Transport(temporary, step.From, back, 1, trial.Tower(temporary)[^blockers..].ToArray(), [], colors);
					if (!trial.Apply(restore)) continue;
					local.Add(restore);
					board = trial;
					budget = remainingBudget;
					result.AddRange(local);
					return true;
				}
			}
			return false;
		}

		private bool SameColors(ReadOnlySpan<int> a, ReadOnlySpan<int> b)
		{
			if (a.Length != b.Length) return false;
			for (int index = 0; index < a.Length; index++) if (colors[a[index]] != colors[b[index]]) return false;
			return true;
		}

		private bool TryRepairRoute(ref ReplayBoard board, Transport step, List<Transport> result, ref int budget)
		{
			int count = step.Cargo.Length;
			if (board.Heights[step.From] < count || !SameColors(board.Tower(step.From)[^count..], step.Cargo)) return false;
			int[] cargo = board.Tower(step.From)[^count..].ToArray();
			bool symmetric = SameColors(cargo, cargo.Reverse().ToArray());
			int size = n * n * 2;
			var distances = Enumerable.Repeat(-1, size).ToArray();
			var parents = Enumerable.Repeat(-1, size).ToArray();
			var incoming = new RouteHop[size];
			var queue = new Queue<int>();
			int root = step.From * 2;
			distances[root] = 0;
			queue.Enqueue(root);
			int goal = -1;
			int maxMoves = Math.Min(8, budget + 1);
			while (queue.TryDequeue(out int index) && goal < 0 && CanSearch) {
				if (distances[index] >= maxMoves) continue;
				int cell = index / 2;
				int parity = 1 - index % 2;
				int topColor = colors[parity == 0 ? cargo[^1] : cargo[0]];
				int foot = board.Heights[cell] - (cell == step.From ? count : 0);
				foreach (Direction4 direction in directions) {
					for (int length = 1; length <= foot + 1; length++) {
						var (i, j) = direction.Move(cell / n, cell % n, length);
						if ((uint)i >= (uint)n || (uint)j >= (uint)n || input.Map[i, j] == -2) break;
						int to = i * n + j;
						if (to == step.From || board.Heights[to] + count > 8) continue;
						bool finished = to == step.To && (parity == 1 || symmetric);
						if (!finished && input.Map[i, j] == topColor) continue;
						int next = to * 2 + parity;
						if (distances[next] >= 0) continue;
						distances[next] = distances[index] + 1;
						parents[next] = index;
						incoming[next] = new RouteHop(cell, direction, length);
						if (finished) { goal = next; break; }
						queue.Enqueue(next);
					}
					if (goal >= 0) break;
				}
			}
			if (goal < 0) return false;
			var route = new List<RouteHop>();
			for (int index = goal; index != root; index = parents[index]) route.Add(incoming[index]);
			route.Reverse();
			var trial = board.Copy();
			var local = new List<Transport>();
			var homed = new List<int>();
			foreach (var hop in route) {
				if (!trial.Capture(hop.Cell, hop.Direction, hop.Length, count, out var moved)) return false;
				local.Add(moved!);
				homed.AddRange(moved!.Returned);
			}
			if (!SameColors(homed.ToArray(), step.Returned)) return false;
			budget -= local.Count - 1;
			board = trial;
			result.AddRange(local);
			return true;
		}

		private sealed record BeamNode(ReplayBoard Board, BeamNode? Parent, Transport? Move, double Score, WholeEvaluation? Evaluation = null, WholeStateKey Key = default);

		private readonly record struct WholeStateKey(ulong First, ulong Second)
		{
			public static WholeStateKey operator ^(WholeStateKey a, WholeStateKey b) => new(a.First ^ b.First, a.Second ^ b.Second);
		}

		private static ulong MixWholeStateKey(ulong value)
		{
			unchecked {
				value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
				value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
				return value ^ (value >> 31);
			}
		}

		private WholeStateKey WholeCellKey(ReplayBoard board, int cell, WholeStateKey[] tokens)
		{
			var key = default(WholeStateKey);
			var tower = board.Tower(cell);
			for (int level = 0; level < tower.Length; level++) key ^= tokens[(cell * 8 + level) * input.K + colors[tower[level]]];
			return key;
		}

		private sealed record WholeEvaluation(int Distance, int Overlap, int SameColor, int RoutePotential,
			int[] RemainingByColor, int[] ReturnCostByColor, int EstimatedReturnCost);

		private int EstimateColorReturnCost(ReplayBoard board, int color, int[][] distances, int[][] floor)
		{
			Span<int> cells = stackalloc int[n * n];
			Span<int> counts = stackalloc int[n * n];
			int occupied = 0, total = 0, individualCost = 0;
			for (int cell = 0; cell < n * n; cell++) {
				int count = 0;
				foreach (int id in board.Tower(cell)) if (colors[id] == color) count++;
				if (count == 0) continue;
				cells[occupied] = cell;
				counts[occupied++] = count;
				total += count;
				individualCost += count * distances[color][cell];
			}
			if (occupied <= 1) return individualCost;

			int mergedCost = int.MaxValue;
			for (int representative = 0; representative < occupied; representative++) {
				// 既存の塔を維持したまま集約する場合の高さ上限を超える候補は使わない。
				if (board.Heights[cells[representative]] + total - counts[representative] > 8) continue;
				int gatherCost = 0;
				for (int source = 0; source < occupied; source++) {
					if (source != representative) gatherCost += floor[cells[source]][cells[representative]];
				}
				int cost = gatherCost + total * distances[color][cells[representative]];
				mergedCost = Math.Min(mergedCost, cost);
			}
			return Math.Min(individualCost, mergedCost);
		}

		private readonly record struct RoutePad(int Color, int Load, int Landing, int Saving);

		private List<RoutePad>[] BuildRoutePads(State baseline)
		{
			var replay = new ReplayBoard(this);
			var steps = new List<Transport>();
			foreach (var (i, j, keep, dir, len) in baseline.Moves) {
				if (!replay.Capture(i * n + j, dir, len, replay.Heights[i * n + j] - keep, out var step))
					throw new InvalidOperationException("Plan20: invalid route prediction.");
				steps.Add(step!);
			}
			var pads = new List<RoutePad>[n * n * 9];
			// 帰巣する集団の最後の連続輸送を遡り、同じ貨物の帰巣ルートだけを記録する。
			for (int home = 0; home < steps.Count; home++) {
				if (stopwatch.ElapsedMilliseconds >= BeamDeadlineMilliseconds) return pads;
				var last = steps[home];
				if (last.Color < 0 || !last.Returned.Order().SequenceEqual(last.SortedCargo)) continue;
				int first = home;
				while (first > 0 && steps[first - 1].To == steps[first].From
					&& steps[first - 1].Returned.Length == 0 && SameGroup(steps[first - 1], last)) first--;
				for (int start = first; start < home; start++) {
					int distance = 0;
					for (int end = start; end <= home; end++) {
						if (steps[end].Direction != steps[start].Direction) break;
						distance += steps[end].Length;
						if (distance > 8) break;
						int saving = end - start;
						if (saving <= 0) continue;
						for (int height = distance - 1; height + last.Cargo.Length <= 8; height++) {
							int key = steps[start].From * 9 + height;
							var entry = new RoutePad(last.Color, last.Cargo.Length, steps[end].To, saving);
							var list = pads[key] ??= [];
							if (!list.Contains(entry)) list.Add(entry);
						}
					}
				}
			}
			return pads;
		}

		public State SearchWholeBoard(State baseline)
		{
			var distances = new int[input.K][];
			for (int color = 0; color < input.K; color++) {
				if (stopwatch.ElapsedMilliseconds >= BeamDeadlineMilliseconds) return baseline;
				var distance = Enumerable.Repeat(n * n, n * n).ToArray();
				int den = input.Dens[color].i * n + input.Dens[color].j;
				var queue = new Queue<int>();
				distance[den] = 0;
				queue.Enqueue(den);
				while (queue.TryDequeue(out int cell)) {
					foreach (var (i, j) in Helper.Adjacence4(cell / n, cell % n, n, n)) {
						int next = i * n + j;
						if (input.Map[i, j] == -2 || distance[next] <= distance[cell] + 1) continue;
						distance[next] = distance[cell] + 1;
						queue.Enqueue(next);
					}
				}
				distances[color] = distance;
			}
			// 壁を考慮した個体間距離は探索前に計算し、候補評価では表を参照する。
			var floor = new int[n * n][];
			for (int source = 0; source < n * n; source++) {
				if (stopwatch.ElapsedMilliseconds >= BeamDeadlineMilliseconds) return baseline;
				var distance = Enumerable.Repeat(n * n, n * n).ToArray();
				floor[source] = distance;
				if (input.Map[source / n, source % n] == -2) continue;
				var queue = new Queue<int>();
				distance[source] = 0;
				queue.Enqueue(source);
				while (queue.TryDequeue(out int cell)) {
					foreach (var (i, j) in Helper.Adjacence4(cell / n, cell % n, n, n)) {
						int next = i * n + j;
						if (input.Map[i, j] == -2 || distance[next] <= distance[cell] + 1) continue;
						distance[next] = distance[cell] + 1;
						queue.Enqueue(next);
					}
				}
			}
			var pads = BuildRoutePads(baseline);
			var landingDependencies = Enumerable.Range(0, n * n).Select(_ => new HashSet<int>()).ToArray();
			var colorDependencies = Enumerable.Range(0, input.K).Select(_ => new HashSet<int>()).ToArray();
			for (int key = 0; key < pads.Length; key++) {
				if (pads[key] == null) continue;
				foreach (var pad in pads[key]) {
					landingDependencies[pad.Landing].Add(key / 9);
					colorDependencies[pad.Color].Add(key / 9);
				}
			}
			var landingCells = landingDependencies.Select(set => set.ToArray()).ToArray();
			var colorCells = colorDependencies.Select(set => set.ToArray()).ToArray();
			var start = new ReplayBoard(this);
			var initialEvaluation = CreateWholeEvaluation(start, distances, pads, floor);
			// 位置・段・色に固定の乱数を割り当て、同色個体IDには依存しないキーを作る。
			var keyTokens = new WholeStateKey[n * n * 8 * input.K];
			for (int index = 0; index < keyTokens.Length; index++) {
				ulong value = (ulong)index + 1;
				keyTokens[index] = new(MixWholeStateKey(value + 0x9E3779B97F4A7C15UL), MixWholeStateKey(value + 0xD1B54A32D192ED03UL));
			}
			var initialKey = default(WholeStateKey);
			for (int cell = 0; cell < n * n; cell++) initialKey ^= WholeCellKey(start, cell, keyTokens);
			var beam = new List<BeamNode> { new(start, null, null, ScoreWholeEvaluation(initialEvaluation, start.Remaining, 0), initialEvaluation, initialKey) };
			var seen = new HashSet<WholeStateKey> { initialKey };
			State best = baseline;
			int reachedDepth = 0, finished = 0;
			long expanded = 0;
			// 途中解は出力しない。元の完成解より短い深さだけを探索する。
			for (int depth = 1; depth < best.Moves.Count && stopwatch.ElapsedMilliseconds < BeamDeadlineMilliseconds; depth++) {
				double progress = (double)depth / baseline.Moves.Count;
				var candidates = new PriorityQueue<BeamNode, double>();
				bool timedOut = false;
				foreach (var node in beam) {
					for (int source = 0; source < n * n; source++) {
						var tower = node.Board.Tower(source);
						var sourceKey = WholeCellKey(node.Board, source, keyTokens);
						for (int count = 1; count <= tower.Length; count++) {
							foreach (var dir in directions) {
								for (int len = 1; len <= tower.Length - count + 1; len++) {
									if (stopwatch.ElapsedMilliseconds >= BeamDeadlineMilliseconds) { timedOut = true; break; }
									var (i, j) = dir.Move(source / n, source % n, len);
									if ((uint)i >= (uint)n || (uint)j >= (uint)n || input.Map[i, j] == -2) break;
									if (node.Board.Heights[i * n + j] + count > 8) continue;
									var next = node.Board.Copy();
									if (!next.Capture(source, dir, len, count, out var move)) continue;
									expanded++;
									if (next.Remaining == 0) {
										var route = new List<Transport> { move! };
										for (var parent = node; parent.Move != null; parent = parent.Parent!) route.Add(parent.Move);
										route.Reverse();
										best = new State(isLocal, input);
										foreach (var step in route)
											best.Move(step.From / n, step.From % n, best.Slimes[step.From / n, step.From % n].Count - step.Cargo.Length, step.Direction, step.Length);
										finished++;
										break;
									}
									// 同色内の個体IDは区別せず、色の積み順が同じ盤面を統合する。
									var key = node.Key ^ sourceKey ^ WholeCellKey(node.Board, move!.To, keyTokens)
										^ WholeCellKey(next, source, keyTokens) ^ WholeCellKey(next, move.To, keyTokens);
									if (!seen.Add(key)) continue;
									var evaluation = UpdateWholeEvaluation(node.Board, next, node.Evaluation!, move!, distances, floor, pads, landingCells, colorCells);
									double score = ScoreWholeEvaluation(evaluation, next.Remaining, progress);
									candidates.Enqueue(new BeamNode(next, node, move, score, evaluation, key), -score);
									if (candidates.Count > WholeBeamWidth) candidates.Dequeue();
								}
								if (timedOut || finished > 0) break;
							}
							if (timedOut || finished > 0) break;
						}
						if (timedOut || finished > 0) break;
					}
					if (timedOut || finished > 0) break;
				}
				reachedDepth = depth;
				if (finished > 0 || timedOut) break;
				beam = candidates.UnorderedItems.Select(item => item.Element).OrderBy(node => node.Score).ToList();
				if (beam.Count == 0) break;
			}
			if (isLocal) Console.Error.WriteLine($"WHOLE20 depth={reachedDepth} expanded={expanded} finished={finished} baseline={baseline.Moves.Count} best={best.Moves.Count}");
			if (best.SlimeCount != 0) throw new InvalidOperationException("Plan20: incomplete whole beam result.");
			return best;
		}

		private (int Distance, int Overlap, int SameColor) WholeCellEvaluation(ReplayBoard board, int cell, int[][] distances)
		{
			var tower = board.Tower(cell);
			int distance = 0, sameColor = 0;
			for (int index = 0; index < tower.Length; index++) {
				int color = colors[tower[index]];
				distance += distances[color][cell];
				if (index > 0 && colors[tower[index - 1]] == color) sameColor++;
			}
			return (distance, Math.Max(0, tower.Length - 1), sameColor);
		}

		private int WholeCellRoutePotential(ReplayBoard board, int cell, int[] remainingByColor, List<RoutePad>[] pads)
		{
			int height = board.Heights[cell];
			if (height == 0 || pads[cell * 9 + height] == null) return 0;
			int bestSaving = 0;
			var tower = board.Tower(cell);
			foreach (var pad in pads[cell * 9 + height]) {
				if (remainingByColor[pad.Color] < pad.Load || input.Map[cell / n, cell % n] == pad.Color
					|| board.Heights[pad.Landing] + pad.Load > 8) continue;
				int localCount = 0;
				foreach (int id in tower) if (colors[id] == pad.Color) localCount++;
				if (remainingByColor[pad.Color] - localCount >= pad.Load) bestSaving = Math.Max(bestSaving, pad.Saving);
			}
			return bestSaving;
		}

		private WholeEvaluation CreateWholeEvaluation(ReplayBoard board, int[][] distances, List<RoutePad>[] pads, int[][] floor)
		{
			int distance = 0, overlap = 0, sameColor = 0, routePotential = 0;
			var remainingByColor = new int[input.K];
			var returnCostByColor = new int[input.K];
			for (int cell = 0; cell < n * n; cell++) {
				var value = WholeCellEvaluation(board, cell, distances);
				distance += value.Distance;
				overlap += value.Overlap;
				sameColor += value.SameColor;
				foreach (int id in board.Tower(cell)) remainingByColor[colors[id]]++;
			}
			for (int cell = 0; cell < n * n; cell++) routePotential += WholeCellRoutePotential(board, cell, remainingByColor, pads);
			int estimatedReturnCost = 0;
			for (int color = 0; color < input.K; color++)
				estimatedReturnCost += returnCostByColor[color] = EstimateColorReturnCost(board, color, distances, floor);
			return new(distance, overlap, sameColor, routePotential, remainingByColor, returnCostByColor, estimatedReturnCost);
		}

		private WholeEvaluation UpdateWholeEvaluation(ReplayBoard before, ReplayBoard after, WholeEvaluation previous,
			Transport move, int[][] distances, int[][] floor, List<RoutePad>[] pads, int[][] landingCells, int[][] colorCells)
		{
			int distance = previous.Distance, overlap = previous.Overlap, sameColor = previous.SameColor;
			var remainingByColor = previous.RemainingByColor;
			Span<bool> marked = stackalloc bool[n * n];
			marked.Clear();
			Span<int> affected = stackalloc int[n * n];
			int affectedCount = 0;
			// 移動セルだけでなく、着地容量と帰巣済み色数に依存する足場も更新する。
			for (int endpoint = 0; endpoint < 2; endpoint++) {
				int cell = endpoint == 0 ? move.From : move.To;
				var oldValue = WholeCellEvaluation(before, cell, distances);
				var newValue = WholeCellEvaluation(after, cell, distances);
				distance += newValue.Distance - oldValue.Distance;
				overlap += newValue.Overlap - oldValue.Overlap;
				sameColor += newValue.SameColor - oldValue.SameColor;
				if (!marked[cell]) { marked[cell] = true; affected[affectedCount++] = cell; }
				foreach (int dependent in landingCells[cell]) {
					if (!marked[dependent]) { marked[dependent] = true; affected[affectedCount++] = dependent; }
				}
			}
			if (move.Returned.Length > 0) {
				remainingByColor = (int[])remainingByColor.Clone();
				foreach (int id in move.Returned) {
					int color = colors[id];
					remainingByColor[color]--;
					foreach (int dependent in colorCells[color]) {
						if (!marked[dependent]) { marked[dependent] = true; affected[affectedCount++] = dependent; }
					}
				}
			}
			int routePotential = previous.RoutePotential;
			for (int index = 0; index < affectedCount; index++) {
				int cell = affected[index];
				routePotential += WholeCellRoutePotential(after, cell, remainingByColor, pads)
					- WholeCellRoutePotential(before, cell, previous.RemainingByColor, pads);
			}
			var returnCostByColor = (int[])previous.ReturnCostByColor.Clone();
			int estimatedReturnCost = previous.EstimatedReturnCost;
			Span<bool> changedColors = stackalloc bool[input.K];
			changedColors.Clear();
			for (int endpoint = 0; endpoint < 2; endpoint++) {
				int cell = endpoint == 0 ? move.From : move.To;
				foreach (int id in before.Tower(cell)) changedColors[colors[id]] = true;
				foreach (int id in after.Tower(cell)) changedColors[colors[id]] = true;
			}
			if (move.Returned.Length > 0) foreach (int id in move.Returned) changedColors[colors[id]] = true;
			for (int color = 0; color < input.K; color++) {
				if (!changedColors[color]) continue;
				estimatedReturnCost -= returnCostByColor[color];
				returnCostByColor[color] = EstimateColorReturnCost(after, color, distances, floor);
				estimatedReturnCost += returnCostByColor[color];
			}
			return new(distance, overlap, sameColor, routePotential, remainingByColor, returnCostByColor, estimatedReturnCost);
		}

		private double ScoreWholeEvaluation(WholeEvaluation evaluation, int remaining, double progress)
		{
			double remainingWeight = WholeRemainingWeight + WholeLateRemainingWeight * progress * progress;
			return (WholeDistanceWeight + WholeTransportWeight) * evaluation.EstimatedReturnCost + remainingWeight * remaining
				- WholeRoutePadWeight * evaluation.RoutePotential
				- (1 - progress) * (WholeOverlapWeight * evaluation.Overlap + WholeSameColorWeight * evaluation.SameColor);
		}

		private double EvaluateWholeBoard(ReplayBoard board, int[][] distances, List<RoutePad>[] pads, double progress = 0)
		{
			int totalDistance = 0, overlap = 0, sameColor = 0;
			Span<int> remainingByColor = stackalloc int[input.K];
			remainingByColor.Clear();
			for (int cell = 0; cell < n * n; cell++) {
				var tower = board.Tower(cell);
				if (tower.Length == 0) continue;
				overlap += tower.Length - 1;
				for (int index = 0; index < tower.Length; index++) {
					int color = colors[tower[index]];
					remainingByColor[color]++;
					totalDistance += distances[color][cell];
					if (index > 0 && colors[tower[index - 1]] == color) sameColor++;
				}
			}
			double routePotential = 0;
			// 莠域ｸｬ萓ｿ縺ｮ濶ｲ縺悟ｸｰ蟾｣貂医∩縺ｪ繧牙ｱ驟ｬ繧呈ｶ医＠縲∫樟蝨ｨ縺ｮ蝪碑・霄ｫ縺ｯ蛻ｩ逕ｨ萓ｿ縺ｫ謨ｰ縺医↑縺・・
			for (int cell = 0; cell < n * n; cell++) {
				int height = board.Heights[cell];
				if (height == 0 || pads[cell * 9 + height] == null) continue;
				int bestSaving = 0;
				foreach (var pad in pads[cell * 9 + height]) {
					if (remainingByColor[pad.Color] < pad.Load || input.Map[cell / n, cell % n] == pad.Color
						|| board.Heights[pad.Landing] + pad.Load > 8) continue;
					int localCount = 0;
					foreach (int id in board.Tower(cell)) if (colors[id] == pad.Color) localCount++;
					if (remainingByColor[pad.Color] - localCount < pad.Load) continue;
					bestSaving = Math.Max(bestSaving, pad.Saving);
				}
				routePotential += bestSaving;
			}
			double remainingWeight = WholeRemainingWeight + WholeLateRemainingWeight * progress * progress;
			return (WholeDistanceWeight + WholeTransportWeight) * totalDistance + remainingWeight * board.Remaining
				- WholeRoutePadWeight * routePotential
				- (1 - progress) * (WholeOverlapWeight * overlap + WholeSameColorWeight * sameColor);
		}

		public State OptimizeBeamIntervals(State baseline)
		{
			var board = new ReplayBoard(this);
			var best = new List<Transport>();
			foreach (var (i, j, keep, dir, len) in baseline.Moves) {
				if (!board.Capture(i * n + j, dir, len, board.Heights[i * n + j] - keep, out var step))
					throw new InvalidOperationException("Plan12: invalid baseline.");
				best.Add(step!);
			}
			// 跳躍の潜在価値を過小評価しないよう、距離は床距離と帰巣可能数を併用する。
			var floor = new int[n * n][];
			for (int source = 0; source < n * n && stopwatch.ElapsedMilliseconds < BeamDeadlineMilliseconds; source++) {
				floor[source] = Enumerable.Repeat(n * n, n * n).ToArray();
				var queue = new Queue<int>();
				floor[source][source] = 0;
				queue.Enqueue(source);
				while (queue.TryDequeue(out int cell)) {
					foreach (var (i, j) in Helper.Adjacence4(cell / n, cell % n, n, n)) {
						int next = i * n + j;
						if (input.Map[i, j] == -2 || floor[source][next] <= floor[source][cell] + 1) continue;
						floor[source][next] = floor[source][cell] + 1;
						queue.Enqueue(next);
					}
				}
			}
			int trials = 0, accepted = 0;
			for (int round = 0; round < 8 && stopwatch.ElapsedMilliseconds < BeamDeadlineMilliseconds; round++) {
				BuildIndex(best, out _, out _, out var checkpoints);
				bool improved = false;
				foreach (int length in new[] { 12, 20, 32, 48 }) {
					for (int first = 0; first + 3 < best.Count && stopwatch.ElapsedMilliseconds < BeamDeadlineMilliseconds; first += 4) {
						int end = Math.Min(best.Count, first + length);
						var ids = best.Skip(first).Take(end - first).SelectMany(s => s.Cargo.Concat(s.Returned)).ToHashSet();
						if (ids.Count > 20 || ids.Select(id => colors[id]).Distinct().Count() < 2) continue;
						var start = checkpoints[first / CheckpointInterval].Copy();
						for (int index = first / CheckpointInterval * CheckpointInterval; index < first; index++) start.Apply(best[index]);
						var target = start.Copy();
						for (int index = first; index < end; index++) target.Apply(best[index]);
						trials++;
						var replacement = SearchInterval(start, target, ids, end - first, floor, Math.Min(BeamDeadlineMilliseconds, stopwatch.ElapsedMilliseconds + 65));
						if (replacement == null) continue;
						best = best.Take(first).Concat(replacement).Concat(best.Skip(end)).ToList();
						accepted++;
						info.Up++;
						improved = true;
						break;
					}
					if (improved || stopwatch.ElapsedMilliseconds >= BeamDeadlineMilliseconds) break;
				}
				if (!improved) break;
			}
			//if (isLocal) Console.Error.WriteLine($"BEAM12 trials={trials} accepted={accepted}");
			if (best.Count >= baseline.Moves.Count) return baseline;
			var result = new State(isLocal, input);
			foreach (var step in best)
				result.Move(step.From / n, step.From % n, result.Slimes[step.From / n, step.From % n].Count - step.Cargo.Length, step.Direction, step.Length);
			if (result.SlimeCount != baseline.SlimeCount) throw new InvalidOperationException("Plan12: incomplete beam result.");
			return result;
		}

		private List<Transport>? SearchInterval(ReplayBoard start, ReplayBoard target, HashSet<int> ids,
			int originalLength, int[][] floor, long deadline)
		{
			var destinations = Enumerable.Repeat(-1, colors.Length).ToArray();
			for (int cell = 0; cell < n * n; cell++) foreach (int id in target.Tower(cell)) destinations[id] = cell;
			var beam = new List<BeamNode> { new(start, null, null, 0) };
			var seen = new HashSet<string> { start.BeamKey() };
			for (int depth = 1; depth < originalLength && stopwatch.ElapsedMilliseconds < deadline; depth++) {
				var candidates = new PriorityQueue<BeamNode, double>();
				foreach (var node in beam) {
					for (int source = 0; source < n * n && stopwatch.ElapsedMilliseconds < deadline; source++) {
						var tower = node.Board.Tower(source);
						for (int count = 1; count <= tower.Length; count++) {
							if (!tower[^count..].ToArray().All(ids.Contains)) break;
							foreach (var dir in directions) {
								for (int len = 1; len <= tower.Length - count + 1; len++) {
									if (stopwatch.ElapsedMilliseconds >= deadline) return null;
									var (i, j) = dir.Move(source / n, source % n, len);
									if ((uint)i >= (uint)n || (uint)j >= (uint)n || input.Map[i, j] == -2) break;
									if (node.Board.Heights[i * n + j] + count > 8) continue;
									var next = node.Board.Copy();
									if (!next.Capture(source, dir, len, count, out var move) || move!.Returned.Any(id => !ids.Contains(id) || destinations[id] >= 0)) continue;
									if (next.SameState(target)) {
										var result = new List<Transport> { move };
										for (var parent = node; parent.Move != null; parent = parent.Parent!) result.Add(parent.Move);
										result.Reverse();
										return result;
									}
									if (!seen.Add(next.BeamKey())) continue;
									double score = 0;
									for (int cell = 0; cell < n * n; cell++) {
										var contents = next.Tower(cell);
										var loads = new Dictionary<int, int>();
										foreach (int id in contents) {
											if (!ids.Contains(id)) continue;
											int goal = destinations[id] >= 0 ? destinations[id] : input.Dens[colors[id]].i * n + input.Dens[colors[id]].j;
											loads.TryGetValue(goal, out int load);
											loads[goal] = load + 1;
										}
										foreach (var (goal, load) in loads) score += floor[cell][goal] * (0.35 + load * 0.1) + load * 0.7;
									}
									candidates.Enqueue(new BeamNode(next, node, move, score), -score);
									if (candidates.Count > 48) candidates.Dequeue();
								}
							}
						}
					}
				}
				beam = candidates.UnorderedItems.Select(item => item.Element).OrderBy(node => node.Score).ToList();
				if (beam.Count == 0) break;
			}
			return null;
		}

		private sealed class ReplayBoard
		{
			private readonly Plan9Optimizer owner;
			private readonly int[] towers;
			public int[] Heights { get; }
			private int remaining;

			public ReplayBoard(Plan9Optimizer owner)
			{
				this.owner = owner;
				Heights = new int[owner.n * owner.n];
				towers = new int[Heights.Length * 8];
				remaining = owner.colors.Length;
				for (int id = 0; id < owner.origins.Length; id++) {
					int cell = owner.origins[id];
					Heights[cell] = 1;
					towers[cell * 8] = id;
				}
			}

			private ReplayBoard(ReplayBoard other)
			{
				owner = other.owner;
				Heights = (int[])other.Heights.Clone();
				towers = (int[])other.towers.Clone();
				remaining = other.remaining;
			}

			public ReplayBoard Copy() => new(this);
			public int Remaining => remaining;
			public string BeamColorKey()
			{
				var key = new StringBuilder();
				for (int cell = 0; cell < Heights.Length; cell++) {
					if (Heights[cell] == 0) continue;
					key.Append(cell).Append(':');
					foreach (int id in Tower(cell)) key.Append((char)('a' + owner.colors[id]));
					key.Append(';');
				}
				return key.ToString();
			}
			public string BeamKey()
			{
				var key = new StringBuilder();
				for (int cell = 0; cell < Heights.Length; cell++) {
					if (Heights[cell] == 0) continue;
					key.Append(cell).Append(':');
					foreach (int id in Tower(cell)) key.Append(id).Append(',');
					key.Append(';');
				}
				return key.ToString();
			}
			public ReadOnlySpan<int> Tower(int cell) => towers.AsSpan(cell * 8, Heights[cell]);
			public bool Apply(Transport step)
				=> Move(step.From, step.Direction, step.Length, step.Cargo.Length, step, false, out _);
			public bool ApplyCompatible(Transport step, out Transport? recorded)
				=> Move(step.From, step.Direction, step.Length, step.Cargo.Length, step, true, out recorded);
			public bool Capture(int from, Direction4 direction, int length, int count, out Transport? recorded)
				=> Move(from, direction, length, count, null, false, out recorded);

			private bool Move(int from, Direction4 direction, int length, int count, Transport? expected, bool matchColors, out Transport? recorded)
			{
				recorded = null;
				if ((uint)from >= (uint)Heights.Length || count <= 0 || count > Heights[from]) return false;
				int left = Heights[from] - count;
				if (length < 1 || length > left + 1) return false;
				int to = -1;
				for (int distance = 1; distance <= length; distance++) {
					var (i, j) = direction.Move(from / owner.n, from % owner.n, distance);
					if ((uint)i >= (uint)owner.n || (uint)j >= (uint)owner.n || owner.input.Map[i, j] == -2) return false;
					to = i * owner.n + j;
				}
				if (to == from || Heights[to] + count > 8) return false;
				Span<int> cargo = stackalloc int[8];
				Tower(from)[left..].CopyTo(cargo);
				if (expected != null && (expected.To != to || !(matchColors
					? owner.SameColors(cargo[..count], expected.Cargo)
					: cargo[..count].SequenceEqual(expected.Cargo)))) return false;
				int sourceHeight = Heights[from];
				int destinationHeight = Heights[to];
				int previousRemaining = remaining;
				Span<int> sourceBefore = stackalloc int[8];
				Span<int> destinationBefore = stackalloc int[8];
				Tower(from).CopyTo(sourceBefore);
				Tower(to).CopyTo(destinationBefore);
				Heights[from] = left;
				for (int index = count - 1; index >= 0; index--) towers[to * 8 + Heights[to]++] = cargo[index];
				Span<int> returned = stackalloc int[16];
				int homeCount = ReturnHome(from, returned);
				homeCount += ReturnHome(to, returned[homeCount..]);
				if (expected != null) {
					bool matched = matchColors ? owner.SameColors(returned[..homeCount], expected.Returned)
						: returned[..homeCount].SequenceEqual(expected.Returned);
					if (!matched) {
						// 失敗した試行の盤面を戻し、短経路や一時退避による修復を同じ状態から試す。
						Heights[from] = sourceHeight;
						Heights[to] = destinationHeight;
						remaining = previousRemaining;
						sourceBefore[..sourceHeight].CopyTo(towers.AsSpan(from * 8));
						destinationBefore[..destinationHeight].CopyTo(towers.AsSpan(to * 8));
						return false;
					}
					if (!matchColors) return true;
				}
				recorded = new Transport(from, to, direction, length, cargo[..count].ToArray(), returned[..homeCount].ToArray(), owner.colors);
				return true;
			}

			private int ReturnHome(int cell, Span<int> returned)
			{
				int color = owner.input.Map[cell / owner.n, cell % owner.n];
				int count = 0;
				while (Heights[cell] > 0) {
					int id = towers[cell * 8 + Heights[cell] - 1];
					if (owner.colors[id] != color) break;
					Heights[cell]--;
					remaining--;
					returned[count++] = id;
				}
				return count;
			}

			public bool SameState(ReplayBoard other)
			{
				if (remaining != other.remaining || !Heights.AsSpan().SequenceEqual(other.Heights)) return false;
				for (int cell = 0; cell < Heights.Length; cell++) if (!Tower(cell).SequenceEqual(other.Tower(cell))) return false;
				return true;
			}
			public bool SameColorState(ReplayBoard other)
			{
				if (remaining != other.remaining || !Heights.AsSpan().SequenceEqual(other.Heights)) return false;
				for (int cell = 0; cell < Heights.Length; cell++)
					if (!owner.SameColors(Tower(cell), other.Tower(cell))) return false;
				return true;
			}
		}
	}
}

public class State
{
	private readonly bool _isLocal;
	private readonly Input _input;

	public int SlimeCount { get; private set; }
	public Stack<int>[,] Slimes { get; private set; }
	public List<(int i, int j, int k, Direction4 d, int l)> Moves { get; private set; } = [];

	public State Copy()
		=> new State(_isLocal, _input) {
			Slimes = Helper.Array2(_input.N, _input.N, (i, j) => new Stack<int>(Slimes[i, j].Reverse())),
			SlimeCount = SlimeCount,
			Moves = new List<(int i, int j, int k, Direction4 d, int l)>(Moves)
		};

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
		long elapsedMax = 0;
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
					elapsedMax = Math.Max(elapsedMax, elapsed);
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
		Console.WriteLine($"elpased max.: {elapsedMax}");

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
