using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace SplineMesh {
    /// <summary>
    /// A component that creates a deformed mesh from a given one along the given spline segment.
    /// The source mesh will always be bended along the X axis.
    /// It can work on a cubic bezier curve or on any interval of a given spline.
    /// On the given interval, the mesh can be place with original scale, stretched, or repeated.
    /// The resulting mesh is stored in a MeshFilter component and automaticaly updated on the next update if the spline segment change.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [ExecuteInEditMode]
    public class MeshBender : MonoBehaviour {
        private bool isDirty;
        private bool isSourceDirty;
        private Mesh result;
        private bool useSpline;
        private Spline spline;
        private float intervalStart, intervalEnd;
        private CubicBezierCurve curve;
        private Dictionary<float, CurveSample> sampleCache = new Dictionary<float, CurveSample>();
        
        private MeshVertex[] _sourceVertices;
        private CurveSample[] _curveSamples;
        private int[] _triangles;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Vector2[] _uv;
        private Vector2[] _uv2;
        private Vector2[] _uv3;
        private Vector2[] _uv4;
        private Vector2[] _uv5;
        private Vector2[] _uv6;
        private Vector2[] _uv7;
        private Vector2[] _uv8;

        private SourceMesh source;
        /// <summary>
        /// The source mesh to bend.
        /// </summary>
        public SourceMesh Source {
            get { return source; }
            set {
                if (value == source) return;
                _sourceVertices = value.Vertices;
                isSourceDirty = true;
                SetDirty();
                source = value;
            }
        }

        private int _repetitionCount;
        private FillingMode mode = FillingMode.StretchToInterval;
        /// <summary>
        /// The scaling mode along the spline
        /// </summary>
        public FillingMode Mode {
            get { return mode; }
            set {
                if (value == mode) return;
                SetDirty();
                mode = value;
            }
        }

        /// <summary>
        /// Sets a curve along which the mesh will be bent.
        /// The mesh will be updated if the curve changes.
        /// </summary>
        /// <param name="curve">The <see cref="CubicBezierCurve"/> to bend the source mesh along.</param>
        public void SetInterval(CubicBezierCurve curve) {
            if (this.curve == curve) return;
            if (curve == null) throw new ArgumentNullException("curve");
            if (this.curve != null) {
                this.curve.Changed -= SetDirty;
            }
            this.curve = curve;
            spline = null;
            curve.Changed += SetDirty;
            useSpline = false;
            SetDirty();
        }

        /// <summary>
        /// Sets a spline's interval along which the mesh will be bent.
        /// If interval end is absent or set to 0, the interval goes from start to spline length.
        /// The mesh will be update if any of the curve changes on the spline, including curves
        /// outside the given interval.
        /// </summary>
        /// <param name="spline">The <see cref="SplineMesh"/> to bend the source mesh along.</param>
        /// <param name="intervalStart">Distance from the spline start to place the mesh minimum X.<param>
        /// <param name="intervalEnd">Distance from the spline start to stop deforming the source mesh.</param>
        public void SetInterval(Spline spline, float intervalStart, float intervalEnd = 0) {
            if (this.spline == spline && Math.Abs(this.intervalStart - intervalStart) < float.Epsilon &&
                Math.Abs(this.intervalEnd - intervalEnd) < float.Epsilon) return;
            if (spline == null) throw new ArgumentNullException("spline");
            if (intervalStart < 0 || intervalStart >= spline.Length) {
                throw new ArgumentOutOfRangeException("interval start must be 0 or greater and lesser than spline length (was " + intervalStart + ")");
            }
            if (intervalEnd != 0 && intervalEnd <= intervalStart || intervalEnd > spline.Length) {
                throw new ArgumentOutOfRangeException("interval end must be 0 or greater than interval start, and lesser than spline length (was " + intervalEnd + ")");
            }

            if (spline != this.spline) {
                if (this.spline != null) {
                    this.spline.Changed -= SetDirty;
                }
                this.spline = spline;
                spline.Changed += SetDirty;   
            }
            
            curve = null;
            this.intervalStart = intervalStart;
            this.intervalEnd = intervalEnd;
            useSpline = true;
            SetDirty();
        }

        private void OnEnable() {
            if(GetComponent<MeshFilter>().sharedMesh != null) {
                result = GetComponent<MeshFilter>().sharedMesh;
            } else {
                GetComponent<MeshFilter>().sharedMesh = result = new Mesh();
                result.name = "Generated by " + GetType().Name;
            }
        }

        private void LateUpdate() {
            ComputeIfNeeded();
        }

        public void ComputeIfNeeded() {
            if (isDirty) {
                Compute();
            }
        }

        private void SetDirty() {
            isDirty = true;
        }

        /// <summary>
        /// Bend the mesh. This method may take time and should not be called more than necessary.
        /// Consider using <see cref="ComputeIfNeeded"/> for faster result.
        /// </summary>
        private  void Compute() {
            isDirty = false;
            switch (Mode) {
                case FillingMode.Once:
                    FillOnce();
                    break;
                case FillingMode.Repeat:
                    FillRepeat();
                    break;
                case FillingMode.StretchToInterval:
                    FillStretch();
                    break;
            }

            isSourceDirty = false;
        }

        private void OnDestroy() {
            if(curve != null) {
                curve.Changed -= Compute;
            }
        }

        /// <summary>
        /// The mode used by <see cref="MeshBender"/> to bend meshes on the interval.
        /// </summary>
        public enum FillingMode {
            /// <summary>
            /// In this mode, source mesh will be placed on the interval by preserving mesh scale.
            /// Vertices that are beyond interval end will be placed on the interval end.
            /// </summary>
            Once,
            /// <summary>
            /// In this mode, the mesh will be repeated to fill the interval, preserving
            /// mesh scale.
            /// This filling process will stop when the remaining space is not enough to
            /// place a whole mesh, leading to an empty interval.
            /// </summary>
            Repeat,
            /// <summary>
            /// In this mode, the mesh is deformed along the X axis to fill exactly the interval.
            /// </summary>
            StretchToInterval
        }

        private void FillOnce() {
            if (isSourceDirty) {
                _triangles = source.Triangles;
                _uv = source.UV;
                _uv2 = source.UV2;
                _uv3 = source.UV3;
                _uv4 = source.UV4;
                _uv5 = source.UV5;
                _uv6 = source.UV6;
                _uv7 = source.UV7;
                _uv8 = source.UV8;
                _vertices = new Vector3[_sourceVertices.Length];
                _normals = new Vector3[_sourceVertices.Length];
            }

            sampleCache.Clear();
            // for each mesh vertex, we found its projection on the curve
            for (var i = 0; i < _sourceVertices.Length; i++) {
                var vert = _sourceVertices[i];
                var distance = vert.position.x - source.MinX;
                if (!sampleCache.TryGetValue(distance, out var sample)) {
                    if (!useSpline) {
                        if (distance > curve.Length) distance = curve.Length;
                        sample = curve.GetSampleAtDistance(distance);
                    } else {
                        var distOnSpline = intervalStart + distance;
                        if (distOnSpline > spline.Length) {
                            if (spline.IsLoop) {
                                while (distOnSpline > spline.Length) {
                                    distOnSpline -= spline.Length;
                                }
                            } else {
                                distOnSpline = spline.Length;
                            }
                        }

                        sample = spline.GetSampleAtDistance(distOnSpline);
                    }

                    sampleCache[distance] = sample;
                }
                var bent = sample.GetBent(vert);
                _vertices[i] = bent.position;
                _normals[i] = bent.normal;
            }

            MeshUtility.Update(result,
                _triangles,
                _vertices,
                _normals,
                _uv, _uv2, _uv3, _uv4, _uv5, _uv6, _uv7, _uv8);
        }

        private void FillRepeat() {
            var intervalLength = useSpline?
                (intervalEnd == 0 ? spline.Length : intervalEnd) - intervalStart :
                curve.Length;
            var repetitionCount = Mathf.FloorToInt(intervalLength / source.Length);

            int[] sourceTriangles = null;
            Vector2[] sourceUv = null; 
            Vector2[] sourceUv2 = null; 
            Vector2[] sourceUv3 = null; 
            Vector2[] sourceUv4 = null; 
            Vector2[] sourceUv5 = null; 
            Vector2[] sourceUv6 = null; 
            Vector2[] sourceUv7 = null; 
            Vector2[] sourceUv8 = null; 
            
            if (_repetitionCount != repetitionCount || isSourceDirty) {
                _vertices = new Vector3[_sourceVertices.Length * repetitionCount];
                _normals = new Vector3[_sourceVertices.Length * repetitionCount];
                
                sourceTriangles = source.Triangles;
                sourceUv = source.UV;
                sourceUv2 = source.UV2;
                sourceUv3 = source.UV3;
                sourceUv4 = source.UV4;
                sourceUv5 = source.UV5;
                sourceUv6 = source.UV6;
                sourceUv7 = source.UV7;
                sourceUv8 = source.UV8; 
                _uv = new Vector2[sourceUv.Length * repetitionCount];
                _uv2 = sourceUv2 != null ? new Vector2[sourceUv2.Length * repetitionCount] : null;
                _uv3 = sourceUv3 != null ? new Vector2[sourceUv3.Length * repetitionCount] : null;
                _uv4 = sourceUv4 != null ? new Vector2[sourceUv4.Length * repetitionCount] : null;
                _uv5 = sourceUv5 != null ? new Vector2[sourceUv5.Length * repetitionCount] : null;
                _uv6 = sourceUv6 != null ? new Vector2[sourceUv6.Length * repetitionCount] : null;
                _uv7 = sourceUv7 != null ? new Vector2[sourceUv7.Length * repetitionCount] : null;
                _uv8 = sourceUv8 != null ? new Vector2[sourceUv8.Length * repetitionCount] : null;
                _triangles = new int[sourceTriangles.Length * repetitionCount];
            }

            // computing vertices and normals
            float offset = 0;
            for (var i = 0; i < repetitionCount; i++) {

                sampleCache.Clear();
                // for each mesh vertex, we found its projection on the curve
                for (var j = 0; j < _sourceVertices.Length; j++) {
                    var vert = _sourceVertices[j];
                    var distance = vert.position.x - source.MinX + offset;
                    if (!sampleCache.TryGetValue(distance, out var sample)) {
                        if (!useSpline) {
                            if (distance > curve.Length) continue;
                            sample = curve.GetSampleAtDistance(distance);
                        } else {
                            var distOnSpline = intervalStart + distance;
                            //if (true) { //spline.isLoop) {
                            while (distOnSpline > spline.Length) {
                                distOnSpline -= spline.Length;
                            }

                            //} else if (distOnSpline > spline.Length) {
                            //    continue;
                            //}
                            sample = spline.GetSampleAtDistance(distOnSpline);
                        }

                        sampleCache[distance] = sample;
                    }

                    var bent = sample.GetBent(vert);
                    var vertexIndex = i * _sourceVertices.Length + j;
                    _vertices[vertexIndex] = bent.position;
                    _normals[vertexIndex] = bent.normal;
                }

                offset += source.Length;
                
                if (_repetitionCount == repetitionCount && !isSourceDirty) continue;
                for (var j = 0; j < sourceTriangles.Length; j++) {
                    var index = sourceTriangles[j];
                    _triangles[j + i * sourceTriangles.Length] = index + source.Vertices.Length * i;
                }

                Array.Copy(sourceUv, 0, _uv, i * sourceUv.Length, sourceUv.Length);
                Array.Copy(sourceUv2, 0, _uv2, i * sourceUv2.Length, sourceUv2.Length);
                Array.Copy(sourceUv3, 0, _uv3, i * sourceUv3.Length, sourceUv3.Length);
                Array.Copy(sourceUv4, 0, _uv4, i * sourceUv4.Length, sourceUv4.Length);
                Array.Copy(sourceUv5, 0, _uv5, i * sourceUv5.Length, sourceUv5.Length);
                Array.Copy(sourceUv6, 0, _uv6, i * sourceUv6.Length, sourceUv6.Length);
                Array.Copy(sourceUv7, 0, _uv7, i * sourceUv7.Length, sourceUv7.Length);
                Array.Copy(sourceUv8, 0, _uv8, i * sourceUv8.Length, sourceUv8.Length);
            }

            MeshUtility.Update(result,
                _triangles,
                _vertices,
                _normals,
                _uv, _uv2, _uv3, _uv4, _uv5, _uv6, _uv7, _uv8);
        }

        private void FillStretch() {
            if (isSourceDirty) {
                _triangles = source.Triangles;
                _uv = source.UV;
                _uv2 = source.UV2;
                _uv3 = source.UV3;
                _uv4 = source.UV4;
                _uv5 = source.UV5;
                _uv6 = source.UV6;
                _uv7 = source.UV7;
                _uv8 = source.UV8;
                _vertices = new Vector3[_sourceVertices.Length];
                _normals = new Vector3[_sourceVertices.Length];
                _curveSamples = new CurveSample[_sourceVertices.Length];
            }
            sampleCache.Clear();
            
            var jobVerticesIn = new NativeArray<MeshVertex>(_sourceVertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var jobVerticesOut = new NativeArray<float3>(_sourceVertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var jobNormalsOut = new NativeArray<float3>(_sourceVertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var jobCurveSamples = new NativeArray<CurveSample>(_sourceVertices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            foreach (var distanceRate in source.SampleGroups.Keys) {
                CurveSample sample;
                if (!useSpline) {
                    sample = curve.GetSampleAtDistance(curve.Length * distanceRate);
                } else {
                    var intervalLength =
                        intervalEnd == 0 ? spline.Length - intervalStart : intervalEnd - intervalStart;
                    var distOnSpline = intervalStart + intervalLength * distanceRate;
                    if (distOnSpline > spline.Length) {
                        distOnSpline = spline.Length;
                    }
                    sample = spline.GetSampleAtDistance(distOnSpline);
                }

                var sampleGroup = source.SampleGroups[distanceRate];

                for (var i = 0; i < sampleGroup.Count; i++) {
                    _curveSamples[sampleGroup[i]] = sample;
                }
            }
            
            jobVerticesIn.CopyFrom(_sourceVertices);
            jobCurveSamples.CopyFrom(_curveSamples);
            
            var job = new CurveSampleBentJob {
                Curves = jobCurveSamples,
                VerticesIn = jobVerticesIn,
                VerticesOut = jobVerticesOut,
                NormalsOut = jobNormalsOut
            };
            job.Schedule(_sourceVertices.Length, 4, default).Complete();
            
            jobVerticesOut.Reinterpret<Vector3>().CopyTo(_vertices);
            jobNormalsOut.Reinterpret<Vector3>().CopyTo(_normals);

            jobCurveSamples.Dispose();
            jobVerticesIn.Dispose();
            jobVerticesOut.Dispose();
            jobNormalsOut.Dispose();

            MeshUtility.Update(result,
                _triangles,
                _vertices,
                _normals,
                _uv, _uv2, _uv3, _uv4, _uv5, _uv6, _uv7, _uv8);
            
            if (TryGetComponent(out MeshCollider collider)) {
                collider.sharedMesh = result;
            }
        }


    }
}