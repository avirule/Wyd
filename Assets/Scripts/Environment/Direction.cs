namespace Environment
{
    public enum Direction : byte
    {
        None = 0,

        /// <summary>
        ///     Positive on X axis
        /// </summary>
        North = 1,

        /// <summary>
        ///     Positive on Z axis
        /// </summary>
        East = 2,

        /// <summary>
        ///     Negative on X axis
        /// </summary>
        South = 4,

        /// <summary>
        ///     Negative on Z axis
        /// </summary>
        West = 8,

        /// <summary>
        ///     Positive on Y axis
        /// </summary>
        Up = 16,

        /// <summary>
        ///     Negative on Y axis
        /// </summary>
        Down = 32
    }
}