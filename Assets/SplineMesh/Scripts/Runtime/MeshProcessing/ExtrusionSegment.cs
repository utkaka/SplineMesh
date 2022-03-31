using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SplineMesh {
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ExtrusionSegment : MonoBehaviour {
        private bool isDirty = false;

        private MeshFilter mf;

        private bool useSpline = false;
        private CubicBezierCurve curve;
        private Spline spline;
        private float intervalStart, intervalEnd;
        
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

        private List<Vertex> shapeVertices = new List<Vertex>();
        /// <summary>
        /// 
        /// </summary>
        public List<Vertex> ShapeVertices {
            get { return shapeVertices; }
            set {
                if (value == shapeVertices) return;
                SetDirty();
                shapeVertices = value;
            }
        }

        private float textureScale = 1;
        /// <summary>
        /// 
        /// </summary>
        public float TextureScale {
            get { return textureScale; }
            set {
                if (value == textureScale) return;
                SetDirty();
                textureScale = value;
            }
        }

        private float textureOffset = 0;
        /// <summary>
        /// 
        /// </summary>
        public float TextureOffset {
            get { return textureOffset; }
            set {
                if (value == textureOffset) return;
                SetDirty();
                textureOffset = value;
            }
        }

        private float sampleSpacing = 0.1f;
        /// <summary>
        /// 
        /// </summary>
        public float SampleSpacing {
            get { return sampleSpacing; }
            set {
                if (value == sampleSpacing) return;
                if (value <= 0) throw new ArgumentOutOfRangeException("SampleSpacing", "Must be greater than 0");
                SetDirty();
                sampleSpacing = value;
            }
        }

        private void OnEnable() {
            mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh == null) {
                mf.sharedMesh = new Mesh();
            }
            var mesh = mf.sharedMesh;
            _uv2 = mesh.uv2;
            _uv3 = mesh.uv3;
            _uv4 = mesh.uv4;
            _uv5 = mesh.uv5;
            _uv6 = mesh.uv6;
            _uv7 = mesh.uv7;
            _uv8 = mesh.uv8;
        }

        /// <summary>
        /// Set the cubic Bézier curve to use to bend the source mesh, and begin to listen to curve control points for changes.
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="update">If let to true, update the resulting mesh immediatly.</param>
        public void SetInterval(CubicBezierCurve curve) {
            if (this.curve == curve) return;
            if (curve == null) throw new ArgumentNullException("curve");

            if (this.curve != null) {
                this.curve.Changed.RemoveListener(SetDirty);
            }
            this.curve = curve;
            spline = null;
            curve.Changed.AddListener(SetDirty);
            useSpline = false;
            SetDirty();
        }

        public void SetInterval(Spline spline, float intervalStart, float intervalEnd = 0) {
            if (this.spline == spline && this.intervalStart == intervalStart && this.intervalEnd == intervalEnd) return;
            if (spline == null) throw new ArgumentNullException("spline");
            if (intervalStart < 0 || intervalStart >= spline.Length) {
                throw new ArgumentOutOfRangeException("interval start must be 0 or greater and lesser than spline length (was " + intervalStart + ")");
            }
            if (intervalEnd != 0 && intervalEnd <= intervalStart || intervalEnd > spline.Length) {
                throw new ArgumentOutOfRangeException("interval end must be 0 or greater than interval start, and lesser than spline length (was " + intervalEnd + ")");
            }
            if (this.spline != null) {
                // unlistening previous spline
                this.spline.CurveChanged.RemoveListener(SetDirty);
            }
            this.spline = spline;
            // listening new spline
            spline.CurveChanged.AddListener(SetDirty);

            curve = null;
            this.intervalStart = intervalStart;
            this.intervalEnd = intervalEnd;
            useSpline = true;
            SetDirty();
        }

        private void SetDirty() {
            isDirty = true;
        }

        private void LateUpdate() {
            ComputeIfNeeded();
        }

        public void ComputeIfNeeded() {
            if (isDirty) {
                Compute();
                isDirty = false;
            }
        }

        private List<CurveSample> GetPath() {
            var path = new List<CurveSample>();
            if (useSpline) {
                // calculate path from spline interval
                var d = intervalStart;
                while (d < intervalEnd) {
                    path.Add(spline.GetSampleAtDistance(d));
                    d += sampleSpacing;
                }
                path.Add(spline.GetSampleAtDistance(intervalEnd));
            } else {
                // calculate path in a curve
                float d = 0;
                while (d < curve.Length) {
                    path.Add(curve.GetSampleAtDistance(d));
                    d += sampleSpacing;
                }
                path.Add(curve.GetSampleAtDistance(curve.Length));
            }
            return path;
        }

        private void Compute() {
            var path = GetPath();

            var vertsInShape = shapeVertices.Count;
            var segmentCount = path.Count - 1;

            _triangles = new int[vertsInShape * segmentCount * 6];
            _vertices = new Vector3[path.Count * vertsInShape];
            _normals = new Vector3[_vertices.Length];
            _uv = new Vector2[_vertices.Length];


            for (var i = 0; i < path.Count; i++) {
                var sample = path[i];
                for (var j = 0; j < vertsInShape; j++) {
                    var v = shapeVertices[j];
                    var bent = sample.GetBent(new MeshVertex(
                        new Vector3(0, v.point.y, -v.point.x),
                        new Vector3(0, v.normal.y, -v.normal.x),
                        new Vector2(v.uCoord, textureScale * (sample.distanceInCurve + textureOffset))));
                    var vertexIndex = i * vertsInShape + j;
                    _vertices[vertexIndex] = bent.position;
                    _normals[vertexIndex] = bent.normal;
                    _uv[vertexIndex] = bent.uv;
                }
            }

            var index = 0;
            var triangleIndex = 0;
            for (var i = 0; i < segmentCount; i++) {
                for (var j = 0; j < vertsInShape; j++) {
                    var offset = j == vertsInShape - 1 ? -(vertsInShape - 1) : 1;
                    var a = index + vertsInShape;
                    var b = index;
                    var c = index + offset;
                    var d = index + offset + vertsInShape;
                    _triangles[triangleIndex++] = c;
                    _triangles[triangleIndex++] = b;
                    _triangles[triangleIndex++] = a;
                    _triangles[triangleIndex++] = a;
                    _triangles[triangleIndex++] = d;
                    _triangles[triangleIndex++] = c;
                    index++;
                }
            }

            MeshUtility.Update(mf.sharedMesh,
                _triangles,
                _vertices,
                _normals,
                _uv, _uv2, _uv3, _uv4, _uv5, _uv6, _uv7, _uv8);
            var mc = GetComponent<MeshCollider>();
            if(mc != null) {
                mc.sharedMesh = mf.sharedMesh;
            }
        }

        [Serializable]
        public class Vertex {
            public Vector2 point;
            public Vector2 normal;
            public float uCoord;

            public Vertex(Vector2 point, Vector2 normal, float uCoord) {
                this.point = point;
                this.normal = normal;
                this.uCoord = uCoord;
            }
            public Vertex(Vertex other) {
                this.point = other.point;
                this.normal = other.normal;
                this.uCoord = other.uCoord;
            }
        }
    }
}
