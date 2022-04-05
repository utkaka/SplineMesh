using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace SplineMesh {
	[BurstCompile(FloatMode = FloatMode.Fast)]
	public struct CurveSamplesLerpJob : IJobParallelFor {
		[ReadOnly]
		public NativeArray<CurveSamplesLerpPair> CurveSamplesPairs;
		[WriteOnly]
		public NativeArray<CurveSample> Results;

		public void Execute(int index) {
			Results[index] = CurveSamplesPairs[index].Lerp();
		}
	}
	
	[BurstCompile(FloatMode = FloatMode.Fast)]
	public struct CurveSamplesGroupsJob : IJobParallelFor {
		[ReadOnly]
		public NativeArray<CurveSample> CurveSampleGroups;
		[ReadOnly]
		public NativeArray<int> VerticesToSampleGroups;
		[WriteOnly]
		public NativeArray<CurveSample> Result;

		public void Execute(int index) {
			Result[index] = CurveSampleGroups[VerticesToSampleGroups[index]];
		}
	}
	
	
	public struct CurveSamplesLerpPair {
		public readonly CurveSample Sample1;
		public readonly CurveSample Sample2;
		public readonly float Time;

		public CurveSamplesLerpPair(CurveSample sample1, CurveSample sample2, float time) {
			Sample1 = sample1;
			Sample2 = sample2;
			Time = time;
		}
		
		public CurveSample Lerp() {
			return new CurveSample(
				math.lerp(Sample1.Location, Sample2.Location, Time),
				math.normalize(math.lerp(Sample1.Tangent, Sample2.Tangent, Time)),
				math.lerp(Sample1.Up, Sample2.Up, Time),
				math.lerp(Sample1.Scale, Sample2.Scale, Time),
				math.lerp(Sample1.Roll, Sample2.Roll, Time),
				math.lerp(Sample1.DistanceInCurve, Sample2.DistanceInCurve, Time),
				math.lerp(Sample1.TimeInCurve, Sample2.TimeInCurve, Time));
		}
	}
}