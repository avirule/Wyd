using System.Collections.Generic;
using Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace Environment.Terrain.Generation
{
    public class CombinedMeshGenerator : ThreadedProcess
    {
        private readonly IEnumerable<Chunk> _Chunks;

        public Mesh Mesh;

        public CombinedMeshGenerator(IEnumerable<Chunk> chunks)
        {
            _Chunks = chunks;
        }

        protected override void ThreadFunction()
        {
            base.ThreadFunction();

            List<CombineInstance> combines = new List<CombineInstance>();

            foreach (Chunk chunk in _Chunks)
            {
                CombineInstance combine = new CombineInstance
                {
                    mesh = chunk.Mesh,
                    transform = Matrix4x4.TRS(chunk.Position, Quaternion.identity, new Vector3(1f, 1f, 1f))
                };

                combines.Add(combine);
            }

            Mesh = new Mesh
            {
                indexFormat = IndexFormat.UInt32
            };
            Mesh.CombineMeshes(combines.ToArray(), true, true);
        }
    }
}