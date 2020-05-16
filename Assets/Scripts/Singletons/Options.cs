#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Serilog;
using SharpConfig;
using UnityEngine;

#endregion

namespace Wyd.Singletons
{
    public class Options : Singleton<Options>, INotifyPropertyChanged
    {
        public const string GENERAL_CATEGORY = "General";
        public const string GRAPHICS_CATEGORY = "Graphics";

        private static readonly Dictionary<string, string> _DefaultComments = new Dictionary<string, string>
        {
            {
                nameof(GPUAcceleration), "Whether the GPU will be used for operations other than rendering.\r\n"
                                         + "Remark: disabling this will notably increase CPU stress."
            },
            {
                nameof(AdvancedMeshing), "Whether chunk meshes will use same-face-combination techniques to generate less total terrain data.\r\n"
                                         + "Remark: this can be useful if you have low VRAM on your graphics card, although it may be overall slower than the naive meshing."
            },
            { nameof(NaiveMeshingGroupSize), "Group size to use when doing naive meshing. This value must be a power of two (min 8, max 1024)." },
            { nameof(DiagnosticBufferSize), "Determines maximum length of internal buffers used to allocate diagnostic data (min 30, max 300)." },
            {
                nameof(TargetFrameRate), "Target FPS internal updaters will attempt to maintain (min 15, max 300).\r\n"
                                         + "Remark: this is a soft limitation. Some operations will necessarily exceed it."
            },
            {
                nameof(VSync), "When enabled, update loop will wait on monitor refresh rate.\r\n"
                               + "Remark: this introduces one frame of delay."
            },
            { nameof(FullScreenMode), $"(0 = {(FullScreenMode)0}, 1 = {(FullScreenMode)1}, 2 = {(FullScreenMode)2}, 3 = {(FullScreenMode)3})" },
            { nameof(RenderDistance), "Radius in chunks to built and render around player (min 1, max 16)." },
        };

        public static readonly string DefaultConfigPath = $@"{Application.persistentDataPath}/config.ini";

        #region Options

        /* GENERAL */

        private Option<bool> _GPUAcceleration;

        public bool GPUAcceleration
        {
            get => _GPUAcceleration.Value;
            set
            {
                _GPUAcceleration.Value = value;
                OnPropertyChanged();
            }
        }

        private Option<bool> _AdvancedMeshing;

        public bool AdvancedMeshing
        {
            get => _AdvancedMeshing.Value;
            set
            {
                _AdvancedMeshing.Value = value;
                OnPropertyChanged();
            }
        }

        private Option<int> _NaiveMeshingGroupSize;

        public int NaiveMeshingGroupSize
        {
            get => _NaiveMeshingGroupSize.Value;
            set
            {
                _NaiveMeshingGroupSize.Value = value;
                OnPropertyChanged();
            }
        }

        private Option<int> _DiagnosticBufferSize;

        public int DiagnosticBufferSize
        {
            get => _DiagnosticBufferSize.Value;
            set
            {
                _DiagnosticBufferSize.Value = value;
                OnPropertyChanged();
            }
        }


        /* GRAPHICS */

        private Option<int> _TargetFrameRate;

