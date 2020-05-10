#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#endregion

namespace Wyd.System.Jobs
{
    public class AsyncParallelJob : AsyncJob
    {
        private readonly int _Length;
        private readonly int _BatchLength;
        private readonly int _TotalBatches;

        protected AsyncParallelJob(int length, int batchLength)
        {
            _Length = length;
            _BatchLength = batchLength;
            _TotalBatches = (int)Math.Ceiling((double)_Length / _BatchLength);
        }

        protected override async Task Process() => await BatchTasksAndAwait();

        protected virtual Task ProcessIndex(int index) => Task.CompletedTask;

        protected async Task BatchTasksAndAwait() => await Task.WhenAll(GetBatchedTasks()).ConfigureAwait(false);

        private IEnumerable<Task> GetBatchedTasks()
        {
            int currentStartIndex = 0, batchIndex = 0;

            for (; batchIndex < _TotalBatches; batchIndex++, currentStartIndex = batchIndex * _BatchLength)
            {
                int currentEndIndex = currentStartIndex + (_BatchLength - 1);

                if (currentEndIndex >= _Length)
                {
                    yield return ProcessIndexes(currentStartIndex, _Length - 1);
                }
                else
                {
                    yield return ProcessIndexes(currentStartIndex, currentEndIndex);
                }
            }
        }

        private async Task ProcessIndexes(int startIndex, int endIndex)
        {
            for (int index = startIndex; index <= endIndex; index++)
            {
                await ProcessIndex(index);
            }
        }
    }
}
