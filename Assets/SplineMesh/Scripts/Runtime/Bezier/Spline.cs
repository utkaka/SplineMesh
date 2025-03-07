using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Serialization;

namespace SplineMesh {
    /// <summary>
    /// A curved line made of oriented nodes.
    /// Each segment is a cubic Bézier curve connected to spline nodes.
    /// It provides methods to get positions and tangent along the spline, specifying a distance or a ratio, plus the curve length.
    /// The spline and the nodes raise events each time something is changed.
    /// </summary>
    [DisallowMultipleComponent]
    public class Spline : MonoBehaviour {
        [FormerlySerializedAs("nodes")]
        [SerializeField]
        private List<SplineNode> _nodes;
        
        private List<CubicBezierCurve> _curves;
        private float _length;

        public float Length => _length;

        public IReadOnlyList<SplineNode> Nodes => _nodes;

        public IReadOnlyList<CubicBezierCurve> Curves => _curves;

        /// <summary>
        /// Event raised when one of the curve changes.
        /// </summary>
        public event Action Changed;

        private void Awake() {
            RefreshCurves();
            ComputeCurves();
        }

        private void OnDestroy() {
            foreach (var curve in _curves) {
                curve.Dispose();
            }
        }

        private void ComputeCurves() {
            _length = 0;
            var handle = default(JobHandle);
            handle = _curves.Aggregate(handle, (current, curve) => JobHandle.CombineDependencies(current, curve.ComputeSamples(default)));
            handle.Complete();
            foreach (var curve in _curves) {
                _length += curve.Length;
            }
            Changed?.Invoke();
        }
        
        private void SetDirty() {
            ComputeCurves();
        }

        private void SetDirty(CubicBezierCurve curve) {
            curve.SetDirty();
            SetDirty();
        }
        
        private void RefreshCurves() {
            _curves = new List<CubicBezierCurve>();
            for (var i = 0; i < _nodes.Count - 1; i++) {
                var n = _nodes[i];
                var next = _nodes[i + 1];
                var curve = new CubicBezierCurve(n, next);
                _curves.Add(curve);
            }
            SetDirty();
        }
        
        public void UpdateNode(int index, SplineNode node) {
            _nodes[index] = node;
            if (index > 0) {
                _curves[index - 1].ConnectEnd(node);
                SetDirty(_curves[index - 1]);
            }

            if (index < _curves.Count) {
                _curves[index].ConnectStart(node);
                SetDirty(_curves[index]);
            }
        }
        
        /// <summary>
        /// Returns an interpolated sample of the spline, containing all curve data at this distance.
        /// Distance must be between 0 and the spline length.
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public CurveSamplesLerpPair GetSampleAtDistance(float d) {
            for (var i = 0; i < _curves.Count; i++) {
                var curve = _curves[i];
                var curveLength = curve.Length;
                // test if distance is approximately equals to curve length, because spline
                // length may be greater than cumulated curve length due to float precision
                if (d > curveLength && d < curveLength + 0.0001f) {
                    d = curveLength;
                }

                if (d > curveLength) {
                    d -= curveLength;
                } else {
                    return curve.GetSampleAtDistance(d);
                }
            }

            return default;
        }
    }
}
