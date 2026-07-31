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
		double sumA = 0;
		double sumC = 0;
		double sumZ = 0;

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
						//$"N={input.N:d2}",
						$"R={input.R:f3}",
						$"P={info.PondCount:d4}",
						$"loop={info.Loop:d8}",
						$"up={info.Up:d4}",
						$"A={info.Accept:d4}",
						$"C={info.CompactnessAverage:f3}",
						$"Z={info.ZAverage:f3}",
						$"score={info.Score:d10}",
						$"elapsed={info.Elapsed:d4}",
					};

					sumA += info.Accept;
					sumC += info.CompactnessAverage;
					sumZ += info.ZAverage;

					Console.WriteLine(status.Join(" "));
					Console.Out.Flush();
				}

				return (info.Score, info.Loop, info.Up);
			},
			(tc, rc) => {
				double averageA = sumA / rc;
				double averageC = sumC / rc;
				double averageZ = sumZ / rc;
				Console.WriteLine($"accept average: {averageA:f3}");
				Console.WriteLine($"compactness average: {averageC:f3}");
				Console.WriteLine($"Z average: {averageZ:f6}");
			});
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	static (Input input, Information info)
		Run(int caseNumber)
	{
		var (isLocal, sw, rnd) = HeuristicHelper.Initialize();
		using var io = HeuristicHelper.CreateIO(@$"D:\kyopro\AtCoder\AHC069\tools\in\", caseNumber);

		var input = new Input(io, isLocal);
		var info = new Information();
		for (int i = 0; i < input.N; i++) {
			for (int j = 0; j < input.N; j++) {
				if (input.Row[i][j] == '#') {
					++info.PondCount;
				}
			}
		}

		var best = Plan5(isLocal, sw, rnd, input, info);

		info.Accept = best.AcceptedCount;
		info.CompactnessAverage = best.ComactnessSum / best.AcceptedCount;
		info.ZAverage /= best.ComactnessSum;
		info.Score = best.Calculate();
		info.Elapsed = sw.ElapsedMilliseconds;

		return (input, info);
	}

	// 公式サンプル相当。左上の最初の空きマスから1回だけBFSし、基準性能を測る。
	static State Plan0(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();
			state.ReleaseDepartedGroups(guest.s);
			input.OutputMove([]);

			var points = state.FindPlacementByBreadthFirstSearch(guest.p);
			bool canAccept = points.Count == guest.p;
			state.ProcessGuest(guest, canAccept, points);
		}
		state.ReleaseDepartedGroups(int.MaxValue);

		return state;
	}

	// コンパクト度を優先し、正方形に近い単一形状を盤面全体から探す。
	static State Plan1(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();
			state.ReleaseDepartedGroups(guest.s);
			input.OutputMove([]);

			var (shape, _) = Utils.CreateNearSquareShape(guest.p);
			var points = state.FindPlacementByFullSearch(shape);
			bool canAccept = points.Count == guest.p;
			state.ProcessGuest(guest, canAccept, points);
		}
		state.ReleaseDepartedGroups(int.MaxValue);

		return state;
	}

	// Plan1で置けない場合だけ複数始点BFSへフォールバックし、受け入れ率を補う。
	static State Plan2(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();

			state.ReleaseDepartedGroups(guest.s);
			input.OutputMove([]);

			//const double Bottom10PercentZ = -1.025242;
			//const double Bottom15PercentZ = -0.829147;
			const double Bottom20PercentZ = -0.673297;
			double z = Utils.CalculateValueZScore(guest);
			bool shouldReject = z < Bottom20PercentZ;
			if (shouldReject) {
				state.ProcessGuest(guest, false, []);
				continue;
			}

			var (shape, _) = Utils.CreateNearSquareShape(guest.p);
			var points = state.FindPlacementByFullSearch(shape);
			if (points.Count != guest.p) {
				points = state.FindPlacementByReverseBreadthFirstSearch(guest.p);
			}

			bool canAccept = points.Count == guest.p;
			state.ProcessGuest(guest, canAccept, points);
			if (canAccept) {
				info.ZAverage += z;
			}
		}
		state.ReleaseDepartedGroups(int.MaxValue);

		return state;
	}

	// 非重複の固定スロットだけを再利用し、時間経過による空き領域の断片化を防ぐ。
	static State Plan3(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		var slots = Utils.CreateFixedSlots(input.Row);
		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();
			state.ReleaseDepartedGroups(guest.s);
			input.OutputMove([]);

			var points = state.FindPlacementByFixedSlots(slots, guest.p);
			bool canAccept = points.Count == guest.p;
			state.ProcessGuest(guest, canAccept, points);
		}
		state.ReleaseDepartedGroups(int.MaxValue);

		return state;
	}

	// Plan3の配置を維持し、未予約領域へ小サイズ用スロットを追加してrejectを減らす。
	static State Plan4(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		var slots = Utils.CreateFixedSlotsForPlan4(input.Row);
		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();
			state.ReleaseDepartedGroups(guest.s);
			input.OutputMove([]);

			var points = state.FindPlacementByFixedSlots(slots, guest.p);
			bool canAccept = points.Count == guest.p;
			state.ProcessGuest(guest, canAccept, points);
		}
		state.ReleaseDepartedGroups(int.MaxValue);

		return state;
	}

	// Plan2の価値選別と定型形状を維持し、隙間用の探索だけを周長重視の領域成長へ置き換える。
	static State Plan5(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();
			state.ReleaseDepartedGroups(guest.s);
			input.OutputMove([]);

			// 低価値グループで将来の配置余地を消費しないよう、Plan2と同じ下位20%を事前拒否する。
			//const double Bottom10PercentZ = -1.025242;
			//const double Bottom15PercentZ = -0.829147;
			const double Bottom20PercentZ = -0.673297;
			double z = Utils.CalculateValueZScore(guest);
			bool shouldReject = z < Bottom20PercentZ;
			if (shouldReject) {
				state.ProcessGuest(guest, false, []);
				continue;
			}

			// 高コンパクトな定型形状を優先し、入らない場合だけ柔軟な領域成長を利用する。
			var (shape, _) = Utils.CreateNearSquareShape(guest.p);
			var points = state.FindPlacementByFullSearch(shape);
			if (points.Count != guest.p) {
				points = state.FindPlacementByReverseBreadthFirstSearch2(guest.p);
			}

			bool canAccept = points.Count == guest.p;
			state.ProcessGuest(guest, canAccept, points);
			if (canAccept) {
				info.ZAverage += z;
			}
		}
		state.ReleaseDepartedGroups(int.MaxValue);

		return state;
	}
}

