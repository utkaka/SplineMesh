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
            
            var bent = new MeshVertex(vertexIn.position, vertexIn.normal, vertexIn.uv);
            
            // application of scale
            bent.position = new float3(0.0f, bent.position.y * curve.scale.y, bent.position.z * curve.scale.x);
            
            // application of roll
            bent.position = math.mul(quaternion.AxisAngle(new float3(1.0f, 0.0f ,0.0f), curve.roll), bent.position);
            bent.normal = math.mul(quaternion.AxisAngle(new float3(1.0f, 0.0f ,0.0f), curve.roll), bent.normal);
            bent.position.x = 0;

            // application of the rotation + location
            //quaternion.RotateY(-90.0f)
            var q =  math.mul(curve.Rotation, quaternion.Euler(0, -90, 0));
            bent.position = math.mul(q, bent.position) + curve.location;
            bent.normal = math.mul(q, bent.normal);

            VerticesOut[i] = bent.position;
            NormalsOut[i] = bent.normal;
        }
    }
    /// <summary>
    /// Imutable class containing all data about a point on a cubic bezier curve.
    /// </summary>
    public struct CurveSample
    {
        public readonly float3 location;
        public readonly float3 tangent;
        public readonly float3 up;
        public readonly float2 scale;
        public readonly float roll;
        public readonly float distanceInCurve;
        public readonly float timeInCurve;

        private quaternion rotation;

        /// <summary>
        /// Rotation is a look-at quaternion calculated from the tangent, roll and up vector. Mixing non zero roll and custom up vector is not advised.
        /// </summary>
        public quaternion Rotation {
            get {
                if (!rotation.Equals(quaternion.identity)) return rotation;
                var upVector = math.cross(tangent,
                    math.normalize(math.cross(
                        math.mul(quaternion.AxisAngle(new float3(0.0f, 0.0f, 1.0f), roll), up), tangent)));
                rotation = quaternion.LookRotation(tangent, upVector);
                return rotation;
            }
        }

        public CurveSample(float3 location, float3 tangent, float3 up, float2 scale, float roll, float distanceInCurve, float timeInCurve) {
            this.location = location;
            this.tangent = tangent;
            this.up = up;
            this.roll = roll;
            this.scale = scale;
            this.distanceInCurve = distanceInCurve;
            this.timeInCurve = timeInCurve;
            rotation = Quaternion.identity;
        }

        public bool Equals(CurveSample other) {
            return math.all(location == other.location) &&
                   math.all(tangent == other.tangent) &&
                   math.all(up == other.up) &&
                   math.all(scale == other.scale) &&
                   math.abs(roll - other.roll) < float.Epsilon &&
                   math.abs(distanceInCurve - other.distanceInCurve) < float.Epsilon &&
                   math.abs(timeInCurve - other.timeInCurve) < float.Epsilon;
        }

        public override bool Equals(object obj) {
            return obj is CurveSample other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(location, tangent, up, scale, roll, distanceInCurve, timeInCurve);
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
                math.lerp(a.location, b.location, t),
                math.normalize(math.lerp(a.tangent, b.tangent, t)),
                math.lerp(a.up, b.up, t),
                math.lerp(a.scale, b.scale, t),
                math.lerp(a.roll, b.roll, t),
                math.lerp(a.distanceInCurve, b.distanceInCurve, t),
                math.lerp(a.timeInCurve, b.timeInCurve, t));
        }

        public MeshVertex GetBent(MeshVertex vert) {
            var res = new MeshVertex(vert.position, vert.normal, vert.uv);

            // application of scale
            res.position = new float3(0.0f, res.position.y * scale.y, res.position.z * scale.x);

            // application of roll
            res.position = math.mul(quaternion.AxisAngle(new float3(1.0f, 0.0f ,0.0f), roll), res.position);
            res.normal = math.mul(quaternion.AxisAngle(new float3(1.0f, 0.0f ,0.0f), roll), res.normal);

            // reset X value
            res.position.x = 0;

            // application of the rotation + location
            Quaternion q = math.mul(Rotation, quaternion.Euler(0, -90, 0));
            res.position = math.mul(q, res.position) + location;
            res.normal = math.mul(q, res.normal);
            return res;
        }
    }
}
