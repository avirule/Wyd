namespace Wyd.System.Extensions
{
    public static class BoolExtensions
    {
        public static unsafe byte ToByte(this bool a)
        {
            bool b = a;
            return *(byte*) &b;
        }
    }
}
