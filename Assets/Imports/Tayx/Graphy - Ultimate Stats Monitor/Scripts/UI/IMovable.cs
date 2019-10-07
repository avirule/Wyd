namespace Tayx.Graphy.UI
{
    public interface IMovable
    {
        /* ----- TODO: ----------------------------
         * 
         * --------------------------------------*/

        /// <summary>
        ///     Sets the position of the module.
        /// </summary>
        /// <param name="newModulePosition">
        ///     The new position of the module.
        /// </param>
        void SetPosition(GraphyManager.ModulePosition newModulePosition);
    }
}
