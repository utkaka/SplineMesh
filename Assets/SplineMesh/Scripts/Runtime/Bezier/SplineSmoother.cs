using Unity.Mathematics;
using UnityEngine;

namespace SplineMesh {
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    [RequireComponent(typeof(Spline))]
    public class SplineSmoother : MonoBehaviour {
        private Spline spline;
        private Spline Spline {
            get {
                if (spline == null) spline = GetComponent<Spline>();
                return spline;
            }
        }

        [Range(0, 1f)] public float curvature = 0.3f;

        private void OnValidate() {
            SmoothAll();
        }

        private void OnEnable() {
            Spline.NodeChanged += OnNodeChanged;
            SmoothAll();
        }
        
        private void OnDisable() {
            Spline.NodeChanged -= OnNodeChanged;
        }

        private void OnNodeChanged(int index) {
            Spline.NodeChanged -= OnNodeChanged;
            SmoothNode(index);
            if (index > 0) SmoothNode(index - 1);
            if (index < Spline.Nodes.Count - 1) SmoothNode(index + 1);
            Spline.NodeChanged += OnNodeChanged;
        }

        private void SmoothNode(int index) {
            var node = Spline.Nodes[index];
            var pos = node.Position;
            // For the direction, we need to compute a smooth vector.
            // Orientation is obtained by substracting the vectors to the previous and next way points,
            // which give an acceptable tangent in most situations.
            // Then we apply a part of the average magnitude of these two vectors, according to the smoothness we want.
            var dir = float3.zero;
            float averageMagnitude = 0;
            if (index != 0) {
                var previousPos = Spline.Nodes[index - 1].Position;
                var toPrevious = pos - previousPos;
                averageMagnitude += math.sqrt(toPrevious.x * toPrevious.x + toPrevious.y * toPrevious.y +
                                              toPrevious.z * toPrevious.z);
                dir += math.normalize(toPrevious);
            }
            if (index != Spline.Nodes.Count - 1) {
                var nextPos = Spline.Nodes[index + 1].Position;
                var toNext = pos - nextPos;
                averageMagnitude += math.sqrt(toNext.x * toNext.x + toNext.y * toNext.y +
                                              toNext.z * toNext.z);
                dir -= math.normalize(toNext);
            }
            averageMagnitude *= 0.5f;
            // This constant should vary between 0 and 0.5, and allows to add more or less smoothness.
            dir = math.normalize(dir) * averageMagnitude * curvature;

            // In SplineMesh, the node direction is not relative to the node position. 
            var controlPoint = dir + pos;

            // We only set one direction at each spline node because SplineMesh only support mirrored direction between curves.
            node.Direction = controlPoint;
            Spline.UpdateNode(index, node);
        }


        private void SmoothAll() {
            for (var i = 0; i < Spline.Nodes.Count; i++) {
                SmoothNode(i);
            }
        }
    }
}
