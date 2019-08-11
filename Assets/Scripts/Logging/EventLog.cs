#region

using System.Collections.Generic;
using Logging.Targets;
using NLog;
using NLog.Config;
using NLog.Targets;
using UnityEngine;
using Logger = NLog.Logger;

#endregion

namespace Logging
{
    public static class EventLog
    {
        private static MemoryTarget memoryTarget;

        public static readonly Logger Logger;

        static EventLog()
        {
            ConfigureLogger();
            Logger = LogManager.GetCurrentClassLogger();
        }

        public static IList<string> Logs => memoryTarget.Logs;

        private static void ConfigureLogger()
        {
            LoggingConfiguration config = new LoggingConfiguration();
            ConsoleTarget consoleTarget = new ConsoleTarget("logConsole");
            memoryTarget = new MemoryTarget();

            if (Debug.isDebugBuild)
            {
                Target.Register<UnityDebuggerTarget>("UnityDebuggerTarget");

                UnityDebuggerTarget unityDebuggerTarget = new UnityDebuggerTarget();

                config.AddRule(LogLevel.Info, LogLevel.Fatal, unityDebuggerTarget);
            }

            config.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, memoryTarget);

            LogManager.Configuration = config;
        }
    }
}