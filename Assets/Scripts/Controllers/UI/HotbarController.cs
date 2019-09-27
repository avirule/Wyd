#region

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Controllers.Entity;
using Controllers.State;
using Game.Entities.Inventory;
using UnityEngine;

#endregion

namespace Controllers.UI
{
    public class HotbarController : SingletonController<HotbarController>
    {
        public const int MAXIMUM_HOTBAR_STACKS = 7;

        private List<DisplayBlockController> _DisplayBlocks;
        private float _InitialSelectedCellPositionX;
        private float _ScrollValue;
        private int _CurrentIndex;

        public GameObject DisplayBlockObject;
        public RectTransform CellsContainer;
        public RectTransform SelectedCell;

        public IReadOnlyList<DisplayBlockController> Blocks => _DisplayBlocks;

        public int CurrentIndex
        {
            get => _CurrentIndex;
            set
            {
                int newValue = 0;

                if (value < 0)
                {
                    newValue = MAXIMUM_HOTBAR_STACKS - (value % (MAXIMUM_HOTBAR_STACKS - 1));
                }
                else
                {
                    newValue = value % MAXIMUM_HOTBAR_STACKS;
                }

                _CurrentIndex = newValue;
            }
        }

        public ushort SelectedId { get; private set; }

        public event EventHandler<int> SelectedBlockChanged;

        private void Awake()
        {
            AssignCurrent(this);

            _DisplayBlocks = new List<DisplayBlockController>(new DisplayBlockController[MAXIMUM_HOTBAR_STACKS]);
            _InitialSelectedCellPositionX = SelectedCell.localPosition.x;
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

            SelectedId = _DisplayBlocks[CurrentIndex]?.BlockId ?? 0;

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

                        _DisplayBlocks.Insert(itemStack.InventoryIndex, displayBlock);
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
