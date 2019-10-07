#region

using UnityEngine;

#endregion

namespace Tayx.Graphy.Graph
{
    public abstract class G_Graph : MonoBehaviour
    {
        /* ----- TODO: ----------------------------
         * 
         * --------------------------------------*/

        #region Methods -> Protected

        /// <summary>
        ///     Updates the graph/s.
        /// </summary>
        protected abstract void UpdateGraph();

        /// <summary>
        ///     Creates the points for the graph/s.
        /// </summary>
        protected abstract void CreatePoints();

        #endregion
    }
}
