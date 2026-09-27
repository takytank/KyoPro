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

		var best = DoPlan11(isLocal, sw, rnd, input, info);

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

	static State DoPlan6(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		return DoPlan4Core(
			isLocal, sw, rnd, input, info,
			requireNonWorsening: true, useTwoStepLookahead: true);
	}

	static State DoPlan9(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var baseline = DoPlan8(isLocal, sw, rnd, input, info);
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

	private sealed class Plan9Optimizer
	{
		private const int SearchMilliseconds = 1200;
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

		public Plan9Optimizer(bool isLocal, Stopwatch sw, Input input, Information info)
		{
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

		private bool CanSearch => stopwatch.ElapsedMilliseconds < SearchMilliseconds;
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
					throw new InvalidOperationException("Plan9: invalid Plan8 operation.");
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
					throw new InvalidOperationException("Plan9: invalid Plan8 operation.");
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
			for (int round = 0; round < 64 && timer.ElapsedMilliseconds < 1200; round++) {
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
					if (timer.ElapsedMilliseconds >= 1200 || trials++ >= 500) break;
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
			for (int round = 0; round < 32 && timer.ElapsedMilliseconds < 1600; round++) {
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
				for (int first = 0; first < best.Count && !improved && timer.ElapsedMilliseconds < 1600; first++) {
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
								if (++trials > 400 || timer.ElapsedMilliseconds >= 1600) break;
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
			for (int round = 0; round < 24 && timer.ElapsedMilliseconds < 1900; round++) {
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
				for (int first = 0; first < best.Count && !improved && timer.ElapsedMilliseconds < 1900; first++) {
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
								if (++trials > 350 || timer.ElapsedMilliseconds >= 1900) break;
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
			for (int round = 0; round < 24 && timer.ElapsedMilliseconds < 2200; round++) {
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
				for (int first = 0; first < best.Count && !improved && timer.ElapsedMilliseconds < 2200; first++) {
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
							if (++trials > 350 || timer.ElapsedMilliseconds >= 2200) break;
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
			for (int round = 0; round < 24 && timer.ElapsedMilliseconds < 2600; round++) {
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
				for (int first = 0; first < best.Count && !improved && timer.ElapsedMilliseconds < 2600; first++) {
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
								if (++trials > 400 || timer.ElapsedMilliseconds >= 2600) break;
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

	static State DoPlan8(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		int n = input.N;
		var colorCounts = new int[input.K];
		foreach (string row in input.C) {
			foreach (char cell in row) {
				if (cell >= 'a' && cell < 'a' + input.K) colorCounts[cell - 'a']++;
			}
		}
		var order = Enumerable.Range(0, input.K).ToArray();
		Array.Sort(order, (a, b) => colorCounts[a] != colorCounts[b]
			? colorCounts[a].CompareTo(colorCounts[b]) : a.CompareTo(b));
		var nestDistances = new int[input.K][,];
		for (int c = 0; c < input.K; c++) nestDistances[c] = BuildDistance(input.Dens[c].i, input.Dens[c].j);
		var floorDistances = BuildAllFloorDistances();
		var previousColorBoards = Enumerable.Range(0, input.K)
			.Select(_ => new HashSet<(ulong, ulong)>()).ToArray();
		var preferredCell = Enumerable.Repeat(-1, input.K).ToArray();
		bool hasPreviousMove = false;
		int previousFromI = -1;
		int previousFromJ = -1;
		int previousToI = -1;
		int previousToJ = -1;
		int previousMoved = 0;
		while (state.SlimeCount > 0 && state.Moves.Count < 100000) {
			int movesAtStart = state.Moves.Count;
			foreach (int color in order) {
				var (denI, denJ) = input.Dens[color];
				while (CountColor(color) > 0 && state.Moves.Count < 100000) {
					if (!previousColorBoards[color].Add(GetBoardKey(color))) {
						if (TryFindProgressMove(color)) continue;
						break;
					}
					// 帰巣搬送中に受け渡した塔は、次の反復で他の塔より先に処理する。
					if (preferredCell[color] >= 0) {
						int cell = preferredCell[color];
						preferredCell[color] = -1;
						int pi = cell / n;
						int pj = cell % n;
						int run = TopRun(state.Slimes[pi, pj], color);
						if (run > 0 && FindPriorityMove(pi, pj, color, denI, denJ,
							out int preferredMoved, out Direction4 preferredDir, out int preferredLen)) {
							if (TryDoMove(pi, pj, preferredMoved, preferredDir, preferredLen)) continue;
						}
					}

					// 高さ7の塔は、合流対象ではなく巣への搬送を先に計画する。
					if (FindTallestHomeMove(color, denI, denJ,
						out int tallI, out int tallJ, out int tallCount,
						out Direction4 tallDir, out int tallLen, out int receiver)) {
						if (TryDoMove(tallI, tallJ, tallCount, tallDir, tallLen)) {
							if (receiver >= 0) preferredCell[color] = receiver;
							continue;
						}
					}

					if (FindPair(color, denI, denJ, out int si, out int sj, out int ti, out int tj, out int moved)) {
						if (FindMove(si, sj, ti, tj, moved, out Direction4 pairDir, out int pairLen, out int pairMoved)
							&& TryDoMove(si, sj, pairMoved, pairDir, pairLen)) continue;
					}

					// 合流できる相手・経路が見つからない場合にだけ、帰巣可能な塔を整理する。
					if (FindUrgentHomeMove(color, denI, denJ,
						out int homeI, out int homeJ, out int homeCount,
						out Direction4 homeDir, out int homeLen)
						&& TryDoMove(homeI, homeJ, homeCount, homeDir, homeLen)) {
						continue;
					}

					// 合流できる相手がない場合は、進行可能な最遠塔を巣へ送る。
					if (FindPair(color, denI, denJ, out si, out sj, out ti, out tj, out moved)
						&& (ti == denI && tj == denJ)
						&& FindMove(si, sj, ti, tj, moved, out Direction4 homePairDir, out int homePairLen, out int homePairMoved)
						&& TryDoMove(si, sj, homePairMoved, homePairDir, homePairLen)) {
						continue;
					}
					if (TryFindProgressMove(color)) continue;
					break;
				}
			}
			if (state.Moves.Count == movesAtStart
				&& !TryFindProgressMove()) break;
		}
		return state;

		bool TryDoMove(int i, int j, int moved, Direction4 direction, int length)
		{
			var (ni, nj) = direction.Move(i, j, length);
			if (IsImmediateReverse(i, j, ni, nj, moved)) return false;
			state.Move(i, j, state.Slimes[i, j].Count - moved, direction, length);
			hasPreviousMove = true;
			previousFromI = i;
			previousFromJ = j;
			previousToI = ni;
			previousToJ = nj;
			previousMoved = moved;
			return true;
		}

		bool IsImmediateReverse(int fromI, int fromJ, int toI, int toJ, int moved)
		{
			return hasPreviousMove
				&& fromI == previousToI && fromJ == previousToJ
				&& toI == previousFromI && toJ == previousFromJ
				&& moved == previousMoved;
		}

		int CountColor(int c)
		{
			int count = 0;
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					foreach (int slime in state.Slimes[i, j]) if (slime == c) count++;
				}
			}
			return count;
		}
		(ulong, ulong) GetBoardKey(int color)
		{
			ulong first = 14695981039346656037UL;
			ulong second = 7809847782465536322UL;
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					foreach (int slime in state.Slimes[i, j]) AddToken((uint)slime + 1);
					AddToken(0);
				}
			}
			return (first, second);

			void AddToken(uint token)
			{
				first = (first ^ token) * 1099511628211UL;
				second ^= token + 0x9e3779b97f4a7c15UL + (second << 6) + (second >> 2);
			}
		}
		int TopRun(Stack<int> tower, int c) { int count = 0; foreach (int x in tower) { if (x != c) break; count++; } return count; }
		int[,] BuildDistance(int ti, int tj) { var d = new int[n, n]; for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) d[i, j] = int.MaxValue; var q = new Queue<(int i, int j)>(); d[ti, tj] = 0; q.Enqueue((ti, tj)); while (q.Count > 0) { var (i, j) = q.Dequeue(); foreach (var (ni, nj) in Helper.Adjacence4(i, j, n, n)) { if (input.Map[ni, nj] == -2 || d[ni, nj] != int.MaxValue) continue; d[ni, nj] = d[i, j] + 1; q.Enqueue((ni, nj)); } } return d; }
		int[,] BuildAllFloorDistances()
		{
			var distances = new int[n * n, n * n];
			for (int si = 0; si < n; si++) {
				for (int sj = 0; sj < n; sj++) {
					int source = si * n + sj;
					for (int cell = 0; cell < n * n; cell++) distances[source, cell] = int.MaxValue;
					distances[source, source] = 0;
					var queue = new Queue<(int i, int j)>();
					queue.Enqueue((si, sj));
					while (queue.Count > 0) {
						var (i, j) = queue.Dequeue();
						foreach (var (ni, nj) in Helper.Adjacence4(i, j, n, n)) {
							int next = ni * n + nj;
							if (input.Map[ni, nj] == -2 || distances[source, next] != int.MaxValue) continue;
							distances[source, next] = distances[source, i * n + j] + 1;
							queue.Enqueue((ni, nj));
						}
					}
				}
			}
			return distances;
		}
		bool FindTallestHomeMove(int c, int denI, int denJ, out int sourceI, out int sourceJ, out int moved, out Direction4 direction, out int length, out int receiver)
		{
			sourceI = sourceJ = moved = length = 0;
			direction = Direction4.N;
			receiver = -1;
			int nestCapacity = 8 - state.Slimes[denI, denJ].Count;
			if (nestCapacity <= 0) return false;

			int bestTowerHeight = 6;
			int bestProgress = int.MaxValue;
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					Stack<int> source = state.Slimes[i, j];
					int run = TopRun(source, c);
					if (source.Count < 7 || run == 0 || nestDistances[c][i, j] == int.MaxValue) continue;
					int countToNest = Math.Min(Math.Min(7, run), nestCapacity);
					if (FindMove(i, j, denI, denJ, countToNest, out Direction4 homeDirection, out int homeLength, out int homeMoved)) {
						var (homeI, homeJ) = homeDirection.Move(i, j, homeLength);
						int blockedReceiver = -1;
						for (int step = 1; step < homeLength; step++) {
							var (pathI, pathJ) = homeDirection.Move(i, j, step);
							if (TopRun(state.Slimes[pathI, pathJ], c) > 0
								&& state.Slimes[pathI, pathJ].Count < 8) {
								blockedReceiver = pathI * n + pathJ;
								break;
							}
						}
						if (blockedReceiver >= 0) {
							var (receiverI, receiverJ) = (blockedReceiver / n, blockedReceiver % n);
							int handoffCount = Math.Min(Math.Min(7, run), 8 - state.Slimes[receiverI, receiverJ].Count);
							int handoffLength = floorDistances[i * n + j, blockedReceiver];
							if (handoffCount > 0 && handoffLength > 0
								&& handoffLength <= source.Count - handoffCount + 1
								&& (source.Count > bestTowerHeight
									|| (source.Count == bestTowerHeight && nestDistances[c][receiverI, receiverJ] < bestProgress))) {
								bestTowerHeight = source.Count;
								bestProgress = nestDistances[c][receiverI, receiverJ];
								sourceI = i;
								sourceJ = j;
								moved = handoffCount;
								direction = homeDirection;
								length = handoffLength;
								receiver = blockedReceiver;
							}
							continue;
						}
						int progress = nestDistances[c][homeI, homeJ];
						if (source.Count > bestTowerHeight || (source.Count == bestTowerHeight && progress < bestProgress)) {
							bestTowerHeight = source.Count;
							bestProgress = progress;
							sourceI = i;
							sourceJ = j;
							moved = homeMoved;
							direction = homeDirection;
							length = homeLength;
							receiver = -1;
						}
					}

					// 巣への直行経路が塞がる/容量不足なら、経路上の同色塔へ運べる分を渡す。
					foreach (Direction4 dir in Direction4Extensions.ForEach4()) {
						int maxLength = source.Count - Math.Min(7, run) + 1;
						for (int len = 1; len <= maxLength; len++) {
							var (ni, nj) = dir.Move(i, j, len);
							if ((uint)ni >= (uint)n || (uint)nj >= (uint)n || input.Map[ni, nj] == -2) break;
							if (input.Map[ni, nj] == c || nestDistances[c][ni, nj] >= nestDistances[c][i, j]) continue;
							int receiverRun = TopRun(state.Slimes[ni, nj], c);
							if (receiverRun == 0) continue;
							int count = Math.Min(Math.Min(7, run), 8 - state.Slimes[ni, nj].Count);
							if (count <= 0) continue;
							if (floorDistances[i * n + j, ni * n + nj] + nestDistances[c][ni, nj]
								!= nestDistances[c][i, j]) continue;
							int progress = nestDistances[c][ni, nj];
							if (source.Count < bestTowerHeight
								|| (source.Count == bestTowerHeight && progress >= bestProgress)) continue;
							bestTowerHeight = source.Count;
							bestProgress = progress;
							sourceI = i;
							sourceJ = j;
							moved = count;
							direction = dir;
							length = len;
							receiver = ni * n + nj;
						}
					}
				}
			}
			return sourceI >= 0 && bestTowerHeight >= 7;
		}
		bool FindPriorityMove(int si, int sj, int c, int denI, int denJ, out int moved, out Direction4 direction, out int length)
		{
			moved = 0;
			direction = Direction4.N;
			length = 0;
			Stack<int> source = state.Slimes[si, sj];
			int run = TopRun(source, c);
			if (run == 0) return false;
			if (source.Count >= 7) return FindMove(si, sj, denI, denJ, Math.Min(7, run), out direction, out length, out moved) && moved > 0;

			long best = long.MaxValue;
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					int targetRun = TopRun(state.Slimes[i, j], c);
					if (targetRun == 0 || (i == si && j == sj) || state.Slimes[i, j].Count >= 8) continue;
					int count = Math.Min(7, Math.Min(run, 8 - state.Slimes[i, j].Count));
					if (!FindMove(si, sj, i, j, count, out Direction4 dir, out int len, out _)) continue;
					long score = (long)floorDistances[si * n + sj, i * n + j] * 10 - Math.Min(count, targetRun);
					if (score >= best) continue;
					best = score;
					moved = count;
					direction = dir;
					length = len;
				}
			}
			return moved > 0 || (FindMove(si, sj, denI, denJ, Math.Min(7, run), out direction, out length, out moved) && moved > 0);
		}
		bool FindUrgentHomeMove(int c, int denI, int denJ, out int sourceI, out int sourceJ, out int moved, out Direction4 direction, out int length)
		{
			sourceI = sourceJ = moved = length = 0;
			direction = Direction4.N;
			int[,] distances = nestDistances[c];
			int nestCapacity = 8 - state.Slimes[denI, denJ].Count;
			if (nestCapacity <= 0) return false;

			// 高さ7では空きが1段しかなくなるため、積み上げや移動経路を塞ぐ前に帰巣させる。
			// 次に巣の隣の塔を帰巣させ、巣周辺の合流先を空ける。
			for (int priority = 0; priority < 2; priority++) {
				for (int i = 0; i < n; i++) {
					for (int j = 0; j < n; j++) {
						Stack<int> tower = state.Slimes[i, j];
						int run = TopRun(tower, c);
						if (run == 0 || distances[i, j] == 0 || distances[i, j] == int.MaxValue) continue;
						bool urgent = priority == 0 ? tower.Count >= 7 : distances[i, j] == 1;
						if (!urgent) continue;

						int count = Math.Min(Math.Min(7, run), nestCapacity);
						if (count == 0 || !FindMove(i, j, denI, denJ, count, out Direction4 dir, out int len, out int homeMoved)) continue;
						var (destI, destJ) = dir.Move(i, j, len);
						if (state.Slimes[destI, destJ].Count + count > 8) continue;

						sourceI = i;
						sourceJ = j;
						moved = homeMoved;
						direction = dir;
						length = len;
						return true;
					}
				}
			}
			return false;
		}
		bool FindPair(int c, int di, int dj, out int bi, out int bj, out int btI, out int btJ, out int bm)
		{
			bi = bj = btI = btJ = -1;
			bm = 0;
			var d = nestDistances[c];
			int farthest = -1;
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					int run = TopRun(state.Slimes[i, j], c);
					if (run > 0 && d[i, j] < int.MaxValue) farthest = Math.Max(farthest, d[i, j]);
				}
			}

			// 最遠の供給元から順に、巣へ向かう最短経路上の合流先を探す。
			for (int sourceDistance = farthest; sourceDistance > 0; sourceDistance--) {
				long bestSourceCost = long.MaxValue;
				for (int i = 0; i < n; i++) {
					for (int j = 0; j < n; j++) {
						int run = TopRun(state.Slimes[i, j], c);
						if (run == 0 || d[i, j] != sourceDistance) continue;
						long sourceDistanceToReceiver = long.MaxValue;
						int sourceTargetRun = 0;
						int sourceCount = 0;
						int sourceTargetI = -1;
						int sourceTargetJ = -1;
						for (int y = 0; y < n; y++) {
							for (int x = 0; x < n; x++) {
								if ((i == y && j == x) || d[y, x] > sourceDistance) continue;
								int targetRun = TopRun(state.Slimes[y, x], c);
								int targetHeight = state.Slimes[y, x].Count;
								if (targetRun == 0 || targetHeight >= 8) continue;
								int count = Math.Min(7, Math.Min(run, 8 - targetHeight));
								int sep = floorDistances[i * n + j, y * n + x];
								// 同距離の塔も候補に含め、近場で合流できる機会を逃さない。
								if (count <= 0 || sep == int.MaxValue
								|| !FindMove(i, j, y, x, count, out _, out _, out _)) continue;
								int sourceJump = Math.Max(1, state.Slimes[i, j].Count - count + 1);
								int directTargetJump = Math.Max(1, targetHeight + 1);
								int mergedTargetJump = Math.Max(1, Math.Min(8, targetHeight + count) + 1);
								int directMoves = EstimateMoves(sourceDistance, sourceJump)
									+ EstimateMoves(d[y, x], directTargetJump);
								int mergeMoves = EstimateMoves(sep, sourceJump)
									+ EstimateMoves(d[y, x], mergedTargetJump);
								int remainingSource = run - count;
								int splitMoves = remainingSource > 0
									? EstimateMoves(Math.Max(0, sourceDistance - sep), Math.Max(1, state.Slimes[i, j].Count - remainingSource + 1))
									: 0;
								mergeMoves += splitMoves;
								// 合流で移動操作数が増える候補は採らず、個別帰巣に任せる。
								if (mergeMoves > directMoves) continue;
								long detour = (long)sep + d[y, x] - sourceDistance;
								long candidateCost = (long)sep * 10000
									+ (long)mergeMoves * 1000
									+ Math.Max(0, detour) * 50
									- (long)Math.Min(count, targetRun) * 10;
								if (candidateCost >= sourceDistanceToReceiver) continue;
								sourceDistanceToReceiver = (int)candidateCost;
								sourceTargetRun = targetRun;
								sourceCount = count;
								sourceTargetI = y;
								sourceTargetJ = x;
							}
						}
						if (sourceTargetI < 0) continue;
						long sourceCost = (long)sourceDistanceToReceiver * 1000
							- (long)Math.Min(sourceCount, sourceTargetRun) * 10;
						if (sourceCost >= bestSourceCost) continue;
						bestSourceCost = sourceCost;
						bi = i;
						bj = j;
						btI = sourceTargetI;
						btJ = sourceTargetJ;
						bm = sourceCount;
					}
				}
				if (bi >= 0) return true;
			}

			// 最短経路上に合流先がない場合は、合流せず最遠の塔を巣へ近づける。
			bi = bj = -1;
			bm = 0;
			long bestHomeScore = long.MaxValue;
			int farthestRun = -1;
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					int run = TopRun(state.Slimes[i, j], c);
					if (run == 0 || d[i, j] == 0 || d[i, j] == int.MaxValue) continue;
					int count = Math.Min(7, run);
					if (!FindMove(i, j, di, dj, count, out _, out _, out _)) continue;
					long score = -d[i, j] * 1000L - count;
					if (score >= bestHomeScore) continue;
					bestHomeScore = score;
					farthestRun = run;
					bi = i;
					bj = j;
				}
			}
			if (bi < 0) return false;
			btI = di;
			btJ = dj;
			bm = Math.Min(Math.Min(7, farthestRun), 8 - state.Slimes[di, dj].Count);
			if (bm <= 0) return false;
			return true;

			int EstimateMoves(int distance, int jumpLength)
			{
				return (distance + jumpLength - 1) / jumpLength;
			}
		}
		bool FindMove(int si, int sj, int ti, int tj, int moved, out Direction4 bestDir, out int bestLen, out int bestMoved)
		{
			bestDir = Direction4.N;
			bestLen = 0;
			bestMoved = 0;
			int current = floorDistances[si * n + sj, ti * n + tj];
			if (current == int.MaxValue) return false;
			long best = long.MaxValue;
			int sourceHeight = state.Slimes[si, sj].Count;
			int sourceColor = state.Slimes[si, sj].Peek();
			int sourceRun = TopRun(state.Slimes[si, sj], sourceColor);
			int max = sourceHeight - moved + 1;
			foreach (var dir in Direction4Extensions.ForEach4()) {
				for (int len = 1; len <= max; len++) {
					bool clear = true;
					for (int step = 1; step <= len; step++) {
						var (i, j) = dir.Move(si, sj, step);
						if ((uint)i >= (uint)n || (uint)j >= (uint)n || input.Map[i, j] == -2) {
							clear = false;
							break;
						}
					}
					if (!clear) break;

					var (ni, nj) = dir.Move(si, sj, len);
					bool isTarget = ni == ti && nj == tj;
					int destinationCapacity = 8 - state.Slimes[ni, nj].Count;
					int actualMoved = isTarget
						? moved
						: Math.Min(sourceRun, destinationCapacity);
					if (actualMoved <= 0 || (isTarget && destinationCapacity < moved)) continue;
					if (len > sourceHeight - actualMoved + 1) continue;
					if (IsImmediateReverse(si, sj, ni, nj, actualMoved)) continue;
					int nextDistance = floorDistances[ni * n + nj, ti * n + tj];
					if (nextDistance >= current) continue;
					long score = nextDistance * 100L + len - (isTarget ? 10000 : 0);
					if (score < best) {
						best = score;
						bestDir = dir;
						bestLen = len;
						bestMoved = actualMoved;
					}
				}
			}
			return bestLen > 0;
		}
		bool TryFindProgressMove(int preferredColor = -1)
		{
			int bestI = -1;
			int bestJ = -1;
			int bestDistance = -1;
			int bestDestI = -1;
			int bestDestJ = -1;
			Direction4 bestDirection = Direction4.N;

			// 通常の合流・帰巣計画が止まった場合だけ、最上段を巣に近づく隣接マスへ1匹ずつ運ぶ。
			for (int i = 0; i < n; i++) {
				for (int j = 0; j < n; j++) {
					Stack<int> tower = state.Slimes[i, j];
					if (tower.Count == 0) continue;
					int color = tower.Peek();
					if (preferredColor >= 0 && color != preferredColor) continue;
					int distance = nestDistances[color][i, j];
					if (distance <= bestDistance || distance == int.MaxValue) continue;
					foreach (Direction4 direction in Direction4Extensions.ForEach4()) {
						var (ni, nj) = direction.Move(i, j);
						if ((uint)ni >= (uint)n || (uint)nj >= (uint)n
							|| input.Map[ni, nj] == -2
							|| state.Slimes[ni, nj].Count >= 8
						|| IsImmediateReverse(i, j, ni, nj, 1)
							|| nestDistances[color][ni, nj] >= distance) continue;
						bestI = i;
						bestJ = j;
						bestDistance = distance;
						bestDestI = ni;
						bestDestJ = nj;
						bestDirection = direction;
						break;
					}
				}
			}

			if (bestI < 0 && preferredColor >= 0) return TryFindProgressMove();
			if (bestI < 0) return false;
			return TryDoMove(bestI, bestJ, 1, bestDirection, 1);
		}
	}

	static State DoPlan4Core(
		bool isLocal, Stopwatch sw, Random rnd, Input input, Information info,
		bool requireNonWorsening, bool useTwoStepLookahead = false,
		State? initialState = null, int[,]? reservedHeights = null)
	{
		var state = initialState ?? new State(isLocal, input);
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
			long bestScore = 0;
			long bestImmediateImprovement = 0;
			bool found = false;

			int minRetained = reservedHeights == null
				? 0
				: Math.Min(reservedHeights[i, j], h);
			for (int k = minRetained; k < h; k++) {
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
						if (improvement <= 0) {
							continue;
						}

						long score = improvement;
						if (useTwoStepLookahead) {
							var sourceAfter = CopyStack(state.Slimes[i, j]);
							var destinationAfter = CopyStack(state.Slimes[destI, destJ]);
							for (int count = 0; count < moved; count++) {
								destinationAfter.Push(sourceAfter.Pop());
							}
							ReturnHome(sourceAfter, i, j);
							ReturnHome(destinationAfter, destI, destJ);

							long nextSourceImprovement = BestNextImprovement(
								i, j, sourceAfter,
								i, j, sourceAfter, destI, destJ, destinationAfter);
							long nextDestinationImprovement = BestNextImprovement(
								destI, destJ, destinationAfter,
								i, j, sourceAfter, destI, destJ, destinationAfter);
							score += Math.Max(nextSourceImprovement, nextDestinationImprovement);
						}

						if (score > bestScore
							|| (score == bestScore && improvement > bestImmediateImprovement)) {
							bestScore = score;
							bestImmediateImprovement = improvement;
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

		long BestNextImprovement(
			int fromI, int fromJ, Stack<int> tower,
			int changedSourceI, int changedSourceJ, Stack<int> changedSource,
			int changedDestinationI, int changedDestinationJ, Stack<int> changedDestination)
		{
			int h = tower.Count;
			if (h == 0) {
				return 0;
			}

			int[] stack = tower.ToArray();
			long bestImprovement = 0;
			int minRetained = reservedHeights == null
				? 0
				: Math.Min(reservedHeights[fromI, fromJ], h);
			for (int k = minRetained; k < h; k++) {
				int moved = h - k;
				long fromDistance = 0;
				for (int index = 0; index < moved; index++) {
					fromDistance += dists[stack[index], fromI, fromJ];
				}

				foreach (var dir in Direction4Extensions.ForEach4()) {
					for (int l = 1; l <= k + 1; l++) {
						bool pathIsFloor = true;
						for (int step = 1; step <= l; step++) {
							var (ii, jj) = dir.Move(fromI, fromJ, step);
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

						var (destI, destJ) = dir.Move(fromI, fromJ, l);
						int destinationHeight =
							GetHypotheticalHeight(destI, destJ,
								changedSourceI, changedSourceJ, changedSource,
								changedDestinationI, changedDestinationJ, changedDestination);
						if (destinationHeight + moved > 8) {
							continue;
						}

						long toDistance = 0;
						bool noSlimeWorsens = true;
						for (int index = 0; index < moved; index++) {
							int color = stack[index];
							int from = dists[color, fromI, fromJ];
							int to = dists[color, destI, destJ];
							toDistance += to;
							if (to > from) {
								noSlimeWorsens = false;
							}
						}

						if (requireNonWorsening && !noSlimeWorsens) {
							continue;
						}

						bestImprovement = Math.Max(bestImprovement, fromDistance - toDistance);
					}
				}
			}

			return bestImprovement;
		}

		int GetHypotheticalHeight(
			int i, int j,
			int changedSourceI, int changedSourceJ, Stack<int> changedSource,
			int changedDestinationI, int changedDestinationJ, Stack<int> changedDestination)
		{
			if (i == changedSourceI && j == changedSourceJ) {
				return changedSource.Count;
			}
			if (i == changedDestinationI && j == changedDestinationJ) {
				return changedDestination.Count;
			}
			return state.Slimes[i, j].Count;
		}

		Stack<int> CopyStack(Stack<int> stack)
		{
			return new Stack<int>(stack.Reverse());
		}

		void ReturnHome(Stack<int> tower, int i, int j)
		{
			int denColor = input.Map[i, j];
			while (denColor >= 0 && tower.Count > 0 && tower.Peek() == denColor) {
				tower.Pop();
			}
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