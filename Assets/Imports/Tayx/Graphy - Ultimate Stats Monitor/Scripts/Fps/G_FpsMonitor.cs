#region

using System.Runtime.CompilerServices;
using UnityEngine;

#endregion

namespace Tayx.Graphy.Fps
{
    public class G_FpsMonitor : MonoBehaviour
    {
        /* ----- TODO: ----------------------------
         * Add summaries to the variables.
         * Add summaries to the functions.
         * --------------------------------------*/

        #region Variables -> Serialized Private

        [SerializeField]
        private int m_averageSamples = 200;

        #endregion

        #region Variables -> Private

        private GraphyManager m_graphyManager;

        private float[] m_averageFpsSamples;
        private int m_avgFpsSamplesOffset;
        private int m_indexMask;
        private int m_avgFpsSamplesCapacity;
        private int m_avgFpsSamplesCount;
        private int m_timeToResetMinMaxFps = 10;

        private float m_timeToResetMinFpsPassed;
        private float m_timeToResetMaxFpsPassed;

        private float unscaledDeltaTime;

        #endregion

        #region Properties -> Public

        public float CurrentFPS { get; private set; }

        public float AverageFPS { get; private set; }

        public float MinFPS { get; private set; }

        public float MaxFPS { get; private set; }

        #endregion

        #region Methods -> Unity Callbacks

        private void Awake()
        {
            Init();
        }

        private void Update()
        {
            unscaledDeltaTime = Time.unscaledDeltaTime;

            m_timeToResetMinFpsPassed += unscaledDeltaTime;
            m_timeToResetMaxFpsPassed += unscaledDeltaTime;

            // Update fps and ms

            CurrentFPS = 1 / unscaledDeltaTime;

            // Update avg fps

            AverageFPS = 0;

            m_averageFpsSamples[ToBufferIndex(m_avgFpsSamplesCount)] = CurrentFPS;
            m_avgFpsSamplesOffset = ToBufferIndex(m_avgFpsSamplesOffset + 1);

            if (m_avgFpsSamplesCount < m_avgFpsSamplesCapacity)
            {
                m_avgFpsSamplesCount++;
            }

            for (int i = 0; i < m_avgFpsSamplesCount; i++)
            {
                AverageFPS += m_averageFpsSamples[i];
            }

            AverageFPS /= m_avgFpsSamplesCount;

            // Checks to reset min and max fps

            if ((m_timeToResetMinMaxFps > 0)
                && (m_timeToResetMinFpsPassed > m_timeToResetMinMaxFps))
            {
                MinFPS = 0;
                m_timeToResetMinFpsPassed = 0;
            }

            if ((m_timeToResetMinMaxFps > 0)
                && (m_timeToResetMaxFpsPassed > m_timeToResetMinMaxFps))
            {
                MaxFPS = 0;
                m_timeToResetMaxFpsPassed = 0;
            }

            // Update min fps

            if ((CurrentFPS < MinFPS) || (MinFPS <= 0))
            {
                MinFPS = CurrentFPS;

                m_timeToResetMinFpsPassed = 0;
            }

            // Update max fps

            if ((CurrentFPS > MaxFPS) || (MaxFPS <= 0))
            {
                MaxFPS = CurrentFPS;

                m_timeToResetMaxFpsPassed = 0;
            }
        }

        #endregion

        #region Methods -> Public

        public void UpdateParameters()
        {
            m_timeToResetMinMaxFps = m_graphyManager.TimeToResetMinMaxFps;
        }

        #endregion

        #region Methods -> Private

        private void Init()
        {
            m_graphyManager = transform.root.GetComponentInChildren<GraphyManager>();

            ResizeSamplesBuffer(m_averageSamples);

            UpdateParameters();
        }


        private void ResizeSamplesBuffer(int size)
        {
            m_avgFpsSamplesCapacity = Mathf.NextPowerOfTwo(size);

            m_averageFpsSamples = new float[m_avgFpsSamplesCapacity];

            m_indexMask = m_avgFpsSamplesCapacity - 1;
            m_avgFpsSamplesOffset = 0;
        }

#if NET_4_6 || NET_STANDARD_2_0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private int ToBufferIndex(int index)
        {
            return (index + m_avgFpsSamplesOffset) & m_indexMask;
        }

        #endregion
    }
}
