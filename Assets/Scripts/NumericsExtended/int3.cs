#region

using System.Numerics;
using System.Runtime.CompilerServices;

#endregion

namespace Wyd.NumericsExtended
{
    public struct int3
    {
        #region DEFAULT STATIC MEMBERS

        public static int3 Zero => (int3) int4.Zero;
        public static int3 One => (int3) int4.One;
        public static int3 Forward => (int3) int4.Forward;
        public static int3 Back => (int3) int4.Back;
        public static int3 Right => (int3) int4.Right;
        public static int3 Left => (int3) int4.Left;
        public static int3 Up => (int3) int4.Up;
        public static int3 Down => (int3) int4.Down;

        #endregion

        private readonly Vector<int> _Values;

        public int X => _Values[0];
        public int Y => _Values[1];
        public int Z => _Values[2];

        public int3(Vector<int> values) => _Values = values;

        public int3(int a, int b, int c)
        {
            _Values = new Vector<int>(new[]
            {
                a,
                b,
                c,
                0,
                0,
                0,
                0,
                0
            });
        }


        public static implicit operator Vector<int>(int3 a) => a._Values;

        public static implicit operator int3(Vector<int> a) => new int3(a);

        public static explicit operator int(int3 a) => a._Values[0];

        public static explicit operator int2(int3 a) => a._Values;

        public static explicit operator int4(int3 a) => a._Values;


        #region OPERATOR OVERLOADS - int3

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 operator +(int3 a, int3 b) => new int3(a._Values + b._Values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 operator -(int3 a, int3 b) => new int3(a._Values - b._Values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 operator *(int3 a, int3 b) => new int3(a._Values * b._Values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 operator /(int3 a, int3 b) => new int3(a._Values / b._Values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 operator ^(int3 a, int3 b) => new int3(a._Values ^ b._Values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 operator ++(int3 a)
        {
            a += One;
            return a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 operator --(int3 a)
        {
            a -= One;
            return a;
        }

        #endregion
    }
}
