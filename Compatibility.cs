// Records and init-only properties are compiled with the current C# compiler.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace DayPlanner
{
    internal static class Numeric
    {
        public static int Clamp(int value, int min, int max)
        {
            if (min > max) throw new ArgumentException("Minimum exceeds maximum.");
            return Math.Min(Math.Max(value, min), max);
        }

        public static double Clamp(double value, double min, double max)
        {
            if (min > max) throw new ArgumentException("Minimum exceeds maximum.");
            return Math.Min(Math.Max(value, min), max);
        }
    }
}
