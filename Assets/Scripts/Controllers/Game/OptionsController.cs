#region

using System.IO;
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
        private static string configPath;

        public static OptionsController Current;

        public int MaximumChunkCacheSize;
        public CacheCullingAggression ChunkCacheCullingAggression;
        public int MaximumChunkLoadTimeBufferSize;
        public int MaximumFrameRateBufferSize;
        public float MaximumInternalFrames;
        public int ShadowRadius;
        public int ExpensiveMeshingRadius;

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

            configPath = $@"{Application.persistentDataPath}\config.ini";
        }

        private void Start()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            Configuration configuration = !File.Exists(configPath)
                ? InitialiseDefaultConfig()
                : Configuration.LoadFromFile(configPath);

            // Graphics
            MaximumFrameRateBufferSize = configuration["Graphics"]["MaximumFrameRateBufferSize"].IntValue;
            MaximumInternalFrames = configuration["Graphics"]["MaximumInternalFrames"].IntValue;
            ShadowRadius = configuration["Graphics"]["ShadowRadius"].IntValue;
            ExpensiveMeshingRadius = configuration["Graphics"]["ExpensiveMeshingRadius"].IntValue;

            // chunking
            MaximumChunkCacheSize = configuration["Chunking"]["MaximumChunkCacheSize"].IntValue;
            ChunkCacheCullingAggression =
                (CacheCullingAggression)  configuration["Chunking"]["ChunkCacheCullingAggression"].IntValue;
            MaximumChunkLoadTimeBufferSize = configuration["Chunking"]["MaximumChunkLoadTimeBufferSize"].IntValue;

            EventLog.Logger.Log(LogLevel.Info, "Configuration file successfully loaded.");
        }

        private static Configuration InitialiseDefaultConfig()
        {
            EventLog.Logger.Log(LogLevel.Info, "Initializing default configuration file...");

            Configuration configuration = new Configuration();

            // Graphics
            configuration["Graphics"]["MaximumInternalFrames"].PreComment =
                "Maximum number of frames internal systems will allow to lapse during updates.";
            configuration["Graphics"]["MaximumInternalFrames"].Comment = "Higher values decrease overall CPU stress.";
            configuration["Graphics"]["MaximumInternalFrames"].IntValue = 30;

            configuration["Graphics"]["MaximumFrameRateBufferSize"].PreComment =
                "Maximum size of buffer for reporting average frame rate.";
            configuration["Graphics"]["MaximumFrameRateBufferSize"].Comment =
                "Higher values decrease frame-to-frame accuracy.";
            configuration["Graphics"]["MaximumFrameRateBufferSize"].IntValue = 60;

            configuration["Graphics"]["ShadowRadius"].PreComment =
                "Defines radius in chunks around player to draw shadows.";
            configuration["Graphics"]["ShadowRadius"].IntValue = 5;

            configuration["Graphics"]["ExpensiveMeshingRadius"].PreComment =
                "Defines radius in chunks around player to generate collision meshes.";
            configuration["Graphics"]["ExpensiveMeshingRadius"].Comment = "High values will cause lag.";
            configuration["Graphics"]["ExpensiveMeshingRadius"].IntValue = 1;

            // Chunking
            configuration["Chunking"]["MaximumChunkCacheSize"].PreComment =
                "Lower values are harder on the CPU, higher values use more RAM.";
            configuration["Chunking"]["MaximumChunkCacheSize"].IntValue = 30;

            configuration["Chunking"]["ChunkCacheCullingAggression"].PreComment =
                "Active culling keeps the total cache size below maximum, passive lets it grow until there's time to cull it.";
            configuration["Chunking"]["ChunkCacheCullingAggression"].Comment =
                "0 = Passive, 1 = Active";
            configuration["Chunking"]["ChunkCacheCullingAggression"].IntValue = 1;

            configuration["Chunking"]["MaximumChunkLoadTimeBufferSize"].PreComment =
                "Lower values give a more accurate frame-to-frame reading, with higher values giving more long-term accuracy.";
            configuration["Chunking"]["MaximumChunkLoadTimeBufferSize"].IntValue = 15;

            EventLog.Logger.Log(LogLevel.Info, "Default configuration initialized. Saving...");

            configuration.SaveToFile(configPath);

            EventLog.Logger.Log(LogLevel.Info, $"Configuration file saved at: {configPath}");

            return configuration;
        }
    }
}