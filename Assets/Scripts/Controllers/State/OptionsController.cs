#region

using System;
using System.IO;
using Controllers.World;
using Logging;
using NLog;
using SharpConfig;
using UnityEngine;

#endregion

namespace Controllers.State
{
    public enum CacheCullingAggression
    {
        /// <summary>
        ///     Passive culling will only cull chunks when
        ///     given enough processing time to do so.
        /// </summary>
        Passive = 0,

        /// <summary>
        ///     Active cache culling will force the game to keep
        ///     the total amount of cached chunks at or below
        ///     the maximum
        /// </summary>
        Active = 1
    }

    public class OptionsController : SingletonController<OptionsController>
    {
        public static class Defaults
        {
            // General
            public const ThreadingMode THREADING_MODE = ThreadingMode.Single;

            // Graphics
            public const int MAXIMUM_FRAME_RATE_BUFFER_SIZE = 60;
            public const int MAXIMUM_INTERNAL_FRAMES = 60;
            public const int VSYNC_LEVEL = 1;
            public const int SHADOW_DISTANCE = 3;

            // Chunking
            public const bool PRE_INITIALIZE_CHUNK_CACHE = true;
            public const int MAXIMUM_CHUNK_CACHE_SIZE = 20;
            public const int MAXIMUM_CHUNK_LOAD_TIME_BUFFER_SIZE = 60;
            public const int PRE_LOAD_CHUNK_DISTANCE = 2;
        }

        private static string _configPath;

        private Configuration _Configuration;

        // General
        public ThreadingMode ThreadingMode;

        // Graphics
        public int MaximumInternalFrames;
        public TimeSpan MaximumInternalFrameTime;
        public int MaximumFrameRateBufferSize;

        // Chunking
        public bool PreInitializeChunkCache;
        public int MaximumChunkCacheSize;
        public int MaximumChunkLoadTimeBufferSize;
        public int PreLoadChunkDistance;

        public int VSyncLevel
        {
            get => QualitySettings.vSyncCount;
            set => QualitySettings.vSyncCount = value;
        }

        public int ShadowDistance;

        private void Awake()
        {
            AssignCurrent(this);

            _configPath = $@"{Application.persistentDataPath}/config.ini";
        }

        private void Start()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            _Configuration = !File.Exists(_configPath)
                ? InitialiseDefaultConfig()
                : Configuration.LoadFromFile(_configPath);

            // General
            if (!GetSetting("General", nameof(ThreadingMode), out ThreadingMode))
            {
                SettingLoadError(nameof(ThreadingMode), Defaults.THREADING_MODE);
                ThreadingMode = Defaults.THREADING_MODE;
            }


            // Graphics
            if (!GetSetting("Graphics", nameof(MaximumInternalFrames), out MaximumInternalFrames)
                || (MaximumInternalFrames < 0)
                || (MaximumInternalFrames > 300))
            {
                SettingLoadError(nameof(MaximumInternalFrames), Defaults.MAXIMUM_INTERNAL_FRAMES);
                MaximumInternalFrames = Defaults.MAXIMUM_INTERNAL_FRAMES;
            }

            MaximumInternalFrameTime = TimeSpan.FromSeconds(1f / MaximumInternalFrames);

            if (!GetSetting("Graphics", nameof(MaximumFrameRateBufferSize), out MaximumFrameRateBufferSize)
                || (MaximumFrameRateBufferSize < 0)
                || (MaximumFrameRateBufferSize > 120))
            {
                SettingLoadError(nameof(MaximumFrameRateBufferSize), Defaults.MAXIMUM_FRAME_RATE_BUFFER_SIZE);
                MaximumFrameRateBufferSize = Defaults.MAXIMUM_FRAME_RATE_BUFFER_SIZE;
            }

            if (!GetSetting("Graphics", nameof(VSyncLevel), out int vSyncLevel) || (vSyncLevel < 0) || (vSyncLevel > 4))
            {
                SettingLoadError(nameof(vSyncLevel), Defaults.VSYNC_LEVEL);
                vSyncLevel = Defaults.VSYNC_LEVEL;
            }

            VSyncLevel = vSyncLevel;

            if (!GetSetting("Graphics", nameof(ShadowDistance), out ShadowDistance)
                || (ShadowDistance < 0)
                || (ShadowDistance > 25))
            {
                SettingLoadError(nameof(ShadowDistance), Defaults.SHADOW_DISTANCE);
                ShadowDistance = Defaults.SHADOW_DISTANCE;
            }


            // Chunking
            if (!GetSetting("Chunking", nameof(PreInitializeChunkCache), out PreInitializeChunkCache))
            {
                SettingLoadError(nameof(PreInitializeChunkCache), Defaults.PRE_INITIALIZE_CHUNK_CACHE);
                PreInitializeChunkCache = Defaults.PRE_INITIALIZE_CHUNK_CACHE;
            }

            if (!GetSetting("Chunking", nameof(MaximumChunkCacheSize), out MaximumChunkCacheSize)
                || (MaximumChunkCacheSize < -1)
                || (MaximumChunkCacheSize > 625))
            {
                SettingLoadError(nameof(MaximumChunkCacheSize), Defaults.MAXIMUM_CHUNK_CACHE_SIZE);
                MaximumChunkCacheSize = Defaults.MAXIMUM_CHUNK_CACHE_SIZE;
            }

