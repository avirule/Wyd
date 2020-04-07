#region

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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

        public async Task PushAsync(T item, CancellationToken cancellationToken = default)
        {
            await _Writer.WriteAsync(item, cancellationToken);
        }

        public async Task<T> TakeAsync(CancellationToken cancellationToken = default)
        {
            while (await _Reader.WaitToReadAsync(cancellationToken))
            {
                return await _Reader.ReadAsync(cancellationToken);
            }

            return default;
        }
    }
}
