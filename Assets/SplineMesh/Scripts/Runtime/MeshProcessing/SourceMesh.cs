using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using Unity.Mathematics;

namespace SplineMesh {
    /// <summary>
    /// This class returns a transformed version of a given source mesh, plus others
    /// informations to help bending the mesh along a curve.
    /// It is imutable to ensure better performances.
    /// 
    /// To obtain an instance, call the static method <see cref="Build(Mesh)"/>.
    /// The building is made in a fluent way.
    /// </summary>
    public class SourceMesh {
        private Vector3 _translation;
        private Quaternion _rotation;
        private Vector3 _scale;

        private Vector2[] _uv;
        private Vector2[] _uv2;
        private Vector2[] _uv3;
        private Vector2[] _uv4;
        private Vector2[] _uv5;
        private Vector2[] _uv6;
        private Vector2[] _uv7;
        private Vector2[] _uv8;
        
        internal MeshVertex[] Vertices { get; private set; }
        internal int[] Triangles { get; private set; }

        internal float MinX { get; private set; }

        internal float Length { get; private set; }

        internal Vector2[] UV => _uv;

        internal Vector2[] UV2 => _uv2;

        internal Vector2[] UV3 => _uv3;

        internal Vector2[] UV4 => _uv4;

        internal Vector2[] UV5 => _uv5;

        internal Vector2[] UV6 => _uv6;

        internal Vector2[] UV7 => _uv7;

        internal Vector2[] UV8 => _uv8;

        public SourceMesh(Mesh mesh, Vector3 translation, Quaternion rotation, Vector3 scale) {
            _translation = translation;
            _rotation = rotation;
            _scale = scale;
            BuildData(mesh);
        }

        private void BuildData(Mesh mesh) {
            _uv = mesh.uv;
            _uv2 = mesh.uv2;
            _uv3 = mesh.uv3;
            _uv4 = mesh.uv4;
            _uv5 = mesh.uv5;
            _uv6 = mesh.uv6;
            _uv7 = mesh.uv7;
            _uv8 = mesh.uv8;
            
            // if the mesh is reversed by scale, we must change the culling of the faces by inversing all triangles.
            // the mesh is reverse only if the number of resersing axes is impair.
            var reversed = _scale.x < 0;
            if (_scale.y < 0) reversed = !reversed;
            if (_scale.z < 0) reversed = !reversed;
            Triangles = reversed ? MeshUtility.GetReversedTriangles(mesh) : mesh.triangles;

            // we transform the source mesh vertices according to rotation/translation/scale
            var i = 0;
            Vertices = new MeshVertex[mesh.vertexCount];
            for (var index = 0; index < mesh.vertices.Length; index++) {
                var vert = mesh.vertices[index];
                var transformed = new MeshVertex(vert, mesh.normals[i++]);
                //  application of rotation
                if (_rotation != Quaternion.identity) {
                    transformed.position = _rotation * transformed.position;
                    transformed.normal = _rotation * transformed.normal;
                }

                if (_scale != Vector3.one) {
                    transformed.position = Vector3.Scale(transformed.position, _scale);
                    transformed.normal = Vector3.Scale(transformed.normal, _scale);
                }

                if (_translation != Vector3.zero) {
                    transformed.position = (Vector3) transformed.position + _translation;
                }

                Vertices[index] = transformed;
            }

            // find the bounds along x
            MinX = float.MaxValue;
            var maxX = float.MinValue;
            foreach (var vert in Vertices) {
                Vector3 p = vert.position;
                maxX = Math.Max(maxX, p.x);
                MinX = Math.Min(MinX, p.x);
            }
            Length = Math.Abs(maxX - MinX);
        }
    }
}
