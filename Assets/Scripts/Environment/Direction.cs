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
        North = 0b00000001,

        /// <summary>
        ///     Positive on X axis
        /// </summary>
        East = 0b00000010,

        /// <summary>
        ///     Negative on Z axis
        /// </summary>
        South = 0b00000100,

        /// <summary>
        ///     Negative on X axis
        /// </summary>
        West = 0b00001000,

        /// <summary>
        ///     Positive on Y axis
        /// </summary>
        Up = 0b00010000,

        /// <summary>
        ///     Negative on Y axis
        /// </summary>
        Down = 0b00100000
    }
}