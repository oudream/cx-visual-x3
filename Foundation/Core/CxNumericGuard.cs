using System;
namespace CxVisual.Foundation.Core
{
    public static class CxNumericGuard
    {
        public static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "Value must be finite");
        }
    }
}
