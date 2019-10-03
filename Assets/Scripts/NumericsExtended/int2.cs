#region

using System.Numerics;
using System.Runtime.CompilerServices;

#endregion

namespace NumericsExtended
{
    public struct int2
    {
        #region DEFAULT STATIC MEMBERS

        public static int2 Zero => (int2) int4.Zero;
        public static int2 One => (int2) int4.One;
        public static int2 Forward => (int2) int4.Forward;
        public static int2 Back => (int2) int4.Back;
        public static int2 Right => (int2) int4.Right;
        public static int2 Left => (int2) int4.Left;
        public static int2 Up => (int2) int4.Up;
        public static int2 Down => (int2) int4.Down;

        #endregion

        private readonly Vector<int> _Values;

        public int X => _Values[0];
        public int Y => _Values[1];

        public int2(Vector<int> values) => _Values = values;

        public int2(int a, int b)
        {
            _Values = new Vector<int>(new[]
            {
                a,
                b,
                0,
                0,
                0,
                0,
                0,
                0
            });
        }


        public static implicit operator Vector<int>(int2 a) => a._Values;

        public static implicit operator int2(Vector<int> a) => new int2(a);

        public static explicit operator int(int2 a) => a._Values[0];

        public static explicit operator int3(int2 a) => a._Values;

        public static explicit operator int4(int2 a) => a._Values;


        #region OPERATOR OVERLOADS - int2

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 operator +(int2 a, int2 b) => a._Values + b._Values;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 operator -(int2 a, int2 b) => a._Values - b._Values;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 operator *(int2 a, int2 b) => a._Values * b._Values;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 operator /(int2 a, int2 b) => a._Values / b._Values;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 operator ^(int2 a, int2 b) => a._Values ^ b._Values;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 operator ++(int2 a)
        {
            a += One;
            return a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 operator --(int2 a)
        {
            a -= One;
            return a;
        }

        #endregion
    }
}