// 同じ固定スロットを共有する人数帯。最大人数分を予約し、実配置では先頭Pマスを使う。
public readonly record struct GuestSizeBand(int MinP, int MaxP);

// 退去後も同じ領域を再利用するための、盤面に固定された非重複の配置候補。
public sealed class FixedSlot
{
	public GuestSizeBand Band { get; }
	public (int x, int y)[] Points { get; }

	public FixedSlot(GuestSizeBand band, IReadOnlyCollection<(int x, int y)> points)
	{
		Band = band;
		Points = points.ToArray();
	}
}

// 配置形状の生成・探索・評価を集約し、各Planは戦略の組み合わせだけを担当する。
public static class Utils
{
	// 入力生成時に基本支払額へ掛けられた 2^Z を逆算する。
	// Vは整数へ丸められているため厳密値ではないが、下位判定には十分な精度がある。
	internal static double CalculateValueFactor(
		(int i, int s, int t, int p, int v) guest)
	{
		int duration = guest.t - guest.s;
		double baseValue = guest.p * Math.Pow(duration, 0.9);
		return guest.v / baseValue;
	}

	// Z～N(0, 0.8) に相当する値を逆算する。
	internal static double CalculateValueZScore(
		(int i, int s, int t, int p, int v) guest)
	{
		return Math.Log2(CalculateValueFactor(guest));
	}

	// 最大サイズを急増させすぎず、Pの生成分布もある程度まとめられる人数帯。
	internal static readonly GuestSizeBand[] GuestSizeBands = [
		new(4, 7),
		new(8, 15),
		new(16, 31),
		new(32, 47),
		new(48, 71),
		new(72, 99),
		new(100, 150),
	];

	// P=round(U^2) の出現確率に対して、候補数の比率が近くなるよう芝生面積を配分する。
	internal static int[] CalculateFixedSlotCounts(string[] row)
	{
		int grassCount = row.Sum(line => line.Count(cell => cell == '.'));
		double minSource = 2.0;
		double maxSource = Math.Sqrt(150.0);
		double sourceRange = maxSource - minSource;
		var probabilities = new double[GuestSizeBands.Length];

		for (int bandIndex = 0; bandIndex < GuestSizeBands.Length; ++bandIndex) {
			var band = GuestSizeBands[bandIndex];
			for (int p = band.MinP; p <= band.MaxP; ++p) {
				double lower = Math.Max(minSource, Math.Sqrt(Math.Max(0, p - 0.5)));
				double upper = Math.Min(maxSource, Math.Sqrt(p + 0.5));
				probabilities[bandIndex] += Math.Max(0, upper - lower) / sourceRange;
			}
		}

		var counts = new int[GuestSizeBands.Length];
		int remainingGrass = grassCount;
		while (true) {
			int selectedBand = -1;
			double selectedRatio = double.MaxValue;
			for (int bandIndex = 0; bandIndex < GuestSizeBands.Length; ++bandIndex) {
				int slotSize = GuestSizeBands[bandIndex].MaxP;
				if (slotSize > remainingGrass || probabilities[bandIndex] <= 0) {
					continue;
				}

				double ratio = (counts[bandIndex] + 1) / probabilities[bandIndex];
				if (ratio < selectedRatio) {
					selectedBand = bandIndex;
					selectedRatio = ratio;
				}
			}

			if (selectedBand < 0) {
				break;
			}

			++counts[selectedBand];
			remainingGrass -= GuestSizeBands[selectedBand].MaxP;
		}

		return counts;
	}

