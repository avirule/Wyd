#region

using System.Collections.Concurrent;
using NLog;
using NLog.Config;
using NLog.Targets;
using UnityEngine;

#endregion

namespace Wyd.Logging.Targets
{
    [Target("UnityDebuggerTarget")]
    public sealed class UnityDebuggerTarget : TargetWithLayout
    {
        private static readonly ConcurrentQueue<string> LogEvents;

        static UnityDebuggerTarget() => LogEvents = new ConcurrentQueue<string>();

        public UnityDebuggerTarget()
        {
            Host = "localhost";
            Layout = "(${logger}) : ${message}";
        }

        [RequiredParameter]
        public string Host { get; set; }

        protected override void Write(LogEventInfo logEvent)
        {
            base.Write(logEvent);

            DebugLog(logEvent, Layout.Render(logEvent));
        }

        private static void DebugLog(LogEventInfo logEvent, string logEventMessage)
        {
            if (logEvent.Level == LogLevel.Info)
            {
                LogEvents.Enqueue(logEventMessage);
            }
            else if (logEvent.Level == LogLevel.Warn)
            {
                LogEvents.Enqueue(logEventMessage);
            }
            else if ((logEvent.Level == LogLevel.Error) || (logEvent.Level == LogLevel.Fatal))
            {
                LogEvents.Enqueue(logEventMessage);
            }
        }

        public static void Flush()
        {
            while (LogEvents.Count > 0)
            {
                LogEvents.TryDequeue(out string logMessage);
                Debug.Log(logMessage);
            }
        }
    }
}
