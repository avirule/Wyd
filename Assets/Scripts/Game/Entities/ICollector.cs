namespace Game.Entities
{
    public interface ICollector
    {
        void AddItem(ushort id, int amount);
        void RemoveItem(ushort id, int amount);
    }
}