	// 大きい帯から先に場所を確保し、互いに重ならないPlan3用スロットを作る。
	internal static List<FixedSlot> CreateFixedSlots(string[] row)
	{
		var slotCounts = CalculateFixedSlotCounts(row);
		var reserved = new bool[row.Length, row[0].Length];
		var slots = new List<FixedSlot>();

		for (int bandIndex = GuestSizeBands.Length - 1; bandIndex >= 0; --bandIndex) {
			var band = GuestSizeBands[bandIndex];
			for (int slotIndex = 0; slotIndex < slotCounts[bandIndex]; ++slotIndex) {
				var points = FindBestFixedSlot(row, reserved, band.MaxP);
				if (points.Count != band.MaxP) {
					break;
				}

				slots.Add(new FixedSlot(band, points));
				foreach (var (x, y) in points) {
					reserved[x, y] = true;
				}
			}
		}

		return slots;
	}

	// Plan3のスロットには触れず、残った領域を4～31人用スロットで可能な限り埋める。
	internal static List<FixedSlot> CreateFixedSlotsForPlan4(string[] row)
	{
		var slots = CreateFixedSlots(row);
		var reserved = new bool[row.Length, row[0].Length];
		foreach (var slot in slots) {
			foreach (var (x, y) in slot.Points) {
				reserved[x, y] = true;
			}
		}

		var probabilities = CalculateGuestSizeBandProbabilities();
		var additionalCounts = new int[3];
		var enabled = new bool[3] { true, true, true };
		while (enabled.Any(value => value)) {
			int selectedBand = -1;
			double selectedRatio = double.MaxValue;
			for (int bandIndex = 0; bandIndex < 3; ++bandIndex) {
				if (enabled[bandIndex] == false) {
					continue;
				}

				double ratio = (additionalCounts[bandIndex] + 1) / probabilities[bandIndex];
				if (ratio < selectedRatio) {
					selectedBand = bandIndex;
					selectedRatio = ratio;
				}
			}

			var band = GuestSizeBands[selectedBand];
			var points = FindBestFixedSlot(row, reserved, band.MaxP);
			if (points.Count != band.MaxP) {
				enabled[selectedBand] = false;
				continue;
			}

			slots.Add(new FixedSlot(band, points));
			++additionalCounts[selectedBand];
			foreach (var (x, y) in points) {
				reserved[x, y] = true;
			}
		}

		return slots;
	}

	// 小サイズ追加スロットの比率を決めるため、各人数帯の理論出現確率を求める。
	private static double[] CalculateGuestSizeBandProbabilities()
	{
		double minSource = 2.0;
		double maxSource = Math.Sqrt(150.0);
		double sourceRange = maxSource - minSource;
		var probabilities = new double[GuestSizeBands.Length];
		for (int bandIndex = 0; bandIndex < GuestSizeBands.Length; ++bandIndex) {
			var band = GuestSizeBands[bandIndex];
			for (int p = band.MinP; p <= band.MaxP; ++p) {
				double lower = Math.Max(minSource, Math.Sqrt(Math.Max(0, p - 0.5)));
				double upper = Math.Min(maxSource, Math.Sqrt(p + 0.5));
				probabilities[bandIndex] += Math.Max(0, upper - lower) / sourceRange;
			}
		}

		return probabilities;
	}

	// 池際を使いつつ形を崩しすぎないよう、複数の成長結果から最良候補を選ぶ。
	private static List<(int x, int y)> FindBestFixedSlot(
		string[] row,
		bool[,] reserved,
		int size)
	{
		int height = row.Length;
		int width = row[0].Length;
		var seeds = new List<(int x, int y, int pondEdges)>();
		for (int x = 0; x < height; ++x) {
			for (int y = 0; y < width; ++y) {
				if (row[x][y] == '.' && reserved[x, y] == false) {
					seeds.Add((x, y, CountPondEdges(row, x, y)));
				}
			}
		}

		seeds.Sort((left, right) => {
			int compare = right.pondEdges.CompareTo(left.pondEdges);
			if (compare != 0) {
				return compare;
			}

			compare = left.x.CompareTo(right.x);
			return compare != 0 ? compare : left.y.CompareTo(right.y);
		});

		List<(int x, int y)> bestPoints = [];
		double bestScore = double.MinValue;
		int seedCount = Math.Min(seeds.Count, 256);
		for (int seedIndex = 0; seedIndex < seedCount; ++seedIndex) {
			var seed = seeds[seedIndex];
			var points = GrowFixedSlot(row, reserved, seed.x, seed.y, size);
			if (points.Count != size) {
				continue;
			}

			double compactness = CalculateCompactness(points);
			int pondEdges = points.Sum(point => CountPondEdges(row, point.x, point.y));
			int minX = points.Min(point => point.x);
			int maxX = points.Max(point => point.x);
			int minY = points.Min(point => point.y);
			int maxY = points.Max(point => point.y);
			double fillRate = (double)size / ((maxX - minX + 1) * (maxY - minY + 1));
			double score = compactness
				+ 0.20 * pondEdges / (4.0 * size)
				+ 0.05 * fillRate;
			if (score > bestScore) {
				bestScore = score;
				bestPoints = points;
			}
		}

		return bestPoints;
	}

