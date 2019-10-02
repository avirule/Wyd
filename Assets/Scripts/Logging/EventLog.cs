#region

using Logging.Targets;
using NLog;
using NLog.Config;
using NLog.Targets;

#endregion

namespace Logging
{
    public static class EventLog
    {
        public static readonly Logger Logger;

        static EventLog()
        {
            ConfigureLogger();
            Logger = LogManager.GetCurrentClassLogger();
        }

        private static void ConfigureLogger()
        {
            LoggingConfiguration config = new LoggingConfiguration();
            ConsoleTarget consoleTarget = new ConsoleTarget("logConsole");
            InGameDebugLogTarget inGameDebugLogTarget = new InGameDebugLogTarget();

#if UNITY_EDITOR
            Target.Register<UnityDebuggerTarget>("UnityDebuggerTarget");

            UnityDebuggerTarget unityDebuggerTarget = new UnityDebuggerTarget();

            config.AddRule(LogLevel.Info, LogLevel.Fatal, unityDebuggerTarget);
#endif

            config.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, inGameDebugLogTarget);

            LogManager.Configuration = config;
        }
    }
}
