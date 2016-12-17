using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDR_FM
{
    class FFTWrapper
    {
        private int fftLength;
        private int fftOverlap;
        private Complex[] overlapBuffer;
        private CircleBuffer<Complex> fftBuffer;
        private FFT fft;

        public FFTWrapper(int length, int overlap)
        {
            //double pow2 = Math.Log(length, 2);
            //fftLength = (int)(pow2 - (pow2 % 1) + 1);
            //fftOverlap = fftLength - length;

            fftLength = length;
            fftOverlap = overlap;
            fft = new FFT(fftLength);
            fftBuffer = new CircleBuffer<Complex>(fftLength * 100);
            overlapBuffer = new Complex[fftOverlap];
        }

        public void push(Complex complex)
        {
            fftBuffer.AddValue(complex);
        }

        public double[] GetFFTDisplayData()
        {
            double[] fftOut = new double[fftLength];
            double[] real = new double[fftLength];
            double[] imag = new double[fftLength];

            for(int i = 0; i < fftLength; i++)
            {
                if(i < fftOverlap)
                {
                    real[i] = overlapBuffer[i].Real;
                    imag[i] = overlapBuffer[i].Imaginary;
                }
                else
                {
                    Complex temp = fftBuffer.GetValue();
                    real[i] = temp.Real;
                    imag[i] = temp.Imaginary;
                    if(i >= fftLength - fftOverlap)
                    {
                        overlapBuffer[i - (fftLength - fftOverlap)] = temp;
                    }
                }
            }

            fft.run(real, imag);

            for(int i = 0; i < fftOut.Length; i++)
            {
                if(i < fftOut.Length / 2)
                {
                    fftOut[i + (fftOut.Length / 2)] = Math.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
                }
                else
                {
                    fftOut[i - (fftOut.Length / 2)] = Math.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
                }
            }

            return fftOut;
        }

        public double[] GetFFTData()
        {
            double[] fftOut = new double[fftLength];
            double[] real = new double[fftLength];
            double[] imag = new double[fftLength];

            for (int i = 0; i < fftLength; i++)
            {
                Complex temp = fftBuffer.GetValue();
                real[i] = temp.Real;
                imag[i] = temp.Imaginary;
            }

            fft.run(real, imag);

            for (int i = 0; i < fftOut.Length; i++)
            {
                fftOut[i] = Math.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
            }

            return fftOut;
        }

        public double[] GetAudioData()
        {
            double[] fftOut = new double[fftLength];


            for (int i = 0; i < fftOut.Length; i++)
            {
                fftOut[i] = fftBuffer.GetValue().Real * 100;
            }

            return fftOut;
        }
    }
}