	// 追加済みセルとの隣接数を優先して成長し、すべての接頭辞を連結に保つ。
	private static List<(int x, int y)> GrowFixedSlot(
		string[] row,
		bool[,] reserved,
		int startX,
		int startY,
		int size)
	{
		int height = row.Length;
		int width = row[0].Length;
		var selected = new bool[height, width];
		var points = new List<(int x, int y)>(size) { (startX, startY) };
		var candidates = new PriorityQueue<(int x, int y), (int adjacency, int pondEdges, int area, int order)>();
		selected[startX, startY] = true;
		int minX = startX;
		int maxX = startX;
		int minY = startY;
		int maxY = startY;
		int order = 0;

		EnqueueNeighbors(startX, startY);
		while (points.Count < size && candidates.Count > 0) {
			var (x, y) = candidates.Dequeue();
			if (selected[x, y]) {
				continue;
			}

			selected[x, y] = true;
			points.Add((x, y));
			minX = Math.Min(minX, x);
			maxX = Math.Max(maxX, x);
			minY = Math.Min(minY, y);
			maxY = Math.Max(maxY, y);
			EnqueueNeighbors(x, y);
		}

		return points.Count == size ? points : [];

		void EnqueueNeighbors(int x, int y)
		{
			ReadOnlySpan<int> deltaX = [-1, 1, 0, 0];
			ReadOnlySpan<int> deltaY = [0, 0, -1, 1];
			for (int direction = 0; direction < 4; ++direction) {
				int nextX = x + deltaX[direction];
				int nextY = y + deltaY[direction];
				if ((uint)nextX >= (uint)height
					|| (uint)nextY >= (uint)width
					|| selected[nextX, nextY]
					|| row[nextX][nextY] != '.'
					|| reserved[nextX, nextY]) {
					continue;
				}

				int adjacency = CountSelectedNeighbors(selected, nextX, nextY);
				int pondEdges = CountPondEdges(row, nextX, nextY);
				int nextMinX = Math.Min(minX, nextX);
				int nextMaxX = Math.Max(maxX, nextX);
				int nextMinY = Math.Min(minY, nextY);
				int nextMaxY = Math.Max(maxY, nextY);
				int area = (nextMaxX - nextMinX + 1) * (nextMaxY - nextMinY + 1);
				candidates.Enqueue(
					(nextX, nextY),
					(-adjacency, -pondEdges, area, order++));
			}
		}
	}

	// 隣接数が多いセルほど周長の増加が小さいため、領域成長の優先度に使う。
	private static int CountSelectedNeighbors(bool[,] selected, int x, int y)
	{
		int height = selected.GetLength(0);
		int width = selected.GetLength(1);
		int count = 0;
		count += x > 0 && selected[x - 1, y] ? 1 : 0;
		count += x + 1 < height && selected[x + 1, y] ? 1 : 0;
		count += y > 0 && selected[x, y - 1] ? 1 : 0;
		count += y + 1 < width && selected[x, y + 1] ? 1 : 0;
		return count;
	}

	// 他の形状では使いにくい池際のマスを固定スロットへ取り込むための評価値。
	private static int CountPondEdges(string[] row, int x, int y)
	{
		int height = row.Length;
		int width = row[0].Length;
		int count = 0;
		count += x > 0 && row[x - 1][y] == '#' ? 1 : 0;
		count += x + 1 < height && row[x + 1][y] == '#' ? 1 : 0;
		count += y > 0 && row[x][y - 1] == '#' ? 1 : 0;
		count += y + 1 < width && row[x][y + 1] == '#' ? 1 : 0;
		return count;
	}

	// 行優先で埋めた長方形候補から、周長が短く正方形に近い形を選ぶ。
	internal static (List<(int x, int y)> points, double compactness) CreateNearSquareShape(int p)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(p);

		List<(int x, int y)> bestShape = [];
		int bestPerimeter = int.MaxValue;
		int bestImbalance = int.MaxValue;
		int bestUnusedArea = int.MaxValue;

		for (int width = 1; width <= p; ++width) {
			int height = (p + width - 1) / width;
			var shape = new List<(int x, int y)>(p);

			for (int index = 0; index < p; ++index) {
				shape.Add((index / width, index % width));
			}

			int perimeter = 2 * (height + width);
			int imbalance = Math.Abs(height - width);
			int unusedArea = height * width - p;

			if (perimeter < bestPerimeter
				|| perimeter == bestPerimeter && imbalance < bestImbalance
				|| perimeter == bestPerimeter && imbalance == bestImbalance
					&& unusedArea < bestUnusedArea) {
				bestShape = shape;
				bestPerimeter = perimeter;
				bestImbalance = imbalance;
				bestUnusedArea = unusedArea;
			}
		}

