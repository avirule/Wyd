namespace Wyd.Extensions
{
    public static class BoolExtensions
    {
        public static unsafe byte ToByte(this bool a)
        {
            bool b = a;
            return *(byte*)&b;
        }

        public static unsafe int ToInt(this bool a)
        {
            bool b = a;
            return *(int*)&b;
        }
    }
}
