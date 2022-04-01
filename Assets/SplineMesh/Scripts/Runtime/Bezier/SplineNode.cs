using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace SplineMesh {
    /// <summary>
    /// Spline node storing a position and a direction (tangent).
    /// Note : you shouldn't modify position and direction manualy but use dedicated methods instead, to insure event raising.
    /// </summary>
    [Serializable]
    public class SplineNode {
        /// <summary>
        /// Event raised when position, direction, scale or roll changes.
        /// </summary>
        public event Action<SplineNode> Changed;
        [SerializeField]
        private float3 position;
        [SerializeField]
        private float3 direction;
        [SerializeField]
        private float3 up = Vector3.up;
        [SerializeField]
        private float2 scale = Vector2.one;
        [SerializeField]
        private float roll;

        /// <summary>
        /// Node position
        /// </summary>
        public float3 Position {
            get => position;
            set {
                if (position.Equals(value)) return;
                position = value;
                Changed?.Invoke(this);
            }
        }

        /// <summary>
        /// Node direction
        /// </summary>
        public float3 Direction {
            get => direction;
            set {
                if (direction.Equals(value)) return;
                direction = value;
                Changed?.Invoke(this);
            }
        }

        /// <summary>
        /// Up vector to apply at this node.
        /// Usefull to specify the orientation when the tangent blend with the world UP (gimball lock)
        /// This value is not used on the spline itself but is commonly used on bended content.
        /// </summary>
        public float3 Up {
            get => up;
            set {
                if (up.Equals(value)) return;
                up = value;
                Changed?.Invoke(this);
            }
        }

        /// <summary>
        /// Scale to apply at this node.
        /// This value is not used on the spline itself but is commonly used on bended content.
        /// </summary>
        public float2 Scale {
            get { return scale; }
            set {
                if (scale.Equals(value)) return;
                scale = value;
                Changed?.Invoke(this);
            }
        }

        /// <summary>
        /// Roll to apply at this node.
        /// This value is not used on the spline itself but is commonly used on bended content.
        /// </summary>
        public float Roll {
            get => roll;
            set {
                if (Math.Abs(roll - value) < float.Epsilon) return;
                roll = value;
                Changed?.Invoke(this);
            }
        }

        public SplineNode(float3 position, float3 direction) {
            Position = position;
            Direction = direction;
        }
    }
}