		double compactness = 4.0 * Math.Sqrt(p) / bestPerimeter;
		return (bestShape, compactness);
	}

	// 相対座標で表した形状を左上から平行移動し、最初の配置可能位置を返す。
	internal static List<(int x, int y)> FindPlacementByFullSearch(
		string[] row,
		bool[,] occupied,
		IReadOnlyList<(int x, int y)> shape)
	{
		if (shape.Count == 0) {
			return [];
		}

		int height = row.Length;
		int width = row[0].Length;

		if (occupied.GetLength(0) != height
			|| occupied.GetLength(1) != width) {
			throw new ArgumentException(
				"公園と占有状態の大きさが一致していません。",
				nameof(occupied));
		}

		int shapeHeight = 0;
		int shapeWidth = 0;
		foreach (var (x, y) in shape) {
			if (x < 0 || y < 0) {
				throw new ArgumentException(
					"形状の相対座標は非負である必要があります。",
					nameof(shape));
			}

			shapeHeight = Math.Max(shapeHeight, x + 1);
			shapeWidth = Math.Max(shapeWidth, y + 1);
		}

		for (int baseX = 0; baseX + shapeHeight <= height; ++baseX) {
			for (int baseY = 0; baseY + shapeWidth <= width; ++baseY) {
				bool canPlace = true;

				foreach (var (offsetX, offsetY) in shape) {
					int x = baseX + offsetX;
					int y = baseY + offsetY;

					if (row[x][y] != '.' || occupied[x, y]) {
						canPlace = false;
						break;
					}
				}

				if (canPlace == false) {
					continue;
				}

				var placement = new List<(int x, int y)>(shape.Count);
				foreach (var (offsetX, offsetY) in shape) {
					placement.Add((baseX + offsetX, baseY + offsetY));
				}

				return placement;
			}
		}

		return [];
	}

	// 公式サンプルと比較するため、左上の最初の空き連結成分だけをBFSする。
	internal static List<(int x, int y)> FindPlacementByBreadthFirstSearch(
		string[] row,
		bool[,] occupied,
		int p)
	{
		int n = row.Length;
		(int x, int y) start = (-1, -1);
		for (int x = 0; x < n && start.x < 0; ++x) {
			for (int y = 0; y < n; ++y) {
				if (row[x][y] == '.' && occupied[x, y] == false) {
					start = (x, y);
					break;
				}
			}
		}

		if (start.x < 0) {
			return [];
		}

		var visited = new bool[n, n];
		var queue = new Queue<(int x, int y)>();
		var points = new List<(int x, int y)>(p);
		visited[start.x, start.y] = true;
		queue.Enqueue(start);

		ReadOnlySpan<int> deltaX = [-1, 1, 0, 0];
		ReadOnlySpan<int> deltaY = [0, 0, -1, 1];
		while (queue.Count > 0 && points.Count < p) {
			var (x, y) = queue.Dequeue();
			points.Add((x, y));

			for (int direction = 0; direction < 4; ++direction) {
				int nextX = x + deltaX[direction];
				int nextY = y + deltaY[direction];
				if ((uint)nextX >= (uint)n
					|| (uint)nextY >= (uint)n
					|| visited[nextX, nextY]
					|| row[nextX][nextY] != '.'
					|| occupied[nextX, nextY]) {
					continue;
				}

				visited[nextX, nextY] = true;
				queue.Enqueue((nextX, nextY));
			}
		}

		return points.Count == p ? points : [];
	}

	// 定型形状が入らない隙間を使うため、右下から未探索の連結成分を順にBFSする。
	internal static List<(int x, int y)> FindPlacementByReverseBreadthFirstSearch(
		string[] row,
		bool[,] occupied,
		int p)
	{
		int n = row.Length;
		var visited = new bool[n, n];
		ReadOnlySpan<int> deltaX = [-1, 1, 0, 0];
		ReadOnlySpan<int> deltaY = [0, 0, -1, 1];

		for (int startX = n - 1; startX >= 0; --startX) {
			for (int startY = n - 1; startY >= 0; --startY) {
				if (visited[startX, startY]
					|| row[startX][startY] != '.'
					|| occupied[startX, startY]) {
					continue;
				}

				var queue = new Queue<(int x, int y)>();
				var points = new List<(int x, int y)>(p);
				visited[startX, startY] = true;
				queue.Enqueue((startX, startY));

				while (queue.Count > 0 && points.Count < p) {
					var (x, y) = queue.Dequeue();
					points.Add((x, y));

					for (int direction = 0; direction < 4; ++direction) {
						int nextX = x + deltaX[direction];
						int nextY = y + deltaY[direction];
						if ((uint)nextX >= (uint)n
							|| (uint)nextY >= (uint)n
							|| visited[nextX, nextY]
							|| row[nextX][nextY] != '.'
							|| occupied[nextX, nextY]) {
							continue;
						}

						visited[nextX, nextY] = true;
						queue.Enqueue((nextX, nextY));
					}
				}

				if (points.Count == p) {
					return points;
				}
			}
		}

		return [];
	}

	// Pを収容する目標矩形を先に固定し、極端に細長い形を作らずに矩形内でPマスを選ぶ。
	internal static List<(int x, int y)> FindPlacementByReverseBreadthFirstSearch2(
		string[] row,
		bool[,] occupied,
		int p)
	{
		int height = row.Length;
		int width = row[0].Length;
		var occupiedPrefixSum = new int[height + 1, width + 1];
		var pondPrefixSum = new int[height + 1, width + 1];
		for (int x = 0; x < height; ++x) {
			int occupiedRowSum = 0;
			int pondRowSum = 0;
			for (int y = 0; y < width; ++y) {
				occupiedRowSum += occupied[x, y] ? 1 : 0;
				pondRowSum += row[x][y] == '#' ? 1 : 0;
				occupiedPrefixSum[x + 1, y + 1] = occupiedPrefixSum[x, y + 1] + occupiedRowSum;
				pondPrefixSum[x + 1, y + 1] = pondPrefixSum[x, y + 1] + pondRowSum;
			}
		}

		var rectangleCandidates = new List<(int boxHeight, int boxWidth, int perimeter, int unusedArea, int imbalance)>();
		int maximumArea = Math.Min(height * width, p + 32);
		for (int boxHeight = 1; boxHeight <= height; ++boxHeight) {
			for (int boxWidth = 1; boxWidth <= width; ++boxWidth) {
				int area = boxHeight * boxWidth;
				if (area < p
					|| area > maximumArea
					|| Math.Max(boxHeight, boxWidth) > 2 * Math.Min(boxHeight, boxWidth)) {
					continue;
				}

				rectangleCandidates.Add((
					boxHeight,
					boxWidth,
					2 * (boxHeight + boxWidth),
					area - p,
					Math.Abs(boxHeight - boxWidth)));
			}
		}

		// 矩形自体の周長を最優先にし、同値なら無駄面積と縦横差が小さいものから試す。
		rectangleCandidates.Sort((left, right) => {
			int compare = left.perimeter.CompareTo(right.perimeter);
			if (compare != 0) {
				return compare;
			}

			compare = left.unusedArea.CompareTo(right.unusedArea);
			return compare != 0 ? compare : left.imbalance.CompareTo(right.imbalance);
		});

		foreach (var rectangle in rectangleCandidates) {
			var positions = new List<(int top, int left, int occupiedCount, int pondCount)>();
			for (int top = height - rectangle.boxHeight; top >= 0; --top) {
				for (int left = width - rectangle.boxWidth; left >= 0; --left) {
					int bottom = top + rectangle.boxHeight;
					int right = left + rectangle.boxWidth;
					int occupiedCount = occupiedPrefixSum[bottom, right]
						- occupiedPrefixSum[top, right]
						- occupiedPrefixSum[bottom, left]
						+ occupiedPrefixSum[top, left];
					int pondCount = pondPrefixSum[bottom, right]
						- pondPrefixSum[top, right]
						- pondPrefixSum[bottom, left]
						+ pondPrefixSum[top, left];

					if (rectangle.boxHeight * rectangle.boxWidth - occupiedCount - pondCount >= p) {
						positions.Add((top, left, occupiedCount, pondCount));
					}
				}
			}

			// 他グループを囲う形は退去後の穴を生むため、池よりも占有マスを強く避ける。
			positions.Sort((left, right) => {
				int compare = left.occupiedCount.CompareTo(right.occupiedCount);
				if (compare != 0) {
					return compare;
				}

				compare = left.pondCount.CompareTo(right.pondCount);
				if (compare != 0) {
					return compare;
				}

				compare = right.top.CompareTo(left.top);
				return compare != 0 ? compare : right.left.CompareTo(left.left);
			});

			foreach (var position in positions) {
				var points = FindCompactRegionInRectangle(
					row,
					occupied,
					position.top,
					position.left,
					rectangle.boxHeight,
					rectangle.boxWidth,
					p);
				if (points.Count == p) {
					return points;
				}
			}
		}

		return [];
	}

	// 目標矩形内の空き連結成分を一度ずつ列挙し、Pマス以上ある成分だけを領域成長へ渡す。
	private static List<(int x, int y)> FindCompactRegionInRectangle(
		string[] row,
		bool[,] occupied,
		int top,
		int left,
		int boxHeight,
		int boxWidth,
		int p)
	{
		int height = row.Length;
		int width = row[0].Length;
		var visited = new bool[height, width];
		ReadOnlySpan<int> deltaX = [-1, 1, 0, 0];
		ReadOnlySpan<int> deltaY = [0, 0, -1, 1];

		for (int startX = top + boxHeight - 1; startX >= top; --startX) {
			for (int startY = left + boxWidth - 1; startY >= left; --startY) {
				if (visited[startX, startY]
					|| row[startX][startY] != '.'
					|| occupied[startX, startY]) {
					continue;
				}

				var component = new List<(int x, int y)>();
				var queue = new Queue<(int x, int y)>();
				visited[startX, startY] = true;
				queue.Enqueue((startX, startY));
				while (queue.Count > 0) {
					var (x, y) = queue.Dequeue();
					component.Add((x, y));
					for (int direction = 0; direction < 4; ++direction) {
						int nextX = x + deltaX[direction];
						int nextY = y + deltaY[direction];
						if (nextX < top
							|| nextX >= top + boxHeight
							|| nextY < left
							|| nextY >= left + boxWidth
							|| visited[nextX, nextY]
							|| row[nextX][nextY] != '.'
							|| occupied[nextX, nextY]) {
							continue;
						}

						visited[nextX, nextY] = true;
						queue.Enqueue((nextX, nextY));
					}
				}

				if (component.Count >= p) {
					// 中央付近から成長させると、矩形の片側だけを使う細長い接頭辞になりにくい。
					int centerX2 = 2 * top + boxHeight - 1;
					int centerY2 = 2 * left + boxWidth - 1;
					var seed = component.MinBy(point => (
						Math.Abs(2 * point.x - centerX2) + Math.Abs(2 * point.y - centerY2),
						-point.x,
						-point.y));
					return GrowCompactRegion(component, seed.x, seed.y, height, width, p);
				}
			}
		}

		return [];
	}

	// 選択済み領域に接する候補を毎回再評価し、局所的な周長増加が小さい順に追加する。
	private static List<(int x, int y)> GrowCompactRegion(
		IReadOnlyCollection<(int x, int y)> component,
		int startX,
		int startY,
		int height,
		int width,
		int p)
	{
		var inComponent = new bool[height, width];
		foreach (var (x, y) in component) {
			inComponent[x, y] = true;
		}

		var selected = new bool[height, width];
		var frontier = new HashSet<(int x, int y)>();
		var points = new List<(int x, int y)>(p) { (startX, startY) };
		selected[startX, startY] = true;
		int minX = startX;
		int maxX = startX;
		int minY = startY;
		int maxY = startY;

		AddNeighborsToFrontier(startX, startY);
		while (points.Count < p) {
			(int x, int y) best = (-1, -1);
			(int neighborPriority, int areaIncrease, int boxPerimeter, int imbalance, int xPriority, int yPriority) bestPriority
				= (int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
			int currentArea = (maxX - minX + 1) * (maxY - minY + 1);

			foreach (var candidate in frontier) {
				int selectedNeighbors = CountSelectedNeighbors(selected, candidate.x, candidate.y);
				int nextMinX = Math.Min(minX, candidate.x);
				int nextMaxX = Math.Max(maxX, candidate.x);
				int nextMinY = Math.Min(minY, candidate.y);
				int nextMaxY = Math.Max(maxY, candidate.y);
				int boxHeight = nextMaxX - nextMinX + 1;
				int boxWidth = nextMaxY - nextMinY + 1;

				// 隣接辺が1本増えるごとに追加後の周長は2減るため、隣接数を最優先する。
				var priority = (
					neighborPriority: -selectedNeighbors,
					areaIncrease: boxHeight * boxWidth - currentArea,
					boxPerimeter: 2 * (boxHeight + boxWidth),
					imbalance: Math.Abs(boxHeight - boxWidth),
					xPriority: -candidate.x,
					yPriority: -candidate.y);
				if (priority.CompareTo(bestPriority) < 0) {
					best = candidate;
					bestPriority = priority;
				}
			}

			// component全体が連結でPマス以上あるため、Pマス選ぶまではfrontierが空にならない。
			frontier.Remove(best);
			selected[best.x, best.y] = true;
			points.Add(best);
			minX = Math.Min(minX, best.x);
			maxX = Math.Max(maxX, best.x);
			minY = Math.Min(minY, best.y);
			maxY = Math.Max(maxY, best.y);
			AddNeighborsToFrontier(best.x, best.y);
		}

		return points;

		void AddNeighborsToFrontier(int x, int y)
		{
			ReadOnlySpan<int> deltaX = [-1, 1, 0, 0];
			ReadOnlySpan<int> deltaY = [0, 0, -1, 1];
			for (int direction = 0; direction < 4; ++direction) {
				int nextX = x + deltaX[direction];
				int nextY = y + deltaY[direction];
				if ((uint)nextX < (uint)height
					&& (uint)nextY < (uint)width
					&& inComponent[nextX, nextY]
					&& selected[nextX, nextY] == false) {
					frontier.Add((nextX, nextY));
				}
			}
		}
	}

	// 領域外との境界を数え、問題定義どおり 4*sqrt(P)/L を計算する。
	internal static double CalculateCompactness(IReadOnlyCollection<(int x, int y)> points)
	{
		var cells = points.ToHashSet();
		int perimeter = 0;
		foreach (var (x, y) in points) {
			perimeter += cells.Contains((x - 1, y)) ? 0 : 1;
			perimeter += cells.Contains((x + 1, y)) ? 0 : 1;
			perimeter += cells.Contains((x, y - 1)) ? 0 : 1;
			perimeter += cells.Contains((x, y + 1)) ? 0 : 1;
		}

		return 4.0 * Math.Sqrt(points.Count) / perimeter;
	}
}

