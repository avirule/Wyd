namespace Game.Entities.Inventory
{
    public class ItemStackOperationEventArgs
    {
        public ItemStackOperation Operation { get; private set; }
        public ItemStack ItemStack { get; private set; }

        public ItemStackOperationEventArgs(ItemStackOperation operation, ItemStack itemStack)
        {
            Operation = operation;
            ItemStack = itemStack;
        }
    }
}
