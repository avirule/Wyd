#region

using System;

#endregion

namespace Wyd.Entities.Inventory
{
    public class ItemStack
    {
        public const int MAXIMUM_STACK_SIZE = sbyte.MaxValue + 1;

        public int InventoryIndex { get; set; }
        public ushort BlockId { get; private set; }
        public int Amount { get; private set; }

        public ItemStack(int inventoryIndex, ushort blockId)
        {
            Initialise(inventoryIndex, blockId);
        }

        public void Initialise(int inventoryIndex, ushort blockId)
        {
            InventoryIndex = inventoryIndex;
            BlockId = blockId;
        }

        public int AllocateAmount(int amount)
        {
            Amount += amount;

            if (Amount < 0)
            {
                // todo destroy
            }

            if (Amount <= MAXIMUM_STACK_SIZE)
            {
                return amount;
            }

            int consumed = Math.Abs(MAXIMUM_STACK_SIZE - Amount);
            Amount = MAXIMUM_STACK_SIZE;
            return consumed;
        }
    }
}
