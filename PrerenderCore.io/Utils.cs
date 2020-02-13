using System.Collections.Generic;
using System.Linq;

namespace PrerenderCore.io
{
    public static class Utils
    {
        public static bool IsBlank(this string str)
        {
            return str == null || str.Trim() == string.Empty;
        }

        public static bool IsNotBlank(this string str)
        {
            return str != null && str.Trim() != string.Empty;
        }

        public static bool IsNotEmpty<T>(this IEnumerable<T> list)
        {
            return list != null && list.Any();
        }
    }
}
