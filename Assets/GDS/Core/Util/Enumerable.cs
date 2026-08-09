using System.Collections.Generic;
using System.Linq;

namespace GDS.Core {

    public static class EnumerableExt {

        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size) {
            T[] bucket = null;
            int count = 0;

            foreach (var item in source) {
                if (bucket == null)
                    bucket = new T[size];

                bucket[count++] = item;

                if (count == size) {
                    yield return bucket;
                    bucket = null;
                    count = 0;
                }
            }

            if (bucket != null && count > 0)
                yield return bucket.Take(count);
        }
    }
}
