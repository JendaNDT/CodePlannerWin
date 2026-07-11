using System;

namespace CodePlanner.Core
{
    public static class LocalizationService
    {
        public static string CurrentLanguage { get; set; } = "cs";

        public static string T(string csVal, string enVal)
        {
            return string.Equals(CurrentLanguage, "cs", StringComparison.OrdinalIgnoreCase) ? csVal : enVal;
        }
    }
}
