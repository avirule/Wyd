#region

using System.Collections.Generic;
using System.Threading.Tasks;

#endregion

namespace Wyd.System.Jobs
{
    public class AsyncParallelJob : AsyncJob
    {
        private readonly int _Length;
        private readonly int _BatchLength;

        public AsyncParallelJob(int length, int batchLength)
        {
            _Length = length;
            _BatchLength = batchLength;
        }


        protected override Task Process()
        {
            List<Task> batchedTasks = new List<Task>();

            for (int aggregateLength = 0; aggregateLength <= _Length; aggregateLength += _BatchLength)
            {
                int aggregateBatchLength = aggregateLength + _BatchLength;

                if (aggregateBatchLength == 0)
                {
                    break;
                }
                else if (batchedRemainingLength < 0)
                {
                    batchedTasks.Add(ProcessBatch(0, aggregateLength));
                    break;
                }
                else
                {
                    batchedTasks.Add(ProcessBatch(batchedRemainingLength, aggregateLength));
                }
            }

            Task.WaitAll(batchedTasks.ToArray(), CancellationToken);

            return Task.CompletedTask;
        }

        protected virtual Task ProcessIndex(int index) => Task.CompletedTask;

        private async Task ProcessBatch(int startIndex, int endIndex)
        {
            for (int index = startIndex; index < endIndex; index++)
            {
                await ProcessIndex(index);
            }
        }
    }
}
