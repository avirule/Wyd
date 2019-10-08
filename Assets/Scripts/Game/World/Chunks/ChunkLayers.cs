#region

using Boo.Lang;
using UnityEngine;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkLayers
    {
        private readonly List<ChunkLayer> _Layers;

        public ChunkLayer this[int index] => _Layers[index];

        public ChunkLayers() => _Layers = new List<ChunkLayer>();

        public void AddLayer()
        {
            _Layers.Add(new ChunkLayer());
        }

        public void RemoveLayer(int yIndex)
        {
            _Layers.RemoveAt(yIndex);
        }

        public void Clear()
        {
            _Layers.Clear();
        }

        public void ModifyBlock(Vector3 position, ushort newId)
        {
            int floorY = Mathf.FloorToInt(position.y);

            if (floorY >= _Layers.Count)
            {
                return;
            }

            _Layers[floorY].ModifyBlock(position, newId);
        }
    }
}
