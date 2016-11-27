using System;

namespace SDR_FM
{
    public class CircleBuffer<T>
    {
        T[] nodes;
        int current;
        int emptySpot;

        public CircleBuffer(int size)
        {
            nodes = new T[size];
            current = 0;
            emptySpot = 0;
        }

        public void AddValue(T value)
        {
            nodes[emptySpot] = value;
            emptySpot++;
            if (emptySpot >= nodes.Length)
            {
                emptySpot = 0;
            }
        }
        public T GetValue()
        {
            int ret = current;
            current++;
            if (current >= nodes.Length)
            {
                current = 0;
            }
            return nodes[ret];
        }
    }
}
