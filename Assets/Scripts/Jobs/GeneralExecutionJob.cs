using System;

namespace Jobs
{
    public class GeneralExecutionJob : Job
    {
        private readonly Action _ExecutionAction;
        
        public GeneralExecutionJob(Action action)
        {
            _ExecutionAction = action;
        }

        protected override void Process()
        {
            _ExecutionAction?.Invoke();
        }
    }
}
