using System;
using System.Collections.Generic;

namespace Mandato.Core
{
    public static class ListExtensions
    {
        public static void Shuffle<T>(this IList<T> list, Random rng = null)
        {
            if (list == null || list.Count <= 1) return;
            var rand = rng ?? new Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = rand.Next(0, i + 1);
                T temp = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }
    }
}
