namespace Static
{
    public static class StaticMath
    {
        public static float Mod(float a, float b)
        {
            return ((a % b) + b) % b;
        }

        public static int ModToInt(float a, float b)
        {
            return (int) Mod(a, b);
        }

        public static int GetRemainder(int a, int mod)
        {
            if (mod > 0)
            {
                return a % mod;
            }

            if (mod == 0)
            {
                return a;
            }

            if (mod < 0)
            {
                return mod % a;
            }

            return 0;
        }
    }
}