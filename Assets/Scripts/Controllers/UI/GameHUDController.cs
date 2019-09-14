#region

using UnityEngine;

#endregion

namespace Controllers.UI
{
    public class GameHUDController : SingletonController<GameHUDController>
    {
        public GameObject DisplayBlock;

        private void Awake()
        {
            AssignCurrent(this);
        }

        private void Start()
        {
            transform.localPosition = Vector3.zero;
            DisplayBlock = Instantiate(DisplayBlock, transform);
            DisplayBlock.transform.localPosition = new Vector3(0f, -185f, 0f);
            DisplayBlock.GetComponent<DisplayBlockController>().InitializeAs(3);
        }
    }
}
