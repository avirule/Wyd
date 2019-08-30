#region

using System;
using System.IO;
using Game.World;
using Logging;
using NLog;
using SharpConfig;
using UnityEngine;

#endregion

namespace Controllers.Game
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

    public class OptionsController : MonoBehaviour
    {
        private static string _configPath;

        public static OptionsController Current;

        private Configuration _Configuration;

        public ThreadingMode ThreadingMode;
        public int MaximumChunkCacheSize;
        public CacheCullingAggression ChunkCacheCullingAggression;
        public int MaximumChunkLoadTimeBufferSize;
        public int MaximumFrameRateBufferSize;
        public int MinimumInternalFrames;
        public float MaximumInternalFrameTime;

        public int VSyncLevel
        {
            get => QualitySettings.vSyncCount;
            set => QualitySettings.vSyncCount = value;
        }

        public int ShadowDistance;
        public int ExpensiveMeshingDistance;

        private void Awake()
        {
            if ((Current != default) && (Current != this))
            {
                Destroy(gameObject);
            }
            else
            {
                Current = this;
            }

            _configPath = $@"{Application.persistentDataPath}\config.ini";
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

            // Graphics
            if (!GetSetting("Graphics", nameof(MinimumInternalFrames), out MinimumInternalFrames) ||
                (MinimumInternalFrames < 0) ||
                (MinimumInternalFrames > 300))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Error loading setting {nameof(MinimumInternalFrames)}.");
                MinimumInternalFrames = 15;
            }

            MaximumInternalFrameTime = 1f / MinimumInternalFrames;

            if (!GetSetting("Graphics", nameof(MaximumFrameRateBufferSize), out MaximumFrameRateBufferSize) ||
                (MaximumFrameRateBufferSize < 0) ||
                (MaximumFrameRateBufferSize > 120))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Error loading setting {nameof(MaximumFrameRateBufferSize)}.");
                MaximumFrameRateBufferSize = 30;
            }

            if (!GetSetting("Graphics", nameof(VSyncLevel), out int vSyncLevel) ||
                (vSyncLevel < 0) ||
                (vSyncLevel > 4))
            {
                EventLog.Logger.Log(LogLevel.Warn, $"Error loading setting {nameof(VSyncLevel)}.");
                vSyncLevel = 0;
            }

            VSyncLevel = vSyncLevel;

            if (!GetSetting("Graphics", nameof(ShadowDistance), out ShadowDistance) ||
                (ShadowDistance < 0) ||
                (ShadowDistance > 25))
            {
                EventLog.Logger.Log(LogLevel.Warn, $"Error loading setting {nameof(ShadowDistance)}.");
                ShadowDistance = 4;
            }

            if (!GetSetting("Graphics", nameof(ExpensiveMeshingDistance), out ExpensiveMeshingDistance) ||
                (ExpensiveMeshingDistance < 1) ||
                (ExpensiveMeshingDistance > 25))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Error loading setting {nameof(ExpensiveMeshingDistance)}.");
                ExpensiveMeshingDistance = 1;
            }


            // Chunking
            if (!GetSetting("Chunking", nameof(ThreadingMode), out ThreadingMode))
            {
                EventLog.Logger.Log(LogLevel.Warn, $"Error loading setting {nameof(ThreadingMode)}.");
                ThreadingMode = ThreadingMode.Variable;
            }

            if (!GetSetting("Chunking", nameof(MaximumChunkCacheSize), out MaximumChunkCacheSize) ||
                (MaximumChunkCacheSize < 0) ||
                (MaximumChunkCacheSize > 625))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Error loading setting {nameof(MaximumChunkCacheSize)}.");
                MaximumChunkCacheSize = 60;
            }

            if (!GetSetting("Chunking", nameof(ChunkCacheCullingAggression), out ChunkCacheCullingAggression))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Error loading setting {nameof(ExpensiveMeshingDistance)}.");
                ChunkCacheCullingAggression = CacheCullingAggression.Active;
            }

            if (!GetSetting("Chunking", nameof(MaximumChunkLoadTimeBufferSize), out MaximumChunkLoadTimeBufferSize) ||
                (MaximumFrameRateBufferSize < 0) ||
                (MaximumChunkLoadTimeBufferSize > 120))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Error loading setting {nameof(MaximumChunkLoadTimeBufferSize)}.");
                MaximumChunkLoadTimeBufferSize = 60;
            }

            EventLog.Logger.Log(LogLevel.Info, "Configuration loaded.");
        }

        private Configuration InitialiseDefaultConfig()
        {
            EventLog.Logger.Log(LogLevel.Info, "Initializing default configuration file...");

            _Configuration = new Configuration();

            // Graphics
            _Configuration["Graphics"][nameof(MinimumInternalFrames)].PreComment =
                "Maximum number of frames internal systems will allow to lapse during updates.";
            _Configuration["Graphics"][nameof(MinimumInternalFrames)].Comment =
                "Higher values decrease overall CPU stress (min 15, max 150).";
            _Configuration["Graphics"][nameof(MinimumInternalFrames)].IntValue = 30;

            _Configuration["Graphics"][nameof(MaximumFrameRateBufferSize)].PreComment =
                "Maximum size of buffer for reporting average frame rate.";
            _Configuration["Graphics"][nameof(MaximumFrameRateBufferSize)].Comment =
                "Higher values decrease frame-to-frame accuracy. (min 0, max 120)";
            _Configuration["Graphics"][nameof(MaximumFrameRateBufferSize)].IntValue = 60;

            _Configuration["Graphics"][nameof(VSyncLevel)].PreComment =
                "Each level increases the number of screen updates to wait before rendering to the screen.";
            _Configuration["Graphics"][nameof(VSyncLevel)].Comment = "Maximum value of 4";
            _Configuration["Graphics"][nameof(VSyncLevel)].IntValue = 0;

            _Configuration["Graphics"][nameof(ShadowDistance)].PreComment =
                "Defines radius in chunks around player to draw shadows.";
            _Configuration["Graphics"][nameof(ShadowDistance)].IntValue = 5;

            _Configuration["Graphics"][nameof(ExpensiveMeshingDistance)].PreComment =
                "Defines radius in chunks around player to generate collision meshes.";
            _Configuration["Graphics"][nameof(ExpensiveMeshingDistance)].Comment = "High values will cause lag.";
            _Configuration["Graphics"][nameof(ExpensiveMeshingDistance)].IntValue = 1;

            // Chunking
            _Configuration["Chunking"][nameof(ThreadingMode)].PreComment =
                "Determines whether the threading mode the game will use when generating chunk data and meshes.";
            _Configuration["Chunking"][nameof(ThreadingMode)].Comment = "(0 = single, 1 = multi, 2 = variable)";
            _Configuration["Chunking"][nameof(ThreadingMode)].IntValue = 2;

            _Configuration["Chunking"][nameof(MaximumChunkCacheSize)].PreComment =
                "Lower values are harder on the CPU, higher values use more RAM.";
            _Configuration["Chunking"][nameof(MaximumChunkCacheSize)].IntValue = 30;

            _Configuration["Chunking"][nameof(ChunkCacheCullingAggression)].PreComment =
                "Active culling keeps the total cache size below maximum, passive lets it grow until there's free frame time to cull it.";
            _Configuration["Chunking"][nameof(ChunkCacheCullingAggression)].Comment =
                "0 = Passive, 1 = Active";
            _Configuration["Chunking"][nameof(ChunkCacheCullingAggression)].IntValue = 1;

            _Configuration["Chunking"][nameof(MaximumChunkLoadTimeBufferSize)].PreComment =
                "Lower values give a more accurate frame-to-frame reading, with higher values giving more long-term accuracy.";
            _Configuration["Chunking"][nameof(MaximumChunkLoadTimeBufferSize)].Comment = "(min 0, max 120)";
            _Configuration["Chunking"][nameof(MaximumChunkLoadTimeBufferSize)].IntValue = 15;

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
    }
}