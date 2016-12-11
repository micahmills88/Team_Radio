using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDR_FM
{
    
    class FFTHandler
    {
        private ConcurrentQueue<Complex[]> rawFFTSamples;
        private ConcurrentQueue<Complex[]> filteredFFTSamples;
        private ConcurrentQueue<double[]> demodulatedFFTSamples;
        private ConcurrentQueue<double[]> audioFFTSamples;

        private ConcurrentQueue<Complex[]> rawFFTSamplesOut;
        private ConcurrentQueue<Complex[]> filteredFFTSamplesOut;
        private ConcurrentQueue<double[]> demodulatedFFTSamplesOut;
        private ConcurrentQueue<double[]> audioFFTSamplesOut;

        private CircleBuffer<Complex> rawFFTBuffer;

        private FFT rawFFT;
        private FFT filteredFFT;
        private FFT demodulatedFFT;
        private FFT audioFFT;

        private int rawFFTLength;
        private int filteredFFTLength;
        private int demodulatedFFTLength;
        private int audioFFTLength;

        private bool isStarted = false;
        private long loopTimer = 0;

        public FFTHandler(int rawSize, int filteredSize, int demodSize, int audioSize)
        {
            rawFFTLength = rawSize;
            filteredFFTLength = filteredSize;
            demodulatedFFTLength = demodSize;
            audioFFTLength = audioSize;

            rawFFT = new FFT(rawSize);
            filteredFFT = new FFT(filteredSize);
            demodulatedFFT = new FFT(demodSize);
            audioFFT = new FFT(audioSize);

            Initialize();
        }

        private void Initialize()
        {
            rawFFTSamples = new ConcurrentQueue<Complex[]>();
            filteredFFTSamples = new ConcurrentQueue<Complex[]>();
            demodulatedFFTSamples = new ConcurrentQueue<double[]>();
            audioFFTSamples = new ConcurrentQueue<double[]>();

            rawFFTSamplesOut = new ConcurrentQueue<Complex[]>();
            filteredFFTSamplesOut = new ConcurrentQueue<Complex[]>();
            demodulatedFFTSamplesOut = new ConcurrentQueue<double[]>();
            audioFFTSamplesOut = new ConcurrentQueue<double[]>();

            rawFFTBuffer = new CircleBuffer<Complex>(rawFFTLength * 100);
        }

        public void addRawSamples(Complex[] samples)
        {
            rawFFTSamples.Enqueue(samples);
        }

        public void addFilteredSamples(Complex[] samples)
        {
            filteredFFTSamples.Enqueue(samples);
        }

        public void addDemodSamples(double[] samples)
        {
            demodulatedFFTSamples.Enqueue(samples);
        }

        public void addAudioSamples(double[] samples)
        {
            audioFFTSamples.Enqueue(samples);
            
        }

        public double[] getRawFFT()
        {
            Complex[] temp = new Complex[rawFFTLength];
            do
            {
                rawFFTSamplesOut.TryDequeue(out temp);
            }
            while (temp == null);

            double[] real = new double[rawFFTLength];
            double[] imag = new double[rawFFTLength];
            
            for(int i = 0; i < rawFFTLength; i++)
            {
                real[i] = temp[i].Real;
                imag[i] = temp[i].Imaginary;
            }
            rawFFT.run(real, imag);
            double[] magnitude = new double[rawFFTLength];
            for(int i = 0; i < rawFFTLength; i++)
            {
                magnitude[i] = Math.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
            }
            return magnitude;
        }

        public void Start()
        {
            isStarted = true;
            Task.Run(() => FFTHandler_DoWork());
        }

        public void Stop()
        {
            isStarted = false;
        }

        public void FFTHandler_DoWork()
        {
            double inputCounter = 0;
            do
            {
                if(DateTime.Now.Millisecond - loopTimer > 9)
                {
                    Complex[] temp = new Complex[19200];
                    rawFFTSamples.TryDequeue(out temp);

                    if (temp != null)
                    {
                        for (int i = 0; i < temp.Length; i++)
                        {
                            rawFFTBuffer.AddValue(temp[i]);
                        }
                        inputCounter += temp.Length;
                    }
                    loopTimer = DateTime.Now.Millisecond;
                }

                if(inputCounter > rawFFTLength)
                {
                    Complex[] tempOut = new Complex[rawFFTLength];
                    for(int i = 0; i < tempOut.Length; i++)
                    {
                        tempOut[i] = rawFFTBuffer.GetValue();
                    }
                    inputCounter -= tempOut.Length;
                }

            }
            while (isStarted);

            Initialize();
        }

    }
}
