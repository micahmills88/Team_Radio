using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDR_FM
{
    /*
     * Class that ingests a single buffer of complex samples
     * The samples are processed in the following order:
     * 1. The samples are filtered down to 200kHz
     * 2. The samples are 
     *
     */
     
    class SignalProcessor
    {

        private static double[] CreateWindowedSincFilter(int taps, double frequencyCutoff)
        {
            double[] sincFilter = new double[taps];
            double M = taps;
            double FC = frequencyCutoff;
            double sum = 0;

            for (int i = 0; i < sincFilter.Length; i++)
            {
                if ((i - M / 2) == 0)
                {
                    sincFilter[i] = (float)(2.0 * Math.PI * FC);
                }
                else
                {
                    sincFilter[i] = (float)(Math.Sin(2.0 * Math.PI * FC * (i - M / 2.0)) / (i - M / 2));
                }
                sincFilter[i] *= (float)(0.54 - 0.46 * Math.Cos(2.0 * Math.PI * i / M));
                sum += sincFilter[i];
            }

            for (int i = 0; i < sincFilter.Length; i++)
            {
                sincFilter[i] /= (float)sum;
            }

            return sincFilter;
        }

        public static Complex[] DownsampleForAudio(Complex[] samples)
        {
            //assuming sample rate of of 1,920,000
            //assuming buffer size of 19,200
            Complex[] downSampled = new Complex[480];
            for(int i = 0; i < 480; i++)
            {
                downSampled[i] = samples[i * 40];
            }
            return downSampled;
        }

        public static Complex[] ApplyWindowFunction(Complex[] samples, double[] window)
        {
            for(int i = 0; i < samples.Length; i++)
            {
                samples[i] *= window[i];
            }
            return samples;
        }

        public static Complex[] LowPassFilter(Complex[] samples)
        {
            return null;
        }

        public double[] Quadrature_Demodulation(Complex[] samples)
        {
            double[] demodSamples = new double[samples.Length];
            for(int i = 1; i < samples.Length; i++)
            {
                demodSamples[i] = Complex.Multiply(samples[i], Complex.Conjugate(samples[i-1])).Phase;
            }
            return demodSamples;
        }

    }
}
