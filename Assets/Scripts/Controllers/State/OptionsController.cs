#region

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;
using NLog;
using SharpConfig;
using UnityEngine;
using Wyd.Graphics;
using Wyd.System.Jobs;
using Wyd.System.Logging;

#endregion

namespace Wyd.Controllers.State
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

    public class OptionsController : SingletonController<OptionsController>, INotifyPropertyChanged
    {
        public static class Defaults
        {
            // General
            public const ThreadingMode THREADING_MODE = ThreadingMode.Multi;
            public const int CPU_CORE_UTILIZATION = 4;
            public const bool GPU_ACCELERATION = true;

            // Graphics
            public const int MINIMUM_INTERNAL_FRAMES = 60;
            public const int MAXIMUM_FRAME_RATE_BUFFER_SIZE = 60;
            public const int VSYNC_LEVEL = 1;
            public const int WINDOW_MODE = (int) WindowMode.Fullscreen;
            public const int RENDER_DISTANCE = 4;
            public const int SHADOW_DISTANCE = 4;

            // Chunking
            public const bool PRE_INITIALIZE_CHUNK_CACHE = false;
            public const int MAXIMUM_CHUNK_CACHE_SIZE = -1;
            public const int MAXIMUM_CHUNK_LOAD_TIME_BUFFER_SIZE = 120;
            public const int PRE_LOAD_CHUNK_DISTANCE = 0;
        }

        public const int MAXIMUM_RENDER_DISTANCE = 32;

        public static string ConfigPath { get; private set; }

        public static readonly WindowMode MaximumWindowModeValue =
            Enum.GetValues(typeof(WindowMode)).Cast<WindowMode>().Last();

        #region PRIVATE FIELDS

        private Configuration _Configuration;
        private ThreadingMode _ThreadingMode;
        private int _CPUCoreUtilization;
        private bool _GPUAcceleration;
        private int _MinimumInternalFrames;
        private int _MaximumFrameRateBufferSize;
        private bool _PreInitializeChunkCache;
        private int _MaximumChunkCacheSize;
        private int _MaximumChunkLoadTimeBufferSize;
        private int _PreLoadChunkDistance;
        private int _ShadowDistance;
        private int _RenderDistance;
        private WindowMode _WindowMode;

        #endregion


        #region GENERAL OPTIONS MEMBERS

        public ThreadingMode ThreadingMode
        {
            get => _ThreadingMode;
            set
            {
                _ThreadingMode = value;
                _Configuration["General"][nameof(ThreadingMode)].IntValue = (int) _ThreadingMode;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public int CPUCoreUtilization
        {
            get => _CPUCoreUtilization;
            set
            {
                _CPUCoreUtilization = Math.Max(value, 1) % (Environment.ProcessorCount + 1);
                _Configuration["General"][nameof(CPUCoreUtilization)].IntValue = _CPUCoreUtilization;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public bool GPUAcceleration
        {
            get => _GPUAcceleration;
            set
            {
                _GPUAcceleration = value;
                _Configuration["General"][nameof(GPUAcceleration)].BoolValue = _GPUAcceleration;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        #endregion


        #region GRAPHICS OPTIONS MEMBERS

        public int MinimumInternalFrames
        {
            get => _MinimumInternalFrames;
            set
            {
                _MinimumInternalFrames = value;
                _Configuration["General"][nameof(MinimumInternalFrames)].IntValue = _MinimumInternalFrames;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public TimeSpan MaximumInternalFrameTime { get; private set; }

        public int MaximumFrameRateBufferSize
        {
            get => _MaximumFrameRateBufferSize;
            set
            {
                _MaximumFrameRateBufferSize = value;
                _Configuration["Graphics"][nameof(MaximumFrameRateBufferSize)].IntValue = _MaximumFrameRateBufferSize;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public int VSyncLevel
        {
            get => QualitySettings.vSyncCount;
            set
            {
                QualitySettings.vSyncCount = value;
                _Configuration["Graphics"][nameof(VSyncLevel)].IntValue = QualitySettings.vSyncCount;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public WindowMode WindowMode
        {
            get => _WindowMode;
            set
            {
                if (_WindowMode == value)
                {
                    return;
                }

                _WindowMode = value;

                switch (_WindowMode)
                {
                    case WindowMode.Fullscreen:
                        Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                        break;
                    case WindowMode.BorderlessWindowed:
                        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                        break;
                    case WindowMode.Windowed:
                        Screen.fullScreenMode = FullScreenMode.Windowed;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }

                _Configuration["Graphics"][nameof(WindowMode)].IntValue = (int) _WindowMode;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public int RenderDistance
        {
            get => _RenderDistance;
            set
            {
                _RenderDistance = value % (MAXIMUM_RENDER_DISTANCE + 1);
                _Configuration["Graphics"][nameof(RenderDistance)].IntValue = _RenderDistance;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public int ShadowDistance
        {
            get => _ShadowDistance;
            set
            {
                _ShadowDistance = value % (MAXIMUM_RENDER_DISTANCE + 1);
                _Configuration["Graphics"][nameof(ShadowDistance)].IntValue = _ShadowDistance;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        #endregion


        #region CHUNKING OPTIONS MEMBERS

        public bool PreInitializeChunkCache
        {
            get => _PreInitializeChunkCache;
            set
            {
                _PreInitializeChunkCache = value;
                _Configuration["Chunking"][nameof(PreInitializeChunkCache)].BoolValue = _PreInitializeChunkCache;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public int MaximumChunkCacheSize
        {
            get => _MaximumChunkCacheSize;
            set
            {
                _MaximumChunkCacheSize = value;
                _Configuration["Chunking"][nameof(MaximumChunkCacheSize)].IntValue = _MaximumChunkCacheSize;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public int MaximumChunkLoadTimeBufferSize
        {
            get => _MaximumChunkLoadTimeBufferSize;
            set
            {
                _MaximumChunkLoadTimeBufferSize = value;
                _Configuration["Chunking"][nameof(MaximumChunkLoadTimeBufferSize)].IntValue =
                    _MaximumChunkLoadTimeBufferSize;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public int PreLoadChunkDistance
        {
            get => _PreLoadChunkDistance;
            set
            {
                _PreLoadChunkDistance = value;
                _Configuration["Chunking"][nameof(PreLoadChunkDistance)].IntValue = _PreLoadChunkDistance;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        private void Awake()
        {
            AssignCurrent(this);

            ConfigPath = $@"{Application.persistentDataPath}/config.ini";
        }

        private void Start()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            _Configuration = !File.Exists(ConfigPath)
                ? InitialiseDefaultConfig()
                : Configuration.LoadFromFile(ConfigPath);

            // General

            if (!GetSetting("General", nameof(ThreadingMode), out _ThreadingMode))
            {
                LogSettingLoadError(nameof(ThreadingMode), Defaults.THREADING_MODE);
                ThreadingMode = Defaults.THREADING_MODE;
                SaveSettings();
            }

            if (!GetSetting("General", nameof(CPUCoreUtilization), out _CPUCoreUtilization)
                || (_CPUCoreUtilization < 0))
            {
                LogSettingLoadError(nameof(CPUCoreUtilization), Defaults.CPU_CORE_UTILIZATION);
                _CPUCoreUtilization = Defaults.CPU_CORE_UTILIZATION;
                SaveSettings();
            }

            if (!GetSetting("General", nameof(GPUAcceleration), out _GPUAcceleration))
            {
                LogSettingLoadError(nameof(GPUAcceleration), Defaults.GPU_ACCELERATION);
                GPUAcceleration = Defaults.GPU_ACCELERATION;
                SaveSettings();
            }


            // Graphics

            if (!GetSetting("Graphics", nameof(MinimumInternalFrames), out _MinimumInternalFrames)
                || (MinimumInternalFrames < 0)
                || (MinimumInternalFrames > 120))
            {
                LogSettingLoadError(nameof(MinimumInternalFrames), Defaults.MINIMUM_INTERNAL_FRAMES);
                MinimumInternalFrames = Defaults.MINIMUM_INTERNAL_FRAMES;
                SaveSettings();
            }

            MaximumInternalFrameTime = TimeSpan.FromSeconds(1d / (MinimumInternalFrames * 2));

            if (!GetSetting("Graphics", nameof(MaximumFrameRateBufferSize), out _MaximumFrameRateBufferSize)
                || (MaximumFrameRateBufferSize < 0)
                || (MaximumFrameRateBufferSize > 120))
            {
                LogSettingLoadError(nameof(MaximumFrameRateBufferSize), Defaults.MAXIMUM_FRAME_RATE_BUFFER_SIZE);
                MaximumFrameRateBufferSize = Defaults.MAXIMUM_FRAME_RATE_BUFFER_SIZE;
                SaveSettings();
            }

            if (!GetSetting("Graphics", nameof(VSyncLevel), out int vSyncLevel) || (vSyncLevel < 0) || (vSyncLevel > 4))
            {
                LogSettingLoadError(nameof(vSyncLevel), Defaults.VSYNC_LEVEL);
                VSyncLevel = Defaults.VSYNC_LEVEL;
                SaveSettings();
            }
            else
            {
                VSyncLevel = vSyncLevel;
            }

            if (!GetSetting("Graphics", nameof(WindowMode), out int windowMode)
                || (windowMode < 0)
                || (windowMode > (int) MaximumWindowModeValue))
            {
                LogSettingLoadError(nameof(WindowMode), Defaults.WINDOW_MODE);
                WindowMode = Defaults.WINDOW_MODE;
                SaveSettings();
            }
            else
            {
                WindowMode = (WindowMode) windowMode;
            }

            if (!GetSetting("Graphics", nameof(RenderDistance), out _RenderDistance)
                || (RenderDistance < 0)
                || (RenderDistance > 48))
            {
                LogSettingLoadError(nameof(RenderDistance), Defaults.RENDER_DISTANCE);
                RenderDistance = Defaults.RENDER_DISTANCE;
                SaveSettings();
            }

            if (!GetSetting("Graphics", nameof(ShadowDistance), out _ShadowDistance)
                || (ShadowDistance < 0)
                || (ShadowDistance > 48))
            {
                LogSettingLoadError(nameof(ShadowDistance), Defaults.SHADOW_DISTANCE);
                ShadowDistance = Defaults.SHADOW_DISTANCE;
                SaveSettings();
            }


            // Chunking

            if (!GetSetting("Chunking", nameof(PreInitializeChunkCache), out _PreInitializeChunkCache))
            {
                LogSettingLoadError(nameof(PreInitializeChunkCache), Defaults.PRE_INITIALIZE_CHUNK_CACHE);
                PreInitializeChunkCache = Defaults.PRE_INITIALIZE_CHUNK_CACHE;
                SaveSettings();
            }

            if (!GetSetting("Chunking", nameof(MaximumChunkCacheSize), out _MaximumChunkCacheSize)
                || (MaximumChunkCacheSize < -1)
                || (MaximumChunkCacheSize > 625))
            {
                LogSettingLoadError(nameof(MaximumChunkCacheSize), Defaults.MAXIMUM_CHUNK_CACHE_SIZE);
                MaximumChunkCacheSize = Defaults.MAXIMUM_CHUNK_CACHE_SIZE;
                SaveSettings();
            }

            if (!GetSetting("Chunking", nameof(MaximumChunkLoadTimeBufferSize), out _MaximumChunkLoadTimeBufferSize)
                || (MaximumFrameRateBufferSize < 0)
                || (MaximumChunkLoadTimeBufferSize > 120))
            {
                LogSettingLoadError(nameof(MaximumChunkLoadTimeBufferSize),
                    Defaults.MAXIMUM_CHUNK_LOAD_TIME_BUFFER_SIZE);
                MaximumChunkLoadTimeBufferSize = Defaults.MAXIMUM_CHUNK_LOAD_TIME_BUFFER_SIZE;
                SaveSettings();
            }

            if (!GetSetting("Chunking", nameof(PreLoadChunkDistance), out _PreLoadChunkDistance)
                || (PreLoadChunkDistance < 0))
            {
                LogSettingLoadError(nameof(PreLoadChunkDistance), Defaults.PRE_LOAD_CHUNK_DISTANCE);
                PreLoadChunkDistance = Defaults.PRE_LOAD_CHUNK_DISTANCE;
                SaveSettings();
            }

            EventLogger.Log(LogLevel.Info, "Configuration loaded.");
        }

        private Configuration InitialiseDefaultConfig()
        {
            EventLogger.Log(LogLevel.Info, "Initializing default configuration file...");

            _Configuration = new Configuration();

            // General
            _Configuration["General"][nameof(ThreadingMode)].PreComment =
                "Determines whether the threading mode the game will use when\r\n"
                + "generating chunk data and meshes.";
            _Configuration["General"][nameof(ThreadingMode)].Comment = "(0 = single, 1 = multi, 2 = variable)";
            _Configuration["General"][nameof(ThreadingMode)].IntValue = (int) Defaults.THREADING_MODE;

            _Configuration["General"][nameof(CPUCoreUtilization)].PreComment =
                "Loosely defines the total number of CPU cores the game will utilize with threading.";
            _Configuration["General"][nameof(CPUCoreUtilization)].IntValue = Defaults.CPU_CORE_UTILIZATION;

            _Configuration["General"][nameof(GPUAcceleration)].PreComment =
                "Determines whether the GPU will be more heavily utilized to increase overall performance.\r\n"
                + "Turning this off will create more work for the CPU.";
            _Configuration["General"][nameof(GPUAcceleration)].BoolValue = Defaults.GPU_ACCELERATION;


            // Graphics

            _Configuration["Graphics"][nameof(MinimumInternalFrames)].PreComment =
                "Minimum number of frames internal systems will target to lapse during updates.";
            _Configuration["Graphics"][nameof(MinimumInternalFrames)].Comment =
                "Higher values decrease overall CPU stress (min 15, max 120).";
            _Configuration["Graphics"][nameof(MinimumInternalFrames)].IntValue =
                Defaults.MINIMUM_INTERNAL_FRAMES;

            _Configuration["Graphics"][nameof(MaximumFrameRateBufferSize)].PreComment =
                "Maximum size of buffer for reporting average frame rate.";
            _Configuration["Graphics"][nameof(MaximumFrameRateBufferSize)].Comment =
                "Higher values decrease frame-to-frame accuracy. (min 0, max 360)";
            _Configuration["Graphics"][nameof(MaximumFrameRateBufferSize)].IntValue =
                Defaults.MAXIMUM_FRAME_RATE_BUFFER_SIZE;

            _Configuration["Graphics"][nameof(VSyncLevel)].PreComment =
                "Each level increases the number of screen updates to wait before rendering to the screen.";
            _Configuration["Graphics"][nameof(VSyncLevel)].Comment = "Maximum value of 4";
            _Configuration["Graphics"][nameof(VSyncLevel)].IntValue = Defaults.VSYNC_LEVEL;

            _Configuration["Graphics"][nameof(WindowMode)].Comment =
                "(0 = Fullscreen, 1 = BorderlessWindowed, 2 = Windowed)";
            _Configuration["Graphics"][nameof(WindowMode)].IntValue = Defaults.WINDOW_MODE;

            _Configuration["Graphics"][nameof(ShadowDistance)].PreComment =
                "Defines radius in chunks around player to draw shadows.";
            _Configuration["Graphics"][nameof(ShadowDistance)].IntValue = Defaults.SHADOW_DISTANCE;

            _Configuration["Graphics"][nameof(RenderDistance)].PreComment =
                "Defines radius in regions around player to draw shadows.";
            _Configuration["Graphics"][nameof(RenderDistance)].Comment = "(min 1, max 48)";
            _Configuration["Graphics"][nameof(RenderDistance)].IntValue = Defaults.RENDER_DISTANCE;


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
                "Lower values give a more accurate frame-to-frame reading, with higher\r\n"
                + "values giving more long-term accuracy.";
            _Configuration["Chunking"][nameof(MaximumChunkLoadTimeBufferSize)].Comment = "(min 0, max 120)";
            _Configuration["Chunking"][nameof(MaximumChunkLoadTimeBufferSize)].IntValue =
                Defaults.MAXIMUM_CHUNK_LOAD_TIME_BUFFER_SIZE;

            _Configuration["Chunking"][nameof(PreLoadChunkDistance)].PreComment =
                "Defines radius of chunks to pre-load.\r\n"
                + "This distance begins at the edge of the render distance.";
            _Configuration["Chunking"][nameof(PreLoadChunkDistance)].IntValue =
                Defaults.PRE_LOAD_CHUNK_DISTANCE;

            EventLogger.Log(LogLevel.Info, "Default configuration initialized. Saving...");

            _Configuration.SaveToFile(ConfigPath);

            EventLogger.Log(LogLevel.Info, $"Configuration file saved at: {ConfigPath}");

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

        private static void LogSettingLoadError(string settingName, object defaultValue)
        {
            EventLogger.Log(LogLevel.Warn, $"Error loading setting `{settingName}`, defaulting to {defaultValue}.");
        }


        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region PUBLIC METHODS

        public void SaveSettings()
        {
            _Configuration.SaveToFile(ConfigPath, Encoding.ASCII);
        }

        #endregion
    }
}
