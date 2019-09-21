#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#endregion

namespace Controllers.Entity
{
    public class InventoryController : MonoBehaviour
    {
        public const int MAXIMUM_STACK_SIZE = sbyte.MaxValue;

        public class ItemStack
        {
            public ushort BlockId { get; private set; }
            public int Amount { get; private set; }

            public ItemStack(ushort blockId)
            {
                Initialise(blockId);
            }

            public void Initialise(ushort blockId)
            {
                BlockId = blockId;
            }

            public int AllocateAmount(int amount)
            {
                Amount += amount;

                if (Amount <= MAXIMUM_STACK_SIZE)
                {
                    return amount;
                }

                int consumed = Math.Abs(MAXIMUM_STACK_SIZE - Amount);
                Amount = MAXIMUM_STACK_SIZE;
                return consumed;
            }
        }

        private List<ItemStack> _InternalInventory;

        public event EventHandler<ItemStack> ItemStackModified;

        private void Awake()
        {
            _InternalInventory = new List<ItemStack>(128);
        }

        public void AddItem(ushort blockId, int amount)
        {
            int remainingAmount = amount;

            while (remainingAmount > 0)
            {
                ItemStack itemStack = GetFirstNonFullItemStackWithId(blockId);

                if (itemStack == default)
                {
                    itemStack = new ItemStack(blockId);
                    _InternalInventory.Add(itemStack);
                }

                int consumedAmount = itemStack.AllocateAmount(remainingAmount);
                remainingAmount -= consumedAmount;
                
                ItemStackModified?.Invoke(this, itemStack);
            }
        }

        private ItemStack GetFirstNonFullItemStackWithId(ushort blockId)
        {
            return _InternalInventory.FirstOrDefault(itemStack =>
                (itemStack.BlockId == blockId) && (itemStack.Amount < MAXIMUM_STACK_SIZE));
        }
    }
}
