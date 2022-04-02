using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace SplineMesh {
    /// <summary>
    /// Spline node storing a position and a direction (tangent).
    /// Note : you shouldn't modify position and direction manualy but use dedicated methods instead, to insure event raising.
    /// </summary>
    [Serializable]
    public struct SplineNode {
        [FormerlySerializedAs("position")]
        [SerializeField]
        private float3 _position;
        [FormerlySerializedAs("direction")]
        [SerializeField]
        private float3 _direction;
        [FormerlySerializedAs("up")]
        [SerializeField]
        private float3 _up;
        [FormerlySerializedAs("scale")]
        [SerializeField]
        private float2 _scale;
        [FormerlySerializedAs("roll")]
        [SerializeField]
        private float _roll;

        public float3 Position {
            get => _position;
            set => _position = value;
        }

        public float3 Direction {
            get => _direction;
            set => _direction = value;
        }

        public float3 Up {
            get => _up;
            set => _up = value;
        }

        public float2 Scale {
            get => _scale;
            set => _scale = value;
        }

        public float Roll {
            get => _roll;
            set => _roll = value;
        }

        public SplineNode(float3 position, float3 direction) {
            _position = position;
            _direction = direction;
            _up = new float3(0.0f, 1.0f, 0.0f);
            _scale = new float2(1.0f, 1.0f);
            _roll = 0.0f;
        }
    }
}
