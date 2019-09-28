#region

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Controllers.UI;
using Game.Entities.Inventory;
using UnityEngine;

#endregion

namespace Controllers.Entity
{
    public class InventoryController : MonoBehaviour
    {
        private ObservableCollection<ItemStack> _InternalInventory;
        private ObservableCollection<ItemStack> _InternalHotbar;

        public IReadOnlyCollection<ItemStack> Inventory => _InternalInventory;
        public IReadOnlyCollection<ItemStack> Hotbar => _InternalHotbar;

        public event NotifyCollectionChangedEventHandler InventoryChanged;
        public event NotifyCollectionChangedEventHandler HotbarChanged;

        private void Awake()
        {
            _InternalInventory = new ObservableCollection<ItemStack>();
            _InternalHotbar = new ObservableCollection<ItemStack>();
        }

        public void AddItem(ushort blockId, int amount)
        {
            int remainingAmount = amount;

            while ((_InternalHotbar.Count
                    <= HotbarController.MAXIMUM_HOTBAR_STACKS) /* todo remove this, make it more... dynamic */
                   && (remainingAmount > 0))
            {
                ItemStack itemStack = GetFirstNonFullItemStackWithId(blockId);

                if (itemStack == default)
                {
                    itemStack = new ItemStack(0, blockId);

                    if (_InternalHotbar.Count <= HotbarController.MAXIMUM_HOTBAR_STACKS)
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
                                HotbarController.MAXIMUM_HOTBAR_STACKS + _InternalInventory.Count));
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
