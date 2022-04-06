using UnityEngine;
using System;
using Unity.Mathematics;

namespace SplineMesh {
    [Serializable]
    public struct MeshVertex {
        public float3 position;
        public float3 normal;
        public float2 uv;

        public MeshVertex(float3 position, float3 normal, float2 uv) {
            this.position = position;
            this.normal = normal;
            this.uv = uv;
        }

        public MeshVertex(float3 position, float3 normal)
            : this(position, normal, Vector2.zero)
        {
        }
    }
}