            if (!GetSetting("Chunking", nameof(MaximumChunkLoadTimeBufferSize), out MaximumChunkLoadTimeBufferSize)
                || (MaximumFrameRateBufferSize < 0)
                || (MaximumChunkLoadTimeBufferSize > 120))
            {
                SettingLoadError(nameof(MaximumChunkLoadTimeBufferSize), Defaults.MAXIMUM_CHUNK_LOAD_TIME_BUFFER_SIZE);
                MaximumChunkLoadTimeBufferSize = Defaults.MAXIMUM_CHUNK_LOAD_TIME_BUFFER_SIZE;
            }

            if (!GetSetting("Chunking", nameof(PreLoadChunkDistance), out PreLoadChunkDistance)
                || (PreLoadChunkDistance < 0))
            {
                SettingLoadError(nameof(PreLoadChunkDistance), Defaults.PRE_LOAD_CHUNK_DISTANCE);
                PreLoadChunkDistance = Defaults.PRE_LOAD_CHUNK_DISTANCE;
            }

            EventLog.Logger.Log(LogLevel.Info, "Configuration loaded.");
        }

        private Configuration InitialiseDefaultConfig()
        {
            EventLog.Logger.Log(LogLevel.Info, "Initializing default configuration file...");

            _Configuration = new Configuration();

            // General
            _Configuration["General"][nameof(ThreadingMode)].PreComment =
                "Determines whether the threading mode the game will use when generating chunk data and meshes.";
            _Configuration["General"][nameof(ThreadingMode)].Comment = "(0 = single, 1 = multi, 2 = variable)";
            _Configuration["General"][nameof(ThreadingMode)].IntValue =
                (int) Defaults.THREADING_MODE;


            // Graphics
            _Configuration["Graphics"][nameof(MaximumInternalFrames)].PreComment =
                "Maximum number of frames internal systems will allow to lapse during updates.";
            _Configuration["Graphics"][nameof(MaximumInternalFrames)].Comment =
                "Higher values decrease overall CPU stress (min 15, max 150).";
            _Configuration["Graphics"][nameof(MaximumInternalFrames)].IntValue =
                Defaults.MAXIMUM_INTERNAL_FRAMES;

            _Configuration["Graphics"][nameof(MaximumFrameRateBufferSize)].PreComment =
                "Maximum size of buffer for reporting average frame rate.";
            _Configuration["Graphics"][nameof(MaximumFrameRateBufferSize)].Comment =
                "Higher values decrease frame-to-frame accuracy. (min 0, max 120)";
            _Configuration["Graphics"][nameof(MaximumFrameRateBufferSize)].IntValue =
                Defaults.MAXIMUM_FRAME_RATE_BUFFER_SIZE;

            _Configuration["Graphics"][nameof(VSyncLevel)].PreComment =
                "Each level increases the number of screen updates to wait before rendering to the screen.";
            _Configuration["Graphics"][nameof(VSyncLevel)].Comment = "Maximum value of 4";
            _Configuration["Graphics"][nameof(VSyncLevel)].IntValue =
                Defaults.VSYNC_LEVEL;

            _Configuration["Graphics"][nameof(ShadowDistance)].PreComment =
                "Defines radius in chunks around player to draw shadows.";
            _Configuration["Graphics"][nameof(ShadowDistance)].IntValue =
                Defaults.SHADOW_DISTANCE;


            // Chunking
            _Configuration["Chunking"][nameof(PreInitializeChunkCache)].PreComment =
                "Determines whether the chunk cache is pre-initialized to safe capacity.";
            _Configuration["Chunking"][nameof(PreInitializeChunkCache)].BoolValue =
                Defaults.PRE_INITIALIZE_CHUNK_CACHE;

            _Configuration["Chunking"][nameof(MaximumChunkCacheSize)].PreComment =
                "Lower values are harder on the CPU, higher values use more RAM.";
            _Configuration["Chunking"][nameof(MaximumChunkCacheSize)].Comment = "(-1 = unlimited)";
            _Configuration["Chunking"][nameof(MaximumChunkCacheSize)].IntValue =
                Defaults.MAXIMUM_CHUNK_CACHE_SIZE;

            _Configuration["Chunking"][nameof(MaximumChunkLoadTimeBufferSize)].PreComment =
                "Lower values give a more accurate frame-to-frame reading, with higher values giving more long-term accuracy.";
            _Configuration["Chunking"][nameof(MaximumChunkLoadTimeBufferSize)].Comment = "(min 0, max 120)";
            _Configuration["Chunking"][nameof(MaximumChunkLoadTimeBufferSize)].IntValue =
                Defaults.MAXIMUM_CHUNK_LOAD_TIME_BUFFER_SIZE;

            _Configuration["Chunking"][nameof(PreLoadChunkDistance)].PreComment =
                "Defines extra radius of chunks to pre-load (build).";
            _Configuration["Chunking"][nameof(PreLoadChunkDistance)].IntValue =
                Defaults.PRE_LOAD_CHUNK_DISTANCE;

            EventLog.Logger.Log(LogLevel.Info, "Default configuration initialized. Saving...");

            _Configuration.SaveToFile(_configPath);

            EventLog.Logger.Log(LogLevel.Info, $"Configuration file saved at: {_configPath}");

            return _Configuration;
        }

        private bool GetSetting<T>(string section, string setting, out T value)
        {
            try
            {
                value = _Configuration[section][setting].GetValue<T>();
            }
            catch (Exception)
            {
                value = default;
                return false;
            }

            return true;
        }

        private void SettingLoadError(string settingName, object defaultValue)
        {
            EventLog.Logger.Log(LogLevel.Warn, $"Error loading setting `{settingName}`, defaulting to {defaultValue}.");
        }
    }
}
