namespace Threading
{
    public interface IThreadedQueue
    {
        bool Disposed { get; }
        bool Running { get; }
        void Start();
        void Abort();

        /// <summary>
        ///     Adds specified ThreadedItem to queue and returns a unique identity
        /// </summary>
        /// <param name="threadedItem"></param>
        /// <returns>unique object identity</returns>
        object AddThreadedItem(ThreadedItem threadedItem);

        bool TryGetFinishedItem(object identity, out ThreadedItem threadedItem);
        void Dispose();
        
    }
}