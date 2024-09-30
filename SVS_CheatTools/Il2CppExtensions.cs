using System.Collections.Generic;

namespace IllusionMods
{
    internal static class Il2CppExtensions
    {
        public static IEnumerable<T> AsManagedEnumerable<T>(this Il2CppSystem.Collections.Generic.List<T> collection)
        {
            foreach (var val in collection)
                yield return val;
        }
        public static IEnumerable<T> AsManagedEnumerable<T>(this Il2CppSystem.Collections.Generic.HashSet<T> collection)
        {
            foreach (var val in collection)
                yield return val;
        }
        public static IEnumerable<KeyValuePair<T1, T2>> AsManagedEnumerable<T1, T2>(this Il2CppSystem.Collections.Generic.Dictionary<T1, T2> collection)
        {
            foreach (var val in collection)
                yield return new KeyValuePair<T1, T2>(val.Key, val.Value);
        }
    }
}