        public int TargetFrameRate
        {
            get => _TargetFrameRate.Value;
            set
            {
                _TargetFrameRate.Value = value;
                TargetFrameTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerSecond * (1d / _TargetFrameRate.Value)));
                OnPropertyChanged();
            }
        }

        public TimeSpan TargetFrameTime { get; private set; }

        private Option<int> _RenderDistance;

        public int RenderDistance
        {
            get => _RenderDistance.Value;
            set
            {
                _RenderDistance.Value = value;
                OnPropertyChanged();
            }
        }

        private Option<bool> _VSync;

        public bool VSync
        {
            get => _VSync.Value;
            set
            {
                _VSync.Value = value;
                QualitySettings.vSyncCount = Convert.ToInt32(_VSync.Value);
                OnPropertyChanged();
            }
        }

        private Option<FullScreenMode> _WindowMode;

        public FullScreenMode FullScreenMode
        {
            get => _WindowMode.Value;
            set
            {
                _WindowMode.Value = value;
                Screen.fullScreenMode = _WindowMode.Value;
                OnPropertyChanged();
            }
        }

        #endregion


        private Configuration _Configuration;


        public event PropertyChangedEventHandler PropertyChanged;

        public Options()
        {
            AssignSingletonInstance(this);

            LoadConfig();
            PropertyChanged += (sender, args) => { };
        }


        private void LoadConfig()
        {
            _Configuration = File.Exists(DefaultConfigPath)
                ? Configuration.LoadFromFile(DefaultConfigPath)
                : InitialiseDefaultConfig();

            // General

            _GPUAcceleration = new Option<bool>(_Configuration, GENERAL_CATEGORY, nameof(GPUAcceleration), true, gpuAcceleration => true,
                PropertyChanged);

            _AdvancedMeshing = new Option<bool>(_Configuration, GENERAL_CATEGORY, nameof(AdvancedMeshing), true, advancedMeshing => true,
                PropertyChanged);

            _NaiveMeshingGroupSize = new Option<int>(_Configuration, GENERAL_CATEGORY, nameof(NaiveMeshingGroupSize), 64,
                naiveMeshingGroupSize =>
                    (naiveMeshingGroupSize >= 8) && (naiveMeshingGroupSize <= 4096) && WydMath.IsPowerOfTwo(naiveMeshingGroupSize), PropertyChanged);

            _DiagnosticBufferSize = new Option<int>(_Configuration, GENERAL_CATEGORY, nameof(DiagnosticBufferSize), 180,
                diagnosticBufferSize => (diagnosticBufferSize >= 30) && (diagnosticBufferSize <= 300), PropertyChanged);


            // Graphics

            _TargetFrameRate = new Option<int>(_Configuration, GRAPHICS_CATEGORY, nameof(TargetFrameRate), Screen.currentResolution.refreshRate,
                targetFrameRate => (targetFrameRate >= 15) && (targetFrameRate <= 300), PropertyChanged);

            TargetFrameTime = TimeSpan.FromTicks((long)(TimeSpan.TicksPerSecond * (1d / TargetFrameRate)));


            _VSync = new Option<bool>(_Configuration, GRAPHICS_CATEGORY, nameof(VSync), true, vSync => true, PropertyChanged);
            QualitySettings.vSyncCount = Convert.ToInt32(VSync);


            _WindowMode = new Option<FullScreenMode>(_Configuration, GRAPHICS_CATEGORY, nameof(FullScreenMode), FullScreenMode.FullScreenWindow,
                windowMode => true, PropertyChanged);
            Screen.fullScreenMode = FullScreenMode;


            _RenderDistance = new Option<int>(_Configuration, GRAPHICS_CATEGORY, nameof(RenderDistance), 8,
                renderDistance => (renderDistance >= 1) && (renderDistance <= 16), PropertyChanged);

            Log.Information($"({nameof(Options)}) Configuration loaded.");
        }

        private Configuration InitialiseDefaultConfig()
        {
            Log.Information($"({nameof(Options)}) Initializing default configuration file...");

            _Configuration = new Configuration();

            // General

            _Configuration[GENERAL_CATEGORY][nameof(GPUAcceleration)].PreComment = _DefaultComments[nameof(GPUAcceleration)];
            _Configuration[GENERAL_CATEGORY][nameof(GPUAcceleration)].BoolValue = true;

            _Configuration[GENERAL_CATEGORY][nameof(AdvancedMeshing)].PreComment = _DefaultComments[nameof(AdvancedMeshing)];
            _Configuration[GENERAL_CATEGORY][nameof(AdvancedMeshing)].BoolValue = true;

            _Configuration[GENERAL_CATEGORY][nameof(NaiveMeshingGroupSize)].PreComment = _DefaultComments[nameof(NaiveMeshingGroupSize)];
            _Configuration[GENERAL_CATEGORY][nameof(NaiveMeshingGroupSize)].IntValue = 64;

            _Configuration[GENERAL_CATEGORY][nameof(DiagnosticBufferSize)].PreComment = _DefaultComments[nameof(DiagnosticBufferSize)];
            _Configuration[GENERAL_CATEGORY][nameof(DiagnosticBufferSize)].IntValue = 180;


            // Graphics

            _Configuration[GRAPHICS_CATEGORY][nameof(TargetFrameRate)].PreComment = _DefaultComments[nameof(TargetFrameRate)];
            _Configuration[GRAPHICS_CATEGORY][nameof(TargetFrameRate)].IntValue = 60;

            _Configuration[GRAPHICS_CATEGORY][nameof(VSync)].PreComment = _DefaultComments[nameof(VSync)];
            _Configuration[GRAPHICS_CATEGORY][nameof(VSync)].BoolValue = true;

            _Configuration[GRAPHICS_CATEGORY][nameof(FullScreenMode)].PreComment = _DefaultComments[nameof(FullScreenMode)];
            _Configuration[GRAPHICS_CATEGORY][nameof(FullScreenMode)].IntValue = (int)FullScreenMode.FullScreenWindow;

            _Configuration[GRAPHICS_CATEGORY][nameof(RenderDistance)].PreComment = _DefaultComments[nameof(RenderDistance)];
            _Configuration[GRAPHICS_CATEGORY][nameof(RenderDistance)].IntValue = 8;


            Log.Information($"({nameof(Options)}) Default configuration initialized. Saving...");

            _Configuration.SaveToFile(DefaultConfigPath);

            Log.Information($"({nameof(Options)}) Configuration file saved at: {DefaultConfigPath}");

            return _Configuration;
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
