using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace SplineMesh {
    /// <summary>
    /// Mathematical object for cubic Bézier curve definition.
    /// It is made of two spline nodes which hold the four needed control points : two positions and two directions
    /// It provides methods to get positions and tangent along the curve, specifying a distance or a ratio, plus the curve length.
    /// 
    /// Note that a time of 0.5 and half the total distance won't necessarily define the same curve point as the curve curvature is not linear.
    /// </summary>
    [Serializable]
    public class CubicBezierCurve {
        private const int STEP_COUNT = 30;
        private const float T_STEP = 1.0f / STEP_COUNT;

        private readonly CurveSample[] samples = new CurveSample[STEP_COUNT + 1];
        
        /// <summary>
        /// Event raised when the curve changes.
        /// </summary>
        public event Action Changed;

        [FormerlySerializedAs("n1")]
        [SerializeField]
        private SplineNode _node1;
        [FormerlySerializedAs("n2")]
        [SerializeField]
        private SplineNode _node2;

        private bool _isDirty;
        /// <summary>
        /// Length of the curve in world unit.
        /// </summary>
        public float Length { get; private set; }

        /// <summary>
        /// Build a new cubic Bézier curve between two given spline node.
        /// </summary>
        /// <param name="node1"></param>
        /// <param name="node2"></param>
        public CubicBezierCurve(SplineNode node1, SplineNode node2) {
            _node1 = node1;
            _node2 = node2;
            _isDirty = true;
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

        /// <summary>
        /// Convinent method to get the third control point of the curve, as the direction of the end spline node indicates the starting tangent of the next curve.
        /// </summary>
        /// <returns></returns>
        public float3 GetInverseDirection() {
            return 2 * _node2.Position - _node2.Direction;
        }

        /// <summary>
        /// Returns point on curve at given time. Time must be between 0 and 1.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private float3 GetLocation(float t) {
            var omt = 1f - t;
            var omt2 = omt * omt;
            var t2 = t * t;
            return
                _node1.Position * (omt2 * omt) +
                _node1.Direction * (3f * omt2 * t) +
                GetInverseDirection() * (3f * omt * t2) +
                _node2.Position * (t2 * t);
        }

        /// <summary>
        /// Returns tangent of curve at given time. Time must be between 0 and 1.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private float3 GetTangent(float t) {
            var omt = 1f - t;
            var omt2 = omt * omt;
            var t2 = t * t;
            var tangent =
                _node1.Position * -omt2 +
                _node1.Direction * (3 * omt2 - 2 * omt) +
                GetInverseDirection() * (-3 * t2 + 2 * t) +
                _node2.Position * (t2);
            return math.normalize(tangent);
        }

        private float3 GetUp(float t) {
            return math.lerp(_node1.Up, _node2.Up, t);
        }

        private float2 GetScale(float t) {
            return math.lerp(_node1.Scale, _node2.Scale, t);
        }

        private float GetRoll(float t) {
            return math.lerp(_node1.Roll, _node2.Roll, t);
        }

        public void ComputeSamples() {
            if (!_isDirty) return;
            Length = 0;
            var previousPosition = GetLocation(0);
            var index = 0;
            for (float t = 0; t < 1; t += T_STEP) {
                var position = GetLocation(t);
                Length += math.distance(previousPosition, position);
                previousPosition = position;
                samples[index++] = CreateSample(Length, t);
            }
            Length += math.distance(previousPosition, GetLocation(1));
            samples[index] = CreateSample(Length, 1);
            _isDirty = false;
            
            Changed?.Invoke();
        }

        private CurveSample CreateSample(float distance, float time) {
            return new CurveSample(
                GetLocation(time),
                GetTangent(time),
                GetUp(time),
                GetScale(time),
                GetRoll(time),
                distance,
                time);
        }

        /// <summary>
        /// Returns an interpolated sample of the curve, containing all curve data at this time.
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public CurveSample GetSample(float time) {
            AssertTimeInBounds(time);
            var previous = samples[0];
            var next = default(CurveSample);
            var found = false;
            foreach (var cp in samples) {
                if (cp.timeInCurve >= time) {
                    next = cp;
                    found = true;
                    break;
                }
                previous = cp;
            }
            if (!found) throw new Exception("Can't find curve samples.");
            var t = next == previous ? 0 : (time - previous.timeInCurve) / (next.timeInCurve - previous.timeInCurve);

            return CurveSample.Lerp(previous, next, t);
        }

        /// <summary>
        /// Returns an interpolated sample of the curve, containing all curve data at this distance.
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public CurveSample GetSampleAtDistance(float d) {
            if (d < 0 || d > Length)
                throw new ArgumentException("Distance must be positive and less than curve length. Length = " + Length + ", given distance was " + d);

            var previous = samples[0];
            var next = default(CurveSample);
            var found = false;
            foreach (var cp in samples) {
                if (cp.distanceInCurve >= d) {
                    next = cp;
                    found = true;
                    break;
                }
                previous = cp;
            }
            if (!found) throw new Exception("Can't find curve samples.");
            var t = next == previous ? 0 : (d - previous.distanceInCurve) / (next.distanceInCurve - previous.distanceInCurve);

            return CurveSample.Lerp(previous, next, t);
        }

        private static void AssertTimeInBounds(float time) {
            if (time < 0 || time > 1) throw new ArgumentException("Time must be between 0 and 1 (was " + time + ").");
        }

        public CurveSample GetProjectionSample(Vector3 pointToProject) {
            var minSqrDistance = float.PositiveInfinity;
            var closestIndex = -1;
            var i = 0;
            foreach (var sample in samples) {
                var sqrDistance = ((Vector3)sample.location - pointToProject).sqrMagnitude;
                if (sqrDistance < minSqrDistance) {
                    minSqrDistance = sqrDistance;
                    closestIndex = i;
                }
                i++;
            }
            CurveSample previous, next;
            if(closestIndex == 0) {
                previous = samples[closestIndex];
                next = samples[closestIndex + 1];
            } else if(closestIndex == samples.Length - 1) {
                previous = samples[closestIndex - 1];
                next = samples[closestIndex];
            } else {
                var toPreviousSample = (pointToProject - (Vector3)samples[closestIndex - 1].location).sqrMagnitude;
                var toNextSample = (pointToProject - (Vector3)samples[closestIndex + 1].location).sqrMagnitude;
                if (toPreviousSample < toNextSample) {
                    previous = samples[closestIndex - 1];
                    next = samples[closestIndex];
                } else {
                    previous = samples[closestIndex];
                    next = samples[closestIndex + 1];
                }
            }

            var onCurve = Vector3.Project(pointToProject - (Vector3)previous.location, (Vector3)next.location - (Vector3)previous.location) + (Vector3)previous.location;
            var rate = (onCurve - (Vector3)previous.location).sqrMagnitude / ((Vector3)next.location - (Vector3)previous.location).sqrMagnitude;
            rate = math.clamp(rate, 0, 1);
            var result = CurveSample.Lerp(previous, next, rate);
            return result;
        }
    }
}
