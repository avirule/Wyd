#region

using Controllers.World;
using UnityEngine;

#endregion

namespace Game.Meshing
{
    public class GreedyMeshing
    {
        // Code ported from https://0fps.net/2012/06/30/meshing-in-a-minecraft-game/

// Note this implemenetation does not support different block types or block normals
// The original author describes how to do this here: https://0fps.net/2012/07/07/meshing-minecraft-part-2/


        private const int CHUNK_SIZE = 32;

// These variables store the location of the chunk in the world, e.g. (0,0,0), (32,0,0), (64,0,0)
        private int chunkPosX = 0;
        private int chunkPosY = 0;
        private int chunkPosZ = 0;

        
    }
}