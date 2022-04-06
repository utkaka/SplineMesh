using UnityEngine;

namespace SplineMesh {
    /// <summary>
    /// Example of component to bend a mesh along a spline with some interpolation of scales and rolls. This component can be used as-is but will most likely be a base for your own component.
    /// 
    /// For explanations of the base component, <see cref="ExamplePipe"/>
    /// 
    /// In this component, we have added properties to make scale and roll vary between spline start and end.
    /// Intermediate scale and roll values are calculated at each spline node accordingly to the distance, then given to the MeshBenders component.
    /// MeshBender applies scales and rolls values by interpollation if they differ from strat to end of the curve.
    /// 
    /// You can easily imagine a list of scales to apply to each node independantly to create your own variation.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExampleTentacle : MonoBehaviour {
        private Spline spline { get => GetComponent<Spline>(); }

        public float startScale = 1, endScale = 1;
        public float startRoll = 0, endRoll = 0;

        private void OnValidate() {
            //
            // apply scale and roll at each node
            float currentLength = 0;
            for (var i = 0; i < spline.Curves.Count; i++) {
                var curve = spline.Curves[i];
                var startRate = currentLength / spline.Length;
                currentLength += curve.Length;
                var endRate = currentLength / spline.Length;
                var node1 = spline.Nodes[i];
                var node2 = spline.Nodes[i + 1];
                
                node1.Scale = Vector2.one * (startScale + (endScale - startScale) * startRate);
                node2.Scale = Vector2.one * (startScale + (endScale - startScale) * endRate);

                node1.Roll = Mathf.Deg2Rad * (startRoll + (endRoll - startRoll) * startRate);
                node2.Roll = Mathf.Deg2Rad * (startRoll + (endRoll - startRoll) * endRate);

                spline.UpdateNode(i, node1);
                spline.UpdateNode(i + 1, node2);
            }
        }
    }
}
