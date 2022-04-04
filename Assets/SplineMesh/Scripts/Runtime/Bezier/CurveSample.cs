using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace SplineMesh {
    [BurstCompile]
    public struct CurveSampleBentJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<CurveSample> Curves;
        [ReadOnly]
        public NativeArray<MeshVertex> VerticesIn;
        [WriteOnly]
        public NativeArray<float3> VerticesOut;
        [WriteOnly]
        public NativeArray<float3> NormalsOut;

        public void Execute(int i) {
            var curve = Curves[i];
            var vertexIn = VerticesIn[i];

            var position = vertexIn.position;
            var normal = vertexIn.normal;
            
            // application of scale
            position = new float3(0.0f, position.y * curve.Scale.y, position.z * curve.Scale.x);

            var rollAxisAngle = quaternion.AxisAngle(new float3(1.0f, 0.0f, 0.0f), curve.Roll);
            
            // application of roll
            position = math.mul(rollAxisAngle, position);
            normal = math.mul(rollAxisAngle, normal);
            position.x = 0;

            // application of the rotation + location
            var q =  math.mul(curve.Rotation, quaternion.Euler(0.0f, -1.57079632679f, 0.0f));
            position = math.mul(q, position) + curve.Location;
            normal = math.mul(q, normal);
            

            VerticesOut[i] = position;
            NormalsOut[i] = normal;
            
        }
    }
    /// <summary>
    /// Imutable class containing all data about a point on a cubic bezier curve.
    /// </summary>
    public struct CurveSample
    {
        public readonly float3 Location;
        public readonly float3 Tangent;
        public readonly float3 Up;
        public readonly float2 Scale;
        public readonly float Roll;
        public readonly float TimeInCurve;
        
        public float DistanceInCurve;

        private quaternion _rotation;
        /// <summary>
        /// Rotation is a look-at quaternion calculated from the tangent, roll and up vector. Mixing non zero roll and custom up vector is not advised.
        /// </summary>
        public quaternion Rotation {
            get {
                if (!_rotation.Equals(quaternion.identity)) return _rotation;
                var upVector = math.cross(Tangent,
                    math.normalize(math.cross(
                        math.mul(quaternion.AxisAngle(new float3(0.0f, 0.0f, 1.0f), Roll), Up), Tangent)));
                _rotation = quaternion.LookRotationSafe(Tangent, upVector);
                return _rotation;
            }
        }

        public CurveSample(float3 location, float3 tangent, float3 up, float2 scale, float roll, float distanceInCurve, float timeInCurve) {
            Location = location;
            Tangent = tangent;
            Up = up;
            Roll = roll;
            Scale = scale;
            DistanceInCurve = distanceInCurve;
            TimeInCurve = timeInCurve;
            _rotation = quaternion.identity;
        }

        public bool Equals(CurveSample other) {
            return math.all(Location == other.Location) &&
                   math.all(Tangent == other.Tangent) &&
                   math.all(Up == other.Up) &&
                   math.all(Scale == other.Scale) &&
                   math.abs(Roll - other.Roll) < float.Epsilon &&
                   math.abs(DistanceInCurve - other.DistanceInCurve) < float.Epsilon &&
                   math.abs(TimeInCurve - other.TimeInCurve) < float.Epsilon;
        }

        public override bool Equals(object obj) {
            return obj is CurveSample other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(Location, Tangent, Up, Scale, Roll, DistanceInCurve, TimeInCurve);
        }

        public static bool operator ==(CurveSample cs1, CurveSample cs2) {
            return cs1.Equals(cs2);
        }

        public static bool operator !=(CurveSample cs1, CurveSample cs2) {
            return !cs1.Equals(cs2);
        }

        /// <summary>
        /// Linearly interpolates between two curve samples.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public static CurveSample Lerp(CurveSample a, CurveSample b, float t) {
            return new CurveSample(
                math.lerp(a.Location, b.Location, t),
                math.normalize(math.lerp(a.Tangent, b.Tangent, t)),
                math.lerp(a.Up, b.Up, t),
                math.lerp(a.Scale, b.Scale, t),
                math.lerp(a.Roll, b.Roll, t),
                math.lerp(a.DistanceInCurve, b.DistanceInCurve, t),
                math.lerp(a.TimeInCurve, b.TimeInCurve, t));
        }

        public MeshVertex GetBent(MeshVertex vert) {
            var res = new MeshVertex(vert.position, vert.normal, vert.uv);

            // application of scale
            res.position = new float3(0.0f, res.position.y * Scale.y, res.position.z * Scale.x);

            // application of roll
            res.position = math.mul(quaternion.AxisAngle(new float3(1.0f, 0.0f ,0.0f), Roll), res.position);
            res.normal = math.mul(quaternion.AxisAngle(new float3(1.0f, 0.0f ,0.0f), Roll), res.normal);

            // reset X value
            res.position.x = 0;

            // application of the rotation + location
            var q = math.mul(Rotation, quaternion.Euler(0, -1.57079632679f, 0));
            res.position = math.mul(q, res.position) + Location;
            res.normal = math.mul(q, res.normal);
            return res;
        }
    }
}
