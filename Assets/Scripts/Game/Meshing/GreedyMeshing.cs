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

        public void GreedyMesh()
        {
            // Sweep over each axis (X, Y and Z)
            for (int d = 0; d < 3; ++d)
            {
                int i, j, k, l, w, h;
                int u = (d + 1) % 3;
                int v = (d + 2) % 3;
                int[] x = new int[3];
                int[] q = new int[3];

                bool[] mask = new bool[CHUNK_SIZE * CHUNK_SIZE];
                q[d] = 1;

                // Check each slice of the chunk one at a time
                for (x[d] = -1; x[d] < CHUNK_SIZE;)
                {
                    // Compute the mask
                    int n = 0;
                    for (x[v] = 0; x[v] < CHUNK_SIZE; ++x[v])
                    {
                        for (x[u] = 0; x[u] < CHUNK_SIZE; ++x[u])
                        {
                            // q determines the direction (X, Y or Z) that we are searching
                            // m.IsBlockAt(x,y,z) takes global map positions and returns true if a block exists there

                            bool blockCurrent = (0 > x[d]) ||
                                                WorldController.Current.BlockExistsAt(
                                                    new Vector3(
                                                        x[0] + chunkPosX,
                                                        x[1] + chunkPosY,
                                                        x[2] + chunkPosZ));
                            bool blockCompare = (x[d] >= (CHUNK_SIZE - 1)) ||
                                                WorldController.Current.BlockExistsAt(
                                                    new Vector3(
                                                        x[0] + q[0] + chunkPosX,
                                                        x[1] + q[1] + chunkPosY,
                                                        x[2] + q[2] + chunkPosZ));

                            // The mask is set to true if there is a visible face between two blocks,
                            //   i.e. both aren't empty and both aren't blocks
                            mask[n++] = blockCurrent != blockCompare;
                        }
                    }

                    ++x[d];

                    n = 0;

                    // Generate a mesh from the mask using lexicographic ordering,      
                    //   by looping over each block in this slice of the chunk
                    for (j = 0; j < CHUNK_SIZE; ++j)
                    {
                        for (i = 0; i < CHUNK_SIZE;)
                        {
                            if (mask[n])
                            {
                                // Compute the width of this quad and store it in w                        
                                //   This is done by searching along the current axis until mask[n + w] is false
                                for (w = 1; ((i + w) < CHUNK_SIZE) && mask[n + w]; w++)
                                {
                                }

                                // Compute the height of this quad and store it in h                        
                                //   This is done by checking if every block next to this row (range 0 to w) is also part of the mask.
                                //   For example, if w is 5 we currently have a quad of dimensions 1 x 5. To reduce triangle count,
                                //   greedy meshing will attempt to expand this quad out to CHUNK_SIZE x 5, but will stop if it reaches a hole in the mask

                                bool done = false;
                                for (h = 1; (j + h) < CHUNK_SIZE; h++)
                                {
                                    // Check each block next to this quad
                                    for (k = 0; k < w; ++k)
                                    {
                                        // If there's a hole in the mask, exit
                                        if (!mask[n + k + (h * CHUNK_SIZE)])
                                        {
                                            done = true;
                                            break;
                                        }
                                    }

                                    if (done)
                                    {
                                        break;
                                    }
                                }

                                x[u] = i;
                                x[v] = j;

                                // du and dv determine the size and orientation of this face
                                int[] du = new int[3];
                                du[u] = w;

                                int[] dv = new int[3];
                                dv[v] = h;

                                // Create a quad for this face. Colour, normal or textures are not stored in this block vertex format.
//                                BlockVertex.AppendQuad(
//                                    new Vector3(x[0], x[1], x[2]), // Top-left vertice position
//                                    new Vector3(x[0] + du[0], x[1] + du[1], x[2] + du[2]), // Top right vertice position
//                                    new Vector3(x[0] + dv[0], x[1] + dv[1], x[2] + dv[2]), // Bottom left vertice position
//                                    new Vector3(x[0] + du[0] + dv[0], x[1] + du[1] + dv[1], x[2] + du[2] + dv[2]) // Bottom right vertice position
//                                );

                                // Clear this part of the mask, so we don't add duplicate faces
                                for (l = 0; l < h; ++l)
                                for (k = 0; k < w; ++k)
                                {
                                    mask[n + k + (l * CHUNK_SIZE)] = false;
                                }

                                // Increment counters and continue
                                i += w;
                                n += w;
                            }
                            else
                            {
                                i++;
                                n++;
                            }
                        }
                    }
                }
            }
        }
    }
}