#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Game.Entities.Inventory;
using UnityEngine;

#endregion

namespace Controllers.Entity.Inventory
{
    public class InventoryController : MonoBehaviour
    {
        public const int MAXIMUM_HOTBAR_STACKS = 5;

        private ObservableCollection<ItemStack> _InternalInventory;
        private ObservableCollection<ItemStack> _InternalHotbar;

        public IReadOnlyCollection<ItemStack> Inventory => _InternalInventory;
        public IReadOnlyCollection<ItemStack> Hotbar => _InternalHotbar;

        public event EventHandler<NotifyCollectionChangedEventArgs> InventoryChanged;
        public event EventHandler<NotifyCollectionChangedEventArgs> HotbarChanged;

        private void Awake()
        {
            _InternalInventory = new ObservableCollection<ItemStack>();
            _InternalHotbar = new ObservableCollection<ItemStack>();
        }

        public void AddItem(ushort blockId, int amount)
        {
            int remainingAmount = amount;

            while (remainingAmount > 0)
            {
                ItemStack itemStack = GetFirstNonFullItemStackWithId(blockId);

                if (itemStack == default)
                {
                    itemStack = new ItemStack(0, blockId);

                    if (_InternalHotbar.Count < MAXIMUM_HOTBAR_STACKS)
                    {
                        itemStack.InventoryIndex = _InternalHotbar.Count;
                        _InternalHotbar.Add(itemStack);
                        HotbarChanged?.Invoke(this,
                            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemStack,
                                _InternalHotbar.Count));
                    }
                    else
                    {
                        _InternalInventory.Add(itemStack);
                        InventoryChanged?.Invoke(this,
                            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemStack,
                                MAXIMUM_HOTBAR_STACKS + _InternalInventory.Count));
                    }
                }

                if (HotbarContainsNonMaxStackOf(blockId))
                {
                    int consumedAmount = itemStack.AllocateAmount(remainingAmount);
                    remainingAmount -= consumedAmount;
                    HotbarChanged?.Invoke(this,
                        new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, itemStack,
                            itemStack.InventoryIndex, itemStack.InventoryIndex));
                }
            }
        }

        private ItemStack GetFirstNonFullItemStackWithId(ushort blockId)
        {
            return _InternalHotbar.FirstOrDefault(itemStack =>
                       (itemStack.BlockId == blockId) && (itemStack.Amount < ItemStack.MAXIMUM_STACK_SIZE))
                   ?? _InternalInventory.FirstOrDefault(itemStack =>
                       (itemStack.BlockId == blockId) && (itemStack.Amount < ItemStack.MAXIMUM_STACK_SIZE));
        }

        private bool HotbarContainsNonMaxStackOf(ushort blockId)
        {
            return _InternalHotbar.Any(itemStack =>
                (itemStack.BlockId == blockId) && (itemStack.Amount < ItemStack.MAXIMUM_STACK_SIZE));
        }
    }
}
