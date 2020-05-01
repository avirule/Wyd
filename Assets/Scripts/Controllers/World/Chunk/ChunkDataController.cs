using Wyd.System.Collections;

namespace Wyd.Controllers.World.Chunk
{
    public class ChunkDataController : ActivationStateChunkController
    {
        public INodeCollection<ushort> Blocks { get; set; }



        protected override void OnEnable()
        {
            base.OnEnable();


        }

        protected override void OnDisable()
        {
            base.OnDisable();

            Blocks = null;
        }
    }
}
