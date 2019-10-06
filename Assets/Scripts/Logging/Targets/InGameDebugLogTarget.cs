#region

using System;
using System.Collections.Generic;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Targets;

#endregion

namespace Wyd.Logging.Targets
{
    [Target("InGameDebugLog")]
    public sealed class InGameDebugLogTarget : TargetWithLayout
    {
        public const string ERROR_COLOR = "#FF6961";
        public const string WARN_COLOR = "#FFFF99";

        public static List<LogEventInfo> DebugEntries;

        public static event EventHandler<LogEventInfo> EventLogged;

        [RequiredParameter]
        public string Host { get; set; }

        public InGameDebugLogTarget()
        {
            Host = "localhost";
            Layout = "${message}";

            DebugEntries = new List<LogEventInfo>();
        }

        protected override void Write(AsyncLogEventInfo logEvent)
        {
            base.Write(logEvent);

            DebugEntries.Add(logEvent.LogEvent);
            OnEventLogged(this, logEvent.LogEvent);
        }

        private static void OnEventLogged(object sender, LogEventInfo eventInfo)
        {
            EventLogged?.Invoke(sender, eventInfo);
        }
    }
}
