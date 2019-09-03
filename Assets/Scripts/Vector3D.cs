#region

using UnityEngine;

#endregion

public struct Vector3D
{
    private const int _PRIME_HASH_ONE = 17;
    private const int _PRIME_HASH_TWO = 23;

    public double X;
    public double Y;
    public double Z;

    public Vector3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3D(Vector3 a)
    {
        X = a.x;
        Y = a.y;
        Z = a.z;
    }

    public Vector3D(Vector3Int a)
    {
        X = a.x;
        Y = a.y;
        Z = a.z;
    }

    public override bool Equals(object obj)
    {
        return obj is Vector3D vector3D && (this == vector3D);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = _PRIME_HASH_ONE;

            hash = (hash * _PRIME_HASH_TWO) + X.GetHashCode();
            hash = (hash * _PRIME_HASH_TWO) + Y.GetHashCode();
            hash = (hash * _PRIME_HASH_TWO) + Z.GetHashCode();

            return hash;
        }
    }

    public override string ToString()
    {
        return $"({X},{Y},{Z})";
    }

    #region CASTS

    public static implicit operator Vector3(Vector3D a)
    {
        return new Vector3((float) a.X, (float) a.Y, (float) a.Z);
    }

    public static implicit operator Vector3D(Vector3 a)
    {
        return new Vector3D(a);
    }

    public static implicit operator Vector3D(Vector3Int a)
    {
        return new Vector3D(a);
    }

    #endregion

    #region STATIC MEMBERS

    public static Vector3D Forward => new Vector3D(0d, 0d, 1d);
    public static Vector3D Backward => new Vector3D(0d, 0d, -1d);
    public static Vector3D Right => new Vector3D(1d, 0d, 0d);
    public static Vector3D Left => new Vector3D(-1d, 0d, 0d);
    public static Vector3D Up => new Vector3D(0d, 1d, 0d);
    public static Vector3D Down => new Vector3D(0d, -1d, 0d);
    public static Vector3D Zero => new Vector3D(0d, 0d, 0d);
    public static Vector3D One => new Vector3D(1d, 1d, 1d);

    public static Vector3D PositiveInfinity =>
        new Vector3D(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);

    public static Vector3D NegativeInfinity =>
        new Vector3D(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);

    #endregion

    #region OPERATOR OVERLOADS

    // Vector3D
    public static Vector3D operator +(Vector3D a, Vector3D b)
    {
        return new Vector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    public static Vector3D operator -(Vector3D a, Vector3D b)
    {
        return new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    public static Vector3D operator *(Vector3D a, Vector3D b)
    {
        return new Vector3D(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    }

    public static Vector3D operator /(Vector3D a, Vector3D b)
    {
        return new Vector3D(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
    }

    public static bool operator ==(Vector3D a, Vector3D b)
    {
        return (a.X == b.X) && (a.Y == b.Y) && (a.Z == b.Z);
    }

    public static bool operator !=(Vector3D a, Vector3D b)
    {
        return (a.X != b.X) || (a.Y != b.Y) || (a.Z != b.Z);
    }

    // Vector3
    public static Vector3D operator +(Vector3D a, Vector3 b)
    {
        return new Vector3D(a.X + b.x, a.Y + b.y, a.Z + b.z);
    }

    public static Vector3D operator -(Vector3D a, Vector3 b)
    {
        return new Vector3D(a.X - b.x, a.Y - b.y, a.Z - b.z);
    }

    public static Vector3D operator *(Vector3D a, Vector3 b)
    {
        return new Vector3D(a.X * b.x, a.Y * b.y, a.Z * b.z);
    }

    public static Vector3D operator /(Vector3D a, Vector3 b)
    {
        return new Vector3D(a.X / b.x, a.Y / b.y, a.Z / b.z);
    }

    public static Vector3D operator +(Vector3 a, Vector3D b)
    {
        return new Vector3D(a.x + b.X, a.y + b.Y, a.z + b.Z);
    }

    public static Vector3D operator -(Vector3 a, Vector3D b)
    {
        return new Vector3D(a.x - b.X, a.y - b.Y, a.z - b.Z);
    }

    public static Vector3D operator *(Vector3 a, Vector3D b)
    {
        return new Vector3D(a.x * b.X, a.y * b.Y, a.z * b.Z);
    }

    public static Vector3D operator /(Vector3 a, Vector3D b)
    {
        return new Vector3D(a.x / b.X, a.y / b.Y, a.z / b.Z);
    }

    // Vector3Int
    public static Vector3D operator +(Vector3D a, Vector3Int b)
    {
        return new Vector3D(a.X + b.x, a.Y + b.y, a.Z + b.z);
    }

    public static Vector3D operator -(Vector3D a, Vector3Int b)
    {
        return new Vector3D(a.X - b.x, a.Y - b.y, a.Z - b.z);
    }

    public static Vector3D operator *(Vector3D a, Vector3Int b)
    {
        return new Vector3D(a.X * b.x, a.Y * b.y, a.Z * b.z);
    }

    public static Vector3D operator /(Vector3D a, Vector3Int b)
    {
        return new Vector3D(a.X / b.x, a.Y / b.y, a.Z / b.z);
    }

    public static Vector3D operator +(Vector3Int a, Vector3D b)
    {
        return new Vector3D(a.x + b.X, a.y + b.Y, a.z + b.Z);
    }

    public static Vector3D operator -(Vector3Int a, Vector3D b)
    {
        return new Vector3D(a.x - b.X, a.y - b.Y, a.z - b.Z);
    }

    public static Vector3D operator *(Vector3Int a, Vector3D b)
    {
        return new Vector3D(a.x * b.X, a.y * b.Y, a.z * b.Z);
    }

    public static Vector3D operator /(Vector3Int a, Vector3D b)
    {
        return new Vector3D(a.x / b.X, a.y / b.Y, a.z / b.Z);
    }

    // Double
    public static Vector3D operator +(Vector3D a, double b)
    {
        return new Vector3D(a.X + b, a.Y + b, a.Z + b);
    }

    public static Vector3D operator -(Vector3D a, double b)
    {
        return new Vector3D(a.X - b, a.Y - b, a.Z - b);
    }

    public static Vector3D operator *(Vector3D a, double b)
    {
        return new Vector3D(a.X * b, a.Y * b, a.Z * b);
    }

    public static Vector3D operator /(Vector3D a, double b)
    {
        return new Vector3D(a.X / b, a.Y / b, a.Z / b);
    }

    public static Vector3D operator +(double a, Vector3D b)
    {
        return new Vector3D(a + b.X, a + b.Y, a + b.Z);
    }

    public static Vector3D operator -(double a, Vector3D b)
    {
        return new Vector3D(a - b.X, a - b.Y, a - b.Z);
    }

    public static Vector3D operator *(double a, Vector3D b)
    {
        return new Vector3D(a * b.X, a * b.Y, a * b.Z);
    }

    public static Vector3D operator /(double a, Vector3D b)
    {
        return new Vector3D(a / b.X, a / b.Y, a / b.Z);
    }

    #endregion
}