using System;
using System.Collections.Generic;
using System.Linq;

namespace NTH.Common
{
    public static class ListExtensions
    {
        private static Random rng = new Random();

        /// <summary>
        /// Xáo trộn ngẫu nhiên các phần tử trong một danh sách.
        /// </summary>
        /// <typeparam name="T">Kiểu dữ liệu của danh sách.</typeparam>
        /// <param name="list">Danh sách cần xáo trộn.</param>
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
