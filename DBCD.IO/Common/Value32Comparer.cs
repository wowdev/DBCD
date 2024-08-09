using System.Collections.Generic;

namespace DBCD.IO.Common
{
    class Value32Comparer : IEqualityComparer<Value32[]>
    {
        public bool Equals(Value32[] x, Value32[] y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (x.Length != y.Length)
                return false;

            for (int i = 0; i < x.Length; i++)
                if (x[i].GetValue<int>() != y[i].GetValue<int>())
                    return false;

            return true;
        }

        public int GetHashCode(Value32[] obj)
        {
            unchecked
            {
                int s = 314, t = 159, hashCode = 0;
                for (int i = 0; i < obj.Length; i++)
                {
                    hashCode = hashCode * s + obj[i].GetValue<int>();
                    s *= t;
                }
                return hashCode;
            }
        }
    }
}
