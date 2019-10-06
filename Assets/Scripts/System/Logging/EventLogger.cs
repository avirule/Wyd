#region

using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Wyd.System.Logging.Targets;

#endregion

namespace Wyd.System.Logging
{
    public static class EventLogger
    {
        private static readonly Logger Logger;

        static EventLogger()
        {
            ConfigureLogger();
            Logger = LogManager.GetCurrentClassLogger();
        }

        private static void ConfigureLogger()
        {
            LoggingConfiguration config = new LoggingConfiguration();

#if UNITY_EDITOR
            Target.Register<UnityDebuggerTarget>(nameof(UnityDebuggerTarget));
            UnityDebuggerTarget unityDebuggerTarget = new UnityDebuggerTarget();
            config.AddRule(LogLevel.Info, LogLevel.Fatal, unityDebuggerTarget);
#endif

            Target.Register<InGameDebugLogTarget>(nameof(InGameDebugLogTarget));
            InGameDebugLogTarget inGameDebugLogTarget = new InGameDebugLogTarget();
            config.AddRule(LogLevel.Info, LogLevel.Fatal, inGameDebugLogTarget);


            FileTarget fileTarget = new FileTarget
            {
                OpenFileFlushTimeout = 30,
                Encoding = Encoding.ASCII,
                LineEnding = LineEndingMode.CRLF,
                ArchiveAboveSize = 4000000, // 4MB
                ArchiveEvery = FileArchivePeriod.Day,
                MaxArchiveFiles = 8,
                EnableArchiveFileCompression = true,
                FileName = @"logs/",
                CreateDirs = true,
                KeepFileOpen = true,
                OpenFileCacheTimeout = 30,
                ConcurrentWrites = false
            };
            
            AsyncTargetWrapper asyncFileWrapper = new AsyncTargetWrapper(fileTarget)
            {
                OverflowAction = AsyncTargetWrapperOverflowAction.Block,
                OptimizeBufferReuse = true,
                ForceLockingQueue = false
            };

            config.AddRule(LogLevel.Trace, LogLevel.Fatal, asyncFileWrapper);

            LogManager.Configuration = config;
        }

        public static void Log(LogLevel level, string message)
        {
            Logger.Log(level, message);
        }

        public static void Log(LogLevel level, object args)
        {
            Logger.Log(level, args);
        }
    }
}
