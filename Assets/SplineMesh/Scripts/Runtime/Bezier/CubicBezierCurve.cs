using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace SplineMesh {
    [BurstCompile]
    public struct ComputeCurveLengthJob : IJob {
        public NativeArray<CurveSample> Samples;
        public NativeArray<float> Length;
        public void Execute() {
            var length = 0.0f;
            for (var i = 0; i <= CubicBezierCurve.STEP_COUNT; i++) {
                var sample = Samples[i];
                if (i > 0) length += math.distance(Samples[i - 1].Location, sample.Location);
                sample.DistanceInCurve = length;
                Samples[i] = sample;
            }

            Length[0] = length;
        }
    }

    [BurstCompile]
    public struct ComputeSamplesJob : IJobParallelFor {
        public SplineNode Node1;
        public SplineNode Node2;
        [WriteOnly]
        public NativeArray<CurveSample> Samples;

        public void Execute(int i) {
            var time = (float)i / CubicBezierCurve.STEP_COUNT;
            //Location
            var omt = 1f - time;
            var omt2 = omt * omt;
            var t2 = time * time;
            var inverseDirection = 2 * Node2.Position - Node2.Direction;
            var location = Node1.Position * (omt2 * omt) +
                           Node1.Direction * (3f * omt2 * time) +
                           inverseDirection * (3f * omt * t2) +
                           Node2.Position * (t2 * time);
            //Tangent
            var tangent =
                Node1.Position * -omt2 +
                Node1.Direction * (3 * omt2 - 2 * omt) +
                inverseDirection * (-3 * t2 + 2 * time) +
                Node2.Position * t2;
            tangent = math.normalize(tangent);
            //Up
            var up = math.lerp(Node1.Up, Node2.Up, time);
            //Scale
            var scale = math.lerp(Node1.Scale, Node2.Scale, time);
            //Roll
            var roll = math.radians(math.lerp(Node1.Roll, Node2.Roll, time));

            Samples[i] = new CurveSample(
                location,
                tangent,
                up,
                scale,
                roll,
                0.0f,
                time);
        }
    }
    /// <summary>
    /// Mathematical object for cubic Bézier curve definition.
    /// It is made of two spline nodes which hold the four needed control points : two positions and two directions
    /// It provides methods to get positions and tangent along the curve, specifying a distance or a ratio, plus the curve length.
    /// 
    /// Note that a time of 0.5 and half the total distance won't necessarily define the same curve point as the curve curvature is not linear.
    /// </summary>
    [Serializable]
    public class CubicBezierCurve  : IDisposable{
        public const int STEP_COUNT = 30;

        [FormerlySerializedAs("n1")]
        [SerializeField]
        private SplineNode _node1;
        [FormerlySerializedAs("n2")]
        [SerializeField]
        private SplineNode _node2;
        
        private NativeArray<CurveSample> _samples;
        private NativeArray<float> _length;

        private bool _isDirty;

        /// <summary>
        /// Length of the curve in world unit.
        /// </summary>
        public float Length => _length[0];

        /// <summary>
        /// Build a new cubic Bézier curve between two given spline node.
        /// </summary>
        /// <param name="node1"></param>
        /// <param name="node2"></param>
        public CubicBezierCurve(SplineNode node1, SplineNode node2) {
            _node1 = node1;
            _node2 = node2;
            _isDirty = true;
            _samples = new NativeArray<CurveSample>(STEP_COUNT + 1, Allocator.Persistent);
            _length = new NativeArray<float>(1, Allocator.Persistent);
        }
        
        public void Dispose() {
            _samples.Dispose();
            _length.Dispose();
        }

        /// <summary>
        /// Change the start node of the curve.
        /// </summary>
        /// <param name="node"></param>
        public void ConnectStart(SplineNode node) {
            _node1 = node;
            _isDirty = true;
        }

        /// <summary>
        /// Change the end node of the curve.
        /// </summary>
        /// <param name="node"></param>
        public void ConnectEnd(SplineNode node) {
            _node2 = node;
            _isDirty = true;
        }

        public void SetDirty() {
            _isDirty = true;
        }

        public JobHandle ComputeSamples(JobHandle jobHandle) {
            if (!_isDirty) return jobHandle;
            var job = new ComputeSamplesJob {
                Node1 = _node1,
                Node2 = _node2,
                Samples = _samples,
            };
            jobHandle = job.Schedule(STEP_COUNT + 1, 4, jobHandle);
            var computeCurveLengthJob = new ComputeCurveLengthJob {
                Samples = _samples,
                Length = _length
            };
            _isDirty = false;
            return computeCurveLengthJob.Schedule(jobHandle);
        }

        /// <summary>
        /// Returns an interpolated sample of the curve, containing all curve data at this time.
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public CurveSamplesLerpPair GetSample(float time) {
            AssertTimeInBounds(time);
            var previous = _samples[0];
            var next = default(CurveSample);
            var found = false;

            foreach (var cp in _samples) {
                if (cp.TimeInCurve >= time) {
                    next = cp;
                    found = true;
                    break;
                }
                previous = cp;
            }
            if (!found) throw new Exception("Can't find curve samples.");
            var t = next == previous ? 0 : (time - previous.TimeInCurve) / (next.TimeInCurve - previous.TimeInCurve);

            return new CurveSamplesLerpPair(previous, next, t);
        }

        /// <summary>
        /// Returns an interpolated sample of the curve, containing all curve data at this distance.
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public CurveSamplesLerpPair GetSampleAtDistance(float d) {
            var nextIndex = -1;
            var samplesCount = _samples.Length;
            for (var i = 0; i < samplesCount; i++) {
                if (_samples[i].DistanceInCurve < d) continue;
                nextIndex = i;
                break;
            }
            if (nextIndex == 0) {
                return new CurveSamplesLerpPair(_samples[nextIndex], default, 0);
            }
            var next = _samples[nextIndex];
            var previous = _samples[nextIndex - 1];
            var t = (d - previous.DistanceInCurve) / (next.DistanceInCurve - previous.DistanceInCurve);
            return new CurveSamplesLerpPair(previous, next, t);
        }

        private static void AssertTimeInBounds(float time) {
            if (time < 0 || time > 1) throw new ArgumentException("Time must be between 0 and 1 (was " + time + ").");
        }

        public CurveSamplesLerpPair GetProjectionSample(Vector3 pointToProject) {
            var minSqrDistance = float.PositiveInfinity;
            var closestIndex = -1;
            var i = 0;
            foreach (var sample in _samples) {
                var sqrDistance = ((Vector3)sample.Location - pointToProject).sqrMagnitude;
                if (sqrDistance < minSqrDistance) {
                    minSqrDistance = sqrDistance;
                    closestIndex = i;
                }
                i++;
            }
            CurveSample previous, next;
            if(closestIndex == 0) {
                previous = _samples[closestIndex];
                next = _samples[closestIndex + 1];
            } else if(closestIndex == _samples.Length - 1) {
                previous = _samples[closestIndex - 1];
                next = _samples[closestIndex];
            } else {
                var toPreviousSample = (pointToProject - (Vector3)_samples[closestIndex - 1].Location).sqrMagnitude;
                var toNextSample = (pointToProject - (Vector3)_samples[closestIndex + 1].Location).sqrMagnitude;
                if (toPreviousSample < toNextSample) {
                    previous = _samples[closestIndex - 1];
                    next = _samples[closestIndex];
                } else {
                    previous = _samples[closestIndex];
                    next = _samples[closestIndex + 1];
                }
            }

            var onCurve = Vector3.Project(pointToProject - (Vector3)previous.Location, (Vector3)next.Location - (Vector3)previous.Location) + (Vector3)previous.Location;
            var rate = (onCurve - (Vector3)previous.Location).sqrMagnitude / ((Vector3)next.Location - (Vector3)previous.Location).sqrMagnitude;
            rate = math.clamp(rate, 0, 1);
            return new CurveSamplesLerpPair(previous, next, rate);
        }
    }
}
