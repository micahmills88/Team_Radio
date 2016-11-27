using System;

namespace SDR_FM
{
    public class FFT
    {

        /* 
         * Computes the discrete Fourier transform (DFT) of the given complex vector, storing the result back into the vector.
         * The vector can have any length. This is a wrapper function.
         */
        private double[] cosTable;
        private double[] sinTable;

        public FFT(int size)
        {
            cosTable = new double[size];
            sinTable = new double[size];
            for (int i = 0; i < size; i++)
            {
                cosTable[i] = (double)Math.Cos(2 * Math.PI * i / size);
                sinTable[i] = (double)Math.Sin(2 * Math.PI * i / size);
            }
        }

        /* 
         * Computes the discrete Fourier transform (DFT) of the given complex vector, storing the result back into the vector.
         * The vector's length must be a power of 2. Uses the Cooley-Tukey decimation-in-time radix-2 algorithm.
         */
        public void TransformRadix2(double[] real, double[] imag)
        {
            // Initialization
            if (real.Length != imag.Length)
                throw new ArgumentException("Mismatched lengths");

            int n = real.Length;
            int levels = 31 - NumberOfLeadingZeros(n);  // Equal to floor(log2(n))
            if (1 << levels != n)
                throw new ArgumentException("Length is not a power of 2");

            // Bit-reversed addressing permutation
            for (int i = 0; i < n; i++)
            {
                int j = (int)((uint)ReverseBits(i) >> (32 - levels));
                if (j > i)
                {
                    double temp = real[i];
                    real[i] = real[j];
                    real[j] = temp;
                    temp = imag[i];
                    imag[i] = imag[j];
                    imag[j] = temp;
                }
            }

            // Cooley-Tukey decimation-in-time radix-2 FFT
            for (int size = 2; size <= n; size *= 2)
            {
                int halfsize = size / 2;
                int tablestep = n / size;
                for (int i = 0; i < n; i += size)
                {
                    for (int j = i, k = 0; j < i + halfsize; j++, k += tablestep)
                    {
                        double tpre = real[j + halfsize] * cosTable[k] + imag[j + halfsize] * sinTable[k];
                        double tpim = -real[j + halfsize] * sinTable[k] + imag[j + halfsize] * cosTable[k];
                        real[j + halfsize] = real[j] - tpre;
                        imag[j + halfsize] = imag[j] - tpim;
                        real[j] += tpre;
                        imag[j] += tpim;
                    }
                }
                if (size == n)  // Prevent overflow in 'size *= 2'
                    break;
            }
        }

        private int NumberOfLeadingZeros(int val)
        {
            if (val == 0)
                return 32;
            int result = 0;
            for (; val >= 0; val <<= 1)
                result++;
            return result;
        }

        private int HighestOneBit(int val)
        {
            for (int i = 1 << 31; i != 0; i = (int)((uint)i >> 1))
            {
                if ((val & i) != 0)
                    return i;
            }
            return 0;
        }

        private int ReverseBits(int val)
        {
            int result = 0;
            for (int i = 0; i < 32; i++, val >>= 1)
                result = (result << 1) | (val & 1);
            return result;
        }

    }
}
