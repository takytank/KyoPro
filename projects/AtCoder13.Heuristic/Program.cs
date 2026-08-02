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
		double sumR = 0;

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
						$"rate={info.Rate:f3}",
						$"elapsed={info.Elapsed:d4}",
					};

					sumA += info.Accept;
					sumC += info.CompactnessAverage;
					sumZ += info.ZAverage;
					sumR += info.Rate;

					Console.WriteLine(status.Join(" "));
					Console.Out.Flush();
				}

				return (info.Score, info.Loop, info.Up);
			},
			(tc, rc) => {
				double averageA = sumA / rc;
				double averageC = sumC / rc;
				double averageZ = sumZ / rc;
				double averageR = sumR / rc;
				Console.WriteLine($"accept average: {averageA:f3}");
				Console.WriteLine($"compactness average: {averageC:f3}");
				Console.WriteLine($"Z average: {averageZ:f6}");
				Console.WriteLine($"rate average: {averageR:f6}");
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

		var best = Plan6(isLocal, sw, rnd, input, info);

		info.Accept = best.AcceptedCount;
		info.CompactnessAverage = best.ComactnessSum / best.AcceptedCount;
		info.ZAverage /= best.ComactnessSum;
		info.Score = best.Calculate();
		info.Rate = (double)info.Score / input.ScoreLimit;
		info.Elapsed = sw.ElapsedMilliseconds;

		return (input, info);
	}

	// Plan5と同じ受理基準を使い、定型形状については全候補から空き領域を残しやすい位置を選ぶ。
	static State Plan6(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();
			state.ReleaseDepartedGroups(guest.s);
			input.OutputMove([]);

			const double Bottom20PercentZ = -0.673297;
			double z = Utils.CalculateValueZScore(guest);
			if (z < Bottom20PercentZ) {
				state.ProcessGuest(guest, false, []);
				continue;
			}

			var (shape, _) = Utils.CreateNearSquareShape(guest.p);
			var points = state.FindPlacementByBestFullSearch(shape);
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

	// 行優先で埋めた長方形候補から、周長が短く正方形に近い形を選ぶ。
	internal static (List<(int x, int y)> points, double compactness) CreateNearSquareShape(int p)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(p);

		int bestWidth = 0;
		int bestPerimeter = int.MaxValue;
		int bestImbalance = int.MaxValue;
		int bestUnusedArea = int.MaxValue;

		for (int width = 1; width <= p; ++width) {
			int height = (p + width - 1) / width;
			int perimeter = 2 * (height + width);
			int imbalance = Math.Abs(height - width);
			int unusedArea = height * width - p;

			if (perimeter < bestPerimeter
				|| perimeter == bestPerimeter && imbalance < bestImbalance
				|| perimeter == bestPerimeter && imbalance == bestImbalance
					&& unusedArea < bestUnusedArea) {
				bestWidth = width;
				bestPerimeter = perimeter;
				bestImbalance = imbalance;
				bestUnusedArea = unusedArea;
			}
		}

		var bestShape = new List<(int x, int y)>(p);
		for (int index = 0; index < p; ++index) {
			bestShape.Add((index / bestWidth, index % bestWidth));
		}

		double compactness = 4.0 * Math.Sqrt(p) / bestPerimeter;
		return (bestShape, compactness);
	}


	// 全ての配置可能位置を調べ、池・盤面外・既存占有領域に接する辺が多い候補を選ぶ。
	// 使いにくい境界側を優先して消費することで、連続した空き領域を残す。
	internal static List<(int x, int y)> FindPlacementByBestFullSearch(
		string[] row,
		bool[,] occupied,
		IReadOnlyList<(int x, int y)> shape)
	{
		if (shape.Count == 0) {
			return [];
		}

		int height = row.Length;
		int width = row[0].Length;
		int shapeHeight = 0;
		int shapeWidth = 0;
		bool isRowMajorRectanglePrefix = true;
		for (int index = 0; index < shape.Count; ++index) {
			var (x, y) = shape[index];
			shapeHeight = Math.Max(shapeHeight, x + 1);
			shapeWidth = Math.Max(shapeWidth, y + 1);
		}
		for (int index = 0; index < shape.Count; ++index) {
			var (x, y) = shape[index];
			if (x != index / shapeWidth || y != index % shapeWidth) {
				isRowMajorRectanglePrefix = false;
				break;
			}
		}

		// CreateNearSquareShapeが返す行優先の矩形プレフィックスは高速経路で処理する。
		// それ以外の形状も利用できるよう、下では汎用探索へフォールバックする。
		if (isRowMajorRectanglePrefix) {
			return FindPlacementByBestRectanglePrefixSearch(
				row,
				occupied,
				shape.Count,
				shapeHeight,
				shapeWidth);
		}

		return FindPlacementByBestGenericFullSearch(row, occupied, shape, shapeHeight, shapeWidth);
	}

	private static List<(int x, int y)> FindPlacementByBestRectanglePrefixSearch(
		string[] row,
		bool[,] occupied,
		int shapeCount,
		int shapeHeight,
		int shapeWidth)
	{
		int height = row.Length;
		int width = row[0].Length;
		int prefixWidth = width + 1;
		var blockedPrefixSum = new int[(height + 1) * prefixWidth];
		// 芝生以外または占有済みのマス数の二次元累積和。
		// 各候補に対する配置可能性を矩形和二回で判定するために使う。
		for (int x = 0; x < height; ++x) {
			int rowSum = 0;
			int previousRowOffset = x * prefixWidth;
			int currentRowOffset = previousRowOffset + prefixWidth;
			for (int y = 0; y < width; ++y) {
				rowSum += row[x][y] != '.' || occupied[x, y] ? 1 : 0;
				blockedPrefixSum[currentRowOffset + y + 1]
					= blockedPrefixSum[previousRowOffset + y + 1] + rowSum;
			}
		}

		int completeRowCount = shapeHeight - 1;
		int lastRowWidth = shapeCount - completeRowCount * shapeWidth;
		int bestBaseX = -1;
		int bestBaseY = -1;
		int bestBoundaryEdges = -1;
		for (int baseX = 0; baseX + shapeHeight <= height; ++baseX) {
			for (int baseY = 0; baseY + shapeWidth <= width; ++baseY) {
				int topRowOffset = baseX * prefixWidth;
				int completeBottomRowOffset = (baseX + completeRowCount) * prefixWidth;
				int right = baseY + shapeWidth;
				int blockedCount = blockedPrefixSum[completeBottomRowOffset + right]
					- blockedPrefixSum[topRowOffset + right]
					- blockedPrefixSum[completeBottomRowOffset + baseY]
					+ blockedPrefixSum[topRowOffset + baseY];
				int lastRowOffset = completeBottomRowOffset;
				int lastRowBottomOffset = lastRowOffset + prefixWidth;
				int lastRowRight = baseY + lastRowWidth;
				blockedCount += blockedPrefixSum[lastRowBottomOffset + lastRowRight]
					- blockedPrefixSum[lastRowOffset + lastRowRight]
					- blockedPrefixSum[lastRowBottomOffset + baseY]
					+ blockedPrefixSum[lastRowOffset + baseY];
				if (blockedCount != 0) {
					continue;
				}

				// 配置可能な候補だけについて、形状の外周が既存の障害物に接する辺数を評価する。
				int boundaryEdges = CountRectanglePrefixBoundaryEdges(
					row, occupied, baseX, baseY, shapeHeight, shapeWidth, lastRowWidth);
				if (boundaryEdges > bestBoundaryEdges) {
					bestBoundaryEdges = boundaryEdges;
					bestBaseX = baseX;
					bestBaseY = baseY;
				}
			}
		}

		if (bestBaseX < 0) {
			return [];
		}

		var bestPlacement = new List<(int x, int y)>(shapeCount);
		for (int index = 0; index < shapeCount; ++index) {
			bestPlacement.Add((bestBaseX + index / shapeWidth, bestBaseY + index % shapeWidth));
		}
		return bestPlacement;
	}

	private static int CountRectanglePrefixBoundaryEdges(
		string[] row,
		bool[,] occupied,
		int baseX,
		int baseY,
		int shapeHeight,
		int shapeWidth,
		int lastRowWidth)
	{
		int boundaryEdges = 0;
		int lastX = baseX + shapeHeight - 1;
		// 行優先の矩形プレフィックスでは、内部辺を除く外周辺だけを数えればよい。
		// 最終行が短い場合は、その欠けた部分に面する直上行の下辺も外周になる。
		for (int y = 0; y < shapeWidth; ++y) {
			boundaryEdges += IsBlocked(row, occupied, baseX - 1, baseY + y) ? 1 : 0;
		}
		for (int y = 0; y < lastRowWidth; ++y) {
			boundaryEdges += IsBlocked(row, occupied, lastX + 1, baseY + y) ? 1 : 0;
		}
		for (int x = 0; x < shapeHeight; ++x) {
			boundaryEdges += IsBlocked(row, occupied, baseX + x, baseY - 1) ? 1 : 0;
		}
		for (int x = 0; x < shapeHeight - 1; ++x) {
			boundaryEdges += IsBlocked(row, occupied, baseX + x, baseY + shapeWidth) ? 1 : 0;
		}
		boundaryEdges += IsBlocked(row, occupied, lastX, baseY + lastRowWidth) ? 1 : 0;
		if (lastRowWidth < shapeWidth) {
			for (int y = lastRowWidth; y < shapeWidth; ++y) {
				boundaryEdges += IsBlocked(row, occupied, lastX, baseY + y) ? 1 : 0;
			}
		}
		return boundaryEdges;
	}

	private static bool IsBlocked(string[] row, bool[,] occupied, int x, int y)
	{
		// 盤面外も問題文の「領域外との境界」として扱う。
		return (uint)x >= (uint)row.Length
			|| (uint)y >= (uint)row[0].Length
			|| row[x][y] != '.'
			|| occupied[x, y];
	}

	private static List<(int x, int y)> FindPlacementByBestGenericFullSearch(
		string[] row,
		bool[,] occupied,
		IReadOnlyList<(int x, int y)> shape,
		int shapeHeight,
		int shapeWidth)
	{
		// 高速経路の前提を満たさない任意形状向けの、従来どおり全セルを調べる経路。
		int height = row.Length;
		int width = row[0].Length;
		var shapeCells = new HashSet<(int x, int y)>(shape);
		int bestBaseX = -1;
		int bestBaseY = -1;
		int bestBoundaryEdges = -1;
		ReadOnlySpan<int> deltaX = [-1, 1, 0, 0];
		ReadOnlySpan<int> deltaY = [0, 0, -1, 1];

		for (int baseX = 0; baseX + shapeHeight <= height; ++baseX) {
			for (int baseY = 0; baseY + shapeWidth <= width; ++baseY) {
				bool canPlace = true;
				foreach (var (offsetX, offsetY) in shape) {
					if (row[baseX + offsetX][baseY + offsetY] != '.'
						|| occupied[baseX + offsetX, baseY + offsetY]) {
						canPlace = false;
						break;
					}
				}
				if (canPlace == false) {
					continue;
				}

				int boundaryEdges = 0;
				foreach (var (offsetX, offsetY) in shape) {
					for (int direction = 0; direction < 4; ++direction) {
						int nextOffsetX = offsetX + deltaX[direction];
						int nextOffsetY = offsetY + deltaY[direction];
						if (shapeCells.Contains((nextOffsetX, nextOffsetY)) == false
							&& IsBlocked(row, occupied, baseX + nextOffsetX, baseY + nextOffsetY)) {
							++boundaryEdges;
						}
					}
				}
				if (boundaryEdges > bestBoundaryEdges) {
					bestBoundaryEdges = boundaryEdges;
					bestBaseX = baseX;
					bestBaseY = baseY;
				}
			}
		}

		if (bestBaseX < 0) {
			return [];
		}
		var bestPlacement = new List<(int x, int y)>(shape.Count);
		foreach (var (x, y) in shape) {
			bestPlacement.Add((bestBaseX + x, bestBaseY + y));
		}
		return bestPlacement;
	}

	// Pを収容する目標矩形を先に固定し、極端に細長い形を作らずに矩形内でPマスを選ぶ。
	internal static List<(int x, int y)> FindPlacementByReverseBreadthFirstSearch2(
		string[] row,
		bool[,] occupied,
		int p)
	{
		var pondPrefixSum = CreatePondPrefixSum(row);
		var rectangleCandidates = CreateRectangleCandidates(row.Length, row[0].Length, p);
		return FindPlacementByReverseBreadthFirstSearch2(
			row,
			occupied,
			p,
			pondPrefixSum,
			rectangleCandidates);
	}

	internal static int[] CreatePondPrefixSum(string[] row)
	{
		int height = row.Length;
		int width = row[0].Length;
		int prefixWidth = width + 1;
		var pondPrefixSum = new int[(height + 1) * prefixWidth];
		for (int x = 0; x < height; ++x) {
			int pondRowSum = 0;
			int previousRowOffset = x * prefixWidth;
			int currentRowOffset = previousRowOffset + prefixWidth;
			for (int y = 0; y < width; ++y) {
				pondRowSum += row[x][y] == '#' ? 1 : 0;
				int currentIndex = currentRowOffset + y + 1;
				int previousIndex = previousRowOffset + y + 1;
				pondPrefixSum[currentIndex] = pondPrefixSum[previousIndex] + pondRowSum;
			}
		}
		return pondPrefixSum;
	}

	internal static (int boxHeight, int boxWidth, int perimeter, int unusedArea, int imbalance)[]
		CreateRectangleCandidates(int height, int width, int p)
	{
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
		return rectangleCandidates.ToArray();
	}

	internal static List<(int x, int y)> FindPlacementByReverseBreadthFirstSearch2(
		string[] row,
		bool[,] occupied,
		int p,
		int[] pondPrefixSum,
		IReadOnlyList<(int boxHeight, int boxWidth, int perimeter, int unusedArea, int imbalance)> rectangleCandidates)
	{
		int height = row.Length;
		int width = row[0].Length;
		int prefixWidth = width + 1;
		var occupiedPrefixSum = new int[(height + 1) * prefixWidth];
		for (int x = 0; x < height; ++x) {
			int occupiedRowSum = 0;
			int previousRowOffset = x * prefixWidth;
			int currentRowOffset = previousRowOffset + prefixWidth;
			for (int y = 0; y < width; ++y) {
				occupiedRowSum += occupied[x, y] ? 1 : 0;
				occupiedPrefixSum[currentRowOffset + y + 1]
					= occupiedPrefixSum[previousRowOffset + y + 1] + occupiedRowSum;
			}
		}

		foreach (var rectangle in rectangleCandidates) {
			var positions = new List<(int top, int left, int occupiedCount, int pondCount)>();
			for (int top = height - rectangle.boxHeight; top >= 0; --top) {
				for (int left = width - rectangle.boxWidth; left >= 0; --left) {
					int bottom = top + rectangle.boxHeight;
					int right = left + rectangle.boxWidth;
					int bottomRowOffset = bottom * prefixWidth;
					int topRowOffset = top * prefixWidth;
					int occupiedCount = occupiedPrefixSum[bottomRowOffset + right]
						- occupiedPrefixSum[topRowOffset + right]
						- occupiedPrefixSum[bottomRowOffset + left]
						+ occupiedPrefixSum[topRowOffset + left];
					int pondCount = pondPrefixSum[bottomRowOffset + right]
						- pondPrefixSum[topRowOffset + right]
						- pondPrefixSum[bottomRowOffset + left]
						+ pondPrefixSum[topRowOffset + left];

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
		int boxArea = boxHeight * boxWidth;
		var visited = new bool[boxArea];
		var queue = new (int x, int y)[boxArea];
		var component = new List<(int x, int y)>(boxArea);
		ReadOnlySpan<int> deltaX = [-1, 1, 0, 0];
		ReadOnlySpan<int> deltaY = [0, 0, -1, 1];

		for (int startX = top + boxHeight - 1; startX >= top; --startX) {
			for (int startY = left + boxWidth - 1; startY >= left; --startY) {
				int startIndex = (startX - top) * boxWidth + startY - left;
				if (visited[startIndex]
					|| row[startX][startY] != '.'
					|| occupied[startX, startY]) {
					continue;
				}

				component.Clear();
				int queueHead = 0;
				int queueTail = 0;
				visited[startIndex] = true;
				queue[queueTail++] = (startX, startY);
				while (queueHead < queueTail) {
					var (x, y) = queue[queueHead++];
					component.Add((x, y));
					for (int direction = 0; direction < 4; ++direction) {
						int nextX = x + deltaX[direction];
						int nextY = y + deltaY[direction];
						if (nextX < top
							|| nextX >= top + boxHeight
							|| nextY < left
							|| nextY >= left + boxWidth
							|| visited[(nextX - top) * boxWidth + nextY - left]
							|| row[nextX][nextY] != '.'
							|| occupied[nextX, nextY]) {
							continue;
						}

						visited[(nextX - top) * boxWidth + nextY - left] = true;
						queue[queueTail++] = (nextX, nextY);
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
		var inComponent = new bool[height * width];
		foreach (var (x, y) in component) {
			inComponent[x * width + y] = true;
		}

		var selected = new bool[height * width];
		var frontier = new HashSet<int>();
		var points = new List<(int x, int y)>(p) { (startX, startY) };
		selected[startX * width + startY] = true;
		int minX = startX;
		int maxX = startX;
		int minY = startY;
		int maxY = startY;

		AddNeighborsToFrontier(startX, startY);
		while (points.Count < p) {
			int bestIndex = -1;
			(int neighborPriority, int areaIncrease, int boxPerimeter, int imbalance, int xPriority, int yPriority) bestPriority
				= (int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
			int currentArea = (maxX - minX + 1) * (maxY - minY + 1);

			foreach (int candidateIndex in frontier) {
				int candidateX = candidateIndex / width;
				int candidateY = candidateIndex - candidateX * width;
				int selectedNeighbors = 0;
				selectedNeighbors += candidateX > 0 && selected[candidateIndex - width] ? 1 : 0;
				selectedNeighbors += candidateX + 1 < height && selected[candidateIndex + width] ? 1 : 0;
				selectedNeighbors += candidateY > 0 && selected[candidateIndex - 1] ? 1 : 0;
				selectedNeighbors += candidateY + 1 < width && selected[candidateIndex + 1] ? 1 : 0;
				int nextMinX = Math.Min(minX, candidateX);
				int nextMaxX = Math.Max(maxX, candidateX);
				int nextMinY = Math.Min(minY, candidateY);
				int nextMaxY = Math.Max(maxY, candidateY);
				int boxHeight = nextMaxX - nextMinX + 1;
				int boxWidth = nextMaxY - nextMinY + 1;

				// 隣接辺が1本増えるごとに追加後の周長は2減るため、隣接数を最優先する。
				var priority = (
					neighborPriority: -selectedNeighbors,
					areaIncrease: boxHeight * boxWidth - currentArea,
					boxPerimeter: 2 * (boxHeight + boxWidth),
					imbalance: Math.Abs(boxHeight - boxWidth),
					xPriority: -candidateX,
					yPriority: -candidateY);
				if (priority.CompareTo(bestPriority) < 0) {
					bestIndex = candidateIndex;
					bestPriority = priority;
				}
			}

			// component全体が連結でPマス以上あるため、Pマス選ぶまではfrontierが空にならない。
			frontier.Remove(bestIndex);
			selected[bestIndex] = true;
			int bestX = bestIndex / width;
			int bestY = bestIndex - bestX * width;
			points.Add((bestX, bestY));
			minX = Math.Min(minX, bestX);
			maxX = Math.Max(maxX, bestX);
			minY = Math.Min(minY, bestY);
			maxY = Math.Max(maxY, bestY);
			AddNeighborsToFrontier(bestX, bestY);
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
					&& (uint)nextY < (uint)width) {
					int nextIndex = nextX * width + nextY;
					if (inComponent[nextIndex]
						&& selected[nextIndex] == false) {
						frontier.Add(nextIndex);
					}
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

	// Plan6のフォールバック用のコンパクト領域成長へ現在の占有盤面を渡す。
	public List<(int x, int y)> FindPlacementByReverseBreadthFirstSearch2(int p)
	{
		return Utils.FindPlacementByReverseBreadthFirstSearch2(_input.Row, _occupied, p);
	}

	public List<(int x, int y)> FindPlacementByBestFullSearch(
		IReadOnlyList<(int x, int y)> shape)
	{
		return Utils.FindPlacementByBestFullSearch(_input.Row, _occupied, shape);
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
	public double Rate { get; set; } = 0;
}

public class Input
{
	private readonly IOManager _io;
	private readonly bool _isLocal;

	public int N { get; }
	public int M { get; }
	public double R { get; }
	public string[] Row { get; }

	public long ScoreLimit { get; private set; }

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
		(int i, int s, int t, int p, int v) ret = _io.Int5();
		ScoreLimit += ret.v;

		return ret;
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