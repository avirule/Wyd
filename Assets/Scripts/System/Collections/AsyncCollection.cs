#region

using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

#endregion

namespace Wyd.System.Collections
{
    public class AsyncCollection<T>
    {
        private readonly ChannelReader<T> _Reader;
        private readonly ChannelWriter<T> _Writer;

        public AsyncCollection()
        {
            Channel<T> channel = Channel.CreateUnbounded<T>();
            _Reader = channel.Reader;
            _Writer = channel.Writer;
        }

        public async ValueTask AddAsync(T item, CancellationToken cancellationToken = default) =>
            await _Writer.WriteAsync(item, cancellationToken);

        public async ValueTask<T> TakeAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_Reader.TryRead(out T item))
                {
                    return item;
                }
                else
                {
                    await Task.Delay(1, cancellationToken);
                }
            }

            return default;
        }
    }
}
