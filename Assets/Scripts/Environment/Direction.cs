namespace Environment
{
    /// <summary>
    ///     6-way cardinal directions in byte values.
    /// </summary>
    public enum Direction : byte
    {
        /// <summary>
        ///     Positive on Z axis
        /// </summary>
        North = 0,

        /// <summary>
        ///     Positive on X axis
        /// </summary>
        East = 2,

        /// <summary>
        ///     Negative on Z axis
        /// </summary>
        South = 4,

        /// <summary>
        ///     Negative on X axis
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