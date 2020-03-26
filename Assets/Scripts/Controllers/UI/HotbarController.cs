#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using UnityEngine;
using Wyd.Controllers.Entity;
using Wyd.Controllers.State;
using Wyd.Game.Entities.Inventory;

#endregion

namespace Wyd.Controllers.UI
{
    public class HotbarController : SingletonController<HotbarController>
    {
        public const int MAXIMUM_HOTBAR_STACKS = 8;

        private ObservableCollection<DisplayBlockController> _InternalInventory;
        private float _InitialSelectedCellPositionX;
        private float _ScrollValue;
        private int _CurrentIndex;

        public GameObject DisplayBlockObject;
        public RectTransform CellsContainer;
        public RectTransform SelectedCell;

        public IReadOnlyList<DisplayBlockController> Blocks => _InternalInventory;

        public int CurrentIndex
        {
            get => _CurrentIndex;
            set
            {
                int newValue = 0;

                if (value < 0)
                {
                    newValue = MAXIMUM_HOTBAR_STACKS - ((value % MAXIMUM_HOTBAR_STACKS) + 2);
                }
                else
                {
                    newValue = value % MAXIMUM_HOTBAR_STACKS;
                }

                _CurrentIndex = newValue;
            }
        }

        public ushort SelectedId { get; private set; }

        public event NotifyCollectionChangedEventHandler HotbarChanged;
        public event EventHandler<int> SelectedBlockChanged;

        private void Awake()
        {
            AssignSingletonInstance(this);

            _InternalInventory =
                new ObservableCollection<DisplayBlockController>(new DisplayBlockController[MAXIMUM_HOTBAR_STACKS]);
            _InitialSelectedCellPositionX = SelectedCell.localPosition.x;

            _InternalInventory.CollectionChanged += (sender, args) => { HotbarChanged?.Invoke(sender, args); };


            // todo move hotbar logic to hotbar and remove from inventory
        }

        private void Start()
        {
            PlayerController.Current.Inventory.HotbarChanged += OnHotbarCollectionChanged;
        }

        private void Update()
        {
            _ScrollValue = InputController.Current.GetAxis("Mouse ScrollWheel");

            if (_ScrollValue != 0f)
            {
                ProcessScrollValue();
            }
        }

        private void ProcessScrollValue()
        {
            int scrollValueInt =
                _ScrollValue < 0 ? Mathf.FloorToInt(_ScrollValue) : Mathf.CeilToInt(_ScrollValue);
            scrollValueInt = -scrollValueInt; // flip polarity for better game feel

            CurrentIndex += scrollValueInt;

            SelectedId = _InternalInventory[CurrentIndex]?.BlockId ?? 0;

            SelectedBlockChanged?.Invoke(this, CurrentIndex);

            Vector3 selectedCellPos = SelectedCell.localPosition;
            selectedCellPos.x = _InitialSelectedCellPositionX + (CurrentIndex * SelectedCell.sizeDelta.x);
            SelectedCell.localPosition = selectedCellPos;
        }

        private void OnHotbarCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (object item in args.NewItems)
                    {
                        if (!(item is ItemStack itemStack)
                            || (args.NewStartingIndex > MAXIMUM_HOTBAR_STACKS))
                        {
                            continue;
                        }

                        GameObject displayBlockGameObject = Instantiate(DisplayBlockObject, CellsContainer.transform);
                        DisplayBlockController displayBlock =
                            displayBlockGameObject.GetComponent<DisplayBlockController>();
                        displayBlock.InitializeAs(itemStack);

                        _InternalInventory.Insert(itemStack.InventoryIndex, displayBlock);
                    }

                    break;
                case NotifyCollectionChangedAction.Move:
                    foreach (object item in args.NewItems)
                    {
                        if (!(item is ItemStack itemStack)
                            || (args.NewStartingIndex > MAXIMUM_HOTBAR_STACKS))
                        {
                            continue;
                        }

                        DisplayBlockController displayBlock = _InternalInventory[itemStack.InventoryIndex];

                        if (displayBlock == default)
                        {
                            return;
                        }

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
