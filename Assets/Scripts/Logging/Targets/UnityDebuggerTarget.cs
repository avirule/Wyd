#region

using NLog;
using NLog.Config;
using NLog.Targets;
using UnityEngine;

#endregion

namespace Logging.Targets
{
    [Target("UnityDebuggerTarget")]
    public sealed class UnityDebuggerTarget : TargetWithLayout
    {
        public UnityDebuggerTarget()
        {
            Host = "localhost";
            Layout = "(${logger}) : ${message}";
        }

        [RequiredParameter] public string Host { get; set; }

        protected override void Write(LogEventInfo logEvent)
        {
            base.Write(logEvent);

            DebugLog(logEvent, Layout.Render(logEvent));
        }

        private static void DebugLog(LogEventInfo logEvent, string logEventMessage)
        {
            if (logEvent.Level == LogLevel.Info)
            {
                Debug.Log(logEventMessage);
            }
            else if (logEvent.Level == LogLevel.Warn)
            {
                Debug.LogWarning(logEventMessage);
            }
            else if ((logEvent.Level == LogLevel.Error) || (logEvent.Level == LogLevel.Fatal))
            {
                Debug.LogError(logEventMessage);
            }
        }
    }
}