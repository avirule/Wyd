namespace Tayx.Graphy.UI
{
    public interface IModifiableState
    {
        /* ----- TODO: ----------------------------
         * --------------------------------------*/

        /// <summary>
        ///     Set the module state.
        /// </summary>
        /// <param name="newState">
        ///     The new state.
        /// </param>
        void SetState(GraphyManager.ModuleState newState, bool silentUpdate);
    }
}
