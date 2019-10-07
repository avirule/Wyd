#region

using UnityEngine;
#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;

#endif

#endregion

namespace Tayx.Graphy.Ram
{
    public class G_RamMonitor : MonoBehaviour
    {
        /* ----- TODO: ----------------------------
         * Add summaries to the variables.
         * Add summaries to the functions.
         * --------------------------------------*/

        #region Variables -> Private

        #endregion

        #region Properties -> Public

        public float AllocatedRam { get; private set; }

        public float ReservedRam { get; private set; }

        public float MonoRam { get; private set; }

        #endregion

        #region Methods -> Unity Callbacks

        private void Update()
        {
#if UNITY_5_6_OR_NEWER
            AllocatedRam = Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
            ReservedRam = Profiler.GetTotalReservedMemoryLong() / 1048576f;
            MonoRam = Profiler.GetMonoUsedSizeLong() / 1048576f;
#else
            m_allocatedRam = Profiler.GetTotalAllocatedMemory()    / 1048576f;
            m_reservedRam = Profiler.GetTotalReservedMemory()     / 1048576f;
            m_monoRam = Profiler.GetMonoUsedSize()            / 1048576f;
#endif
        }

        #endregion
    }
}
