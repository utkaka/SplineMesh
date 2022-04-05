using System;
using System.Collections;
using System.Collections.Generic;
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
    [ExecuteInEditMode]
    public class Spline : MonoBehaviour {
        public enum ListChangeType {
            Add,
            Insert,
            Remove
        }

        /// <summary>
        /// The spline nodes.
        /// Warning, this collection shouldn't be changed manualy. Use specific methods to add and remove nodes.
        /// It is public only for the user to enter exact values of position and direction in the inspector (and serialization purposes).
        /// </summary>
        [FormerlySerializedAs("nodes")]
        [SerializeField]
        private List<SplineNode> _nodes;

        /// <summary>
        /// The generated curves. Should not be changed in any way, use nodes instead.
        /// </summary>
        [FormerlySerializedAs("curves")]
        [SerializeField]
        private List<CubicBezierCurve> _curves;

        /// <summary>
        /// The spline length in world units.
        /// </summary>
        [FormerlySerializedAs("Length")]
        [SerializeField]
        private float _length;

        [FormerlySerializedAs("isLoop")]
        [SerializeField]
        private bool _isLoop;

        private Coroutine _computeCoroutine;

        public bool IsLoop {
            get => _isLoop;
            set {
                _isLoop = value;
                UpdateLoopNodes(true);
            }
        }

        public float Length => _length;

        public IReadOnlyList<SplineNode> Nodes => _nodes;

        public IReadOnlyList<CubicBezierCurve> Curves => _curves;

        /// <summary>
        /// Event raised when one of the curve changes.
        /// </summary>
        public event Action Changed;
        public event Action<int> NodeChanged;
        public event Action<ListChangeType, int> NodeListChanged;

        private void OnEnable() {
            RefreshCurves();
        }

        /// <summary>
        /// Clear the nodes and curves, then add two default nodes for the reset spline to be visible in editor.
        /// </summary>
        private void Reset() {
            _nodes.Clear();
            _curves.Clear();
            AddNode(new SplineNode(new Vector3(5, 0, 0), new Vector3(5, 0, -3)));
            AddNode(new SplineNode(new Vector3(10, 0, 0), new Vector3(10, 0, 3)));
        }

        private IEnumerator ComputeCurves() {
            yield return Application.isEditor ? null : new WaitForEndOfFrame();
            _length = 0;
            for (var i = 0; i < _curves.Count; i++) {
                var curve = _curves[i];
                curve.ComputeSamples();
                _length += curve.Length;
            }
            _computeCoroutine = null;
            Changed?.Invoke();
        }
        
        private void SetDirty() {
            if (_computeCoroutine != null) return;
            _computeCoroutine = StartCoroutine(ComputeCurves());
        }

        private void SetDirty(CubicBezierCurve curve) {
            curve.SetDirty();
            SetDirty();
        }

        /// <summary>
	    /// Refreshes the spline's internal list of curves.
	    // </summary>
        private void RefreshCurves() {
            _curves.Clear();
            for (var i = 0; i < _nodes.Count - 1; i++) {
                var n = _nodes[i];
                var next = _nodes[i + 1];
                var curve = new CubicBezierCurve(n, next);
                curve.ComputeSamples();
                _curves.Add(curve);
            }
        }

        /// <summary>
        /// Adds a node at the end of the spline.
        /// </summary>
        /// <param name="node"></param>
        public void AddNode(SplineNode node) {
            _nodes.Add(node);
            if (_nodes.Count != 1) {
                var previousNode = _nodes[^2];
                var curve = new CubicBezierCurve(previousNode, node);
                _curves.Add(curve);
            }
            NodeListChanged?.Invoke(ListChangeType.Add, _nodes.Count - 1);
            SetDirty();
        }
        
        public void UpdateNode(int index, SplineNode node, bool fromLoop = false) {
            _nodes[index] = node;
            if (index > 0) {
                _curves[index - 1].ConnectEnd(node);
                SetDirty(_curves[index - 1]);
            }

            if (index < _curves.Count) {
                _curves[index].ConnectStart(node);
                SetDirty(_curves[index]);
            }
            NodeChanged?.Invoke(index);
            if (!_isLoop || fromLoop) return;
            if (index == 0) UpdateLoopNodes(true);
            else if (index == _nodes.Count - 1) UpdateLoopNodes(false);
        }

        /// <summary>
        /// Insert the given node in the spline at index. Index must be greater than 0 and less than node count.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="node"></param>
        public void InsertNode(int index, SplineNode node) {
            if (index == 0)
                throw new Exception("Can't insert a node at index 0");
            var nextNode = _nodes[index];
            _nodes.Insert(index, node);
            _curves[index - 1].ConnectEnd(node);
            var curve = new CubicBezierCurve(node, nextNode);
            _curves.Insert(index, curve);
            NodeListChanged?.Invoke(ListChangeType.Insert, index);
            SetDirty();
        }

        /// <summary>
        /// Remove the given node from the spline. The given node must exist and the spline must have more than 2 nodes.
        /// </summary>
        /// <param name="index"></param>
        public void RemoveNode(int index) {
            if (_nodes.Count <= 2) {
                throw new Exception("Can't remove the node because a spline needs at least 2 nodes.");
            }
            var toRemove = index == _nodes.Count - 1 ? _curves[index - 1] : _curves[index];
            if (index != 0 && index != _nodes.Count - 1) {
                var nextNode = _nodes[index + 1];
                _curves[index - 1].ConnectEnd(nextNode);
            }
            _nodes.RemoveAt(index);
            _curves.Remove(toRemove);
            NodeListChanged?.Invoke(ListChangeType.Remove, index);
            SetDirty();
        }

        private void UpdateLoopNodes(bool start) {
            if (!_isLoop) return;
            if (start) {
                UpdateNode(_nodes.Count - 1, _nodes[0], true);
            } else {
                UpdateNode(0, _nodes[^1], true);
            }
        }

        /// <summary>
        /// Returns an interpolated sample of the spline, containing all curve data at this time.
        /// Time must be between 0 and the number of nodes.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public CurveSamplesLerpPair GetSample(float t) {
            var index = GetNodeIndexForTime(t);
            return _curves[index].GetSample(t - index);
        }

        /// <summary>
        /// Returns the curve at the given time.
        /// Time must be between 0 and the number of nodes.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public CubicBezierCurve GetCurve(float t) {
            return _curves[GetNodeIndexForTime(t)];
        }

        private int GetNodeIndexForTime(float t) {
            if (t < 0 || t > _nodes.Count - 1) {
                throw new ArgumentException(
                    $"Time must be between 0 and last node index ({_nodes.Count - 1}). Given time was {t}.");
            }
            var res = Mathf.FloorToInt(t);
            if (res == _nodes.Count - 1)
                res--;
            return res;
        }

        /*public CurveSamplesLerpPair GetProjectionSample(Vector3 pointToProject) {
            var closest = default(CurveSamplesLerpPair);
            var minSqrDistance = float.MaxValue;
            foreach (var curve in _curves) {
                var projection = curve.GetProjectionSample(pointToProject);
                if (curve == _curves[0]) {
                    closest = projection;
                    minSqrDistance = ((Vector3)projection.Location - pointToProject).sqrMagnitude;
                    continue;
                }
                var sqrDist = ((Vector3)projection.Location - pointToProject).sqrMagnitude;
                if (sqrDist < minSqrDistance) {
                    minSqrDistance = sqrDist;
                    closest = projection;
                }
            }
            return closest;
        }*/
        
        /// <summary>
        /// Returns an interpolated sample of the spline, containing all curve data at this distance.
        /// Distance must be between 0 and the spline length.
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public CurveSamplesLerpPair GetSampleAtDistance(float d) {
            if (d < 0 || d > _length)
                throw new ArgumentException(
                    $"Distance must be between 0 and spline length ({_length}). Given distance was {d}.");
            foreach (var curve in _curves) {
                // test if distance is approximately equals to curve length, because spline
                // length may be greater than cumulated curve length due to float precision
                if(d > curve.Length && d < curve.Length + 0.0001f) {
                    d = curve.Length;
                }
                if (d > curve.Length) {
                    d -= curve.Length;
                } else {
                    return curve.GetSampleAtDistance(d);
                }
            }
            throw new Exception("Something went wrong with GetSampleAtDistance.");
        }
    }
}
