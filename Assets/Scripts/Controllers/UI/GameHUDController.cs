#region

using System.Collections.Generic;
using Controllers.Entity;
using UnityEngine;

#endregion

namespace Controllers.UI
{
    public class GameHUDController : SingletonController<GameHUDController>
    {
        private List<GameObject> _DisplayBlocks;

        public GameObject Hotbar;
        public GameObject DisplayBlockObject;

        private int placedBlocks;

        private void Awake()
        {
            AssignCurrent(this);

            _DisplayBlocks = new List<GameObject>();
        }

        private void Start()
        {
            transform.localPosition = Vector3.zero;
            PlayerController.Current.Inventory.ItemStackModified += (sender, itemStack) =>
            {
                GameObject displayBlock = Instantiate(DisplayBlockObject, Hotbar.transform);
                displayBlock.GetComponent<DisplayBlockController>().InitializeAs(itemStack.BlockId);

                _DisplayBlocks.Add(displayBlock);
                
                placedBlocks += 1;
            };
        }
    }
}
