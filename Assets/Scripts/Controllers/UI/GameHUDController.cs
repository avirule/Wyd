#region

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Controllers.Entity;
using Controllers.Entity.Inventory;
using Game.Entities.Inventory;
using UnityEngine;

#endregion

namespace Controllers.UI
{
    public class GameHUDController : SingletonController<GameHUDController>
    {
        private List<DisplayBlockController> _DisplayBlocks;

        public GameObject Hotbar;
        public GameObject DisplayBlockObject;

        private void Awake()
        {
            AssignCurrent(this);

            _DisplayBlocks = new List<DisplayBlockController>();
        }

        private void Start()
        {
            transform.localPosition = Vector3.zero;
            PlayerController.Current.Inventory.HotbarChanged += OnHotbarCollectionChanged;
        }


        private void OnHotbarCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (object item in args.NewItems)
                    {
                        if (!(item is ItemStack itemStack)
                            || (args.NewStartingIndex > InventoryController.MAXIMUM_HOTBAR_STACKS))
                        {
                            continue;
                        }

                        GameObject displayBlockGameObject = Instantiate(DisplayBlockObject, Hotbar.transform);
                        DisplayBlockController displayBlock =
                            displayBlockGameObject.GetComponent<DisplayBlockController>();
                        displayBlock.InitializeAs(itemStack);

                        _DisplayBlocks.Insert(itemStack.InventoryIndex, displayBlock);
                    }

                    break;
                case NotifyCollectionChangedAction.Move:
                    foreach (object item in args.NewItems)
                    {
                        if (!(item is ItemStack itemStack)
                            || (args.NewStartingIndex > InventoryController.MAXIMUM_HOTBAR_STACKS))
                        {
                            continue;
                        }

                        DisplayBlockController displayBlock = _DisplayBlocks[itemStack.InventoryIndex];
                        displayBlock.AmountText.text = itemStack.Amount.ToString();
                    }

                    break;
                case NotifyCollectionChangedAction.Remove:
                    break;
                case NotifyCollectionChangedAction.Replace:
                    break;
                case NotifyCollectionChangedAction.Reset:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
