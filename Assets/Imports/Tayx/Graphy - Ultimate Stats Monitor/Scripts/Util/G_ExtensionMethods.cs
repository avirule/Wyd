#region

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Tayx.Graphy.Utils
{
    public static class G_ExtensionMethods
    {
        /* ----- TODO: ----------------------------
         * Add summaries to the functions.
         * --------------------------------------*/

        #region Methods -> Extension Methods

        /// <summary>
        ///     Functions as the SetActive function in the GameObject class, but for a list of them.
        /// </summary>
        /// <param name="gameObjects">
        ///     List of GameObjects.
        /// </param>
        /// <param name="active">
        ///     Wether to turn them on or off.
        /// </param>
        public static List<GameObject> SetAllActive(this List<GameObject> gameObjects, bool active)
        {
            foreach (GameObject gameObj in gameObjects)
            {
                gameObj.SetActive(active);
            }

            return gameObjects;
        }

        public static List<Image> SetOneActive(this List<Image> images, int active)
        {
            for (int i = 0; i < images.Count; i++)
            {
                images[i].gameObject.SetActive(i == active);
            }

            return images;
        }

        public static List<Image> SetAllActive(this List<Image> images, bool active)
        {
            foreach (Image image in images)
            {
                image.gameObject.SetActive(active);
            }

            return images;
        }

        #endregion
    }
}
