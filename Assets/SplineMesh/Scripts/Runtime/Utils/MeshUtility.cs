using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SplineMesh {
    public class MeshUtility {

        /// <summary>
        /// Returns a mesh with reserved triangles to turn back the face culling.
        /// This is usefull when a mesh needs to have a negative scale.
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static int[] GetReversedTriangles(Mesh mesh) {
            var res = mesh.triangles.ToArray();
            var triangleCount = res.Length / 3;
            for (var i = 0; i < triangleCount; i++) {
                var tmp = res[i * 3];
                res[i * 3] = res[i * 3 + 1];
                res[i * 3 + 1] = tmp;
            }
            return res;
        }

        /// <summary>
        /// Returns a mesh similar to the given source plus given optionnal parameters.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="source"></param>
        /// <param name="triangles"></param>
        /// <param name="vertices"></param>
        /// <param name="normals"></param>
        /// <param name="uv"></param>
        /// <param name="uv2"></param>
        /// <param name="uv3"></param>
        /// <param name="uv4"></param>
        /// <param name="uv5"></param>
        /// <param name="uv6"></param>
        /// <param name="uv7"></param>
        /// <param name="uv8"></param>
        public static void Update(Mesh mesh,
	        int[] triangles,
            Vector3[] vertices,
            Vector3[] normals,
            Vector2[] uv,
            Vector2[] uv2,
            Vector2[] uv3,
            Vector2[] uv4,
            Vector2[] uv5,
            Vector2[] uv6,
            Vector2[] uv7,
            Vector2[] uv8) {
            /*mesh.hideFlags = source.hideFlags;
#if UNITY_2017_3_OR_NEWER
            mesh.indexFormat = source.indexFormat;
#endif*/
	        mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.uv2 = uv2;
            mesh.uv3 = uv3;
            mesh.uv4 = uv4;
            mesh.uv5 = uv5;
            mesh.uv6 = uv6;
            mesh.uv7 = uv7;
            mesh.uv8 = uv8;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
    }
}