// 現在の占有状態・利用中グループ・所持金を管理する。探索方針の選択はPlan側で行う。
public class State
{
	private readonly bool _isLocal;
	private readonly Input _input;
	// trueのマスは現在いずれかの利用中グループが占有している。
	private readonly bool[,] _occupied;
	// 退去時の解放と利用料加算に必要な情報だけを、受け入れ済みグループごとに保持する。
	private readonly Dictionary<int, (int departureTime, long usageFee, List<(int x, int y)> points, double compactness)> _activeGroups = [];
	// 到着ごとに全グループを走査せず、退去時刻が早い順に処理する。
	private readonly PriorityQueue<int, int> _departureQueue = new();
	private long _money;

	public int AcceptedCount { get; private set; } = 0;
	public double ComactnessSum { get; private set; } = 0;

	public State(bool isLocal, Input input)
	{
		_isLocal = isLocal;
		_input = input;
		_occupied = new bool[input.N, input.N];
	}

	// Utilsへ現在盤面を渡すための薄い窓口。戦略上の分岐は持たない。
	public List<(int x, int y)> FindPlacementByBreadthFirstSearch(int p)
	{
		return Utils.FindPlacementByBreadthFirstSearch(_input.Row, _occupied, p);
	}

	public List<(int x, int y)> FindPlacementByReverseBreadthFirstSearch(int p)
	{
		return Utils.FindPlacementByReverseBreadthFirstSearch(_input.Row, _occupied, p);
	}

