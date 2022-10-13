using ECommons.DalamudServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons
{
    public class CircularArray<T> : IEnumerable<T>
    {
        T[] values;
        bool[] isFilled;
        public CircularArray(int capacity)
        {
            if (capacity < 2)
            {
                throw new Exception("Capacity must be at least 2");
            }
            values = new T[capacity];
            isFilled = new bool[capacity];
            for (var i = 0; i < isFilled.Length; i++)
            {
                isFilled[i] = false;
            }
        }

        public void Push(T value)
        {
            for (var i = 0; i < isFilled.Length; i++)
            {
                if (!isFilled[i])
                {
                    isFilled[i] = true;
                    values[i] = value;
                    return;
                }
            }
            for (var i = 1; i < values.Length; i++)
            {
                values[i - 1] = values[i];
            }
            values[^1] = value;
        }

        public T this[int index]
        {
            get
            {
                return values[index];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (isFilled[i])
                {
                    yield return values[i];
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return values.GetEnumerator();
        }
    }
}