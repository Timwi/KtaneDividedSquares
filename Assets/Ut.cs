using System;
using System.Collections.Generic;
using System.Linq;

using Rnd = UnityEngine.Random;

namespace DividedSquares
{
    static class Ut
    {
        public static T PickRandom<T>(this IEnumerable<T> src)
        {
            if (src == null)
                throw new ArgumentNullException("src");
            var lst = (src as IList<T>) ?? src.ToArray();
            if (lst.Count == 0)
                throw new ArgumentException("Cannot pick a random element from an empty collection.", "src");
            return lst[Rnd.Range(0, lst.Count)];
        }
    }
}
