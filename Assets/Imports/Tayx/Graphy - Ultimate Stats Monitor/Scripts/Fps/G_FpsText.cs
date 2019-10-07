#region

using Tayx.Graphy.Utils.NumString;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Tayx.Graphy.Fps
{
    public class G_FpsText : MonoBehaviour
    {
        /* ----- TODO: ----------------------------
         * Add summaries to the variables.
         * Add summaries to the functions.
         * Check if we should add a "RequireComponent" for "FpsMonitor".
         * Improve the IntString Init to come from the core instead.
         * --------------------------------------*/

        #region Variables -> Serialized Private

        [SerializeField]
        private Text m_fpsText;

        [SerializeField]
        private Text m_msText;

        [SerializeField]
        private Text m_avgFpsText;

        [SerializeField]
        private Text m_minFpsText;

        [SerializeField]
        private Text m_maxFpsText;

        #endregion

        #region Variables -> Private

        private GraphyManager m_graphyManager;

        private G_FpsMonitor m_fpsMonitor;

        private int m_updateRate = 4; // 4 updates per sec.

        private int m_frameCount;

        private float m_deltaTime;

        private float m_fps;

        private const int m_minFps = 0;
        private const int m_maxFps = 10000;

        private const string m_msStringFormat = "0.0";

        #endregion

        #region Methods -> Unity Callbacks

        private void Awake()
        {
            Init();
        }

        private void Update()
        {
            m_deltaTime += Time.unscaledDeltaTime;

            m_frameCount++;

            // Only update texts 'm_updateRate' times per second

            if (m_deltaTime > (1f / m_updateRate))
            {
                m_fps = m_frameCount / m_deltaTime;

                // Update fps and ms

                m_fpsText.text = Mathf.RoundToInt(m_fps).ToStringNonAlloc();
                m_msText.text = ((m_deltaTime / m_frameCount) * 1000f).ToStringNonAlloc(m_msStringFormat);

                // Update min fps

                m_minFpsText.text = m_fpsMonitor.MinFPS.ToInt().ToStringNonAlloc();

                SetFpsRelatedTextColor(m_minFpsText, m_fpsMonitor.MinFPS);

                // Update max fps

                m_maxFpsText.text = m_fpsMonitor.MaxFPS.ToInt().ToStringNonAlloc();

                SetFpsRelatedTextColor(m_maxFpsText, m_fpsMonitor.MaxFPS);

                // Update avg fps

                m_avgFpsText.text = m_fpsMonitor.AverageFPS.ToInt().ToStringNonAlloc();

                SetFpsRelatedTextColor(m_avgFpsText, m_fpsMonitor.AverageFPS);

                // Reset variables

                m_deltaTime = 0f;
                m_frameCount = 0;
            }
        }

        #endregion

        #region Methods -> Public

        public void UpdateParameters()
        {
            m_updateRate = m_graphyManager.FpsTextUpdateRate;
        }

        #endregion

        #region Methods -> Private

        /// <summary>
        ///     Assigns color to a text according to their fps numeric value and
        ///     the colors specified in the 3 categories (Good, Caution, Critical).
        /// </summary>
        /// <param name="text">
        ///     UI Text component to change its color
        /// </param>
        /// <param name="fps">
        ///     Numeric fps value
        /// </param>
        private void SetFpsRelatedTextColor(Text text, float fps)
        {
            if (fps > m_graphyManager.GoodFPSThreshold)
            {
                text.color = m_graphyManager.GoodFPSColor;
            }
            else if (fps > m_graphyManager.CautionFPSThreshold)
            {
                text.color = m_graphyManager.CautionFPSColor;
            }
            else
            {
                text.color = m_graphyManager.CriticalFPSColor;
            }
        }

        private void Init()
        {
            //TODO: Replace this with one activated from the core and figure out the min value.
            if (!G_IntString.Inited || (G_IntString.MinValue > m_minFps) || (G_IntString.MaxValue < m_maxFps))
            {
                G_IntString.Init
                (
                    m_minFps,
                    m_maxFps
                );
            }

            m_graphyManager = transform.root.GetComponentInChildren<GraphyManager>();

            m_fpsMonitor = GetComponent<G_FpsMonitor>();

            UpdateParameters();
        }

        #endregion
    }
}
