#region

using System;
using System.Collections.Generic;
using Wyd.Collections;

#endregion

namespace Wyd.Singletons
{
    public class Diagnostics : Singleton<Diagnostics>
    {
        private readonly Dictionary<string, FixedConcurrentQueue<TimeSpan>> _DiagnosticTimes;

        public FixedConcurrentQueue<TimeSpan> this[string name]
        {
            get
            {
                if (!_DiagnosticTimes.ContainsKey(name))
                {
                    throw new KeyNotFoundException(name);
                }
                else
                {
                    return _DiagnosticTimes[name];
                }
            }
        }

        public event EventHandler<TimeSpan> DiagnosticBuffersChanged;

        public Diagnostics()
        {
            AssignSingletonInstance(this);

            _DiagnosticTimes = new Dictionary<string, FixedConcurrentQueue<TimeSpan>>();
        }

        public void RegisterDiagnosticBuffer(string name)
        {
            if (_DiagnosticTimes.ContainsKey(name))
            {
                throw new ArgumentException("Diagnostic entry already exists.", nameof(name));
            }
            else
            {
                _DiagnosticTimes.Add(name, new FixedConcurrentQueue<TimeSpan>(Options.Instance.DiagnosticBufferSize));
                _DiagnosticTimes[name].ItemEnqueued += DiagnosticBuffersChanged;
            }
        }

        public void UnregisterDiagnosticTimeEntry(string name)
        {
            if (!_DiagnosticTimes.ContainsKey(name))
            {
                throw new ArgumentException("Diagnostic entry does not exists.", nameof(name));
            }
            else
            {
                _DiagnosticTimes.Remove(name);
            }
        }

        public TimeSpan GetAverage(string diagnosticRegister)
        {
            if (_DiagnosticTimes.TryGetValue(diagnosticRegister, out FixedConcurrentQueue<TimeSpan> diagnosticBuffer))
            {
                switch (diagnosticBuffer.Count)
                {
                    case 0:
                        return TimeSpan.Zero;
                    case 1 when diagnosticBuffer.TryPeek(out TimeSpan onlyTimeSpan):
                        return onlyTimeSpan;
                    default:
                        int indexes = 0;
                        double sum = 0d;

                        foreach (TimeSpan timeSpan in _DiagnosticTimes[diagnosticRegister])
                        {
                            sum += timeSpan.TotalSeconds;
                            indexes += 1;
                        }

                        return TimeSpan.FromTicks((long)(TimeSpan.TicksPerSecond * (sum / indexes)));
                }
            }
            else
            {
                throw new KeyNotFoundException($"Diagnostic register '{diagnosticRegister}' does not exist.");
            }
        }
    }
}