	// Plan5用のコンパクト領域成長へ現在の占有盤面を渡す。
	public List<(int x, int y)> FindPlacementByReverseBreadthFirstSearch2(int p)
	{
		return Utils.FindPlacementByReverseBreadthFirstSearch2(_input.Row, _occupied, p);
	}

	public List<(int x, int y)> FindPlacementByFullSearch(
		IReadOnlyList<(int x, int y)> shape)
	{
		return Utils.FindPlacementByFullSearch(_input.Row, _occupied, shape);
	}

	// 小さいPでもスロット全体を予約扱いにし、同じスロットの同時利用を防ぐ。
	public List<(int x, int y)> FindPlacementByFixedSlots(
		IReadOnlyList<FixedSlot> slots,
		int p)
	{
		foreach (var slot in slots) {
			if (p < slot.Band.MinP || slot.Band.MaxP < p) {
				continue;
			}

			bool isAvailable = true;
			foreach (var (x, y) in slot.Points) {
				if (_occupied[x, y]) {
					isAvailable = false;
					break;
				}
			}

			if (isAvailable) {
				var points = new List<(int x, int y)>(p);
				for (int pointIndex = 0; pointIndex < p; ++pointIndex) {
					points.Add(slot.Points[pointIndex]);
				}

				return points;
			}
		}

		return [];
	}

