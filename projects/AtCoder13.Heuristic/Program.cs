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
		double sumO = 0;
		double sumE = 0;

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
						//$"N={input.N:d2}",
						$"R={input.R:f3}",
						$"P={info.PondCount:d4}",
						$"PC={input.PondClusterCount:d3}",
						$"Th={info.Theta:d4}",
						//$"loop={info.Loop:d8}",
						//$"up={info.Up:d4}",
						$"AL={info.AcceptLimit:d4}",
						$"A={info.Accept:d4}",
						$"M={info.MoveCount:d4}",
						$"C={info.CompactnessAverage:f3}",
						$"ZT={info.ZThresholdAverage:+0.000;-0.000}",
						$"Z={info.ZAverage:f3}",
						$"O={info.OccupyAverage:f3}",
						$"SR={info.AcceptedSizeRatio:f3}",
						$"VR={info.AcceptedValueRatio:f3}",
						$"score={info.Score:d10}",
						$"rate={info.ScoreRate:f3}",
						$"elapsed={info.Elapsed:d4}",
					};

					sumA += info.Accept;
					sumC += info.CompactnessAverage;
					sumZ += info.ZAverage;
					sumR += info.ScoreRate;
					sumO += info.OccupyAverage;
					sumE += info.Elapsed;

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
				double averageO = sumO / rc;
				double averageE = sumE / rc;
				Console.WriteLine($"accept average: {averageA:f3}");
				Console.WriteLine($"compactness average: {averageC:f3}");
				Console.WriteLine($"Z average: {averageZ:f6}");
				Console.WriteLine($"occupy average: {averageO:f6}");
				Console.WriteLine($"rate average: {averageR:f6}");
				Console.WriteLine($"elapsed average: {averageE:f3}");
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

		var best = Plan12(isLocal, sw, rnd, input, info);

		info.Theta = (int)(input.StayTimeSum / input.M);
		info.AcceptLimit = input.CalculateAcceptLimit();
		info.Accept = best.AcceptedCount;
		info.AcceptedSizeRatio = best.AcceptedCount == 0 || input.PersonSum == 0
			? 0.0
			: (double)best.AcceptedPersonSum * input.M / best.AcceptedCount / input.PersonSum;
		info.AcceptedValueRatio = best.AcceptedCount == 0 || input.ScoreLimit == 0
			? 0.0
			: (double)best.AcceptedValueSum * input.M / best.AcceptedCount / input.ScoreLimit;
		info.CompactnessAverage = best.ComactnessSum / best.AcceptedCount;
		info.ZAverage /= best.ComactnessSum;
		info.ZThresholdAverage /= input.M;
		info.OccupyAverage /= input.M;
		info.Score = best.Calculate();
		info.ScoreRate = (double)info.Score / input.ScoreLimit;
		info.Elapsed = sw.ElapsedMilliseconds;

		bool doesEmulate = false;
		if (isLocal && doesEmulate) {
			Emulate(input);
		}

		return (input, info);
	}

	static void Emulate(Input input)
	{
		input.Emulate();
	}

	// Plan5と同じ受理基準を使い、定型形状については全候補から空き領域を残しやすい位置を選ぶ。
	static State Plan6(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();
			state.ReleaseDepartedGroups(guest.s);
			input.OutputMove([], info);

			//const double Bottom10PercentZ = -1.025242;
			//const double Bottom15PercentZ = -0.829147;
			const double Bottom20PercentZ = -0.673297;
			//const double Bottom25PercentZ = -0.539592;
			//const double Bottom30PercentZ = -0.419520;
			//const double Bottom35PercentZ = -0.308256;
			//const double Bottom40PercentZ = -0.202678;
			//const double Bottom45PercentZ = -0.100529;
			//const double Bottom50PercentZ = 0.000000;
			double z = Utils.CalculateValueZScore(guest);
			double zThreshold = Bottom20PercentZ;
			info.ZThresholdAverage += zThreshold;
			if (z < zThreshold) {
				state.ProcessGuest(guest, false, [], info);
				continue;
			}

			var (shape, _) = Utils.CreateNearSquareShape(guest.p);
			var points = state.FindPlacementByBestFullSearch(shape);
			if (points.Count != guest.p) {
				points = state.FindPlacementByReverseBreadthFirstSearch2(guest.p);
			}

			bool canAccept = points.Count == guest.p;
			state.ProcessGuest(guest, canAccept, points, info);
			if (canAccept) {
				info.ZAverage += z;
			}
		}
		state.ReleaseDepartedGroups(int.MaxValue);

		return state;
	}

	// Plan7 : Plan6の配置探索を維持し、盤面混雑度に応じて低価値・大面積・長期滞在を選別する。
	// Plan8 : Plan7のreject判定に、処理済みゲストから得られる動的スコアレートを加える。
	// Plan9 : Plan8の選別を維持し、通常配置に失敗したときだけ利用中グループ1組の再配置を試す。

	// Plan9の配置不能時に、全利用中グループを候補として最良の再配置を選ぶ。
	static State Plan10(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		var parameters = Utils.LoadPlan7Parameters(
			new(-0.60, 0.85, 0.30, 0.35, 0.35, 0.00, 0.10, 10.0, 0.0, 0.30, 0.40, 0.20, 0.65, 0.65, 5.00)); // 確定パラメーター
		if (isLocal) {
			// パラメーター調整時はここでparametersを上書きする
		}
		long arrivedValueSum = 0;

		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();
			state.ReleaseDepartedGroups(guest.s);
			arrivedValueSum += guest.v;

			double z = Utils.CalculateValueZScore(guest);
			double progress = (double)guestCount / input.M;
			double dynamicRate = progress >= parameters.DynamicRateStartProgress
				? state.GetProjectedRate(arrivedValueSum)
				: double.NaN;
			double zThreshold = Utils.ShouldRejectDynamically(
				guest,
				state.OccupiedRate,
				progress,
				input.PondClusterCount,
				parameters,
				dynamicRate);
			info.ZThresholdAverage += zThreshold;
			if (z < zThreshold) {
				input.RecordDecision(guestCount, z, zThreshold, "threshold");
				input.OutputMove([], info);
				state.ProcessGuest(guest, false, [], info);
				continue;
			}

			var (shape, _) = Utils.CreateNearSquareShape(guest.p);
			var points = state.FindPlacementByBestFullSearch(shape);
			if (points.Count != guest.p) {
				points = state.FindPlacementByReverseBreadthFirstSearch2(guest.p);
			}

			if (points.Count == guest.p) {
				input.RecordDecision(guestCount, z, zThreshold, "accepted-normal");
				input.OutputMove([], info);
				state.ProcessGuest(guest, true, points, info);
				info.ZAverage += z;
				continue;
			}

			input.RecordDecision(guestCount, z, zThreshold, "normal-placement-failed");
			if (state.TryMoveBestGroupForGuest(shape, guest, out var moveGroup, out points)) {
				input.RecordDecision(guestCount, z, zThreshold, "accepted-move");
				input.OutputMove([moveGroup], info);
				state.ProcessGuest(guest, true, points, info);
				info.ZAverage += z;
			} else {
				input.RecordDecision(guestCount, z, zThreshold, "move-failed");
				input.OutputMove([], info);
				state.ProcessGuest(guest, false, [], info);
			}
		}
		state.ReleaseDepartedGroups(int.MaxValue);

		return state;
	}

	// Plan10の全利用中グループ再配置を維持し、Plan11専用のreject閾値を使用する。
	static State Plan11(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		var parameters = Utils.LoadPlan7Parameters(
			new(-0.65, 0.85, 0.30, 0.35, 0.35, 0.00, 0.10, 10.0, 0.0, 0.30, 0.40, 0.20, 0.65, 0.65, 5.00)); // 確定パラメーター
		if (isLocal) {
			// パラメーター調整時はここでparametersを上書きする
		}
		long arrivedValueSum = 0;

		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();
			state.ReleaseDepartedGroups(guest.s);
			arrivedValueSum += guest.v;

			double z = Utils.CalculateValueZScore(guest);
			double progress = (double)guestCount / input.M;
			double dynamicRate = progress >= parameters.DynamicRateStartProgress
				? state.GetProjectedRate(arrivedValueSum)
				: double.NaN;
			double zThreshold = Utils.CalculatePlan11RejectThreshold(
				guest,
				state.OccupiedRate,
				progress,
				input.PondClusterCount,
				parameters,
				dynamicRate);
			info.ZThresholdAverage += zThreshold;
			if (z < zThreshold) {
				input.OutputMove([], info);
				state.ProcessGuest(guest, false, [], info);
				continue;
			}

			var (shape, _) = Utils.CreateNearSquareShape(guest.p);
			var points = state.FindPlacementByBestFullSearch(shape);
			if (points.Count != guest.p) {
				points = state.FindPlacementByReverseBreadthFirstSearch2(guest.p);
			}

			if (points.Count == guest.p) {
				input.OutputMove([], info);
				state.ProcessGuest(guest, true, points, info);
				info.ZAverage += z;
				continue;
			}

			if (state.TryMoveBestGroupForGuest(shape, guest, out var moveGroup, out points)) {
				input.OutputMove([moveGroup], info);
				state.ProcessGuest(guest, true, points, info);
				info.ZAverage += z;
			} else {
				input.OutputMove([], info);
				state.ProcessGuest(guest, false, [], info);
			}
		}
		state.ReleaseDepartedGroups(int.MaxValue);

		return state;
	}

	// Plan10の選別・移動探索を維持し、客の退去予測込みで空きスペースを計算する
	static State Plan12(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		var parameters = Utils.LoadPlan7Parameters(
			new(-0.60, 0.85, 0.30, 0.35, 0.35, 0.00, 0.10, 10.0, 0.0, 0.30, 0.40, 0.20, 0.65, 0.65, 5.00)); // 確定パラメーター
		if (isLocal) {
			// パラメーター調整時はここでparametersを上書きする
		}

		long arrivedValueSum = 0;

		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();
			state.ReleaseDepartedGroups(guest.s);
			arrivedValueSum += guest.v;

			double z = Utils.CalculateValueZScore(guest);
			double progress = (double)guestCount / input.M;
			double dynamicRate = progress >= parameters.DynamicRateStartProgress
				? state.GetProjectedRate(arrivedValueSum)
				: double.NaN;
			double projectedOccupiedRate = state.GetProjectedOccupiedRate(guest.s);
			double zThreshold = Utils.ShouldRejectDynamically(
				guest, projectedOccupiedRate, progress, input.PondClusterCount, parameters, dynamicRate);
			info.ZThresholdAverage += zThreshold;
			if (z < zThreshold) {
				input.RecordDecision(guestCount, z, zThreshold, "threshold");
				input.OutputMove([], info);
				state.ProcessGuest(guest, false, [], info);
				continue;
			}

			var (shape, _) = Utils.CreateNearSquareShape(guest.p);
			//var points = state.FindPlacementByEffectiveSpaceSearch(shape);
			var points = state.FindPlacementByBestFullSearch(shape);
			if (points.Count != guest.p) {
				points = state.FindPlacementByReverseBreadthFirstSearch2(guest.p);
			}
			if (points.Count == guest.p) {
				input.RecordDecision(guestCount, z, zThreshold, "accepted-normal");
				input.OutputMove([], info);
				state.ProcessGuest(guest, true, points, info);
				info.ZAverage += z;
				continue;
			}

			input.RecordDecision(guestCount, z, zThreshold, "normal-placement-failed");
			if (state.TryMoveBestGroupForGuest(shape, guest, out var moveGroup, out points)) {
				input.RecordDecision(guestCount, z, zThreshold, "accepted-move");
				input.OutputMove([moveGroup], info);
				state.ProcessGuest(guest, true, points, info);
				info.ZAverage += z;
			} else {
				input.RecordDecision(guestCount, z, zThreshold, "move-failed");
				input.OutputMove([], info);
				state.ProcessGuest(guest, false, [], info);
			}
		}
		state.ReleaseDepartedGroups(int.MaxValue);
		return state;
	}

	// Plan12の受理・移動方針を維持し、最後に任意連結形状を許す完全フォールバックを使う。
	static State Plan13(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input, enableCompleteFallback: true);
		var parameters = Utils.LoadPlan7Parameters(
			new(-0.60, 0.85, 0.30, 0.35, 0.35, 0.00, 0.10, 10.0, 0.0, 0.30, 0.40, 0.20, 0.65, 0.65, 5.00)); // 確定パラメーター
		if (isLocal) {
			// パラメーター調整時はここでparametersを上書きする
		}

		long arrivedValueSum = 0;
		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();
			state.ReleaseDepartedGroups(guest.s);
			arrivedValueSum += guest.v;

			double z = Utils.CalculateValueZScore(guest);
			double progress = (double)guestCount / input.M;
			double dynamicRate = progress >= parameters.DynamicRateStartProgress
				? state.GetProjectedRate(arrivedValueSum)
				: double.NaN;
			double projectedOccupiedRate = state.GetProjectedOccupiedRate(guest.s);
			double zThreshold = Utils.ShouldRejectDynamically(
				guest, projectedOccupiedRate, progress, input.PondClusterCount, parameters, dynamicRate);
			info.ZThresholdAverage += zThreshold;
			if (z < zThreshold) {
				input.RecordDecision(guestCount, z, zThreshold, "threshold");
				input.OutputMove([], info);
				state.ProcessGuest(guest, false, [], info);
				continue;
			}

			var (shape, _) = Utils.CreateNearSquareShape(guest.p);
			var points = state.FindPlacementByBestFullSearch(shape);
			if (points.Count != guest.p) {
				points = state.FindPlacementByReverseBreadthFirstSearch2(guest.p);
			}
			if (points.Count == guest.p) {
				input.RecordDecision(guestCount, z, zThreshold, "accepted-normal");
				input.OutputMove([], info);
				state.ProcessGuest(guest, true, points, info);
				info.ZAverage += z;
				continue;
			}

			input.RecordDecision(guestCount, z, zThreshold, "normal-placement-failed");
			if (state.TryMoveBestGroupForGuest(shape, guest, out var moveGroup, out points)) {
				input.RecordDecision(guestCount, z, zThreshold, "accepted-move");
				input.OutputMove([moveGroup], info);
				state.ProcessGuest(guest, true, points, info);
				info.ZAverage += z;
			} else {
				input.RecordDecision(guestCount, z, zThreshold, "move-failed");
				input.OutputMove([], info);
				state.ProcessGuest(guest, false, [], info);
			}
		}
		state.ReleaseDepartedGroups(int.MaxValue);
		return state;
	}

	// Plan12の配置・移動方針を維持し、Zしきい値をセル時間影価格による受理判定へ置き換える。
	static State Plan14(bool isLocal, Stopwatch sw, Random rnd, Input input, Information info)
	{
		var state = new State(isLocal, input);
		var parameters = Utils.LoadPlan7Parameters(
			new(-0.60, 0.85, 0.30, 0.35, 0.35, 0.00, 0.10, 10.0, 0.0, 0.30, 0.40, 0.20, 0.65, 0.65, 5.00)); // Plan12の確定パラメーター
		long arrivedValueSum = 0;
		for (int guestCount = 0; guestCount < input.M; ++guestCount) {
			var guest = input.InputGuest();
			state.ReleaseDepartedGroups(guest.s);
			state.RegisterArrivedGuest(guest);
			arrivedValueSum += guest.v;

			double z = Utils.CalculateValueZScore(guest);
			var (shape, expectedCompactness) = Utils.CreateNearSquareShape(guest.p);
			double progress = (double)guestCount / input.M;
			double dynamicRate = progress >= parameters.DynamicRateStartProgress
				? state.GetProjectedRate(arrivedValueSum)
				: double.NaN;
			double plan12Threshold = Utils.ShouldRejectDynamically(
				guest,
				state.GetProjectedOccupiedRate(guest.s),
				progress,
				input.PondClusterCount,
				parameters,
				dynamicRate);
			double averageLoadRate = state.GetAverageScheduledLoadRate(guest.s, guest.t);
			double zThreshold = Math.Max(-0.4, plan12Threshold - 0.30 * (1.0 - averageLoadRate));
			bool isWorthAccepting = state.IsWorthAcceptingByCellTime(guest, expectedCompactness);
			info.ZThresholdAverage += zThreshold;
			if (isWorthAccepting == false || z < zThreshold) {
				input.RecordDecision(guestCount, z, zThreshold, isWorthAccepting ? "quality-threshold" : "shadow-price");
				input.OutputMove([], info);
				state.ProcessGuest(guest, false, [], info);
				continue;
			}

			var points = state.FindPlacementByBestFullSearch(shape);
			if (points.Count != guest.p) {
				points = state.FindPlacementByReverseBreadthFirstSearch2(guest.p);
			}
			if (points.Count == guest.p) {
				input.RecordDecision(guestCount, z, 0.0, "accepted-normal");
				input.OutputMove([], info);
				state.ProcessGuest(guest, true, points, info);
				info.ZAverage += z;
				continue;
			}

			input.RecordDecision(guestCount, z, 0.0, "normal-placement-failed");
			if (state.TryMoveBestGroupForGuest(shape, guest, out var moveGroup, out points)) {
				input.RecordDecision(guestCount, z, 0.0, "accepted-move");
				input.OutputMove([moveGroup], info);
				state.ProcessGuest(guest, true, points, info);
				info.ZAverage += z;
			} else {
				input.RecordDecision(guestCount, z, 0.0, "move-failed");
				input.OutputMove([], info);
				state.ProcessGuest(guest, false, [], info);
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

	internal sealed record Plan7Parameters(
		double BaseZ,
		double OccupiedCoefficient,
		double SizeCoefficient,
		double DurationCoefficient,
		double LateRelaxation,
		double SmallShortBonus,
		double FewPondClusterBonus,
		double PondClusterReference,
		double FewPondClusterOccupiedStart,
		double DynamicRateBonus,
		double DynamicRateReference,
		double DynamicRateStartProgress,
		double HighRateLowOccupancyThreshold,
		double HighRateLowOccupancyReference,
		double HighRateLowOccupancyBonus);

	internal static Plan7Parameters GetDefaultParameters()
		=> new(
			-0.75, 0.55, 0.30, 0.25, 0.35, 0.00, 0.00, 10.0, 0.0, 0.00, 0.50, 0.20,
			0.60, 0.50, 0.00);

	// 空きが十分ある間は低価値グループも受け入れ、混雑するほど低価値グループを拒否する。
	internal static double ShouldRejectDynamically(
		(int i, int s, int t, int p, int v) guest,
		double occupiedRate,
		double progress,
		int pondClusterCount,
		Plan7Parameters parameters,
		double dynamicRate = double.NaN)
	{
		return CalculateRejectThreshold(
			guest, occupiedRate, progress, pondClusterCount, parameters, dynamicRate);
	}

	internal static double CalculateRejectThreshold(
		(int i, int s, int t, int p, int v) guest,
		double occupiedRate,
		double progress,
		int pondClusterCount,
		Plan7Parameters parameters,
		double dynamicRate = double.NaN)
	{
		double sizeRate = guest.p / 150.0;
		double durationRate = Math.Clamp((guest.t - guest.s) / 50000.0, 0.0, 1.0);
		double lateRate = Math.Clamp((progress - 0.65) / 0.35, 0.0, 1.0);
		double minimumZ = parameters.BaseZ
			+ parameters.OccupiedCoefficient * occupiedRate
			+ parameters.SizeCoefficient * occupiedRate * sizeRate
			+ parameters.DurationCoefficient * occupiedRate * durationRate
			- parameters.LateRelaxation * lateRate
			- parameters.SmallShortBonus
				* (1.0 - sizeRate) * (1.0 - durationRate);
		double fewClusterRate = Math.Clamp(
			(parameters.PondClusterReference - pondClusterCount) / parameters.PondClusterReference,
			0.0,
			1.0);
		double occupiedRateAfterStart = parameters.FewPondClusterOccupiedStart <= 0.0
			? 1.0
			: Math.Clamp(
				(occupiedRate - parameters.FewPondClusterOccupiedStart)
					/ (1.0 - parameters.FewPondClusterOccupiedStart),
				0.0,
				1.0);
		minimumZ -= parameters.FewPondClusterBonus * fewClusterRate
			* occupiedRateAfterStart;
		if (double.IsNaN(dynamicRate) == false) {
			double highDynamicRate = Math.Clamp(
				dynamicRate - parameters.DynamicRateReference,
				0.0,
				1.0);
			minimumZ -= parameters.DynamicRateBonus * highDynamicRate * fewClusterRate;
		}
		return minimumZ;
	}

	internal static double CalculatePlan11RejectThreshold(
		(int i, int s, int t, int p, int v) guest,
		double occupiedRate,
		double progress,
		int pondClusterCount,
		Plan7Parameters parameters,
		double dynamicRate)
	{
		double minimumZ = CalculateRejectThreshold(
			guest, occupiedRate, progress, pondClusterCount, parameters, dynamicRate);
		if (double.IsNaN(dynamicRate)) {
			return minimumZ;
		}

		double highRate = Math.Clamp(
			(dynamicRate - parameters.HighRateLowOccupancyThreshold)
				/ (1.0 - parameters.HighRateLowOccupancyThreshold),
			0.0,
			1.0);
		double lowOccupancy = Math.Clamp(
			(parameters.HighRateLowOccupancyReference - occupiedRate)
				/ parameters.HighRateLowOccupancyReference,
			0.0,
			1.0);
		return minimumZ - parameters.HighRateLowOccupancyBonus * highRate * lowOccupancy;
	}

	internal static Plan7Parameters LoadPlan7Parameters(
		Plan7Parameters? overrideParameters = null)
		=> overrideParameters is not null
			? overrideParameters
			: GetDefaultParameters();

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
		=> FindPlacementByBestFullSearch(
			row, occupied, shape, 0, row.Length - 1, 0, row[0].Length - 1);

	internal static List<(int x, int y)> FindPlacementByBestFullSearch(
		string[] row,
		bool[,] occupied,
		IReadOnlyList<(int x, int y)> shape,
		int minBaseX,
		int maxBaseX,
		int minBaseY,
		int maxBaseY)
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
				shapeWidth,
				minBaseX,
				maxBaseX,
				minBaseY,
				maxBaseY);
		}

		return FindPlacementByBestGenericFullSearch(
			row, occupied, shape, shapeHeight, shapeWidth,
			minBaseX, maxBaseX, minBaseY, maxBaseY);
	}

	// Plan12用。配置後の空きセル同士の隣接辺数を局所的に最大化する。
	internal static List<(int x, int y)> FindPlacementByEffectiveSpaceSearch(
		string[] row,
		bool[,] occupied,
		IReadOnlyList<(int x, int y)> shape)
	{
		if (shape.Count == 0) {
			return [];
		}

		int height = row.Length;
		int width = row[0].Length;
		int shapeHeight = shape.Max(point => point.x) + 1;
		int shapeWidth = shape.Max(point => point.y) + 1;
		var shapeCells = shape.ToHashSet();
		var best = (freeAdjacency: -1, boundaryEdges: -1, baseX: -1, baseY: -1);
		for (int baseX = 0; baseX + shapeHeight <= height; ++baseX) {
			for (int baseY = 0; baseY + shapeWidth <= width; ++baseY) {
				bool canPlace = true;
				foreach (var (offsetX, offsetY) in shape) {
					if (row[baseX + offsetX][baseY + offsetY] != '.' || occupied[baseX + offsetX, baseY + offsetY]) {
						canPlace = false;
						break;
					}
				}
				if (canPlace == false) {
					continue;
				}

				int boundaryEdges = 0;
				foreach (var (offsetX, offsetY) in shape) {
					foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }) {
						if (shapeCells.Contains((offsetX + dx, offsetY + dy)) == false
							&& IsBlocked(row, occupied, baseX + offsetX + dx, baseY + offsetY + dy)) {
							++boundaryEdges;
						}
					}
				}
				int freeAdjacency = CountFreeAdjacencyAfterPlacement(
					row, occupied, shapeCells, baseX, baseY);
				if (freeAdjacency > best.freeAdjacency
					|| freeAdjacency == best.freeAdjacency && boundaryEdges > best.boundaryEdges) {
					best = (freeAdjacency, boundaryEdges, baseX, baseY);
				}
			}
		}

		if (best.baseX < 0) {
			return [];
		}

		return shape.Select(point => (best.baseX + point.x, best.baseY + point.y)).ToList();
	}

	private static int CountFreeAdjacencyAfterPlacement(
		string[] row,
		bool[,] occupied,
		IReadOnlySet<(int x, int y)> shapeCells,
		int baseX,
		int baseY)
	{
		int lostEdges = 0;
		foreach (var (x, y) in shapeCells) {
			foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }) {
				int nx = baseX + x + dx;
				int ny = baseY + y + dy;
				if ((uint)nx >= (uint)row.Length || (uint)ny >= (uint)row[0].Length
					|| row[nx][ny] != '.' || occupied[nx, ny]
					|| shapeCells.Contains((x + dx, y + dy))) {
					continue;
				}
				++lostEdges;
			}
		}

		return -lostEdges;
	}

	private static int CountNearSquarePlacements(string[] row, bool[,] occupied, int personCount)
	{
		var (shape, _) = CreateNearSquareShape(personCount);
		int shapeHeight = shape.Max(point => point.x) + 1;
		int shapeWidth = shape.Max(point => point.y) + 1;
		int count = 0;
		for (int baseX = 0; baseX + shapeHeight <= row.Length; ++baseX) {
			for (int baseY = 0; baseY + shapeWidth <= row[0].Length; ++baseY) {
				bool canPlace = true;
				foreach (var (x, y) in shape) {
					if (row[baseX + x][baseY + y] != '.' || occupied[baseX + x, baseY + y]) {
						canPlace = false;
						break;
					}
				}
				if (canPlace) {
					++count;
				}
			}
		}
		return count;
	}

	private static List<(int x, int y)> FindPlacementByBestRectanglePrefixSearch(
		string[] row,
		bool[,] occupied,
		int shapeCount,
		int shapeHeight,
		int shapeWidth,
		int minBaseX,
		int maxBaseX,
		int minBaseY,
		int maxBaseY)
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
		minBaseX = Math.Clamp(minBaseX, 0, height - shapeHeight);
		maxBaseX = Math.Clamp(maxBaseX, 0, height - shapeHeight);
		minBaseY = Math.Clamp(minBaseY, 0, width - shapeWidth);
		maxBaseY = Math.Clamp(maxBaseY, 0, width - shapeWidth);
		for (int baseX = minBaseX; baseX <= maxBaseX; ++baseX) {
			for (int baseY = minBaseY; baseY <= maxBaseY; ++baseY) {
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
		int shapeWidth,
		int minBaseX,
		int maxBaseX,
		int minBaseY,
		int maxBaseY)
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

		minBaseX = Math.Clamp(minBaseX, 0, height - shapeHeight);
		maxBaseX = Math.Clamp(maxBaseX, 0, height - shapeHeight);
		minBaseY = Math.Clamp(minBaseY, 0, width - shapeWidth);
		maxBaseY = Math.Clamp(maxBaseY, 0, width - shapeWidth);
		for (int baseX = minBaseX; baseX <= maxBaseX; ++baseX) {
			for (int baseY = minBaseY; baseY <= maxBaseY; ++baseY) {
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

	// 形状制約を設けず、Pマス以上の空き連結成分から必ず連結領域を切り出す最終フォールバック。
	internal static List<(int x, int y)> FindPlacementByAnyConnectedComponent(
		string[] row,
		bool[,] occupied,
		int p)
	{
		int height = row.Length;
		int width = row[0].Length;
		var visited = new bool[height, width];
		var queue = new (int x, int y)[height * width];
		List<(int x, int y)> bestPoints = [];
		double bestCompactness = -1.0;
		ReadOnlySpan<int> deltaX = [-1, 1, 0, 0];
		ReadOnlySpan<int> deltaY = [0, 0, -1, 1];

		for (int startX = 0; startX < height; ++startX) {
			for (int startY = 0; startY < width; ++startY) {
				if (visited[startX, startY] || row[startX][startY] != '.' || occupied[startX, startY]) {
					continue;
				}

				var component = new List<(int x, int y)>();
				int queueHead = 0;
				int queueTail = 0;
				visited[startX, startY] = true;
				queue[queueTail++] = (startX, startY);
				while (queueHead < queueTail) {
					var (x, y) = queue[queueHead++];
					component.Add((x, y));
					for (int direction = 0; direction < 4; ++direction) {
						int nextX = x + deltaX[direction];
						int nextY = y + deltaY[direction];
						if ((uint)nextX >= (uint)height
							|| (uint)nextY >= (uint)width
							|| visited[nextX, nextY]
							|| row[nextX][nextY] != '.'
							|| occupied[nextX, nextY]) {
							continue;
						}

						visited[nextX, nextY] = true;
						queue[queueTail++] = (nextX, nextY);
					}
				}

				if (component.Count < p) {
					continue;
				}

				int minX = component.Min(point => point.x);
				int maxX = component.Max(point => point.x);
				int minY = component.Min(point => point.y);
				int maxY = component.Max(point => point.y);
				var seed = component.MinBy(point => (
					Math.Abs(2 * point.x - minX - maxX) + Math.Abs(2 * point.y - minY - maxY),
					-point.x,
					-point.y));
				var points = GrowCompactRegion(component, seed.x, seed.y, height, width, p);
				double compactness = CalculateCompactness(points);
				if (compactness > bestCompactness) {
					bestCompactness = compactness;
					bestPoints = points;
				}
			}
		}

		return bestPoints;
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
		IReadOnlyList<(int boxHeight, int boxWidth, int perimeter, int unusedArea, int imbalance)> rectangleCandidates,
		int minBaseX = 0,
		int maxBaseX = int.MaxValue,
		int minBaseY = 0,
		int maxBaseY = int.MaxValue)
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
			(int top, int left, int occupiedCount, int pondCount) bestPosition = default;
			bool hasBestPosition = false;
			int boxHeight = rectangle.boxHeight;
			int boxWidth = rectangle.boxWidth;
			int rectangleArea = boxHeight * boxWidth;
			int candidateMaxTop = Math.Min(
				height - boxHeight,
				maxBaseX);
			int candidateMinTop = Math.Max(
				0,
				minBaseX - boxHeight + 1);
			int candidateMaxLeft = Math.Min(
				width - boxWidth,
				maxBaseY);
			int candidateMinLeft = Math.Max(
				0,
				minBaseY - boxWidth + 1);
			for (int top = candidateMaxTop; top >= candidateMinTop; --top) {
				int bottom = top + boxHeight;
				int bottomRowOffset = bottom * prefixWidth;
				int topRowOffset = top * prefixWidth;
				int bottomRightIndex = bottomRowOffset + candidateMaxLeft + boxWidth;
				int topRightIndex = topRowOffset + candidateMaxLeft + boxWidth;
				int bottomLeftIndex = bottomRowOffset + candidateMaxLeft;
				int topLeftIndex = topRowOffset + candidateMaxLeft;
				for (int left = candidateMaxLeft; left >= candidateMinLeft; --left) {
					int occupiedCount = occupiedPrefixSum[bottomRightIndex]
						- occupiedPrefixSum[topRightIndex]
						- occupiedPrefixSum[bottomLeftIndex]
						+ occupiedPrefixSum[topLeftIndex];
					int pondCount = pondPrefixSum[bottomRightIndex]
						- pondPrefixSum[topRightIndex]
						- pondPrefixSum[bottomLeftIndex]
						+ pondPrefixSum[topLeftIndex];

					if (rectangleArea - occupiedCount - pondCount >= p) {
						positions.Add((top, left, occupiedCount, pondCount));
					}

					--bottomRightIndex;
					--topRightIndex;
					--bottomLeftIndex;
					--topLeftIndex;
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

		static bool IsBetterRectanglePosition(
			(int top, int left, int occupiedCount, int pondCount) left,
			(int top, int left, int occupiedCount, int pondCount) right)
		{
			return CompareRectanglePositions(left, right) < 0;
		}

		static int CompareRectanglePositions(
			(int top, int left, int occupiedCount, int pondCount) left,
			(int top, int left, int occupiedCount, int pondCount) right)
		{
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
		}
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
	private const int TimeBucketWidth = 500;
	private const int TimeBucketCount = 100000 / TimeBucketWidth;
	private readonly bool _isLocal;
	private readonly Input _input;
	private readonly bool _enableCompleteFallback;
	// trueのマスは現在いずれかの利用中グループが占有している。
	private readonly bool[,] _occupied;
	private readonly int _grassCellCount;
	private int _occupiedCellCount;
	// 退去時の解放と利用料加算に必要な情報だけを、受け入れ済みグループごとに保持する。
	private readonly Dictionary<int, (int departureTime, int value, long usageFee, List<(int x, int y)> points, double compactness)> _activeGroups = [];
	// 到着ごとに全グループを走査せず、退去時刻が早い順に処理する。
	private readonly PriorityQueue<int, int> _departureQueue = new();
	private readonly int[] _scheduledCellLoad = new int[TimeBucketCount];
	private long _arrivedValueSum;
	private long _arrivedCellTimeSum;
	private long _money;

	public int AcceptedCount { get; private set; } = 0;
	public long AcceptedPersonSum { get; private set; } = 0;
	public long AcceptedValueSum { get; private set; } = 0;
	public double ComactnessSum { get; private set; } = 0;
	public double OccupiedRate => (double)_occupiedCellCount / _grassCellCount;
	public long ProjectedScore => _money + _activeGroups.Values.Sum(group => group.usageFee);

	public State(bool isLocal, Input input, bool enableCompleteFallback = false)
	{
		_isLocal = isLocal;
		_input = input;
		_enableCompleteFallback = enableCompleteFallback;
		_occupied = new bool[input.N, input.N];
		_grassCellCount = input.Row.Sum(row => row.Count(cell => cell == '.'));
	}

	public void RegisterArrivedGuest((int i, int s, int t, int p, int v) guest)
	{
		_arrivedValueSum += guest.v;
		_arrivedCellTimeSum += (long)guest.p * (guest.t - guest.s);
	}

	public double ObservedValueDensity => _arrivedCellTimeSum == 0
		? 0.0
		: (double)_arrivedValueSum / _arrivedCellTimeSum;

	// 候補が占める各時間区間について、混雑度が高いほど非線形に上がるセル時間価格を積分する。
	public double CalculateCellTimeOpportunityCost(int arrivalTime, int departureTime, int personCount)
	{
		double baseDensity = ObservedValueDensity > 0.0 ? ObservedValueDensity : 0.5;
		double cost = 0.0;
		int firstBucket = Math.Clamp(arrivalTime / TimeBucketWidth, 0, TimeBucketCount - 1);
		int lastBucket = Math.Clamp((departureTime - 1) / TimeBucketWidth, 0, TimeBucketCount - 1);
		for (int bucket = firstBucket; bucket <= lastBucket; ++bucket) {
			int bucketStart = bucket * TimeBucketWidth;
			int bucketEnd = bucketStart + TimeBucketWidth;
			int overlap = Math.Min(departureTime, bucketEnd) - Math.Max(arrivalTime, bucketStart);
			double loadRate = (double)(_scheduledCellLoad[bucket] + personCount) / _grassCellCount;
			double shadowPrice = baseDensity * Math.Pow(loadRate / 0.65, 4.0);
			cost += personCount * overlap * shadowPrice;
		}

		return cost;
	}

	public bool IsWorthAcceptingByCellTime(
		(int i, int s, int t, int p, int v) guest,
		double expectedCompactness)
	{
		double expectedUsageFee = guest.v * expectedCompactness;
		double opportunityCost = CalculateCellTimeOpportunityCost(guest.s, guest.t, guest.p);
		return expectedUsageFee > opportunityCost;
	}

	public double GetAverageScheduledLoadRate(int arrivalTime, int departureTime)
	{
		int firstBucket = Math.Clamp(arrivalTime / TimeBucketWidth, 0, TimeBucketCount - 1);
		int lastBucket = Math.Clamp((departureTime - 1) / TimeBucketWidth, 0, TimeBucketCount - 1);
		long weightedLoad = 0;
		int totalDuration = 0;
		for (int bucket = firstBucket; bucket <= lastBucket; ++bucket) {
			int bucketStart = bucket * TimeBucketWidth;
			int bucketEnd = bucketStart + TimeBucketWidth;
			int overlap = Math.Min(departureTime, bucketEnd) - Math.Max(arrivalTime, bucketStart);
			weightedLoad += (long)_scheduledCellLoad[bucket] * overlap;
			totalDuration += overlap;
		}

		return totalDuration == 0
			? 0.0
			: Math.Clamp((double)weightedLoad / totalDuration / _grassCellCount, 0.0, 1.0);
	}

	public double GetProjectedRate(long arrivedValueSum)
	{
		return arrivedValueSum <= 0
			? 0.0
			: (double)ProjectedScore / arrivedValueSum;
	}

	public double GetProjectedOccupiedRate(int currentTime, int horizon = 100)
	{
		int projectedReleasedCellCount = 0;
		int releaseLimit = currentTime + horizon;
		foreach (var group in _activeGroups.Values) {
			if (group.departureTime <= releaseLimit) {
				projectedReleasedCellCount += group.points.Count;
			}
		}

		return Math.Max(
			0.0,
			(double)(_occupiedCellCount - projectedReleasedCellCount) / _grassCellCount);
	}

	// Plan6のフォールバック用のコンパクト領域成長へ現在の占有盤面を渡す。
	public List<(int x, int y)> FindPlacementByReverseBreadthFirstSearch2(int p)
	{
		var points = Utils.FindPlacementByReverseBreadthFirstSearch2(
			_input.Row,
			_occupied,
			p,
			_input.PondPrefixSum,
			_input.GetRectangleCandidates(p));
		return points.Count == p || _enableCompleteFallback == false
			? points
			: Utils.FindPlacementByAnyConnectedComponent(_input.Row, _occupied, p);
	}

	public List<(int x, int y)> FindPlacementByReverseBreadthFirstSearch2(
		int p,
		int minBaseX,
		int maxBaseX,
		int minBaseY,
		int maxBaseY)
	{
		var points = Utils.FindPlacementByReverseBreadthFirstSearch2(
			_input.Row,
			_occupied,
			p,
			_input.PondPrefixSum,
			_input.GetRectangleCandidates(p),
			minBaseX,
			maxBaseX,
			minBaseY,
			maxBaseY);
		return points.Count == p || _enableCompleteFallback == false
			? points
			: Utils.FindPlacementByAnyConnectedComponent(_input.Row, _occupied, p);
	}

	public List<(int x, int y)> FindPlacementByBestFullSearch(
		IReadOnlyList<(int x, int y)> shape)
	{
		return Utils.FindPlacementByBestFullSearch(_input.Row, _occupied, shape);
	}

	public List<(int x, int y)> FindPlacementByBestFullSearch(
		IReadOnlyList<(int x, int y)> shape,
		int minBaseX,
		int maxBaseX,
		int minBaseY,
		int maxBaseY)
	{
		return Utils.FindPlacementByBestFullSearch(
			_input.Row, _occupied, shape, minBaseX, maxBaseX, minBaseY, maxBaseY);
	}

	public List<(int x, int y)> FindPlacementByEffectiveSpaceSearch(
		IReadOnlyList<(int x, int y)> shape)
	{
		return Utils.FindPlacementByEffectiveSpaceSearch(_input.Row, _occupied, shape);
	}

	// 通常配置に失敗した場合だけ、最も早く退去する1組を再配置して空間を作る。
	public bool TryMoveOneGroupForGuest(
		IReadOnlyList<(int x, int y)> guestShape,
		int guestSize,
		out (int j, List<(int z, int w)> points) moveGroup,
		out List<(int x, int y)> guestPoints)
	{
		moveGroup = default;
		guestPoints = [];
		if (_activeGroups.Count == 0) {
			return false;
		}

		var candidate = _activeGroups.MinBy(pair => pair.Value.departureTime);
		int groupId = candidate.Key;
		var group = candidate.Value;
		SetOccupied(group.points, false);

		guestPoints = FindPlacementByBestFullSearch(guestShape);
		if (guestPoints.Count != guestSize) {
			guestPoints = FindPlacementByReverseBreadthFirstSearch2(guestSize);
		}
		if (guestPoints.Count != guestSize) {
			SetOccupied(group.points, true);
			guestPoints = [];
			return false;
		}

		SetOccupied(guestPoints, true);
		var (groupShape, _) = Utils.CreateNearSquareShape(group.points.Count);
		var movedPoints = FindPlacementByBestFullSearch(groupShape);
		if (movedPoints.Count != group.points.Count) {
			movedPoints = FindPlacementByReverseBreadthFirstSearch2(group.points.Count);
		}
		SetOccupied(guestPoints, false);
		if (movedPoints.Count != group.points.Count) {
			SetOccupied(group.points, true);
			guestPoints = [];
			return false;
		}

		SetOccupied(movedPoints, true);
		double movedCompactness = Math.Min(group.compactness, Utils.CalculateCompactness(movedPoints));
		long movedUsageFee = CalculateUsageFee(group.value, movedCompactness);
		long moveCost = Math.Max((long)Math.Floor(group.value * _input.R + 0.5), 1);
		_money -= moveCost;
		ComactnessSum += movedCompactness - group.compactness;
		_activeGroups[groupId] = (group.departureTime, group.value, movedUsageFee, movedPoints, movedCompactness);
		moveGroup = (groupId, movedPoints.Select(point => (point.x, point.y)).ToList());
		return true;
	}

	// 全利用中グループを候補にし、新規客の受入による増分が最大の移動だけを確定する。
	public bool TryMoveBestGroupForGuest(
		IReadOnlyList<(int x, int y)> guestShape,
		(int i, int s, int t, int p, int v) guest,
		out (int j, List<(int z, int w)> points) moveGroup,
		out List<(int x, int y)> guestPoints)
	{
		moveGroup = default;
		guestPoints = [];
		if (_activeGroups.Count == 0) {
			return false;
		}

		long bestGain = long.MinValue;
		int bestGroupId = -1;
		List<(int x, int y)> bestMovedPoints = [];
		List<(int x, int y)> bestGuestPoints = [];
		var groupShapes = new Dictionary<int, List<(int x, int y)>>();
		long minimumMoveCost = _activeGroups.Values.Min(group =>
			Math.Max((long)Math.Floor(group.value * _input.R + 0.5), 1));
		long maximumPossibleGain = guest.v - minimumMoveCost;

		foreach (var candidate in _activeGroups) {
			int groupId = candidate.Key;
			var group = candidate.Value;
			if (guest.p + group.points.Count > _grassCellCount - _occupiedCellCount + group.points.Count) {
				continue;
			}
			SetOccupied(group.points, false);

			int groupMinX = group.points.Min(point => point.x);
			int groupMaxX = group.points.Max(point => point.x);
			int groupMinY = group.points.Min(point => point.y);
			int groupMaxY = group.points.Max(point => point.y);
			int guestShapeHeight = guestShape.Max(point => point.x) + 1;
			int guestShapeWidth = guestShape.Max(point => point.y) + 1;
			int minBaseX = groupMinX - guestShapeHeight + 1;
			int maxBaseX = groupMaxX;
			int minBaseY = groupMinY - guestShapeWidth + 1;
			int maxBaseY = groupMaxY;
			var candidateGuestPoints = FindPlacementByBestFullSearch(
				guestShape, minBaseX, maxBaseX, minBaseY, maxBaseY);
			if (candidateGuestPoints.Count != guest.p) {
				candidateGuestPoints = FindPlacementByReverseBreadthFirstSearch2(
					guest.p, groupMinX, groupMaxX, groupMinY, groupMaxY);
			}
			if (candidateGuestPoints.Count == guest.p) {
				SetOccupied(candidateGuestPoints, true);
				if (groupShapes.TryGetValue(group.points.Count, out var groupShape) == false) {
					(groupShape, _) = Utils.CreateNearSquareShape(group.points.Count);
					groupShapes.Add(group.points.Count, groupShape);
				}
				var candidateMovedPoints = FindPlacementByBestFullSearch(groupShape);
				if (candidateMovedPoints.Count != group.points.Count) {
					candidateMovedPoints = FindPlacementByReverseBreadthFirstSearch2(group.points.Count);
				}
				SetOccupied(candidateGuestPoints, false);

				if (candidateMovedPoints.Count == group.points.Count) {
					double guestCompactness = Utils.CalculateCompactness(candidateGuestPoints);
					double movedCompactness = Math.Min(
						group.compactness,
						Utils.CalculateCompactness(candidateMovedPoints));
					long guestUsageFee = CalculateUsageFee(guest.v, guestCompactness);
					long movedUsageFee = CalculateUsageFee(group.value, movedCompactness);
					long moveCost = Math.Max((long)Math.Floor(group.value * _input.R + 0.5), 1);
					long gain = guestUsageFee + movedUsageFee - group.usageFee - moveCost;
					if (gain > bestGain) {
						bestGain = gain;
						bestGroupId = groupId;
						bestMovedPoints = candidateMovedPoints.ToList();
						bestGuestPoints = candidateGuestPoints.ToList();
					}
					if (bestGain >= maximumPossibleGain) {
						SetOccupied(candidateGuestPoints, false);
						SetOccupied(group.points, true);
						break;
					}
				}
			}

			SetOccupied(group.points, true);
		}

		if (bestGroupId < 0) {
			return false;
		}

		var bestGroup = _activeGroups[bestGroupId];
		SetOccupied(bestGroup.points, false);
		SetOccupied(bestMovedPoints, true);
		var finalCompactness = Math.Min(
			bestGroup.compactness,
			Utils.CalculateCompactness(bestMovedPoints));
		long finalUsageFee = CalculateUsageFee(bestGroup.value, finalCompactness);
		long finalMoveCost = Math.Max((long)Math.Floor(bestGroup.value * _input.R + 0.5), 1);
		_money -= finalMoveCost;
		ComactnessSum += finalCompactness - bestGroup.compactness;
		_activeGroups[bestGroupId] = (
			bestGroup.departureTime,
			bestGroup.value,
			finalUsageFee,
			bestMovedPoints,
			finalCompactness);
		moveGroup = (bestGroupId, bestMovedPoints.Select(point => (point.x, point.y)).ToList());
		guestPoints = bestGuestPoints;
		return true;
	}

	// Planが決めた配置結果を状態へ反映し、インタラクティブ出力を確定する。
	public void ProcessGuest(
		(int i, int s, int t, int p, int v) guest,
		bool canAccept,
		List<(int x, int y)> points,
		Information info)
	{
		if (canAccept) {
			double compactness = Utils.CalculateCompactness(points);
			Accept(guest.i, guest.s, guest.t, guest.v, points, compactness);
			++AcceptedCount;
			AcceptedPersonSum += guest.p;
			AcceptedValueSum += guest.v;
			ComactnessSum += compactness;
		}

		_input.OutputReception(canAccept, points);
		info.OccupyAverage += OccupiedRate;
	}

	// 利用料は移動しない各Planでは配置時に確定するため、退去時用に保存しておく。
	private void Accept(
		int groupId,
		int arrivalTime,
		int departureTime,
		int value,
		List<(int x, int y)> points,
		double compactness)
	{
		SetOccupied(points, true);
		_occupiedCellCount += points.Count;

		long usageFee = CalculateUsageFee(value, compactness);
		_activeGroups.Add(groupId, (departureTime, value, usageFee, points, compactness));
		_departureQueue.Enqueue(groupId, departureTime);
		int firstBucket = Math.Clamp(arrivalTime / TimeBucketWidth, 0, TimeBucketCount - 1);
		int lastBucket = Math.Clamp((departureTime - 1) / TimeBucketWidth, 0, TimeBucketCount - 1);
		for (int bucket = firstBucket; bucket <= lastBucket; ++bucket) {
			_scheduledCellLoad[bucket] += points.Count;
		}
	}

	private void SetOccupied(IEnumerable<(int x, int y)> points, bool occupied)
	{
		foreach (var (x, y) in points) {
			_occupied[x, y] = occupied;
		}
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
			SetOccupied(group.points, false);
			_occupiedCellCount -= group.points.Count;

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
	public int Theta { get; set; } = 0;
	public int AcceptLimit { get; set; } = 0;
	public int Accept { get; set; } = 0;
	public int MoveCount { get; set; } = 0;
	public double CompactnessAverage { get; set; } = 0;
	public double ZThresholdAverage { get; set; } = 0;
	public double ZAverage { get; set; } = 0;
	public double OccupyAverage { get; set; } = 0;
	public double ScoreRate { get; set; } = 0;
	public double AcceptedSizeRatio { get; set; } = 0;
	public double AcceptedValueRatio { get; set; } = 0;
}

public class Input
{
	private readonly IOManager _io;
	private readonly bool _isLocal;
	private readonly Dictionary<int, (int boxHeight, int boxWidth, int perimeter, int unusedArea, int imbalance)[]> _rectangleCandidates = [];

	public int N { get; }
	public int M { get; }
	public int PondClusterCount { get; }
	public double R { get; }
	public string[] Row { get; }
	public int[] PondPrefixSum { get; }

	public (int s, int t, int p, int v)[] Guests { get; private set; }
	public List<(int j, List<(int z, int w)> points)>[] Moves { get; private set; }
	public (bool takes, List<(int x, int y)> points)[] Receptions { get; private set; }
	public (double z, double threshold, string decision)[] Decisions { get; private set; }
	private int _turn = 0;

	public long ScoreLimit { get; private set; }
	public long StayTimeSum { get; private set; }
	public long PersonStayTimeSum { get; private set; }
	public long PersonSum { get; private set; }
	public int GrassCellCount => Row.Sum(row => row.Count(cell => cell == '.'));

	public Input(IOManager io, bool isLocal)
	{
		_io = io;
		_isLocal = isLocal;

		(N, M) = io.Int2();
		R = io.Double();
		Row = io.ArrayString(N);
		PondClusterCount = CountPondClusters();
		ConvertSmallEnclosedGrassToPonds();
		PondPrefixSum = Utils.CreatePondPrefixSum(Row);

		Guests = new (int s, int t, int p, int v)[M];
		Moves = new List<(int j, List<(int z, int w)> points)>[M];
		Receptions = new (bool takes, List<(int x, int y)> points)[M];
		Decisions = new (double z, double threshold, string decision)[M];
	}

	public (int i, int s, int t, int p, int v) InputGuest()
	{
		(int i, int s, int t, int p, int v) ret = _io.Int5();
		Guests[ret.i] = (ret.s, ret.t, ret.p, ret.v);

		ScoreLimit += ret.v;
		StayTimeSum += ret.t - ret.s;
		PersonStayTimeSum += (long)ret.p * (ret.t - ret.s);
		PersonSum += ret.p;

		return ret;
	}

	public void OutputMove(List<(int j, List<(int z, int w)> points)> moveGroups, Information info)
	{
		Moves[_turn] = moveGroups;
		info.MoveCount += moveGroups.Count;
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
		Receptions[_turn] = (takes, points);
		if (takes) {
			_io.WriteLine("Yes");
			foreach (var (x, y) in points) {
				_io.WriteLine($"{x} {y}");
			}
		} else {
			_io.WriteLine("No");
		}

		_io.Flush();
		++_turn;
	}

	public void RecordDecision(int turn, double z, double threshold, string decision)
	{
		Decisions[turn] = (z, threshold, decision);
	}

	public int CalculateAcceptLimit()
	{
		if (PersonStayTimeSum <= 0) {
			return 0;
		}

		long capacity = (long)GrassCellCount * 100000;
		long limit = capacity * M / PersonStayTimeSum;
		return (int)Math.Clamp(limit, 0, M);
	}

	// 保存済みの入出力を公式の処理順に適用し、各ターンの空間利用状況を可視化する。
	// 本番の探索ループからは呼び出さず、ローカル解析時だけ利用する。
	public void Emulate()
	{
		var owner = new int[N, N];
		for (int x = 0; x < N; ++x) {
			for (int y = 0; y < N; ++y) {
				owner[x, y] = -1;
			}
		}
		var active = new Dictionary<int, List<(int x, int y)>>();
		var reports = new List<string>(M);
		var occupiedSnapshots = new List<HashSet<(int x, int y)>>(M);
		var counterfactualCandidates = new List<(int turn, int departureTime, int p, int v, double z, double threshold, List<(int x, int y)> points)>();
		int totalFreeGrass = 0;
		int totalSmallComponents = 0;
		int rejectedWithAlternative = 0;
		int rejectedWithoutAlternative = 0;
		var rejectionCounts = new Dictionary<string, int>();
		int moveFailedCount = 0;
		int moveFailedWithLargeComponent = 0;
		int moveFailedUnlockedByOneGroup = 0;
		var moveFailedReports = new List<string>();
		var acceptedSpaceMetrics = new List<(int turn, int largest, int small, int freeAdjacency, int placements64, int placements96, int placements128)>();

		for (int turn = 0; turn < M; ++turn) {
			var guest = Guests[turn];
			if (Moves[turn] is null || Receptions[turn].points is null) {
				continue;
			}

			// 到着時刻より前に退去したグループの領域を解放する。
			var departed = active
				.Where(pair => Guests[pair.Key].t < guest.s)
				.Select(pair => pair.Key)
				.ToArray();
			foreach (int groupId in departed) {
				ClearOwner(owner, active[groupId], groupId);
				active.Remove(groupId);
			}

			// 複数移動は全グループを先に空けてから同時配置する。
			var movedGroups = Moves[turn];
			foreach (var (groupId, _) in movedGroups) {
				if (active.TryGetValue(groupId, out var oldPoints)) {
					ClearOwner(owner, oldPoints, groupId);
				}
			}
			foreach (var (groupId, newPoints) in movedGroups) {
				if (active.ContainsKey(groupId) == false) {
					reports.Add($"turn={turn:d4} INVALID move group={groupId}: not active");
					continue;
				}

				if (CanOwn(owner, newPoints, groupId) == false) {
					reports.Add($"turn={turn:d4} INVALID move group={groupId}: collision");
					continue;
				}

				SetOwner(owner, newPoints, groupId);
				active[groupId] = newPoints;
			}

			var reception = Receptions[turn];
			if (reception.takes) {
				if (CanOwn(owner, reception.points, turn) == false) {
					reports.Add($"turn={turn:d4} INVALID reception group={turn}");
				} else {
					SetOwner(owner, reception.points, turn);
					active[turn] = reception.points;
				}
			}

			var freeComponents = FindFreeComponents(owner);
			int freeGrass = freeComponents.Sum(component => component.Count);
			int smallComponents = freeComponents.Count(component => component.Count < Math.Max(4, guest.p));
			int largestComponent = freeComponents.Count == 0
				? 0
				: freeComponents.Max(component => component.Count);
			totalFreeGrass += freeGrass;
			totalSmallComponents += smallComponents;
			if (reception.takes) {
				var occupied = ToOccupied(owner);
				acceptedSpaceMetrics.Add((
					turn,
					largestComponent,
					freeComponents.Count(component => component.Count < 32),
					CountFreeAdjacency(occupied),
					CountNearSquarePlacements(occupied, 64),
					CountNearSquarePlacements(occupied, 96),
					CountNearSquarePlacements(occupied, 128)));
			}

			string alternative = "-";
			if (reception.takes == false) {
				var decision = Decisions[turn];
				var (shape, _) = Utils.CreateNearSquareShape(guest.p);
				var alternativePoints = Utils.FindPlacementByBestFullSearch(Row, ToOccupied(owner), shape);
				if (alternativePoints.Count == guest.p) {
					++rejectedWithAlternative;
					alternative = $"alternative=shape C={Utils.CalculateCompactness(alternativePoints):f3}";
					if (decision.decision == "threshold") {
						counterfactualCandidates.Add((
							turn, guest.t, guest.p, guest.v, decision.z, decision.threshold,
							alternativePoints));
					}
				} else {
					++rejectedWithoutAlternative;
				}

				string rejection = string.IsNullOrEmpty(decision.decision)
					? "unknown"
					: decision.decision;
				rejectionCounts[rejection] = rejectionCounts.GetValueOrDefault(rejection) + 1;
				alternative += $" reason={rejection} z={decision.z:f3} threshold={decision.threshold:f3}";
				if (rejection == "move-failed") {
					++moveFailedCount;
					var largeComponents = freeComponents.Where(component => component.Count >= guest.p).ToList();
					if (largeComponents.Count > 0) {
						++moveFailedWithLargeComponent;
					}

					var boundaryGroups = FindBoundaryGroups(owner, freeComponents);
					var (diagnosticShape, _) = Utils.CreateNearSquareShape(guest.p);
					var unlockingGroups = new List<int>();
					foreach (int groupId in boundaryGroups) {
						if (active.TryGetValue(groupId, out var groupPoints) == false) {
							continue;
						}

						ClearOwner(owner, groupPoints, groupId);
						var unlocked = Utils.FindPlacementByBestFullSearch(Row, ToOccupied(owner), diagnosticShape);
						if (unlocked.Count != guest.p) {
							unlocked = Utils.FindPlacementByReverseBreadthFirstSearch2(
								Row, ToOccupied(owner), guest.p, PondPrefixSum, GetRectangleCandidates(guest.p));
						}
						SetOwner(owner, groupPoints, groupId);
						if (unlocked.Count == guest.p) {
							unlockingGroups.Add(groupId);
						}
					}

					if (unlockingGroups.Count > 0) {
						++moveFailedUnlockedByOneGroup;
					}
					moveFailedReports.Add(
						$"turn={turn:d4} p={guest.p:d3} free={freeGrass:d4} largest={largestComponent:d4} "
						+ $"components>=={largeComponents.Count}/{largeComponents.Sum(component => component.Count)} "
						+ $"boundary={boundaryGroups.Count} unlock={unlockingGroups.Count} "
						+ $"unlockSizes=[{string.Join(',', unlockingGroups.Select(groupId => active[groupId].Count))}]");
				}
			}

			string placement = reception.takes
				? DescribePlacement(reception.points)
				: "placement=reject";
			reports.Add(
				$"turn={turn:d4} guestP={guest.p:d3} guestV={guest.v:d8} "
				+ $"free={freeGrass:d4} largest={largestComponent:d4} small={smallComponents:d3} "
				+ $"active={active.Count:d3} {placement} {alternative}");
			occupiedSnapshots.Add(OccupiedCells(owner));
		}

		var moveFailedTurns = Enumerable.Range(0, M)
			.Where(turn => Decisions[turn].decision == "move-failed")
			.ToHashSet();
		var followedByMoveFailed = acceptedSpaceMetrics
			.Where(metric => Enumerable.Range(metric.turn + 1, Math.Min(50, M - metric.turn - 1))
				.Any(moveFailedTurns.Contains))
			.ToList();
		var notFollowedByMoveFailed = acceptedSpaceMetrics
			.Where(metric => Enumerable.Range(metric.turn + 1, Math.Min(50, M - metric.turn - 1))
				.Any(moveFailedTurns.Contains) == false)
			.ToList();

		int nonInterferingCount = 0;
		long nonInterferingValueSum = 0;
		long nonInterferingPersonSum = 0;
		foreach (var candidate in counterfactualCandidates) {
			bool interferes = false;
			for (int futureTurn = candidate.turn + 1; futureTurn < M; ++futureTurn) {
				if (Guests[futureTurn].s >= candidate.departureTime) {
					break;
				}
				if (Intersects(candidate.points, occupiedSnapshots[futureTurn])) {
					interferes = true;
					break;
				}
			}

			if (interferes == false) {
				++nonInterferingCount;
				nonInterferingValueSum += candidate.v;
				nonInterferingPersonSum += candidate.p;
			}
		}

		Console.WriteLine("[Emulate] turn reports");
		foreach (string report in reports) {
			Console.WriteLine(report);
		}
		Console.WriteLine(
			$"[Emulate] free average={(double)totalFreeGrass / M:f2} "
			+ $"small components={totalSmallComponents} "
			+ $"rejected with alternative={rejectedWithAlternative} "
			+ $"without alternative={rejectedWithoutAlternative}");
		Console.WriteLine("[Emulate] rejection reasons "
			+ string.Join(" ", rejectionCounts.OrderBy(pair => pair.Key)
				.Select(pair => $"{pair.Key}={pair.Value}")));
		Console.WriteLine(
			$"[Emulate] threshold alternative non-interfering={nonInterferingCount}/{counterfactualCandidates.Count} "
			+ $"valueSum={nonInterferingValueSum} personSum={nonInterferingPersonSum}");
		Console.WriteLine(
			$"[Emulate] move-failed diagnostics count={moveFailedCount} "
			+ $"large-component={moveFailedWithLargeComponent} "
			+ $"unlocked-by-one-group={moveFailedUnlockedByOneGroup}");
		foreach (string report in moveFailedReports) {
			Console.WriteLine($"[Emulate] move-failed {report}");
		}
		PrintSpaceMetricAverage("followed-by-move-failed", followedByMoveFailed);
		PrintSpaceMetricAverage("not-followed-by-move-failed", notFollowedByMoveFailed);

		HashSet<(int x, int y)> OccupiedCells(int[,] currentOwner)
		{
			var cells = new HashSet<(int x, int y)>();
			for (int x = 0; x < N; ++x) {
				for (int y = 0; y < N; ++y) {
					if (currentOwner[x, y] >= 0) {
						cells.Add((x, y));
					}
				}
			}
			return cells;
		}

		bool Intersects(IReadOnlyList<(int x, int y)> points, HashSet<(int x, int y)> occupied)
		{
			return points.Any(occupied.Contains);
		}

		int CountFreeAdjacency(bool[,] occupied)
		{
			int count = 0;
			for (int x = 0; x < N; ++x) {
				for (int y = 0; y < N; ++y) {
					if (Row[x][y] != '.' || occupied[x, y]) {
						continue;
					}
					if (x + 1 < N && Row[x + 1][y] == '.' && occupied[x + 1, y] == false) {
						++count;
					}
					if (y + 1 < N && Row[x][y + 1] == '.' && occupied[x, y + 1] == false) {
						++count;
					}
				}
			}
			return count;
		}

		int CountNearSquarePlacements(bool[,] occupied, int personCount)
		{
			var (shape, _) = Utils.CreateNearSquareShape(personCount);
			int shapeHeight = shape.Max(point => point.x) + 1;
			int shapeWidth = shape.Max(point => point.y) + 1;
			int count = 0;
			for (int baseX = 0; baseX + shapeHeight <= N; ++baseX) {
				for (int baseY = 0; baseY + shapeWidth <= N; ++baseY) {
					if (shape.All(point => Row[baseX + point.x][baseY + point.y] == '.'
						&& occupied[baseX + point.x, baseY + point.y] == false)) {
						++count;
					}
				}
			}
			return count;
		}

		void PrintSpaceMetricAverage(
			string label,
			IReadOnlyList<(int turn, int largest, int small, int freeAdjacency, int placements64, int placements96, int placements128)> metrics)
		{
			if (metrics.Count == 0) {
				Console.WriteLine($"[Emulate] placement-metrics {label}=0");
				return;
			}
			Console.WriteLine(
				$"[Emulate] placement-metrics {label} count={metrics.Count} "
				+ $"largest={metrics.Average(metric => metric.largest):f1} "
				+ $"small={metrics.Average(metric => metric.small):f1} "
				+ $"adjacency={metrics.Average(metric => metric.freeAdjacency):f1} "
				+ $"p64={metrics.Average(metric => metric.placements64):f1} "
				+ $"p96={metrics.Average(metric => metric.placements96):f1} "
				+ $"p128={metrics.Average(metric => metric.placements128):f1}");
		}

		HashSet<int> FindBoundaryGroups(int[,] currentOwner, IReadOnlyList<List<(int x, int y)>> components)
		{
			var groups = new HashSet<int>();
			foreach (var component in components) {
				foreach (var (x, y) in component) {
					foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }) {
						int nx = x + dx;
						int ny = y + dy;
						if ((uint)nx < (uint)N && (uint)ny < (uint)N && currentOwner[nx, ny] >= 0) {
							groups.Add(currentOwner[nx, ny]);
						}
					}
				}
			}
			return groups;
		}

		bool[,] ToOccupied(int[,] currentOwner)
		{
			var occupied = new bool[N, N];
			for (int x = 0; x < N; ++x) {
				for (int y = 0; y < N; ++y) {
					occupied[x, y] = currentOwner[x, y] >= 0;
				}
			}

			return occupied;
		}

		void SetOwner(int[,] currentOwner, IReadOnlyList<(int x, int y)> points, int groupId)
		{
			foreach (var (x, y) in points) {
				currentOwner[x, y] = groupId;
			}
		}

		void ClearOwner(int[,] currentOwner, IReadOnlyList<(int x, int y)> points, int groupId)
		{
			foreach (var (x, y) in points) {
				if (currentOwner[x, y] == groupId) {
					currentOwner[x, y] = -1;
				}
			}
		}

		bool CanOwn(int[,] currentOwner, IReadOnlyList<(int x, int y)> points, int groupId)
		{
			var seen = new HashSet<(int x, int y)>();
			foreach (var (x, y) in points) {
				if ((uint)x >= (uint)N || (uint)y >= (uint)N
					|| Row[x][y] != '.'
					|| seen.Add((x, y)) == false
					|| currentOwner[x, y] >= 0 && currentOwner[x, y] != groupId) {
					return false;
				}
			}

			return true;
		}

		List<List<(int x, int y)>> FindFreeComponents(int[,] currentOwner)
		{
			var visited = new bool[N, N];
			var components = new List<List<(int x, int y)>>();
			for (int x = 0; x < N; ++x) {
				for (int y = 0; y < N; ++y) {
					if (visited[x, y] || Row[x][y] != '.' || currentOwner[x, y] >= 0) {
						continue;
					}

					var component = new List<(int x, int y)>();
					var queue = new Queue<(int x, int y)>();
					visited[x, y] = true;
					queue.Enqueue((x, y));
					while (queue.Count > 0) {
						var point = queue.Dequeue();
						component.Add(point);
						foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }) {
							int nx = point.x + dx;
							int ny = point.y + dy;
							if ((uint)nx < (uint)N && (uint)ny < (uint)N
								&& visited[nx, ny] == false
								&& Row[nx][ny] == '.'
								&& currentOwner[nx, ny] < 0) {
								visited[nx, ny] = true;
								queue.Enqueue((nx, ny));
							}
						}
					}

					components.Add(component);
				}
			}

			return components;
		}

		string DescribePlacement(IReadOnlyList<(int x, int y)> points)
		{
			if (points.Count == 0) {
				return "placement=empty";
			}

			int minX = points.Min(point => point.x);
			int maxX = points.Max(point => point.x);
			int minY = points.Min(point => point.y);
			int maxY = points.Max(point => point.y);
			return $"placement=C={Utils.CalculateCompactness(points):f3} "
				+ $"box={maxX - minX + 1}x{maxY - minY + 1}";
		}
	}

	public IReadOnlyList<(int boxHeight, int boxWidth, int perimeter, int unusedArea, int imbalance)>
		GetRectangleCandidates(int p)
	{
		if (_rectangleCandidates.TryGetValue(p, out var candidates) == false) {
			candidates = Utils.CreateRectangleCandidates(N, N, p);
			_rectangleCandidates.Add(p, candidates);
		}
		return candidates;
	}

	private int CountPondClusters()
	{
		var visited = new bool[N, N];
		int[] dx = [-1, 1, 0, 0];
		int[] dy = [0, 0, -1, 1];
		int clusterCount = 0;

		for (int x = 0; x < N; ++x) {
			for (int y = 0; y < N; ++y) {
				if (visited[x, y] || Row[x][y] != '#') {
					continue;
				}

				++clusterCount;
				var queue = new Queue<(int x, int y)>();
				queue.Enqueue((x, y));
				visited[x, y] = true;
				while (queue.TryDequeue(out var point)) {
					for (int direction = 0; direction < 4; ++direction) {
						int nx = point.x + dx[direction];
						int ny = point.y + dy[direction];
						if (nx >= 0 && nx < N && ny >= 0 && ny < N
							&& visited[nx, ny] == false && Row[nx][ny] == '#') {
							visited[nx, ny] = true;
							queue.Enqueue((nx, ny));
						}
					}
				}
			}
		}

		return clusterCount;
	}

	private void ConvertSmallEnclosedGrassToPonds()
	{
		var visited = new bool[N, N];
		char[][] temp = Row.Select(row => row.ToCharArray()).ToArray();
		int[] dx = [-1, 1, 0, 0];
		int[] dy = [0, 0, -1, 1];

		for (int x = 0; x < N; ++x) {
			for (int y = 0; y < N; ++y) {
				if (visited[x, y] || Row[x][y] != '.') {
					continue;
				}

				var component = new List<(int x, int y)>();
				var queue = new Queue<(int x, int y)>();
				queue.Enqueue((x, y));
				visited[x, y] = true;
				bool enclosed = true;

				while (queue.TryDequeue(out var point)) {
					component.Add(point);
					for (int direction = 0; direction < 4; ++direction) {
						int nx = point.x + dx[direction];
						int ny = point.y + dy[direction];
						if (nx < 0 || nx >= N || ny < 0 || ny >= N) {
							enclosed = false;
							continue;
						}
						if (Row[nx][ny] == '.') {
							if (visited[nx, ny] == false) {
								visited[nx, ny] = true;
								queue.Enqueue((nx, ny));
							}
						} else if (Row[nx][ny] != '#') {
							enclosed = false;
						}
					}
				}

				if (enclosed && component.Count < 4) {
					foreach (var (cx, cy) in component) {
						temp[cx][cy] = '#';
					}
				}
			}
		}

		for (int x = 0; x < N; ++x) {
			Row[x] = new string(temp[x]);
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
		int localTestCaseRunCount,
		double judgePreTestCaseCount,
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