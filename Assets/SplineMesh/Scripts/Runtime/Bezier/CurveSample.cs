using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace SplineMesh {
    [BurstCompile]
    public struct CurveSampleBentJob : IJobFor {
        [ReadOnly]
        public NativeArray<CurveSample> Curves;
        [ReadOnly]
        public NativeArray<MeshVertex> VerticesIn;
        [WriteOnly]
        public NativeArray<Vector3> VerticesOut;
        [WriteOnly]
        public NativeArray<Vector3> NormalsOut;

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
                if (rotation == Quaternion.identity) {
                    var upVector = math.cross(tangent, Vector3.Cross(Quaternion.AngleAxis(roll, Vector3.forward) * up, tangent).normalized);
                    rotation = Quaternion.LookRotation(tangent, upVector);
                }
                return rotation;
            }
        }

        public CurveSample(Vector3 location, Vector3 tangent, Vector3 up, Vector2 scale, float roll, float distanceInCurve, float timeInCurve) {
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
                Vector3.Lerp(a.location, b.location, t),
                Vector3.Lerp(a.tangent, b.tangent, t).normalized,
                Vector3.Lerp(a.up, b.up, t),
                Vector2.Lerp(a.scale, b.scale, t),
                Mathf.Lerp(a.roll, b.roll, t),
                Mathf.Lerp(a.distanceInCurve, b.distanceInCurve, t),
                Mathf.Lerp(a.timeInCurve, b.timeInCurve, t));
        }

        public MeshVertex GetBent(MeshVertex vert) {
            var res = new MeshVertex(vert.position, vert.normal, vert.uv);

            // application of scale
            res.position = Vector3.Scale(res.position, new Vector3(0, scale.y, scale.x));

            // application of roll
            res.position = Quaternion.AngleAxis(roll, Vector3.right) * res.position;
            res.normal = Quaternion.AngleAxis(roll, Vector3.right) * res.normal;

            // reset X value
            res.position.x = 0;

            // application of the rotation + location
            Quaternion q = Rotation * Quaternion.Euler(0, -90, 0);
            res.position = q * res.position + (Vector3)location;
            res.normal = q * res.normal;
            return res;
        }
    }
}