	// Planが決めた配置結果を状態へ反映し、インタラクティブ出力を確定する。
	public void ProcessGuest(
		(int i, int s, int t, int p, int v) guest,
		bool canAccept,
		List<(int x, int y)> points)
	{
		if (canAccept) {
			double compactness = Utils.CalculateCompactness(points);
			Accept(guest.i, guest.t, guest.v, points, compactness);
			++AcceptedCount;
			ComactnessSum += compactness;
		}

		_input.OutputReception(canAccept, points);
	}

	// 利用料は移動しない各Planでは配置時に確定するため、退去時用に保存しておく。
	private void Accept(int groupId, int departureTime, int value, List<(int x, int y)> points, double compactness)
	{
		foreach (var (x, y) in points) {
			_occupied[x, y] = true;
		}

		long usageFee = CalculateUsageFee(value, compactness);
		_activeGroups.Add(groupId, (departureTime, usageFee, points, compactness));
		_departureQueue.Enqueue(groupId, departureTime);
	}

	private static long CalculateUsageFee(int value, double compactness)
	{
		return (long)Math.Floor(value * compactness + 0.5);
	}

	// 問題の時刻順序に従い、T_j < currentTime のグループを解放して利用料を加算する。
	public void ReleaseDepartedGroups(int currentTime)
	{
		while (_departureQueue.TryPeek(out int groupId, out int departureTime)
			&& departureTime < currentTime) {
			_departureQueue.Dequeue();
			var group = _activeGroups[groupId];
			foreach (var (x, y) in group.points) {
				_occupied[x, y] = false;
			}

			_money += group.usageFee;
			_activeGroups.Remove(groupId);
		}
	}

	// 最終退去処理は各Planが済ませるため、ここでは現在の絶対スコアだけを返す。
	public long Calculate()
	{
		return Math.Max(0, _money);
	}
}

public class Information
{
	public long Time { get; set; } = 0;
	public long Score { get; set; } = 0;
	public int Loop { get; set; } = 0;
	public int Up { get; set; } = 0;
	public long Elapsed { get; set; } = 0;

	public int PondCount { get; set; } = 0;
	public int Accept { get; set; } = 0;
	public double CompactnessAverage { get; set; } = 0;
	public double ZAverage { get; set; } = 0;
}

public class Input
{
	private readonly IOManager _io;
	private readonly bool _isLocal;

	public int N { get; }
	public int M { get; }
	public double R { get; }
	public string[] Row { get; }

	public Input(IOManager io, bool isLocal)
	{
		_io = io;
		_isLocal = isLocal;

		(N, M) = io.Int2();
		R = io.Double();
		Row = io.ArrayString(N);
	}

	public (int i, int s, int t, int p, int v) InputGuest()
	{
		return _io.Int5();
	}

	public void OutputMove(List<(int j, List<(int z, int w)> points)> moveGroups)
	{
		_io.WriteLine(moveGroups.Count);
		foreach (var (j, points) in moveGroups) {
			_io.WriteLine(j);
			foreach (var (z, w) in points) {
				_io.WriteLine($"{z} {w}");
			}
		}
	}

	public void OutputReception(bool takes, List<(int x, int y)> points)
	{
		if (takes) {
			_io.WriteLine("Yes");
			foreach (var (x, y) in points) {
				_io.WriteLine($"{x} {y}");
			}
		} else {
			_io.WriteLine("No");
		}

		_io.Flush();
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